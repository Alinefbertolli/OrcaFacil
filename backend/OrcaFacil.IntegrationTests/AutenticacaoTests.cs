using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using OrcaFacil.Application.Autenticacao;
using OrcaFacil.IntegrationTests.Infra;

namespace OrcaFacil.IntegrationTests;

[Collection(ColecaoApi.Nome)]
public class AutenticacaoTests(ApiFixture api)
{
    [Fact]
    public async Task Sem_token_a_API_recusa_com_401()
    {
        var resposta = await api.Fabrica.CreateClient().GetAsync("/api/v1/produtos");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Registrar_ja_abre_a_sessao_do_administrador()
    {
        var sessao = await api.NovaEmpresaAsync();

        var resposta = await sessao.Cliente.GetAsync("/api/v1/usuarios/me");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        sessao.Autenticacao.Usuario.Perfil.Should().Be(OrcaFacil.Domain.Enums.PerfilUsuario.Administrador);
    }

    [Fact]
    public async Task Login_com_senha_errada_da_401_e_com_a_certa_da_um_token_que_funciona()
    {
        var sessao = await api.NovaEmpresaAsync();
        var cliente = api.Fabrica.CreateClient();

        var errada = await cliente.PostAsJsonAsync("/api/v1/auth/login", new LoginRequisicao(sessao.Email, "SenhaErrada123"), ApiFixture.Json);
        errada.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var certa = await Garantir.LerAsync<RespostaAutenticacao>(
            await cliente.PostAsJsonAsync("/api/v1/auth/login", new LoginRequisicao(sessao.Email, sessao.Senha), ApiFixture.Json));
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", certa.TokenAcesso);
        (await cliente.GetAsync("/api/v1/usuarios/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_token_renova_a_sessao_e_so_pode_ser_usado_uma_vez()
    {
        var sessao = await api.NovaEmpresaAsync();
        var cliente = api.Fabrica.CreateClient();
        var pedido = new RenovarSessaoRequisicao(sessao.Autenticacao.RefreshToken);

        var primeira = await cliente.PostAsJsonAsync("/api/v1/auth/refresh", pedido, ApiFixture.Json);
        var novaSessao = await Garantir.LerAsync<RespostaAutenticacao>(primeira);
        novaSessao.TokenAcesso.Should().NotBeNullOrWhiteSpace();

        // Rotação: o refresh token antigo deixa de valer depois de usado (protege contra roubo de token).
        var reuso = await cliente.PostAsJsonAsync("/api/v1/auth/refresh", pedido, ApiFixture.Json);
        reuso.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // E mais: o reuso é tratado como roubo, então TODAS as sessões do usuário caem (inclusive a nova).
        var sessaoNova = await cliente.PostAsJsonAsync("/api/v1/auth/refresh",
            new RenovarSessaoRequisicao(novaSessao.RefreshToken), ApiFixture.Json);
        sessaoNova.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Depois_de_5_tentativas_de_login_por_minuto_a_API_responde_429()
    {
        // Uma API separada, com o limite REAL (5 por minuto), só para este teste.
        await using var fabrica = api.CriarFabrica(tentativasDeLogin: 5);
        var cliente = fabrica.CreateClient();
        var pedido = new LoginRequisicao("ninguem@teste.com", "SenhaErrada123");

        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
            codigos.Add((await cliente.PostAsJsonAsync("/api/v1/auth/login", pedido, ApiFixture.Json)).StatusCode);

        codigos.Take(5).Should().AllBeEquivalentTo(HttpStatusCode.Unauthorized);
        codigos[5].Should().Be(HttpStatusCode.TooManyRequests);
    }
}
