using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrcaFacil.Application.Autenticacao;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Infrastructure.Persistencia;
using Testcontainers.PostgreSql;

namespace OrcaFacil.IntegrationTests.Infra;

/// <summary>
/// Prepara o "mundo" dos testes de integração UMA vez para todos os testes da coleção:
/// 1) um PostgreSQL 17 de verdade num contêiner Docker descartável (apagado no fim);
/// 2) a API de verdade, rodando em memória (WebApplicationFactory), apontando para esse banco;
/// 3) as migrations aplicadas (o banco fica igual ao de produção).
/// Nada disso toca no seu banco de desenvolvimento nem nos seus segredos.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    // A mesma imagem do docker-compose: o Docker reaproveita o download.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public WebApplicationFactory<Program> Fabrica { get; private set; } = null!;

    /// <summary>Lê e escreve o JSON da API (enums em texto, nomes em camelCase).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Fabrica = CriarFabrica(tentativasDeLogin: 1000);

        // Aplica as migrations no banco novo (o mesmo que "dotnet ef database update").
        using var escopo = Fabrica.Services.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<OrcaFacilDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Fabrica.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Uma API configurada para teste. "Testing" (e não "Development") garante que os user-secrets
    /// da máquina NÃO são lidos: a conexão e a chave do JWT são só as daqui.
    /// </summary>
    public WebApplicationFactory<Program> CriarFabrica(int tentativasDeLogin) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("Jwt:Chave", Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
            builder.UseSetting("Seguranca:TentativasDeAutenticacaoPorMinuto", tentativasDeLogin.ToString());
            builder.UseSetting("Uploads:Diretorio", Path.Combine(Path.GetTempPath(), "orcafacil-testes", Guid.NewGuid().ToString()));
        });

    /// <summary>Cria uma empresa nova (com o administrador) e devolve um cliente HTTP já logado.</summary>
    public async Task<SessaoDeTeste> NovaEmpresaAsync(string nome = "Papelaria Teste")
    {
        var cliente = Fabrica.CreateClient();
        var senha = "Senha123forte";
        var email = $"admin-{Guid.NewGuid():N}@teste.com";
        var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/registrar", new RegistrarEmpresaRequisicao(
            nome, nome, Cnpj.Gerar(), SegmentoEmpresa.Papelaria, "Admin Teste", email, senha), Json);
        await Garantir.SucessoAsync(resposta);

        var autenticacao = (await resposta.Content.ReadFromJsonAsync<RespostaAutenticacao>(Json))!;
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", autenticacao.TokenAcesso);
        return new SessaoDeTeste(cliente, autenticacao, email, senha);
    }
}

public sealed record SessaoDeTeste(HttpClient Cliente, RespostaAutenticacao Autenticacao, string Email, string Senha)
{
    public Guid EmpresaId => Autenticacao.Usuario.EmpresaId;
}

/// <summary>
/// Todos os testes de integração ficam na MESMA coleção: compartilham o contêiner (subir o Postgres
/// leva alguns segundos) e rodam um de cada vez. Cada teste cria a SUA empresa, então um não enxerga
/// os dados do outro (é o próprio multiempresa trabalhando a nosso favor).
/// </summary>
[CollectionDefinition(Nome)]
public sealed class ColecaoApi : ICollectionFixture<ApiFixture>
{
    public const string Nome = "API com PostgreSQL";
}

/// <summary>Gera CNPJs válidos (com dígitos verificadores corretos) e diferentes a cada chamada.</summary>
public static class Cnpj
{
    public static string Gerar()
    {
        var digitos = new int[14];
        for (var i = 0; i < 8; i++) digitos[i] = RandomNumberGenerator.GetInt32(10);
        digitos[8] = 0; digitos[9] = 0; digitos[10] = 0; digitos[11] = 1; // "0001" = matriz
        digitos[12] = Digito(digitos, 12, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        digitos[13] = Digito(digitos, 13, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return string.Concat(digitos);
    }

    private static int Digito(int[] digitos, int quantos, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < quantos; i++) soma += digitos[i] * pesos[i];
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}

public static class Garantir
{
    /// <summary>Como EnsureSuccessStatusCode, mas mostra o corpo da resposta no erro (ajuda a entender a falha).</summary>
    public static async Task SucessoAsync(HttpResponseMessage resposta)
    {
        if (resposta.IsSuccessStatusCode) return;
        var corpo = await resposta.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"{(int)resposta.StatusCode} {resposta.StatusCode} em {resposta.RequestMessage?.Method} {resposta.RequestMessage?.RequestUri}: {corpo}");
    }

    public static async Task<T> LerAsync<T>(HttpResponseMessage resposta)
    {
        await SucessoAsync(resposta);
        return (await resposta.Content.ReadFromJsonAsync<T>(ApiFixture.Json))!;
    }
}
