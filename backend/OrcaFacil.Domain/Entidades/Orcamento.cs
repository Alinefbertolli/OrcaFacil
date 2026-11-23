using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Domain.Entidades;

/// <summary>
/// Cabeçalho do orçamento (cliente, número, status, validade) e raiz do agregado
/// Orcamento → OpcaoOrcamento (até 3 geradas + manual) → ItemOrcamento.
/// </summary>
public class Orcamento : EntidadeDaEmpresa
{
    /// <summary>Máquina de estados do briefing. Finalizado é terminal.</summary>
    private static readonly Dictionary<StatusOrcamento, StatusOrcamento[]> TransicoesPermitidas = new()
    {
        [StatusOrcamento.EmElaboracao] = [StatusOrcamento.AguardandoAprovacao],
        [StatusOrcamento.AguardandoAprovacao] = [StatusOrcamento.Aprovado, StatusOrcamento.Reprovado, StatusOrcamento.EmElaboracao],
        [StatusOrcamento.Aprovado] = [StatusOrcamento.Finalizado],
        [StatusOrcamento.Reprovado] = [StatusOrcamento.EmElaboracao],
        [StatusOrcamento.Finalizado] = []
    };

    private readonly List<OpcaoOrcamento> _opcoes = [];

    private Orcamento() { }

    public Orcamento(string numero, Guid usuarioId, Guid? clienteId, DateOnly validadeAte, Guid? listaImportadaId = null,
        ModoOrcamento modo = ModoOrcamento.Comparativo)
    {
        Modo = modo;
        Numero = Guarda.TextoObrigatorio(numero, "Número", 30);
        UsuarioId = usuarioId;
        ClienteId = clienteId;
        ValidadeAte = validadeAte;
        ListaImportadaId = listaImportadaId;
        Status = StatusOrcamento.EmElaboracao;
    }

    public string Numero { get; private set; } = null!;
    public Guid UsuarioId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public Guid? ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }
    public Guid? ListaImportadaId { get; private set; }
    public ListaImportada? ListaImportada { get; private set; }

    /// <summary>Preenchido quando o orçamento é uma duplicação de outro.</summary>
    public Guid? OrcamentoOrigemId { get; private set; }

    public StatusOrcamento Status { get; private set; }

    /// <summary>Único (itens exatos) ou Comparativo (opções com produtos equivalentes).</summary>
    public ModoOrcamento Modo { get; private set; }
    public DateOnly ValidadeAte { get; private set; }
    public string? CondicoesComerciais { get; private set; }
    public string? Observacoes { get; private set; }

    public IReadOnlyCollection<OpcaoOrcamento> Opcoes => _opcoes.AsReadOnly();
    public OpcaoOrcamento? OpcaoSelecionada => _opcoes.SingleOrDefault(o => o.Selecionada);

    public bool PodeSerEditado => Status == StatusOrcamento.EmElaboracao;

    public OpcaoOrcamento AdicionarOpcao(TipoOpcaoOrcamento tipo)
    {
        GarantirEditavel();
        if (_opcoes.Any(o => o.Tipo == tipo))
            throw new DomainException($"O orçamento já possui uma opção do tipo {tipo}.");

        var opcao = new OpcaoOrcamento(Id, tipo);
        _opcoes.Add(opcao);
        return opcao;
    }

    public void SelecionarOpcao(Guid opcaoId)
    {
        GarantirEditavel();
        var escolhida = _opcoes.FirstOrDefault(o => o.Id == opcaoId)
            ?? throw new DomainException("Opção de orçamento não encontrada.");

        if (!escolhida.Itens.Any())
            throw new DomainException("Não é possível selecionar uma opção sem itens.");

        foreach (var opcao in _opcoes) opcao.Desmarcar();
        escolhida.Marcar();
    }

    /// <summary>
    /// Tira a seleção de todas as opções. Usado antes de selecionar outra opção:
    /// o banco só aceita UMA opção marcada por orçamento, então desmarcamos e salvamos primeiro.
    /// </summary>
    public void LimparSelecao()
    {
        GarantirEditavel();
        foreach (var opcao in _opcoes) opcao.Desmarcar();
    }

    /// <summary>Preenche à mão o preço de um item (por exemplo, um item que ficou "sem preço").</summary>
    public void InformarPrecoItem(Guid opcaoId, Guid itemId, decimal precoUnitario)
    {
        GarantirEditavel();
        var opcao = _opcoes.FirstOrDefault(o => o.Id == opcaoId)
            ?? throw new DomainException("Opção de orçamento não encontrada.");
        opcao.InformarPrecoManual(itemId, precoUnitario);
    }

    public void AtualizarDados(Guid? clienteId, DateOnly validadeAte, string? condicoesComerciais, string? observacoes)
    {
        GarantirEditavel();
        ClienteId = clienteId;
        ValidadeAte = validadeAte;
        CondicoesComerciais = condicoesComerciais?.Trim();
        Observacoes = observacoes?.Trim();
    }

    public void AlterarStatus(StatusOrcamento novoStatus)
    {
        if (!TransicoesPermitidas[Status].Contains(novoStatus))
            throw new DomainException($"Não é permitido alterar o status de '{Status}' para '{novoStatus}'.");

        // Para ir à aprovação é preciso ter uma proposta definida.
        if (novoStatus == StatusOrcamento.AguardandoAprovacao)
        {
            var selecionada = OpcaoSelecionada
                ?? throw new DomainException("Selecione uma opção de orçamento antes de enviar para aprovação.");

            // Regra central: nunca enviar ao cliente um valor incompleto ou inventado.
            if (selecionada.ItensSemPreco > 0)
                throw new DomainException(
                    $"A opção selecionada tem {selecionada.ItensSemPreco} item(ns) sem preço. Informe os preços antes de enviar.");
        }

        Status = novoStatus;
    }

    /// <summary>
    /// Economia da opção selecionada em relação à opção mais cara.
    /// Zero se não houver seleção ou houver uma única opção.
    /// </summary>
    public decimal CalcularEconomia()
    {
        var selecionada = OpcaoSelecionada;
        if (selecionada is null || _opcoes.Count < 2) return 0m;
        return _opcoes.Max(o => o.ValorTotal) - selecionada.ValorTotal;
    }

    /// <summary>
    /// Cria uma cópia em elaboração, com novo número, copiando opções e itens.
    /// A cópia mantém a mesma seleção de opção do original.
    /// </summary>
    public Orcamento Duplicar(string novoNumero, Guid usuarioId, DateOnly novaValidade)
    {
        var copia = new Orcamento(novoNumero, usuarioId, ClienteId, novaValidade, ListaImportadaId, Modo)
        {
            OrcamentoOrigemId = Id,
            CondicoesComerciais = CondicoesComerciais,
            Observacoes = Observacoes
        };
        PropagarEmpresaPara(copia); // a cópia pertence à mesma empresa do original

        foreach (var opcao in _opcoes)
        {
            var novaOpcao = copia.AdicionarOpcao(opcao.Tipo);
            foreach (var item in opcao.Itens)
                novaOpcao.AdicionarItem(item.CriarCopia());
            if (opcao.Selecionada) novaOpcao.Marcar();
        }

        return copia;
    }

    private void GarantirEditavel()
    {
        if (!PodeSerEditado)
            throw new DomainException($"Orçamentos com status '{Status}' não podem ser alterados.");
    }
}

