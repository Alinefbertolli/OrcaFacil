using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Application.Abstracoes;

/// <summary>
/// Transforma senhas em "hash": uma impressão digital irreversível, com sal aleatório.
/// O banco guarda só o hash; nem quem tem acesso ao banco consegue descobrir a senha.
/// </summary>
public interface IHasherSenha
{
    string GerarHash(string senha);
    bool Verificar(string senhaHash, string senhaInformada);
}

/// <summary>Emite os tokens da sessão. Implementado na API (JWT).</summary>
public interface IServicoTokens
{
    /// <summary>Token de acesso (JWT, curta duração) com os dados do usuário e da empresa.</summary>
    TokenAcessoGerado GerarTokenAcesso(Usuario usuario);

    /// <summary>Refresh token: valor aleatório de longa duração, usado para obter um novo token de acesso.</summary>
    TokenGerado GerarRefreshToken();

    /// <summary>Token de uso único para o link "esqueci minha senha".</summary>
    TokenGerado GerarTokenRecuperacaoSenha();

    /// <summary>Hash do token para guardar no banco (o valor original só existe com o usuário).</summary>
    string CalcularHash(string valor);
}

public sealed record TokenAcessoGerado(string Token, DateTime ExpiraEm);

/// <summary>Token aleatório: Valor vai para o usuário; Hash vai para o banco.</summary>
public sealed record TokenGerado(string Valor, string Hash, DateTime ExpiraEm);

/// <summary>Envio de e-mails do sistema.</summary>
public interface IEnvioEmail
{
    Task EnviarRecuperacaoSenhaAsync(string destinatario, string nome, string token, DateTime expiraEm,
        CancellationToken cancellationToken = default);
}

/// <summary>Usuário logado nesta requisição (lido do token JWT). Null = ninguém logado.</summary>
public interface IUsuarioAtual
{
    Guid? UsuarioId { get; }
}

/// <summary>
/// Consultas usadas no login, ANTES de se saber a empresa do usuário.
/// Por isso ignoram o filtro automático por empresa (com cuidado e só aqui).
/// </summary>
public interface IRepositorioAutenticacao
{
    Task<Usuario?> ObterUsuarioPorEmailAsync(string emailNormalizado, CancellationToken cancellationToken = default);
    Task<Usuario?> ObterUsuarioPorIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<bool> EmailEmUsoAsync(string emailNormalizado, CancellationToken cancellationToken = default);
    Task<Empresa?> ObterEmpresaAsync(Guid empresaId, CancellationToken cancellationToken = default);
    Task<bool> CnpjEmUsoAsync(string cnpj, CancellationToken cancellationToken = default);
    Task<RefreshToken?> ObterRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefreshToken>> ListarRefreshTokensAtivosAsync(Guid usuarioId, DateTime agoraUtc,
        CancellationToken cancellationToken = default);
    Task<TokenRecuperacaoSenha?> ObterTokenRecuperacaoAsync(string tokenHash, CancellationToken cancellationToken = default);

    void Adicionar(Empresa empresa);
    void Adicionar(Usuario usuario);
    void Adicionar(RefreshToken refreshToken);
    void Adicionar(TokenRecuperacaoSenha token);
    void Adicionar(LogSistema log);
}
