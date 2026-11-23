using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.API.Seguranca;

/// <summary>
/// Emite os tokens da sessão.
///
/// Um JWT (JSON Web Token) tem 3 partes separadas por ponto: cabeçalho.conteúdo.assinatura.
/// O conteúdo (claims) é apenas codificado — qualquer um consegue LER —, mas só quem tem a
/// chave secreta consegue ASSINAR. Se alguém alterar o conteúdo (ex.: trocar a empresa),
/// a assinatura deixa de bater e a API recusa o token. Por isso nunca guardamos segredos no JWT.
/// </summary>
public sealed class ServicoTokensJwt(IOptions<ConfiguracaoJwt> opcoes, TimeProvider relogio) : IServicoTokens
{
    private readonly ConfiguracaoJwt _configuracao = opcoes.Value;
    private static readonly JsonWebTokenHandler Manipulador = new();

    public TokenAcessoGerado GerarTokenAcesso(Usuario usuario)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        var expiraEm = agora.AddMinutes(_configuracao.MinutosAcesso);

        var descritor = new SecurityTokenDescriptor
        {
            Issuer = _configuracao.Emissor,
            Audience = _configuracao.Audiencia,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expiraEm,
            SigningCredentials = new SigningCredentials(_configuracao.ObterChaveAssinatura(), SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [ClaimsOrcaFacil.UsuarioId] = usuario.Id.ToString(),
                [ClaimsOrcaFacil.Nome] = usuario.Nome,
                [ClaimsOrcaFacil.Email] = usuario.Email,
                [ClaimsOrcaFacil.Perfil] = usuario.Perfil.ToString(),
                [ClaimsOrcaFacil.EmpresaId] = usuario.EmpresaId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString() // identificador único do token
            }
        };

        return new TokenAcessoGerado(Manipulador.CreateToken(descritor), expiraEm);
    }

    public TokenGerado GerarRefreshToken() =>
        GerarTokenAleatorio(relogio.GetUtcNow().UtcDateTime.AddDays(_configuracao.DiasRefresh));

    public TokenGerado GerarTokenRecuperacaoSenha() =>
        GerarTokenAleatorio(relogio.GetUtcNow().UtcDateTime.AddMinutes(_configuracao.MinutosRecuperacaoSenha));

    /// <summary>SHA-256 do token. Diferente da senha, o token já é aleatório e longo, então um hash rápido basta.</summary>
    public string CalcularHash(string valor) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));

    private TokenGerado GerarTokenAleatorio(DateTime expiraEm)
    {
        // 64 bytes de um gerador criptográfico (impossível de adivinhar), em texto seguro para URL.
        var valor = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return new TokenGerado(valor, CalcularHash(valor), expiraEm);
    }
}
