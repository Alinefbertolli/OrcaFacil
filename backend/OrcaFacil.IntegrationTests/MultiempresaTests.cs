using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Application.Comum;
using OrcaFacil.IntegrationTests.Infra;

namespace OrcaFacil.IntegrationTests;

/// <summary>
/// O teste mais importante de um SaaS: uma loja NUNCA pode ver ou mexer nos dados de outra.
/// Nos testes de unidade isso não dá para provar, porque o filtro por empresa é feito pelo
/// EF Core + PostgreSQL de verdade (os "global query filters").
/// </summary>
[Collection(ColecaoApi.Nome)]
public class MultiempresaTests(ApiFixture api)
{
    [Fact]
    public async Task Uma_empresa_nao_enxerga_nem_altera_os_produtos_de_outra()
    {
        var lojaA = await api.NovaEmpresaAsync("Papelaria A");
        var lojaB = await api.NovaEmpresaAsync("Papelaria B");
        var categoriaA = await lojaA.CategoriaAsync("Cadernos");
        var produtoA = await lojaA.ProdutoAsync("SEGREDO-A", "Caderno da loja A", categoriaA, 10m);

        // B lista: não aparece.
        var listaB = await Garantir.LerAsync<PaginaResultado<ProdutoResumo>>(
            await lojaB.Cliente.GetAsync("/api/v1/produtos?busca=SEGREDO"));
        listaB.Itens.Should().BeEmpty();

        // B tenta abrir, alterar e excluir pelo id: para B, esse produto "não existe" (404, e não 403,
        // para nem confirmar que o id existe em outra empresa).
        (await lojaB.Cliente.GetAsync($"/api/v1/produtos/{produtoA.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var categoriaB = await lojaB.CategoriaAsync("Cadernos");
        var alteracao = new ProdutoRequisicao("SEGREDO-A", "Hackeado", null, "UN", categoriaB, null, 0.01m);
        (await lojaB.Cliente.PutAsJsonAsync($"/api/v1/produtos/{produtoA.Id}", alteracao, ApiFixture.Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await lojaB.Cliente.DeleteAsync($"/api/v1/produtos/{produtoA.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A continua vendo o produto intacto.
        var produtoDepois = await Garantir.LerAsync<ProdutoDetalhe>(await lojaA.Cliente.GetAsync($"/api/v1/produtos/{produtoA.Id}"));
        produtoDepois.Nome.Should().Be("Caderno da loja A");
        produtoDepois.PrecoVenda.Should().Be(10m);
    }

    [Fact]
    public async Task Duas_empresas_podem_usar_o_mesmo_codigo_de_produto()
    {
        var lojaA = await api.NovaEmpresaAsync();
        var lojaB = await api.NovaEmpresaAsync();

        await lojaA.ProdutoAsync("CAD-001", "Caderno A", await lojaA.CategoriaAsync("Cadernos"), 10m);
        var produtoB = await lojaB.ProdutoAsync("CAD-001", "Caderno B", await lojaB.CategoriaAsync("Cadernos"), 12m);

        produtoB.Codigo.Should().Be("CAD-001");
    }
}
