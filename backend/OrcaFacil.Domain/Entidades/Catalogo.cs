using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Domain.Entidades;

public class Categoria : EntidadeDaEmpresa
{
    public string Nome { get; set; } = null!;
    public string? Descricao { get; set; }
    public Guid? CategoriaPaiId { get; set; }
    public Categoria? CategoriaPai { get; set; }
    public ICollection<Categoria> Subcategorias { get; private set; } = [];
    public ICollection<Produto> Produtos { get; private set; } = [];
}

/// <summary>
/// Marca com nível de reconhecimento definido pelo usuário (1 a 5).
/// A estratégia "Marcas reconhecidas" usa este dado explícito em vez de o sistema presumir reputação.
/// </summary>
public class Marca : EntidadeDaEmpresa
{
    public const int NivelMinimo = 1;
    public const int NivelMaximo = 5;

    private int _nivelReconhecimento = 3;

    public string Nome { get; set; } = null!;

    public int NivelReconhecimento
    {
        get => _nivelReconhecimento;
        set
        {
            if (value is < NivelMinimo or > NivelMaximo)
                throw new DomainException($"Nível de reconhecimento deve estar entre {NivelMinimo} e {NivelMaximo}.");
            _nivelReconhecimento = value;
        }
    }

    public ICollection<Produto> Produtos { get; private set; } = [];
}

public class Produto : EntidadeDaEmpresa
{
    /// <summary>Código interno único (SKU).</summary>
    public string Codigo { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public string? Descricao { get; set; }

    /// <summary>Sigla livre e normalizada em maiúsculas: UN, CX, PCT, KG, G, M, M2, L, RESMA, PAR, KIT...</summary>
    public string UnidadeMedida { get; set; } = null!;

    public Guid CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public Guid? MarcaId { get; set; }
    public Marca? Marca { get; set; }

    /// <summary>
    /// PREÇO DE VENDA da loja: é o valor que aparece no orçamento para o cliente final.
    /// (Os preços dos fornecedores, em ProdutoFornecedor, são o CUSTO: uso interno.)
    /// Null = a loja ainda não cadastrou o preço; o orçamento mostra o item como "sem preço".
    /// </summary>
    public decimal? PrecoVenda { get; private set; }

    /// <summary>
    /// Grupo de equivalência (opcional), ex.: "caderno 10 materias".
    /// Produtos com o MESMO grupo são substitutos entre si no orçamento comparativo.
    /// Guardado normalizado (minúsculas, sem acentos, espaços simples) para "Caderno 10 Matérias" = "caderno 10 materias".
    /// </summary>
    public string? GrupoEquivalencia { get; private set; }

    public bool Ativo { get; set; } = true;

    public ICollection<ProdutoFornecedor> Fornecedores { get; private set; } = [];
    public ICollection<HistoricoPreco> HistoricoPrecos { get; private set; } = [];
    public Estoque? Estoque { get; private set; }

    public const int TamanhoMaximoGrupo = 100;

    public void DefinirPrecoVenda(decimal? preco)
    {
        if (preco is decimal valor)
            Guarda.NaoNegativo(valor, "Preço de venda");
        PrecoVenda = preco;
    }

    public void DefinirGrupoEquivalencia(string? grupo)
    {
        var normalizado = NormalizarGrupo(grupo);
        if (normalizado is { Length: > TamanhoMaximoGrupo })
            throw new DomainException($"O grupo de equivalência deve ter no máximo {TamanhoMaximoGrupo} caracteres.");
        GrupoEquivalencia = normalizado;
    }

    /// <summary>"  Caderno  10 Matérias " → "caderno 10 materias". Vazio → null.</summary>
    public static string? NormalizarGrupo(string? grupo)
    {
        if (string.IsNullOrWhiteSpace(grupo)) return null;

        // Remove acentos: "á" vira "a" + "´" (FormD); depois descartamos o "´" (NonSpacingMark).
        var decomposto = grupo.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var semAcento = new System.Text.StringBuilder(decomposto.Length);
        foreach (var letra in decomposto)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(letra) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
                semAcento.Append(letra);
        }

        var palavras = semAcento.ToString().Normalize(System.Text.NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', palavras);
    }
}

public class Fornecedor : EntidadeDaEmpresa
{
    private decimal? _avaliacao;

