using System.Net;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrcaFacil.Infrastructure.Persistencia;
using OrcaFacil.IntegrationTests.Infra;

namespace OrcaFacil.IntegrationTests;

/// <summary>O banco criado pelas migrations é igual ao modelo do código? A API enxerga o banco?</summary>
[Collection(ColecaoApi.Nome)]
public class BancoESaudeTests(ApiFixture api)
{
    [Fact]
    public async Task Health_check_responde_que_esta_tudo_bem()
    {
        var resposta = await api.Fabrica.CreateClient().GetAsync("/health");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Todas_as_migrations_foram_aplicadas_e_o_modelo_nao_tem_mudanca_sem_migration()
    {
        using var escopo = api.Fabrica.Services.CreateScope();
        var banco = escopo.ServiceProvider.GetRequiredService<OrcaFacilDbContext>();

        (await banco.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        // Se alguém mudar uma entidade e esquecer o "dotnet ef migrations add", este teste avisa.
        banco.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [Fact]
    public async Task Extensao_pg_trgm_da_busca_por_semelhanca_esta_instalada()
    {
        using var escopo = api.Fabrica.Services.CreateScope();
        var banco = escopo.ServiceProvider.GetRequiredService<OrcaFacilDbContext>();

        var extensoes = await banco.Database
            .SqlQueryRaw<string>("SELECT extname AS \"Value\" FROM pg_extension")
            .ToListAsync();

        extensoes.Should().Contain("pg_trgm");
    }
}
