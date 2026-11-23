using OrcaFacil.Domain.Comum;

namespace OrcaFacil.Domain.Entidades;

/// <summary>
/// Refresh token rotativo. Só o hash é persistido: um vazamento do banco não permite sequestrar sessões.
/// </summary>
public class RefreshToken : EntidadeBase
{
    private RefreshToken() { }

    public RefreshToken(Guid usuarioId, string tokenHash, DateTime expiraEm)
    {
        UsuarioId = usuarioId;
        TokenHash = tokenHash;
        ExpiraEm = expiraEm;
    }

    public Guid UsuarioId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiraEm { get; private set; }
    public DateTime? RevogadoEm { get; private set; }
    public Guid? SubstituidoPorId { get; private set; }

    public bool EstaAtivo(DateTime agoraUtc) => RevogadoEm is null && agoraUtc < ExpiraEm;

    public void Revogar(DateTime agoraUtc, Guid? substituidoPorId = null)
    {
        RevogadoEm ??= agoraUtc;
        SubstituidoPorId = substituidoPorId;
    }
}

/// <summary>Token de uso único para redefinição de senha.</summary>
public class TokenRecuperacaoSenha : EntidadeBase
{
    private TokenRecuperacaoSenha() { }

    public TokenRecuperacaoSenha(Guid usuarioId, string tokenHash, DateTime expiraEm)
    {
        UsuarioId = usuarioId;
        TokenHash = tokenHash;
        ExpiraEm = expiraEm;
    }

    public Guid UsuarioId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiraEm { get; private set; }
    public DateTime? UsadoEm { get; private set; }

    public void Utilizar(DateTime agoraUtc)
    {
        if (UsadoEm is not null) throw new DomainException("Este link de recuperação já foi utilizado.");
        if (agoraUtc >= ExpiraEm) throw new DomainException("Este link de recuperação expirou.");
        UsadoEm = agoraUtc;
    }
}
