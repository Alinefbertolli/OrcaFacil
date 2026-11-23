using FluentValidation;
using FluentValidation.Results;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Autenticacao;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Application.Usuarios;

// ---------------------------------------------------------------- DTOs

public sealed record UsuarioResposta(
    Guid Id, string Nome, string Email, PerfilUsuario Perfil, bool Ativo, DateTime? UltimoLoginEm, DateTime CriadoEm)
{
    public static UsuarioResposta De(Usuario u) =>
        new(u.Id, u.Nome, u.Email, u.Perfil, u.Ativo, u.UltimoLoginEm, u.CriadoEm);
}

public sealed record AtualizarMeuPerfilRequisicao(string Nome);
public sealed record AlterarMinhaSenhaRequisicao(string SenhaAtual, string NovaSenha);
public sealed record CriarUsuarioRequisicao(string Nome, string Email, string Senha, PerfilUsuario Perfil);
public sealed record AlterarPerfilRequisicao(PerfilUsuario Perfil);
public sealed record AlterarSituacaoRequisicao(bool Ativo);

// ---------------------------------------------------------------- Validadores

public sealed class AtualizarMeuPerfilValidador : AbstractValidator<AtualizarMeuPerfilRequisicao>
{
    public AtualizarMeuPerfilValidador() =>
        RuleFor(r => r.Nome).NotEmpty().WithMessage("Informe o nome.").MaximumLength(150);
}

public sealed class AlterarMinhaSenhaValidador : AbstractValidator<AlterarMinhaSenhaRequisicao>
{
    public AlterarMinhaSenhaValidador()
    {
        RuleFor(r => r.SenhaAtual).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(r => r.NovaSenha).SenhaForte()
            .NotEqual(r => r.SenhaAtual).WithMessage("A nova senha deve ser diferente da atual.");
    }
}

public sealed class CriarUsuarioValidador : AbstractValidator<CriarUsuarioRequisicao>
{
    public CriarUsuarioValidador()
    {
        RuleFor(r => r.Nome).NotEmpty().WithMessage("Informe o nome.").MaximumLength(150);
        RuleFor(r => r.Email).EmailValido();
        RuleFor(r => r.Senha).SenhaForte();
        RuleFor(r => r.Perfil).IsInEnum().WithMessage("Perfil inválido.");
    }
}

// ---------------------------------------------------------------- Casos de uso

