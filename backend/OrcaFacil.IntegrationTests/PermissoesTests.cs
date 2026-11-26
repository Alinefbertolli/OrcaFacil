using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using OrcaFacil.Application.Autenticacao;
using OrcaFacil.Application.Usuarios;
using OrcaFacil.Domain.Enums;
using OrcaFacil.IntegrationTests.Infra;

namespace OrcaFacil.IntegrationTests;

/// <summary>Cada perfil só faz o que pode: o operador (vendedor) não importa catálogo nem cria usuários.</summary>
[Collection(ColecaoApi.Nome)]
public class PermissoesTests(ApiFixture api)
{
    [Fact]
    public async Task Operador_consulta_produtos_mas_nao_importa_planilha_nem_cria_usuario()
    {
        var admin = await api.NovaEmpresaAsync();
        var email = $"operador-{Guid.NewGuid():N}@teste.com";
        await Garantir.SucessoAsync(await admin.Cliente.PostAsJsonAsync("/api/v1/usuarios",
            new CriarUsuarioRequisicao("Operador Teste", email, "Senha123forte", PerfilUsuario.Operador), ApiFixture.Json));

        var operador = api.Fabrica.CreateClient();
        var login = await Garantir.LerAsync<RespostaAutenticacao>(await operador.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequisicao(email, "Senha123forte"), ApiFixture.Json));
        operador.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.TokenAcesso);

        (await operador.GetAsync("/api/v1/produtos")).StatusCode.Should().Be(HttpStatusCode.OK);

        using var planilha = new MultipartFormDataContent
        {
            { new ByteArrayContent("Código;Nome\nA1;Caderno"u8.ToArray()), "arquivo", "produtos.csv" }
        };
        (await operador.PostAsync("/api/v1/produtos/importacao?simular=true", planilha))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await operador.PostAsJsonAsync("/api/v1/usuarios",
                new CriarUsuarioRequisicao("Intruso", "intruso@teste.com", "Senha123forte", PerfilUsuario.Administrador), ApiFixture.Json))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
