using FluentValidation;
using Microsoft.Extensions.Logging;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Application.Autenticacao;

/// <summary>
/// Casos de uso de autenticação: criar conta, login, renovar sessão, logout e recuperação de senha.
///
/// Como funciona uma sessão:
/// - Token de ACESSO (JWT): vale 15 minutos e vai em toda requisição. Curto de propósito:
///   se for roubado, logo deixa de funcionar.
/// - REFRESH token: vale 7 dias e só serve para pedir um novo token de acesso, sem digitar a senha.
///   É "rotativo": cada uso gera um novo e invalida o anterior.
/// </summary>
public sealed class ServicoAutenticacao(
    IRepositorioAutenticacao repositorio,
    IUnitOfWork unidadeDeTrabalho,
    IHasherSenha hasher,
    IServicoTokens tokens,
    IEnvioEmail envioEmail,
    TimeProvider relogio,
    IValidator<RegistrarEmpresaRequisicao> validadorRegistro,
    IValidator<LoginRequisicao> validadorLogin,
    IValidator<EsqueciSenhaRequisicao> validadorEsqueciSenha,
    IValidator<RedefinirSenhaRequisicao> validadorRedefinicao,
    ILogger<ServicoAutenticacao> logger)
{
    public const string MensagemCredenciaisInvalidas = "E-mail ou senha inválidos.";
    private const string MensagemSessaoInvalida = "Sessão inválida ou expirada. Faça login novamente.";

    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    /// <summary>Cria a empresa e o primeiro usuário (administrador) e já devolve a sessão aberta.</summary>
    public async Task<RespostaAutenticacao> RegistrarAsync(RegistrarEmpresaRequisicao requisicao, CancellationToken ct = default)
    {
        await validadorRegistro.ValidateAndThrowAsync(requisicao, ct);

        var email = Usuario.NormalizarEmail(requisicao.Email);
        var cnpj = DocumentoFiscal.SomenteDigitos(requisicao.Cnpj);

        if (await repositorio.CnpjEmUsoAsync(cnpj, ct))
            throw new ConflitoException("Já existe uma conta no OrçaFácil para este CNPJ.");
        if (await repositorio.EmailEmUsoAsync(email, ct))
            throw new ConflitoException("Este e-mail já está em uso.");

        var empresa = new Empresa(requisicao.RazaoSocial, cnpj, requisicao.Segmento, requisicao.NomeFantasia);
        var administrador = new Usuario(requisicao.NomeAdministrador, email, hasher.GerarHash(requisicao.Senha),
            PerfilUsuario.Administrador);

        // Ainda não há ninguém logado, então a empresa do usuário é informada explicitamente.
        administrador.VincularEmpresa(empresa.Id);

        repositorio.Adicionar(empresa);
        repositorio.Adicionar(administrador);
        repositorio.Adicionar(new LogSistema(NivelLog.Auditoria, "EmpresaRegistrada", empresa.Id, administrador.Id,
            nameof(Empresa), empresa.Id));

        var (resposta, _) = AbrirSessao(administrador, empresa, registrarLogin: true);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);

        logger.LogInformation("Nova empresa registrada: {EmpresaId}", empresa.Id);
        return resposta;
    }

    public async Task<RespostaAutenticacao> LoginAsync(LoginRequisicao requisicao, CancellationToken ct = default)
    {
        await validadorLogin.ValidateAndThrowAsync(requisicao, ct);

        var usuario = await repositorio.ObterUsuarioPorEmailAsync(Usuario.NormalizarEmail(requisicao.Email), ct);

        if (usuario is null)
        {
            // Calcula um hash mesmo assim: a resposta demora o mesmo tempo que um login com e-mail
            // existente. Sem isso, um atacante poderia descobrir e-mails cadastrados medindo o tempo.
            _ = hasher.GerarHash(requisicao.Senha);
            logger.LogWarning("Tentativa de login com e-mail não cadastrado.");
            throw new NaoAutorizadoException(MensagemCredenciaisInvalidas);
        }

        if (!hasher.Verificar(usuario.SenhaHash, requisicao.Senha))
        {
            repositorio.Adicionar(new LogSistema(NivelLog.Aviso, "LoginFalhou", usuario.EmpresaId, usuario.Id,
                nameof(Usuario), usuario.Id));
            await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
            logger.LogWarning("Senha incorreta para o usuário {UsuarioId}", usuario.Id);
            throw new NaoAutorizadoException(MensagemCredenciaisInvalidas);
        }

        // Só depois de conferir a senha revelamos se a conta está desativada.
        var empresa = await ObterEmpresaComAcessoAsync(usuario, ct);

        var (resposta, _) = AbrirSessao(usuario, empresa, registrarLogin: true);
        repositorio.Adicionar(new LogSistema(NivelLog.Auditoria, "Login", usuario.EmpresaId, usuario.Id,
            nameof(Usuario), usuario.Id));
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return resposta;
    }

    /// <summary>Troca um refresh token válido por uma sessão nova (rotação de tokens).</summary>
    public async Task<RespostaAutenticacao> RenovarAsync(RenovarSessaoRequisicao requisicao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requisicao.RefreshToken))
            throw new NaoAutorizadoException(MensagemSessaoInvalida);

        var tokenAtual = await repositorio.ObterRefreshTokenAsync(tokens.CalcularHash(requisicao.RefreshToken), ct)
            ?? throw new NaoAutorizadoException(MensagemSessaoInvalida);

        if (tokenAtual.RevogadoEm is not null)
        {
            // Um token JÁ USADO foi apresentado de novo: sinal de que foi copiado por outra pessoa.
            // Por segurança, todas as sessões do usuário são encerradas.
            await RevogarTodasAsSessoesAsync(tokenAtual.UsuarioId, ct);
            await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
            logger.LogWarning("Reuso de refresh token detectado para o usuário {UsuarioId}. Sessões encerradas.",
                tokenAtual.UsuarioId);
            throw new NaoAutorizadoException(MensagemSessaoInvalida);
        }

        if (!tokenAtual.EstaAtivo(Agora))
            throw new NaoAutorizadoException(MensagemSessaoInvalida);

        var usuario = await repositorio.ObterUsuarioPorIdAsync(tokenAtual.UsuarioId, ct)
            ?? throw new NaoAutorizadoException(MensagemSessaoInvalida);
        var empresa = await ObterEmpresaComAcessoAsync(usuario, ct);

        var (resposta, novoToken) = AbrirSessao(usuario, empresa, registrarLogin: false);
        tokenAtual.Revogar(Agora, substituidoPorId: novoToken.Id);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return resposta;
    }

    /// <summary>Encerra a sessão. Responde com sucesso mesmo se o token não existir (nada a revelar).</summary>
    public async Task LogoutAsync(RenovarSessaoRequisicao requisicao, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(requisicao.RefreshToken))
            return;

        var token = await repositorio.ObterRefreshTokenAsync(tokens.CalcularHash(requisicao.RefreshToken), ct);
        if (token is null || !token.EstaAtivo(Agora))
            return;

        token.Revogar(Agora);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    /// <summary>
    /// Envia o link de redefinição. Não informa se o e-mail existe: a resposta é sempre a mesma,
    /// para ninguém usar esta tela para descobrir quem tem conta.
    /// </summary>
    public async Task EsqueciSenhaAsync(EsqueciSenhaRequisicao requisicao, CancellationToken ct = default)
    {
        await validadorEsqueciSenha.ValidateAndThrowAsync(requisicao, ct);

        var usuario = await repositorio.ObterUsuarioPorEmailAsync(Usuario.NormalizarEmail(requisicao.Email), ct);
        if (usuario is null || !usuario.Ativo)
        {
            logger.LogInformation("Recuperação de senha pedida para e-mail inexistente ou usuário inativo.");
            return;
        }

        var token = tokens.GerarTokenRecuperacaoSenha();
        repositorio.Adicionar(new TokenRecuperacaoSenha(usuario.Id, token.Hash, token.ExpiraEm));
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);

        await envioEmail.EnviarRecuperacaoSenhaAsync(usuario.Email, usuario.Nome, token.Valor, token.ExpiraEm, ct);
    }

    public async Task RedefinirSenhaAsync(RedefinirSenhaRequisicao requisicao, CancellationToken ct = default)
    {
        await validadorRedefinicao.ValidateAndThrowAsync(requisicao, ct);

        var registro = await repositorio.ObterTokenRecuperacaoAsync(tokens.CalcularHash(requisicao.Token), ct)
            ?? throw new DomainException("Link de recuperação inválido.");

        registro.Utilizar(Agora); // recusa link já usado ou expirado

        var usuario = await repositorio.ObterUsuarioPorIdAsync(registro.UsuarioId, ct)
            ?? throw new DomainException("Link de recuperação inválido.");

        usuario.DefinirSenhaHash(hasher.GerarHash(requisicao.NovaSenha));

        // Senha nova = todas as sessões antigas encerradas (inclusive a de quem roubou a senha antiga).
        await RevogarTodasAsSessoesAsync(usuario.Id, ct);
        repositorio.Adicionar(new LogSistema(NivelLog.Auditoria, "SenhaRedefinida", usuario.EmpresaId, usuario.Id,
            nameof(Usuario), usuario.Id));
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    private async Task RevogarTodasAsSessoesAsync(Guid usuarioId, CancellationToken ct)
    {
        foreach (var token in await repositorio.ListarRefreshTokensAtivosAsync(usuarioId, Agora, ct))
            token.Revogar(Agora);
    }

    private async Task<Empresa> ObterEmpresaComAcessoAsync(Usuario usuario, CancellationToken ct)
    {
        if (!usuario.Ativo)
            throw new NaoAutorizadoException("Usuário desativado. Procure o administrador da sua empresa.");

        var empresa = await repositorio.ObterEmpresaAsync(usuario.EmpresaId, ct)
            ?? throw new NaoAutorizadoException(MensagemSessaoInvalida);

        if (!empresa.Ativa)
            throw new NaoAutorizadoException("O acesso desta empresa está suspenso.");

        return empresa;
    }

    /// <summary>Gera token de acesso + refresh token e registra o refresh token para ser salvo.</summary>
    private (RespostaAutenticacao Resposta, RefreshToken NovoRefreshToken) AbrirSessao(
        Usuario usuario, Empresa empresa, bool registrarLogin)
    {
        if (registrarLogin)
            usuario.RegistrarLogin(Agora);

        var acesso = tokens.GerarTokenAcesso(usuario);
        var refresh = tokens.GerarRefreshToken();
        var refreshToken = new RefreshToken(usuario.Id, refresh.Hash, refresh.ExpiraEm);
        repositorio.Adicionar(refreshToken);

        var resposta = new RespostaAutenticacao(acesso.Token, acesso.ExpiraEm, refresh.Valor, refresh.ExpiraEm,
            UsuarioLogadoResposta.De(usuario, empresa));
        return (resposta, refreshToken);
    }
}
