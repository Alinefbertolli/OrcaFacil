using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Application.Orcamentos;

/// <summary>
/// Um produto do catálogo da loja que pode entrar no orçamento, já com o que o motor precisa saber:
/// preço de venda e nível de reconhecimento da marca.
/// </summary>
/// <param name="PrecoVenda">Null = a loja não cadastrou o preço (o item fica "sem preço").</param>
/// <param name="NivelMarca">1 a 5 (cadastro da marca). Null = produto sem marca.</param>
/// <param name="SaldoEstoque">Saldo atual no estoque. Null = a loja não controla o estoque deste produto.</param>
public sealed record ProdutoCandidato(
    Guid Id,
    string Codigo,
    string Nome,
    string UnidadeMedida,
    decimal? PrecoVenda,
    bool Ativo,
    string? MarcaNome,
    int? NivelMarca,
    decimal? SaldoEstoque = null);

/// <summary>
/// Consulta o catálogo para o motor de orçamentação. Implementado na Infrastructure
/// (usa a busca por similaridade pg_trgm do PostgreSQL).
/// </summary>
public interface ICatalogoEquivalentes
{
    /// <summary>Os produtos com esses Ids, como candidatos (inclusive inativos, para avisar o usuário).</summary>
    Task<IReadOnlyList<ProdutoCandidato>> ObterAsync(IEnumerable<Guid> produtoIds, CancellationToken ct = default);

    /// <summary>
    /// Produtos ATIVOS equivalentes ao produto informado (ele mesmo incluído, se estiver ativo).
    /// Regra (decisão da usuária, 18/11/2026: "vamos usar os dois"):
    /// 1) se o produto tem Grupo de equivalência → os produtos do mesmo grupo;
    /// 2) senão → produtos da MESMA categoria com nome parecido (similaridade pg_trgm).
    /// </summary>
    Task<IReadOnlyList<ProdutoCandidato>> BuscarEquivalentesAsync(Guid produtoId, CancellationToken ct = default);
}

/// <summary>Carrega o orçamento JUNTO com as opções e os itens (o repositório genérico não carrega filhos).</summary>
public interface IRepositorioOrcamentos
{
    Task<Orcamento?> ObterCompletoAsync(Guid id, CancellationToken ct = default);
}
