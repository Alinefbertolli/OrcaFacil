using System.Net.Http.Json;
using OrcaFacil.Application.Cadastros;

namespace OrcaFacil.IntegrationTests.Infra;

/// <summary>Atalhos para montar o catálogo de uma empresa pela própria API.</summary>
public static class Catalogo
{
    public static async Task<Guid> CategoriaAsync(this SessaoDeTeste sessao, string nome) =>
        (await Garantir.LerAsync<CategoriaResposta>(await sessao.Cliente.PostAsJsonAsync("/api/v1/categorias",
            new CategoriaRequisicao(nome, null, null), ApiFixture.Json))).Id;

    public static async Task<Guid> MarcaAsync(this SessaoDeTeste sessao, string nome, int nivel) =>
        (await Garantir.LerAsync<MarcaResposta>(await sessao.Cliente.PostAsJsonAsync("/api/v1/marcas",
            new MarcaRequisicao(nome, nivel), ApiFixture.Json))).Id;

    public static async Task<ProdutoDetalhe> ProdutoAsync(this SessaoDeTeste sessao, string codigo, string nome,
        Guid categoriaId, decimal preco, Guid? marcaId = null, string? grupo = null) =>
        await Garantir.LerAsync<ProdutoDetalhe>(await sessao.Cliente.PostAsJsonAsync("/api/v1/produtos",
            new ProdutoRequisicao(codigo, nome, null, "UN", categoriaId, marcaId, preco, grupo), ApiFixture.Json));
}
