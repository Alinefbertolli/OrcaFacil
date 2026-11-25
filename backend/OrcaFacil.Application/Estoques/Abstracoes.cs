using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Application.Estoques;

/// <summary>
/// Situação de um produto no estoque, para uma quantidade pedida.
/// "NaoControlado" = a loja não controla o estoque deste produto (ex.: vende sob encomenda):
/// o sistema não sabe o saldo e por isso não bloqueia nada.
/// </summary>
public enum DisponibilidadeEstoque
{
    NaoControlado = 0,
    Disponivel = 1,
    /// <summary>Tem saldo, mas menos do que o pedido.</summary>
    Parcial = 2,
    Indisponivel = 3
}

public static class Disponibilidade
{
    /// <param name="saldo">Saldo atual. Null = produto sem controle de estoque.</param>
    public static DisponibilidadeEstoque Avaliar(decimal? saldo, decimal quantidadePedida) => saldo switch
    {
        null => DisponibilidadeEstoque.NaoControlado,
        var s when s >= quantidadePedida => DisponibilidadeEstoque.Disponivel,
        > 0 => DisponibilidadeEstoque.Parcial,
        _ => DisponibilidadeEstoque.Indisponivel
    };

    /// <summary>Pode atender o pedido? (sem controle de estoque conta como "pode": a loja decide.)</summary>
    public static bool Atende(decimal? saldo, decimal quantidadePedida) =>
        Avaliar(saldo, quantidadePedida) is DisponibilidadeEstoque.NaoControlado or DisponibilidadeEstoque.Disponivel;
}

public sealed record EstoqueResumo(
    Guid ProdutoId, string ProdutoCodigo, string ProdutoNome, string UnidadeMedida,
    decimal QuantidadeDisponivel, decimal EstoqueMinimo, bool AbaixoDoMinimo, string? Localizacao);

public sealed record MovimentacaoResposta(
    Guid Id, TipoMovimentacaoEstoque Tipo, decimal Quantidade, decimal SaldoAnterior, decimal SaldoPosterior,
    string? Motivo, Guid UsuarioId, Guid? OrcamentoId, DateTime CriadoEm);

/// <summary>Acesso ao estoque. Um produto tem no máximo um registro de estoque (1:1).</summary>
public interface IRepositorioEstoques
{
    /// <summary>O estoque do produto, para alterar (rastreado). Null = produto ainda sem controle de estoque.</summary>
    Task<Estoque?> ObterPorProdutoAsync(Guid produtoId, CancellationToken ct = default);

    /// <summary>Os estoques desses produtos, para alterar (uma consulta só).</summary>
    Task<IReadOnlyList<Estoque>> ListarPorProdutosAsync(IEnumerable<Guid> produtoIds, CancellationToken ct = default);

    /// <summary>Saldo atual por produto (só os produtos que têm controle de estoque aparecem).</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> ObterSaldosAsync(IEnumerable<Guid> produtoIds, CancellationToken ct = default);

    void Adicionar(Estoque estoque);

    Task<PaginaResultado<EstoqueResumo>> ListarAsync(ParametrosPaginacao paginacao, bool somenteAbaixoDoMinimo,
        string? busca, CancellationToken ct = default);

    Task<PaginaResultado<MovimentacaoResposta>> ListarMovimentacoesAsync(Guid produtoId, ParametrosPaginacao paginacao,
        CancellationToken ct = default);
}
