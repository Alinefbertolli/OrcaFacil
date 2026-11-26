using System.Globalization;
using System.Text;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Application.Cadastros;

// Fase 11.5 (parte 1): importar o catálogo (produtos, preços e estoque) de uma planilha exportada do ERP.
// Quase todo ERP exporta Excel ou CSV, então isto funciona com qualquer um, sem integração.

public enum FormatoPlanilha
{
    Xlsx = 1,
    Csv = 2
}

/// <summary>
/// Lê e gera planilhas. Implementado na Infrastructure (ClosedXML para .xlsx; leitor próprio para .csv).
/// </summary>
public interface IPlanilhasCatalogo
{
    /// <summary>
    /// Lê a primeira aba com dados. Cada item é uma linha da planilha (a posição 0 é a linha 1),
    /// e cada linha é a lista de textos das células. Números vêm no formato neutro ("12.5").
    /// </summary>
    /// <exception cref="OrcaFacil.Domain.Comum.DomainException">Arquivo corrompido ou ilegível.</exception>
    IReadOnlyList<IReadOnlyList<string>> Ler(Stream conteudo, FormatoPlanilha formato);

    /// <summary>Planilha modelo (.xlsx) com o cabeçalho certo e uma aba de instruções.</summary>
    byte[] GerarModelo();
}

/// <summary>As informações que o OrçaFácil sabe ler de cada coluna.</summary>
public enum CampoPlanilha
{
    Codigo,
    Nome,
    Descricao,
    PrecoVenda,
    Estoque,
    EstoqueMinimo,
    Unidade,
    Categoria,
    Marca,
    GrupoEquivalencia
}

/// <summary>Uma linha da planilha já convertida. Null = coluna ausente ou célula vazia ("não mexer").</summary>
public sealed record LinhaCatalogo(
    int NumeroLinha,
    string Codigo,
    string? Nome,
    string? Descricao,
    decimal? PrecoVenda,
    decimal? Estoque,
    decimal? EstoqueMinimo,
    string? Unidade,
    string? Categoria,
    string? Marca,
    string? GrupoEquivalencia);

public sealed record ErroLinhaPlanilha(int NumeroLinha, string? Codigo, string? Nome, string Mensagem);

public sealed record PlanilhaInterpretada(
    IReadOnlyList<CampoPlanilha> ColunasReconhecidas,
    IReadOnlyList<LinhaCatalogo> Linhas,
    IReadOnlyList<ErroLinhaPlanilha> Erros);

/// <summary>
/// Transforma as células da planilha em <see cref="LinhaCatalogo"/>. Código puro, sem banco: fácil de testar.
/// 1) acha a linha de cabeçalho (precisa ter Código e Nome/Descrição), reconhecendo vários nomes de coluna;
/// 2) converte números no jeito brasileiro ("R$ 1.234,56") ou neutro ("1234.56");
/// 3) aponta os problemas de cada linha sem parar as outras.
/// </summary>
public static class InterpretadorPlanilhaCatalogo
{
    public const int MaximoLinhas = 5000;
    private const int LinhasParaProcurarCabecalho = 10;

    /// <summary>
    /// Nomes de coluna aceitos para cada campo, já "normalizados" (minúsculas, sem acento, sem pontuação).
    /// A ordem importa: se a planilha tiver "Nome" e "Descrição", "Nome" vira o nome e "Descrição" a descrição.
    /// Comparamos o título INTEIRO (e não "contém"), para "Preço de custo" não ser confundido com o preço de venda.
    /// </summary>
    private static readonly (CampoPlanilha Campo, string[] Nomes)[] Apelidos =
    [
        (CampoPlanilha.Codigo, ["codigo", "cod", "sku", "referencia", "ref", "codigo do produto", "codigo produto",
            "cod produto", "codigo interno", "cod interno"]),
        (CampoPlanilha.Nome, ["nome", "nome do produto", "nome produto", "produto", "descricao", "descricao do produto",
            "descricao produto"]),
        (CampoPlanilha.Descricao, ["descricao", "descricao complementar", "descricao detalhada", "detalhes"]),
        (CampoPlanilha.PrecoVenda, ["preco de venda", "preco venda", "preco", "valor de venda", "valor venda", "valor",
            "preco unitario", "valor unitario", "preco final"]),
        (CampoPlanilha.EstoqueMinimo, ["estoque minimo", "minimo", "qtd minima", "quantidade minima"]),
        (CampoPlanilha.Estoque, ["estoque", "saldo", "estoque atual", "saldo atual", "saldo em estoque", "saldo estoque",
            "quantidade", "quantidade em estoque", "qtd", "qtde", "qtd estoque"]),
        (CampoPlanilha.Unidade, ["unidade", "unidade de medida", "un", "und", "unid", "medida"]),
        (CampoPlanilha.GrupoEquivalencia, ["grupo de equivalencia", "grupo equivalencia", "equivalencia"]),
        (CampoPlanilha.Categoria, ["categoria", "categoria do produto", "departamento", "secao", "grupo", "familia"]),
        (CampoPlanilha.Marca, ["marca", "fabricante"]),
    ];