/// <summary>
/// Gestão de usuários DENTRO da empresa do usuário logado.
/// O filtro automático por empresa garante que um administrador nunca enxergue usuários de outra loja.
/// </summary>
public sealed class ServicoUsuarios(
    IRepositorio<Usuario> usuarios,
    IRepositorioAutenticacao repositorioAutenticacao,
    IUnitOfWork unidadeDeTrabalho,
    IUsuarioAtual usuarioAtual,
    IHasherSenha hasher,
    TimeProvider relogio,
    IValidator<AtualizarMeuPerfilRequisicao> validadorPerfil,
    IValidator<AlterarMinhaSenhaRequisicao> validadorSenha,
    IValidator<CriarUsuarioRequisicao> validadorCriacao)
{
    private Guid IdUsuarioLogado =>
        usuarioAtual.UsuarioId ?? throw new NaoAutorizadoException("Faça login para continuar.");

    public async Task<UsuarioResposta> ObterMeuPerfilAsync(CancellationToken ct = default) =>
        UsuarioResposta.De(await usuarios.ObterPorIdOuFalharAsync(IdUsuarioLogado, ct));

    public async Task<UsuarioResposta> AtualizarMeuPerfilAsync(AtualizarMeuPerfilRequisicao requisicao, CancellationToken ct = default)
    {
        await validadorPerfil.ValidateAndThrowAsync(requisicao, ct);
        var usuario = await usuarios.ObterPorIdOuFalharAsync(IdUsuarioLogado, ct);

        usuario.AtualizarPerfil(requisicao.Nome);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return UsuarioResposta.De(usuario);
    }

    public async Task AlterarMinhaSenhaAsync(AlterarMinhaSenhaRequisicao requisicao, CancellationToken ct = default)
    {
        await validadorSenha.ValidateAndThrowAsync(requisicao, ct);
        var usuario = await usuarios.ObterPorIdOuFalharAsync(IdUsuarioLogado, ct);

        if (!hasher.Verificar(usuario.SenhaHash, requisicao.SenhaAtual))
            throw new ValidationException([new ValidationFailure(nameof(requisicao.SenhaAtual), "A senha atual está incorreta.")]);

        usuario.DefinirSenhaHash(hasher.GerarHash(requisicao.NovaSenha));
        await EncerrarSessoesAsync(usuario.Id, ct); // outros dispositivos precisarão entrar de novo
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    public async Task<PaginaResultado<UsuarioResposta>> ListarAsync(ParametrosPaginacao paginacao, string? busca,
        CancellationToken ct = default)
    {
        var termo = busca?.Trim().ToLower() ?? string.Empty;
        var pagina = termo.Length == 0
            ? await usuarios.ListarAsync(paginacao, ordenarPor: u => u.Nome, cancellationToken: ct)
            : await usuarios.ListarAsync(paginacao,
                filtro: u => u.Nome.ToLower().Contains(termo) || u.Email.Contains(termo),
                ordenarPor: u => u.Nome, cancellationToken: ct);

        return pagina.Mapear(UsuarioResposta.De);
    }

    public async Task<UsuarioResposta> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        UsuarioResposta.De(await usuarios.ObterPorIdOuFalharAsync(id, ct));

    /// <summary>Cria um usuário na empresa do administrador logado.</summary>
    public async Task<UsuarioResposta> CriarAsync(CriarUsuarioRequisicao requisicao, CancellationToken ct = default)
    {
        await validadorCriacao.ValidateAndThrowAsync(requisicao, ct);

        var email = Usuario.NormalizarEmail(requisicao.Email);
        if (await repositorioAutenticacao.EmailEmUsoAsync(email, ct))
            throw new ConflitoException("Este e-mail já está em uso.");

        // A empresa é preenchida automaticamente ao salvar (empresa do administrador logado).
        var usuario = new Usuario(requisicao.Nome, email, hasher.GerarHash(requisicao.Senha), requisicao.Perfil);
        usuarios.Adicionar(usuario);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return UsuarioResposta.De(usuario);
    }

    public async Task<UsuarioResposta> AlterarPerfilAsync(Guid id, AlterarPerfilRequisicao requisicao, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(requisicao.Perfil))
            throw new ValidationException([new ValidationFailure(nameof(requisicao.Perfil), "Perfil inválido.")]);

        // Evita que a empresa fique sem administrador por engano.
        if (id == IdUsuarioLogado && requisicao.Perfil != PerfilUsuario.Administrador)
            throw new DomainException("Você não pode remover o seu próprio acesso de administrador.");

        var usuario = await usuarios.ObterPorIdOuFalharAsync(id, ct);
        usuario.AlterarPermissao(requisicao.Perfil);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return UsuarioResposta.De(usuario);
    }

    public async Task<UsuarioResposta> AlterarSituacaoAsync(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct = default)
    {
        if (id == IdUsuarioLogado && !requisicao.Ativo)
            throw new DomainException("Você não pode desativar o seu próprio usuário.");

        var usuario = await usuarios.ObterPorIdOuFalharAsync(id, ct);
        if (requisicao.Ativo)
        {
            usuario.Ativar();
        }
        else
        {
            usuario.Desativar();
            await EncerrarSessoesAsync(usuario.Id, ct); // desativado = desconectado imediatamente
        }

        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return UsuarioResposta.De(usuario);
    }

    private async Task EncerrarSessoesAsync(Guid usuarioId, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        foreach (var token in await repositorioAutenticacao.ListarRefreshTokensAtivosAsync(usuarioId, agora, ct))
            token.Revogar(agora);
    }
}
