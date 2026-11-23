using Microsoft.EntityFrameworkCore;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Infrastructure.Persistencia;

namespace OrcaFacil.Infrastructure.Importacao;

/// <summary>
/// Busca por SIMILARIDADE de texto com a extensão pg_trgm do PostgreSQL (instalada na Fase 2).
///
/// Como funciona: o texto é quebrado em "trigramas" (pedaços de 3 letras).
/// "caneta" → {"  c"," ca","can","ane","net","eta","ta "}. Quanto mais pedaços em comum,
/// mais parecidos os textos. Assim "canetas azul" encontra "Caneta esferográfica azul",
/// mesmo com plural, ordem diferente ou pequenos erros de digitação.
/// - similarity(a, b): compara os textos inteiros;
/// - word_similarity(a, b): procura "a" como trecho de "b" (bom para descrições curtas).
/// </summary>
public sealed class BuscaProdutosSimilares(OrcaFacilDbContext contexto, IEmpresaAtual empresaAtual) : IBuscaProdutosSimilares
{
    public async Task<IReadOnlyList<ProdutoSimilar>> BuscarAsync(string descricao, int limite, CancellationToken ct = default)
    {
        // SQL escrito à mão (a consulta usa funções do PostgreSQL): por isso filtramos a empresa
        // explicitamente aqui. Os valores entre chaves viram parâmetros (sem risco de SQL injection).
        if (empresaAtual.EmpresaId is not Guid empresaId || string.IsNullOrWhiteSpace(descricao))
            return [];

        var termo = descricao.Trim();
        var linhas = await contexto.Database.SqlQuery<LinhaSimilar>($"""
            SELECT p."Id", p."Codigo", p."Nome", p."UnidadeMedida",
                   GREATEST(similarity(p."Nome", {termo}), word_similarity({termo}, p."Nome"))::double precision AS "Similaridade"
            FROM "Produtos" AS p
            WHERE p."EmpresaId" = {empresaId}
              AND p."Ativo"
              AND (p."Nome" % {termo} OR {termo} <% p."Nome")
            ORDER BY "Similaridade" DESC
            LIMIT {limite}
            """).ToListAsync(ct);

        return linhas.Select(l => new ProdutoSimilar(l.Id, l.Codigo, l.Nome, l.UnidadeMedida, l.Similaridade)).ToList();
    }
}

/// <summary>Formato de cada linha devolvida pelo SQL (os nomes precisam bater com as colunas).</summary>
internal sealed class LinhaSimilar
{
    public Guid Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = string.Empty;
    public double Similaridade { get; set; }
}