public class OpcaoOrcamento : EntidadeBase
{
    private readonly List<ItemOrcamento> _itens = [];

    private OpcaoOrcamento() { }

    internal OpcaoOrcamento(Guid orcamentoId, TipoOpcaoOrcamento tipo)
    {
        OrcamentoId = orcamentoId;
        Tipo = tipo;
    }

    public Guid OrcamentoId { get; private set; }
    public Orcamento Orcamento { get; private set; } = null!;
    public TipoOpcaoOrcamento Tipo { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal FreteTotal { get; private set; }
    public decimal ValorTotal { get; private set; }
    public bool Selecionada { get; private set; }

    public IReadOnlyCollection<ItemOrcamento> Itens => _itens.AsReadOnly();

    /// <summary>Indica se algum preço da opção não veio de cotação real (exibido como alerta).</summary>
    public bool PossuiPrecoNaoCotado =>
        _itens.Any(i => i.OrigemPreco is OrigemPreco.Estimado or OrigemPreco.Simulado);

    /// <summary>Quantos itens ainda estão "sem preço" (o total da opção está incompleto).</summary>
    public int ItensSemPreco => _itens.Count(i => i.PrecoPendente);

    // Observação: a verificação de "orçamento editável" é feita pela raiz (Orcamento)
    // nos casos de uso; aqui garantimos a consistência dos totais.
    public void AdicionarItem(ItemOrcamento item)
    {
        item.VincularOpcao(Id);
        _itens.Add(item);
        RecalcularTotais();
    }

    public void RemoverItem(Guid itemId)
    {
        var item = _itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException("Item não encontrado na opção.");
        _itens.Remove(item);
        RecalcularTotais();
    }

    public void AtualizarPrecoItem(Guid itemId, decimal precoUnitario, OrigemPreco origem, decimal? frete)
    {
        var item = _itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException("Item não encontrado na opção.");
        item.DefinirPreco(precoUnitario, origem, frete);
        RecalcularTotais();
    }

    internal void InformarPrecoManual(Guid itemId, decimal precoUnitario)
    {
        var item = _itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new DomainException("Item não encontrado na opção.");
        item.DefinirPreco(precoUnitario, OrigemPreco.Manual, item.Frete);
        RecalcularTotais();
    }

    /// <summary>Totais sempre derivados dos itens — nunca informados pelo cliente da API.</summary>
    public void RecalcularTotais()
    {
        Subtotal = _itens.Sum(i => i.PrecoTotal);
        FreteTotal = _itens.Sum(i => i.Frete ?? 0m);
        ValorTotal = Subtotal + FreteTotal;
    }

    internal void Marcar() => Selecionada = true;
    internal void Desmarcar() => Selecionada = false;
}

public class ItemOrcamento : EntidadeBase
{
    private ItemOrcamento() { }

