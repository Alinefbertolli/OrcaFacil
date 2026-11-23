using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OrcaFacil.API.Seguranca;

/// <summary>Configurações dos tokens, lidas da seção "Jwt" do appsettings + user-secrets.</summary>
public sealed class ConfiguracaoJwt
{
    public const string Secao = "Jwt";
    private const int TamanhoMinimoChaveBytes = 32; // 256 bits, exigido pelo HMAC-SHA256

    /// <summary>Quem emite o token (este sistema).</summary>
    public string Emissor { get; set; } = "OrcaFacil";

    /// <summary>Para quem o token é emitido (o frontend).</summary>
    public string Audiencia { get; set; } = "OrcaFacil.Web";

    /// <summary>
    /// Chave secreta que "assina" os tokens. Quem a conhece consegue criar tokens válidos,
    /// por isso ela NUNCA fica no código nem no appsettings: vai no user-secrets ou numa variável de ambiente.
    /// </summary>
    public string Chave { get; set; } = string.Empty;

    public int MinutosAcesso { get; set; } = 15;
    public int DiasRefresh { get; set; } = 7;
    public int MinutosRecuperacaoSenha { get; set; } = 60;

    public SymmetricSecurityKey ObterChaveAssinatura() => new(Encoding.UTF8.GetBytes(Chave));

    /// <summary>Impede a API de iniciar com uma chave ausente ou fraca (falha cedo, com instrução clara).</summary>
    public void Validar()
    {
        if (Encoding.UTF8.GetByteCount(Chave) < TamanhoMinimoChaveBytes)
            throw new InvalidOperationException(
                "A chave JWT (Jwt:Chave) não foi configurada ou é curta demais (mínimo de 32 caracteres). " +
                "Gere e grave uma chave com o user-secrets, na pasta backend (veja o README, seção \"Chave JWT\").");
    }
}

/// <summary>Nomes das informações (claims) gravadas dentro do token.</summary>
public static class ClaimsOrcaFacil
{
    public const string UsuarioId = "sub";
    public const string Nome = "name";
    public const string Email = "email";
    public const string Perfil = "role";
    public const string EmpresaId = "empresa_id";
}