    /// <summary>Nome amigável de cada campo (para mostrar na tela).</summary>
    public static string NomeDoCampo(CampoPlanilha campo) => campo switch
    {
        CampoPlanilha.Codigo => "Código",
        CampoPlanilha.Nome => "Nome",
        CampoPlanilha.Descricao => "Descrição",
        CampoPlanilha.PrecoVenda => "Preço de venda",
        CampoPlanilha.Estoque => "Estoque",
        CampoPlanilha.EstoqueMinimo => "Estoque mínimo",
        CampoPlanilha.Unidade => "Unidade",
        CampoPlanilha.Categoria => "Categoria",
        CampoPlanilha.Marca => "Marca",
        CampoPlanilha.GrupoEquivalencia => "Grupo de equivalência",
        _ => campo.ToString()
    };

    public static PlanilhaInterpretada Interpretar(IReadOnlyList<IReadOnlyList<string>> linhas)
    {
        var (indiceCabecalho, colunas) = AcharCabecalho(linhas);

        var resultado = new List<LinhaCatalogo>();
        var erros = new List<ErroLinhaPlanilha>();
        var codigosVistos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var linhasComDados = 0;

        for (var i = indiceCabecalho + 1; i < linhas.Count; i++)
        {
            var celulas = linhas[i];
            if (celulas.All(string.IsNullOrWhiteSpace)) continue; // linha em branco: ignora

            if (++linhasComDados > MaximoLinhas)
                throw Validacao.Erro("arquivo",
                    $"A planilha passa do limite de {MaximoLinhas} produtos. Divida em arquivos menores.");

            var numero = i + 1; // o usuário conta as linhas a partir de 1, como no Excel
            string? Texto(CampoPlanilha campo) =>
                colunas.TryGetValue(campo, out var c) && c < celulas.Count ? Validacao.TextoOpcional(celulas[c]) : null;

            var codigo = Texto(CampoPlanilha.Codigo);
            var nome = Texto(CampoPlanilha.Nome);
            var problemas = new List<string>();

            if (codigo is null)
            {
                erros.Add(new ErroLinhaPlanilha(numero, null, nome, "Linha sem código (SKU)."));
                continue;
            }

            if (codigosVistos.TryGetValue(codigo, out var primeiraLinha))
            {
                erros.Add(new ErroLinhaPlanilha(numero, codigo, nome,
                    $"Código repetido na planilha (já apareceu na linha {primeiraLinha})."));
                continue;
            }
            codigosVistos[codigo] = numero;

            Tamanho(problemas, "Código", codigo, 50);
            Tamanho(problemas, "Nome", nome, 200);
            var descricao = Texto(CampoPlanilha.Descricao);
            Tamanho(problemas, "Descrição", descricao, 2000);
            var unidade = Texto(CampoPlanilha.Unidade)?.ToUpperInvariant();
            Tamanho(problemas, "Unidade", unidade, 10);
            var categoria = Texto(CampoPlanilha.Categoria);
            Tamanho(problemas, "Categoria", categoria, 100);
            var marca = Texto(CampoPlanilha.Marca);
            Tamanho(problemas, "Marca", marca, 100);
            var grupo = Texto(CampoPlanilha.GrupoEquivalencia);
            Tamanho(problemas, "Grupo de equivalência", grupo, Produto.TamanhoMaximoGrupo);

            var preco = Numero(problemas, "Preço de venda", Texto(CampoPlanilha.PrecoVenda));
            var estoque = Numero(problemas, "Estoque", Texto(CampoPlanilha.Estoque));
            var minimo = Numero(problemas, "Estoque mínimo", Texto(CampoPlanilha.EstoqueMinimo));

            if (problemas.Count > 0)
            {
                erros.Add(new ErroLinhaPlanilha(numero, codigo, nome, string.Join(" ", problemas)));
                continue;
            }

            resultado.Add(new LinhaCatalogo(numero, codigo, nome, descricao, preco, estoque, minimo, unidade,
                categoria, marca, grupo));
        }

        return new PlanilhaInterpretada(colunas.Keys.Order().ToList(), resultado, erros);
    }

