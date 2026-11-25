using OrcaFacil.Application.Comum;
using OrcaFacil.Application.Estoques;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Tests.Application;

/// <summary>Estoques em memória (as listagens paginadas usam SQL de verdade e são testadas no Swagger).</summary>
internal sealed class FakeEstoques : IRepositorioEstoques
{
    public List<Estoque> Itens { get; } = [];

    public Task<Estoque?> ObterPorProdutoAsync(Guid produtoId, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(e => e.ProdutoId == produtoId));

    public Task<IReadOnlyList<Estoque>> ListarPorProdutosAsync(IEnumerable<Guid> produtoIds, CancellationToken ct = default)
    {
        var ids = produtoIds.ToHashSet();
        return Task.FromResult<IReadOnlyList<Estoque>>(Itens.Where(e => ids.Contains(e.ProdutoId)).ToList());
    }

    public Task<IReadOnlyDictionary<Guid, decimal>> ObterSaldosAsync(IEnumerable<Guid> produtoIds, CancellationToken ct = default)
    {
        var ids = produtoIds.ToHashSet();
        return Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(
            Itens.Where(e => ids.Contains(e.ProdutoId)).ToDictionary(e => e.ProdutoId, e => e.QuantidadeDisponivel));
    }

    public void Adicionar(Estoque estoque) => Itens.Add(estoque);

    public Task<PaginaResultado<EstoqueResumo>> ListarAsync(ParametrosPaginacao paginacao, bool somenteAbaixoDoMinimo,
        string? busca, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<PaginaResultado<MovimentacaoResposta>> ListarMovimentacoesAsync(Guid produtoId, ParametrosPaginacao paginacao,
        CancellationToken ct = default) => throw new NotSupportedException();

    /// <summary>Cria o estoque de um produto já com saldo (atalho para montar cenários).</summary>
    public Estoque ComSaldo(Guid produtoId, decimal saldo)
    {
        var estoque = new Estoque(produtoId, estoqueMinimo: 0);
        if (saldo > 0) estoque.RegistrarEntrada(saldo, Guid.NewGuid(), "Saldo inicial do teste");
        Itens.Add(estoque);
        return estoque;
    }
}
