using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Domain.Entidades;

public class Usuario : EntidadeDaEmpresa
{
    private Usuario() { } // EF Core

    public Usuario(string nome, string email, string senhaHash, PerfilUsuario perfil)
    {
        Nome = Guarda.TextoObrigatorio(nome, "Nome", 150);
        Email = NormalizarEmail(email);
        DefinirSenhaHash(senhaHash);
        Perfil = perfil;
        Ativo = true;
    }

    public string Nome { get; private set; } = null!;
    public string Email { get; private set; } = null!;

    /// <summary>Hash gerado por IHasherSenha (Infrastructure). Nunca a senha em texto puro.</summary>
    public string SenhaHash { get; private set; } = null!;
    public PerfilUsuario Perfil { get; private set; }
    public bool Ativo { get; private set; }
    public DateTime? UltimoLoginEm { get; private set; }

    public static string NormalizarEmail(string email) =>
        Guarda.TextoObrigatorio(email, "E-mail", 200).ToLowerInvariant();

    public void AtualizarPerfil(string nome) => Nome = Guarda.TextoObrigatorio(nome, "Nome", 150);

    public void DefinirSenhaHash(string senhaHash) =>
        SenhaHash = Guarda.TextoObrigatorio(senhaHash, "Hash de senha", 500);

    public void AlterarPermissao(PerfilUsuario perfil) => Perfil = perfil;

    public void RegistrarLogin(DateTime agoraUtc) => UltimoLoginEm = agoraUtc;

    public void Desativar() => Ativo = false;
    public void Ativar() => Ativo = true;
}
