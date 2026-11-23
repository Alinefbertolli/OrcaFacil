using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Domain.Entidades;

/// <summary>
/// Lista de materiais recebida (arquivo ou texto). Agregado que controla o ciclo
/// Processando → AguardandoRevisao → Revisada. Só uma lista Revisada pode virar orçamento.
/// </summary>
public class ListaImportada : EntidadeDaEmpresa
{
    private readonly List<ItemListaImportada> _itens = [];

    private ListaImportada() { }

    public ListaImportada(Guid usuarioId, FormatoImportacao formato, string? nomeArquivoOriginal, Guid? clienteId = null)
    {
        UsuarioId = usuarioId;
        Formato = formato;
        NomeArquivoOriginal = nomeArquivoOriginal;
        ClienteId = clienteId;
        Status = StatusImportacao.Processando;
    }

    public Guid UsuarioId { get; private set; }
    public Usuario Usuario { get; private set; } = null!;
    public Guid? ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }

    public FormatoImportacao Formato { get; private set; }
    public string? NomeArquivoOriginal { get; private set; }

    /// <summary>Caminho interno (nome aleatório, fora da pasta pública).</summary>
    public string? CaminhoArmazenamento { get; set; }
    public string? TextoExtraido { get; private set; }
    public StatusImportacao Status { get; private set; }
    public string? MensagemErro { get; private set; }

    public IReadOnlyCollection<ItemListaImportada> Itens => _itens.AsReadOnly();

    /// <summary>Itens que seguem para orçamentação (exclui os removidos na revisão).</summary>
    public IEnumerable<ItemListaImportada> ItensValidos => _itens.Where(i => !i.Excluido).OrderBy(i => i.Ordem);

    /// <summary>Conclui a extração e coloca a lista em revisão.</summary>
    public void ConcluirExtracao(string textoExtraido, IEnumerable<ItemListaImportada> itensInterpretados)
    {
        GarantirStatus(StatusImportacao.Processando);
        TextoExtraido = textoExtraido;

        var ordem = 1;
        foreach (var item in itensInterpretados)
        {
            item.VincularLista(Id, ordem++);
            _itens.Add(item);
        }

        Status = StatusImportacao.AguardandoRevisao;
    }

    public void RegistrarFalha(string mensagem)
    {
        MensagemErro = mensagem;
        Status = StatusImportacao.Erro;
    }

    public ItemListaImportada AdicionarItemManual(string descricao, decimal quantidade, string unidade, string? marca)
    {
        GarantirStatus(StatusImportacao.AguardandoRevisao);
        var item = new ItemListaImportada(descricao, quantidade, unidade, marca, textoOriginal: null, confianca: 1m);
        item.VincularLista(Id, _itens.Count == 0 ? 1 : _itens.Max(i => i.Ordem) + 1);
        _itens.Add(item);
        return item;
    }

    public void EditarItem(Guid itemId, string descricao, decimal quantidade, string unidade, string? marca, Guid? produtoId)
    {
        GarantirStatus(StatusImportacao.AguardandoRevisao);
        ObterItem(itemId).Corrigir(descricao, quantidade, unidade, marca, produtoId);
    }

    public void ExcluirItem(Guid itemId)
    {
        GarantirStatus(StatusImportacao.AguardandoRevisao);
        ObterItem(itemId).Excluir();
    }

    /// <summary>Regra do briefing: nunca gerar orçamento sem revisão do usuário.</summary>
    public void ConfirmarRevisao()
    {
        GarantirStatus(StatusImportacao.AguardandoRevisao);
        if (!ItensValidos.Any())
            throw new DomainException("A lista não possui itens válidos para orçamentar.");
        Status = StatusImportacao.Revisada;
    }

    public void GarantirProntaParaOrcamento()
    {
        if (Status != StatusImportacao.Revisada)
            throw new DomainException("A lista precisa ser revisada e confirmada antes de gerar orçamentos.");
    }

    private ItemListaImportada ObterItem(Guid itemId) =>
        _itens.FirstOrDefault(i => i.Id == itemId) ?? throw new DomainException("Item não encontrado na lista.");

    private void GarantirStatus(StatusImportacao esperado)
    {
        if (Status != esperado)
            throw new DomainException($"Operação não permitida: a lista está em '{Status}'.");
    }
}

public class ItemListaImportada : EntidadeBase
{
    private ItemListaImportada() { }

    public ItemListaImportada(string descricao, decimal quantidade, string unidade, string? marca,
        string? textoOriginal, decimal confianca)
    {
        Corrigir(descricao, quantidade, unidade, marca, produtoId: null);
        TextoOriginal = textoOriginal;
        ConfiancaInterpretacao = Math.Clamp(confianca, 0m, 1m);
    }

    public Guid ListaImportadaId { get; private set; }
    public ListaImportada ListaImportada { get; private set; } = null!;
    public int Ordem { get; private set; }

    /// <summary>Linha original, preservada para o usuário comparar com a interpretação.</summary>
    public string? TextoOriginal { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal Quantidade { get; private set; }
    public string UnidadeMedida { get; private set; } = null!;
    public string? Marca { get; private set; }

    /// <summary>Produto do catálogo associado (sugestão automática ou escolha do usuário).</summary>
    public Guid? ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }

    /// <summary>0 a 1. Itens abaixo de 0,7 são destacados na tela de revisão.</summary>
    public decimal ConfiancaInterpretacao { get; private set; }
    public bool Excluido { get; private set; }

    internal void VincularLista(Guid listaId, int ordem)
    {
        ListaImportadaId = listaId;
        Ordem = ordem;
    }

    /// <summary>Associação automática a um produto do catálogo, com a confiança calculada pela busca.</summary>
    public void SugerirProduto(Guid produtoId, decimal novaConfianca)
    {
        ProdutoId = produtoId;
        ConfiancaInterpretacao = Math.Clamp(novaConfianca, 0m, 1m);
    }

    internal void Corrigir(string descricao, decimal quantidade, string unidade, string? marca, Guid? produtoId)
    {
        Descricao = Guarda.TextoObrigatorio(descricao, "Descrição", 300);
        Quantidade = Guarda.Positivo(quantidade, "Quantidade");
        UnidadeMedida = Guarda.TextoObrigatorio(unidade, "Unidade de medida", 10).ToUpperInvariant();
        Marca = string.IsNullOrWhiteSpace(marca) ? null : marca.Trim();
        ProdutoId = produtoId; // null = sem produto do catálogo associado
        ConfiancaInterpretacao = 1m; // corrigido por humano
    }

    internal void Excluir() => Excluido = true;
}
