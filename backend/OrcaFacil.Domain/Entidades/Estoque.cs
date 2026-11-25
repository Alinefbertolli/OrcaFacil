using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Domain.Entidades;

/// <summary>
/// Saldo de um produto (1:1). Toda alteração de saldo passa por um método
/// que devolve a MovimentacaoEstoque correspondente: não existe saldo sem rastro.
/// </summary>
public class Estoque : EntidadeDaEmpresa
{
    private readonly List<MovimentacaoEstoque> _movimentacoes = [];

    private Estoque() { }

    public Estoque(Guid produtoId, decimal estoqueMinimo, string? localizacao = null)
    {
        ProdutoId = produtoId;
        DefinirEstoqueMinimo(estoqueMinimo);
        Localizacao = localizacao;
    }

    public Guid ProdutoId { get; private set; }
    public Produto Produto { get; private set; } = null!;
    public decimal QuantidadeDisponivel { get; private set; }
    public decimal EstoqueMinimo { get; private set; }
    public string? Localizacao { get; set; }

    public const int TamanhoMaximoLocalizacao = 100;

    /// <summary>Token de concorrência (xmin no PostgreSQL) evita duas saídas simultâneas furarem o saldo.</summary>
    public uint Versao { get; private set; }

    public IReadOnlyCollection<MovimentacaoEstoque> Movimentacoes => _movimentacoes.AsReadOnly();

    public bool AbaixoDoMinimo => QuantidadeDisponivel < EstoqueMinimo;

    public void DefinirEstoqueMinimo(decimal minimo) =>
        EstoqueMinimo = Guarda.NaoNegativo(minimo, "Estoque mínimo");

    public MovimentacaoEstoque RegistrarEntrada(decimal quantidade, Guid usuarioId, string? motivo, Guid? orcamentoId = null)
    {
        Guarda.Positivo(quantidade, "Quantidade");
        return Movimentar(TipoMovimentacaoEstoque.Entrada, quantidade, QuantidadeDisponivel + quantidade,
            usuarioId, motivo, orcamentoId);
    }

    /// <param name="orcamentoId">Preenchido quando a saída é a venda de um orçamento finalizado (rastreabilidade).</param>
    public MovimentacaoEstoque RegistrarSaida(decimal quantidade, Guid usuarioId, string? motivo, Guid? orcamentoId = null)
    {
        Guarda.Positivo(quantidade, "Quantidade");
        if (quantidade > QuantidadeDisponivel)
            throw new DomainException(
                $"Saldo insuficiente. Disponível: {QuantidadeDisponivel:0.###}, solicitado: {quantidade:0.###}.");

        return Movimentar(TipoMovimentacaoEstoque.Saida, quantidade, QuantidadeDisponivel - quantidade,
            usuarioId, motivo, orcamentoId);
    }

    /// <summary>Inventário: define o saldo real contado. O motivo é obrigatório para auditoria.</summary>
    public MovimentacaoEstoque AjustarSaldo(decimal saldoContado, Guid usuarioId, string motivo)
    {
        Guarda.NaoNegativo(saldoContado, "Saldo contado");
        Guarda.TextoObrigatorio(motivo, "Motivo do ajuste", 500);
        var diferenca = Math.Abs(saldoContado - QuantidadeDisponivel);
        if (diferenca == 0)
            throw new DomainException("O saldo contado é igual ao saldo atual; nenhum ajuste necessário.");

        return Movimentar(TipoMovimentacaoEstoque.Ajuste, diferenca, saldoContado, usuarioId, motivo, orcamentoId: null);
    }

    private MovimentacaoEstoque Movimentar(TipoMovimentacaoEstoque tipo, decimal quantidade, decimal novoSaldo,
        Guid usuarioId, string? motivo, Guid? orcamentoId)
    {
        var movimentacao = new MovimentacaoEstoque(Id, tipo, quantidade, QuantidadeDisponivel, novoSaldo,
            usuarioId, motivo, orcamentoId);
        PropagarEmpresaPara(movimentacao);
        QuantidadeDisponivel = novoSaldo;
        _movimentacoes.Add(movimentacao);
        return movimentacao;
    }
}

/// <summary>Registro imutável de movimentação.</summary>
public class MovimentacaoEstoque : EntidadeDaEmpresa
{
    private MovimentacaoEstoque() { }

    internal MovimentacaoEstoque(Guid estoqueId, TipoMovimentacaoEstoque tipo, decimal quantidade,
        decimal saldoAnterior, decimal saldoPosterior, Guid usuarioId, string? motivo, Guid? orcamentoId)
    {
        EstoqueId = estoqueId;
        Tipo = tipo;
        Quantidade = quantidade;
        SaldoAnterior = saldoAnterior;
        SaldoPosterior = saldoPosterior;
        UsuarioId = usuarioId;
        Motivo = motivo?.Trim();
        OrcamentoId = orcamentoId;
    }

    public Guid EstoqueId { get; private set; }
    public Estoque Estoque { get; private set; } = null!;
    public TipoMovimentacaoEstoque Tipo { get; private set; }
    public decimal Quantidade { get; private set; }
    public decimal SaldoAnterior { get; private set; }
    public decimal SaldoPosterior { get; private set; }
    public string? Motivo { get; private set; }
    public Guid UsuarioId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public Guid? OrcamentoId { get; private set; }
    public Orcamento? Orcamento { get; private set; }
}