    public ItemOrcamento(string descricao, string? marca, decimal quantidade, string unidadeMedida,
        decimal precoUnitario, OrigemPreco origemPreco, Guid? produtoId = null, Guid? fornecedorId = null,
        Guid? cotacaoId = null, decimal? frete = null, int? prazoEntregaDias = null, bool disponivel = true)
    {
        Descricao = Guarda.TextoObrigatorio(descricao, "Descrição", 300);
        Marca = marca;
        Quantidade = Guarda.Positivo(quantidade, "Quantidade");
        UnidadeMedida = Guarda.TextoObrigatorio(unidadeMedida, "Unidade de medida", 10).ToUpperInvariant();
        ProdutoId = produtoId;
        FornecedorId = fornecedorId;
        CotacaoId = cotacaoId;
        PrazoEntregaDias = prazoEntregaDias;
        Disponivel = disponivel;
        DefinirPreco(precoUnitario, origemPreco, frete);
    }

    public Guid OpcaoOrcamentoId { get; private set; }
    public OpcaoOrcamento OpcaoOrcamento { get; private set; } = null!;
    public Guid? ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }
    public Guid? FornecedorId { get; private set; }
    public Fornecedor? Fornecedor { get; private set; }
    public Guid? CotacaoId { get; private set; }
    public Cotacao? Cotacao { get; private set; }

    public string Descricao { get; private set; } = null!;
    public string? Marca { get; private set; }
    public decimal Quantidade { get; private set; }
    public string UnidadeMedida { get; private set; } = null!;
    public decimal PrecoUnitario { get; private set; }
    public decimal PrecoTotal { get; private set; }
    public decimal? Frete { get; private set; }
    public int? PrazoEntregaDias { get; private set; }
    public bool Disponivel { get; private set; }
    public OrigemPreco OrigemPreco { get; private set; }

    /// <summary>Linha da lista importada que originou este item (liga as opções na comparação lado a lado).</summary>
    public Guid? ItemListaImportadaId { get; private set; }

    /// <summary>Posição do item no orçamento (a mesma da lista importada).</summary>
    public int Ordem { get; private set; }

    /// <summary>
    /// True = a loja ainda precisa informar o preço (produto não associado ou sem preço de venda).
    /// Nesse caso PrecoUnitario/PrecoTotal ficam 0 e NÃO devem ser mostrados como preço.
    /// </summary>
    public bool PrecoPendente { get; private set; }

    /// <summary>Avisos para o vendedor (ex.: "marca pedida não encontrada", "unidade diferente").</summary>
    public string? Observacao { get; private set; }

    public const int TamanhoMaximoObservacao = 500;

    /// <summary>Cria um item "sem preço": aparece no orçamento, mas a loja precisa preencher o valor.</summary>
    public static ItemOrcamento SemPreco(string descricao, string? marca, decimal quantidade, string unidadeMedida,
        Guid? produtoId = null, string? observacao = null)
    {
        var item = new ItemOrcamento(descricao, marca, quantidade, unidadeMedida, 0m, OrigemPreco.Manual, produtoId)
        {
            PrecoPendente = true
        };
        item.AdicionarObservacao(observacao);
        return item;
    }

    public void VincularLinhaDaLista(Guid itemListaImportadaId, int ordem)
    {
        ItemListaImportadaId = itemListaImportadaId;
        Ordem = ordem;
    }

    public void AdicionarObservacao(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return;
        var juntos = Observacao is null ? texto.Trim() : $"{Observacao} {texto.Trim()}";
        Observacao = juntos.Length <= TamanhoMaximoObservacao ? juntos : juntos[..TamanhoMaximoObservacao];
    }

    internal void VincularOpcao(Guid opcaoId) => OpcaoOrcamentoId = opcaoId;

    internal void DefinirPreco(decimal precoUnitario, OrigemPreco origem, decimal? frete)
    {
        PrecoUnitario = Guarda.NaoNegativo(precoUnitario, "Preço unitário");
        if (frete is not null) Guarda.NaoNegativo(frete.Value, "Frete");
        Frete = frete;
        OrigemPreco = origem;
        PrecoPendente = false; // recebeu um preço de verdade
        // Arredondamento comercial (meio para cima) a 2 casas no total do item.
        PrecoTotal = Math.Round(Quantidade * PrecoUnitario, 2, MidpointRounding.AwayFromZero);
    }

    internal ItemOrcamento CriarCopia() =>
        new(Descricao, Marca, Quantidade, UnidadeMedida, PrecoUnitario, OrigemPreco,
            ProdutoId, FornecedorId, CotacaoId, Frete, PrazoEntregaDias, Disponivel)
        {
            ItemListaImportadaId = ItemListaImportadaId,
            Ordem = Ordem,
            PrecoPendente = PrecoPendente,
            Observacao = Observacao
        };
}