    public string RazaoSocial { get; set; } = null!;
    public string? NomeFantasia { get; set; }

    /// <summary>CNPJ somente dígitos, validado na camada Application (DocumentoFiscal.CnpjValido).</summary>
    public string Cnpj { get; set; } = null!;
    public string? Contato { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public Endereco? Endereco { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Nota de 0 a 5 atribuída pela equipe de compras.</summary>
    public decimal? Avaliacao
    {
        get => _avaliacao;
        set
        {
            if (value is < 0 or > 5)
                throw new DomainException("Avaliação do fornecedor deve estar entre 0 e 5.");
            _avaliacao = value;
        }
    }

    public ICollection<ProdutoFornecedor> Produtos { get; private set; } = [];
}

/// <summary>Associação N:N produto × fornecedor com a tabela de preço vigente.</summary>
public class ProdutoFornecedor : EntidadeDaEmpresa
{
    private ProdutoFornecedor() { }

    public ProdutoFornecedor(Guid produtoId, Guid fornecedorId)
    {
        ProdutoId = produtoId;
        FornecedorId = fornecedorId;
    }

    public Guid ProdutoId { get; private set; }
    public Produto Produto { get; private set; } = null!;
    public Guid FornecedorId { get; private set; }
    public Fornecedor Fornecedor { get; private set; } = null!;

    public string? CodigoNoFornecedor { get; set; }
    public decimal? PrecoAtual { get; private set; }
    public int? PrazoEntregaDias { get; set; }
    public bool Disponivel { get; set; } = true;
    public DateTime? PrecoAtualizadoEm { get; private set; }

    /// <summary>
    /// Atualiza o preço e devolve o registro de histórico correspondente,
    /// para que nenhuma alteração de preço deixe de ser historizada.
    /// </summary>
    public HistoricoPreco AtualizarPreco(decimal preco, DateTime agoraUtc)
    {
        Guarda.NaoNegativo(preco, "Preço");
        PrecoAtual = preco;
        PrecoAtualizadoEm = agoraUtc;
        var historico = new HistoricoPreco(ProdutoId, FornecedorId, preco, OrigemPreco.FornecedorCadastrado, agoraUtc);
        PropagarEmpresaPara(historico);
        return historico;
    }
}

public class HistoricoPreco : EntidadeDaEmpresa
{
    private HistoricoPreco() { }

    public HistoricoPreco(Guid produtoId, Guid? fornecedorId, decimal preco, OrigemPreco origem, DateTime registradoEm)
    {
        ProdutoId = produtoId;
        FornecedorId = fornecedorId;
        Preco = Guarda.NaoNegativo(preco, "Preço");
        Origem = origem;
        RegistradoEm = registradoEm;
    }

    public Guid ProdutoId { get; private set; }
    public Produto Produto { get; private set; } = null!;
    public Guid? FornecedorId { get; private set; }
    public Fornecedor? Fornecedor { get; private set; }
    public decimal Preco { get; private set; }
    public OrigemPreco Origem { get; private set; }
    public DateTime RegistradoEm { get; private set; }
}

/// <summary>Oferta retornada por uma consulta de preço, já normalizada.</summary>
public class Cotacao : EntidadeDaEmpresa
{
    public Guid? ProdutoId { get; set; }
    public Produto? Produto { get; set; }
    public Guid? FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    /// <summary>Nome do provedor de integração que gerou a oferta (ex.: "Simulado").</summary>
    public string Provedor { get; set; } = null!;
    public string TermoPesquisa { get; set; } = null!;
    public string DescricaoEncontrada { get; set; } = null!;
    public string? MarcaEncontrada { get; set; }
    public string? UnidadeMedida { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal? Frete { get; set; }
    public int? PrazoEntregaDias { get; set; }
    public bool Disponivel { get; set; }
    public OrigemPreco Origem { get; set; }
    public DateTime ConsultadoEm { get; set; }
    public DateTime? ValidaAte { get; set; }
    public string? UrlOferta { get; set; }
}
