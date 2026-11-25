using Microsoft.EntityFrameworkCore;
using OrcaFacil.Application.Comum;
using OrcaFacil.Application.Estoques;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Infrastructure.Persistencia;

namespace OrcaFacil.Infrastructure.Estoques;

/// <summary>Consultas de estoque (o filtro automático por empresa vale em todas elas).</summary>
public sealed class RepositorioEstoques(OrcaFacilDbContext contexto) : IRepositorioEstoques
{
    public Task<Estoque?> ObterPorProdutoAsync(Guid produtoId, CancellationToken ct = default) =>
        contexto.Estoques.FirstOrDefaultAsync(e => e.ProdutoId == produtoId, ct);

    public async Task<IReadOnlyList<Estoque>> ListarPorProdutosAsync(IEnumerable<Guid> produtoIds, CancellationToken ct = default)
    {
        var ids = produtoIds.Distinct().ToList();
        return await contexto.Estoques.Where(e => ids.Contains(e.ProdutoId)).ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> ObterSaldosAsync(IEnumerable<Guid> produtoIds, CancellationToken ct = default)
    {
        var ids = produtoIds.Distinct().ToList();
        return await contexto.Estoques.AsNoTracking()
            .Where(e => ids.Contains(e.ProdutoId))
            .ToDictionaryAsync(e => e.ProdutoId, e => e.QuantidadeDisponivel, ct);
    }

    public void Adicionar(Estoque estoque) => contexto.Estoques.Add(estoque);

    public async Task<PaginaResultado<EstoqueResumo>> ListarAsync(ParametrosPaginacao paginacao, bool somenteAbaixoDoMinimo,
        string? busca, CancellationToken ct = default)
    {
        var consulta = contexto.Estoques.AsNoTracking();
        if (somenteAbaixoDoMinimo)
            consulta = consulta.Where(e => e.QuantidadeDisponivel < e.EstoqueMinimo);
        var termo = busca ?? string.Empty; // já vem em minúsculas (Validacao.NormalizarBusca)
        if (termo.Length > 0)
            consulta = consulta.Where(e => e.Produto.Nome.ToLower().Contains(termo) || e.Produto.Codigo.ToLower().Contains(termo));

        var total = await consulta.LongCountAsync(ct);
        var itens = await consulta
            .OrderBy(e => e.Produto.Nome).ThenBy(e => e.Id)
            .Skip(paginacao.Saltar).Take(paginacao.TamanhoPagina)
            .Select(e => new EstoqueResumo(e.ProdutoId, e.Produto.Codigo, e.Produto.Nome, e.Produto.UnidadeMedida,
                e.QuantidadeDisponivel, e.EstoqueMinimo, e.QuantidadeDisponivel < e.EstoqueMinimo, e.Localizacao))
            .ToListAsync(ct);

        return new PaginaResultado<EstoqueResumo>(itens, paginacao.Pagina, paginacao.TamanhoPagina, total);
    }

    public async Task<PaginaResultado<MovimentacaoResposta>> ListarMovimentacoesAsync(Guid produtoId,
        ParametrosPaginacao paginacao, CancellationToken ct = default)
    {
        var consulta = contexto.MovimentacoesEstoque.AsNoTracking().Where(m => m.Estoque.ProdutoId == produtoId);

        var total = await consulta.LongCountAsync(ct);
        var itens = await consulta
            .OrderByDescending(m => m.CriadoEm).ThenByDescending(m => m.Id) // mais recentes primeiro
            .Skip(paginacao.Saltar).Take(paginacao.TamanhoPagina)
            .Select(m => new MovimentacaoResposta(m.Id, m.Tipo, m.Quantidade, m.SaldoAnterior, m.SaldoPosterior,
                m.Motivo, m.UsuarioId, m.OrcamentoId, m.CriadoEm))
            .ToListAsync(ct);

        return new PaginaResultado<MovimentacaoResposta>(itens, paginacao.Pagina, paginacao.TamanhoPagina, total);
    }
}