    /// <summary>
    /// Converte "R$ 1.234,56", "1234,56", "1,234.56", "12.5" ou "1.500" em número. Vazio = null.
    /// Regra para separadores: com vírgula E ponto, o que vem por último é o decimal;
    /// só uma vírgula = decimal; só um ponto = decimal, EXCETO com exatamente 3 dígitos depois
    /// ("1.500" = mil e quinhentos, como se escreve no Brasil); vários do mesmo = separador de milhar.
    /// </summary>
    public static bool TentarLerNumero(string? texto, out decimal? valor)
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;

        var limpo = texto.Replace("R$", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "").Replace(" ", "").Trim();
        if (limpo.Length == 0) return true;

        var virgulas = limpo.Count(c => c == ',');
        var pontos = limpo.Count(c => c == '.');

        if (virgulas > 0 && pontos > 0)
        {
            var decimalEhVirgula = limpo.LastIndexOf(',') > limpo.LastIndexOf('.');
            limpo = decimalEhVirgula ? limpo.Replace(".", "").Replace(',', '.') : limpo.Replace(",", "");
        }
        else if (virgulas > 0)
        {
            limpo = virgulas == 1 ? limpo.Replace(',', '.') : limpo.Replace(",", "");
        }
        else if (pontos > 1 || (pontos == 1 && limpo.Length - limpo.IndexOf('.') - 1 == 3 && !limpo.StartsWith("0.")))
        {
            // "1.234.567" ou "1.500": no Brasil o ponto com 3 dígitos depois é separador de milhar ("0.500" não).
            limpo = limpo.Replace(".", "");
        }

        if (!decimal.TryParse(limpo, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var numero))
            return false;

        valor = numero;
        return true;
    }

    /// <summary>"Preço de Venda (R$)" → "preco de venda r". Sem acentos, minúsculas, só letras/números.</summary>
    public static string NormalizarTitulo(string? titulo)
    {
        if (string.IsNullOrWhiteSpace(titulo)) return string.Empty;

        var decomposto = titulo.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var texto = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria == UnicodeCategory.NonSpacingMark) continue; // o acento separado da letra
            texto.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return string.Join(' ', texto.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static (int Indice, Dictionary<CampoPlanilha, int> Colunas) AcharCabecalho(
        IReadOnlyList<IReadOnlyList<string>> linhas)
    {
        for (var i = 0; i < Math.Min(linhas.Count, LinhasParaProcurarCabecalho); i++)
        {
            var colunas = IdentificarColunas(linhas[i]);
            if (colunas.ContainsKey(CampoPlanilha.Codigo) && colunas.ContainsKey(CampoPlanilha.Nome))
                return (i, colunas);
        }

        throw Validacao.Erro("arquivo",
            "Não encontrei o cabeçalho da planilha. A primeira linha precisa ter pelo menos as colunas " +
            "\"Código\" e \"Nome\" (ou \"Descrição\"). Dica: baixe a planilha modelo.");
    }

    private static Dictionary<CampoPlanilha, int> IdentificarColunas(IReadOnlyList<string> cabecalho)
    {
        var titulos = cabecalho.Select(NormalizarTitulo).ToList();
        var usadas = new HashSet<int>();
        var colunas = new Dictionary<CampoPlanilha, int>();

        foreach (var (campo, nomes) in Apelidos)
        {
            foreach (var nome in nomes)
            {
                var posicao = titulos.FindIndex(t => t == nome);
                if (posicao >= 0 && usadas.Add(posicao))
                {
                    colunas[campo] = posicao;
                    break;
                }
            }
        }

        return colunas;
    }

    private static void Tamanho(List<string> problemas, string campo, string? valor, int maximo)
    {
        if (valor is { Length: var n } && n > maximo)
            problemas.Add($"{campo} com mais de {maximo} caracteres.");
    }

    private static decimal? Numero(List<string> problemas, string campo, string? texto)
    {
        if (!TentarLerNumero(texto, out var valor))
        {
            problemas.Add($"{campo} inválido: \"{texto}\".");
            return null;
        }

        if (valor < 0)
        {
            problemas.Add($"{campo} não pode ser negativo.");
            return null;
        }

        return valor;
    }
}
