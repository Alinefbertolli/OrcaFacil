using Microsoft.EntityFrameworkCore;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Orcamentos;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Infrastructure.Persistencia;

namespace OrcaFacil.Infrastructure.Orcamentos;

/// <summary>
/// Encontra os produtos EQUIVALENTES (substitutos) de um produto, para o orçamento comparativo.
/// 1) Grupo de equivalência preenchido → todos os produtos ativos do mesmo grupo (decisão da loja, mais confiável);
/// 2) sem grupo → produtos ativos da MESMA categoria com nome parecido (pg_trgm, a mesma técnica da importação).
/// </summary>
public sealed class CatalogoEquivalentes(OrcaFacilDbContext contexto, IEmpresaAtual empresaAtual) : ICatalogoEquivalentes
{
    /// <summary>
    /// Similaridade mínima (0 a 1) entre os NOMES para considerar dois produtos equivalentes quando não há grupo.
    /// Mais alto = mais exigente. Se aparecerem trocas estranhas, cadastre o Grupo de equivalência.
    /// </summary>
    public const double SimilaridadeMinima = 0.5;

    public async Task<IReadOnlyList<ProdutoCandidato>> ObterAsync(IEnumerable<Guid> produtoIds, CancellationToken ct = default)
    {
        var ids = produtoIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await Projetar(contexto.Produtos.Where(p => ids.Contains(p.Id))).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProdutoCandidato>> BuscarEquivalentesAsync(Guid produtoId, CancellationToken ct = default)
    {
        var produto = await contexto.Produtos.AsNoTracking()
            .Where(p => p.Id == produtoId)
            .Select(p => new { p.Id, p.Nome, p.CategoriaId, p.GrupoEquivalencia })
            .FirstOrDefaultAsync(ct);
        if (produto is null) return [];

        // 1) Pelo grupo (o filtro automático por empresa vale aqui, pois é uma consulta LINQ comum).
        if (produto.GrupoEquivalencia is string grupo)
            return await Projetar(contexto.Produtos.Where(p => p.Ativo && p.GrupoEquivalencia == grupo)).ToListAsync(ct);

        // 2) Pela similaridade do nome. SQL à mão (função do PostgreSQL): filtramos a empresa explicitamente.
        if (empresaAtual.EmpresaId is not Guid empresaId) return [];

        var ids = await contexto.Database.SqlQuery<Guid>($"""
            SELECT p."Id" AS "Value"
            FROM "Produtos" AS p
            WHERE p."EmpresaId" = {empresaId}
              AND p."Ativo"
              AND p."CategoriaId" = {produto.CategoriaId}
              AND similarity(p."Nome", {produto.Nome}) >= {SimilaridadeMinima}
            """).ToListAsync(ct);
        ids.Add(produto.Id); // o próprio produto sempre conta (se estiver ativo)

        return await Projetar(contexto.Produtos.Where(p => p.Ativo && ids.Contains(p.Id))).ToListAsync(ct);
    }

    /// <summary>Busca só as colunas necessárias, já juntando a marca (um único SELECT com JOIN).</summary>
    private static IQueryable<ProdutoCandidato> Projetar(IQueryable<Produto> consulta) =>
        consulta.AsNoTracking().Select(p => new ProdutoCandidato(
            p.Id, p.Codigo, p.Nome, p.UnidadeMedida, p.PrecoVenda, p.Ativo,
            p.Marca != null ? p.Marca.Nome : null,
            p.Marca != null ? p.Marca.NivelReconhecimento : (int?)null,
            p.Estoque != null ? p.Estoque.QuantidadeDisponivel : (decimal?)null)); // null = sem controle de estoque
}
