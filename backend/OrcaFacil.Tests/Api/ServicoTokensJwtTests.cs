using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrcaFacil.API.Seguranca;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Api;

public class ServicoTokensJwtTests
{
    private static readonly ConfiguracaoJwt Configuracao = new() { Chave = new string('k', 64) };
    private readonly ServicoTokensJwt _servico = new(Options.Create(Configuracao), TimeProvider.System);

    private static TokenValidationParameters Validacao(SecurityKey chave) => new()
    {
        ValidIssuer = Configuracao.Emissor,
        ValidAudience = Configuracao.Audiencia,
        IssuerSigningKey = chave
    };

    private static Usuario UsuarioDaEmpresa(Guid empresaId)
    {
        var usuario = new Usuario("Ana", "ana@loja.com", "hash", PerfilUsuario.Gerente);
        usuario.VincularEmpresa(empresaId);
        return usuario;
    }

    [Fact]
    public async Task Token_de_acesso_e_valido_e_carrega_empresa_e_perfil()
    {
        var empresaId = Guid.NewGuid();
        var usuario = UsuarioDaEmpresa(empresaId);

        var token = _servico.GerarTokenAcesso(usuario);
        var resultado = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Token, Validacao(Configuracao.ObterChaveAssinatura()));

        resultado.IsValid.Should().BeTrue();
        resultado.Claims[ClaimsOrcaFacil.EmpresaId].Should().Be(empresaId.ToString());
        resultado.Claims[ClaimsOrcaFacil.Perfil].Should().Be("Gerente");
        resultado.Claims[ClaimsOrcaFacil.UsuarioId].Should().Be(usuario.Id.ToString());
    }

    [Fact]
    public async Task Token_assinado_com_outra_chave_e_recusado()
    {
        var token = _servico.GerarTokenAcesso(UsuarioDaEmpresa(Guid.NewGuid()));
        var chaveDeUmAtacante = new ConfiguracaoJwt { Chave = new string('x', 64) }.ObterChaveAssinatura();

        var resultado = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, Validacao(chaveDeUmAtacante));

        resultado.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Refresh_token_e_aleatorio_e_so_o_hash_vai_para_o_banco()
    {
        var primeiro = _servico.GerarRefreshToken();
        var segundo = _servico.GerarRefreshToken();

        primeiro.Valor.Should().NotBe(segundo.Valor);
        primeiro.Hash.Should().NotBe(primeiro.Valor);
        _servico.CalcularHash(primeiro.Valor).Should().Be(primeiro.Hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("chave-curta")]
    public void Api_nao_inicia_com_chave_ausente_ou_curta(string chave) =>
        FluentActions.Invoking(() => new ConfiguracaoJwt { Chave = chave }.Validar())
            .Should().Throw<InvalidOperationException>();
}
