using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OrcaFacil.Application.Importacao;

/// <summary>Resultado da leitura de uma linha da lista.</summary>
/// <param name="QuantidadeInformada">false quando a linha não trazia quantidade (assumimos 1).</param>
public sealed record ItemInterpretado(
    string TextoOriginal,
    string Descricao,
    decimal Quantidade,
    string UnidadeMedida,
    string? Marca,
    decimal Confianca,
    bool QuantidadeInformada);

/// <summary>
/// Transforma texto livre (digitado, colado do WhatsApp ou extraído de um arquivo) em itens estruturados.
///
/// Usa regras (expressões regulares + dicionário de unidades), não "adivinhação": o resultado é
/// previsível e testável. Cada item recebe uma CONFIANÇA (0 a 1); os de confiança baixa são
/// destacados na revisão, que é sempre obrigatória antes de orçar.
///
/// Formatos reconhecidos (exemplos):
///   "20 cx caneta azul BIC"      "20x caneta azul"      "20cx caneta"
///   "caneta azul - 20 cx"        "caneta azul (20 un)"  "caneta azul: 20"
///   "caneta azul 20 cx"          "caneta azul marca BIC"
/// </summary>
public static class InterpretadorLista
{
    public const int MaximoItens = 500;
    public const decimal ConfiancaParaDispensarAtencao = 0.7m;
    private const int TamanhoMaximoDescricao = 300;
    private const int TamanhoMaximoMarca = 100;
    private const RegexOptions Opcoes = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // "[10:32, 18/11/2026] Ana: ..." (mensagem copiada) e "18/11/2026 10:32 - Ana: ..." (conversa exportada)
    private static readonly Regex PrefixoWhatsAppCopiado =
        new(@"^\[\d{1,2}:\d{2}(?::\d{2})?,?\s*\d{1,2}/\d{1,2}(?:/\d{2,4})?\]\s*[^:]{1,60}:\s*", Opcoes);
    private static readonly Regex PrefixoWhatsAppExportado =
        new(@"^\d{1,2}/\d{1,2}(?:/\d{2,4})?,?\s+\d{1,2}:\d{2}(?::\d{2})?\s*-\s*[^:]{1,60}:\s*", Opcoes);

    // Marcadores de lista: "- ", "* ", "• ", "1. ", "2) ", "a) "
    private static readonly Regex Marcador = new(@"^(?:[-*•·–—>]+\s*|\d{1,3}[.)]\s+|[a-z]\)\s+)", Opcoes);

    // Frases de conversa que não são itens (só ignoradas se a linha não tiver números).
    private static readonly Regex FraseDeConversa = new(
        @"^(bom dia|boa tarde|boa noite|ol[aá]|oi\b|obrigad[oa]|segue|preciso|gostaria|por favor|favor|lista|or[çc]amento|att|abra[çc]o|<m[ií]dia oculta>|<media omitted>)",
        Opcoes);

    private const string Numero = @"\d{1,3}(?:\.\d{3})+(?:,\d+)?|\d+(?:[.,]\d+)?";

    // Quantidade no início: "20cx caneta" (unidade grudada), "20 x caneta", "20 caneta"
    private static readonly Regex QuantidadeGrudadaNaUnidade = new($@"^(?<qtd>{Numero})(?<un>[a-zç²³]{{1,6}})\s+(?<resto>.+)$", Opcoes);
    private static readonly Regex QuantidadeNoInicio = new($@"^(?<qtd>{Numero})\s*(?:x|×)?\s+(?<resto>.+)$", Opcoes);

    // Quantidade no fim, com separador: "caneta - 20 cx", "caneta (20 un)", "caneta: 20", "caneta x20"
    private static readonly Regex QuantidadeNoFimComSeparador =
        new($@"^(?<desc>.+?)\s*(?:[-–:=(]|\bx)\s*(?<qtd>{Numero})\s*(?<un>[a-zçãéú²³]{{1,10}})?\s*\)?\s*$", Opcoes);

    // Quantidade no fim sem separador, desde que seguida de unidade conhecida: "caneta 20 cx"
    private static readonly Regex QuantidadeNoFimComUnidade =
        new($@"^(?<desc>.+?)\s+(?<qtd>{Numero})\s+(?<un>[a-zçãéú²³]{{1,10}})\s*$", Opcoes);

    private static readonly Regex MarcaExplicita = new(@"\s*[,;-]?\s*\bmarca\s*[:\-]?\s*(?<marca>[^,;()]+?)\s*$", Opcoes);
    private static readonly Regex PreposicaoInicial = new(@"^(?:de|do|da|dos|das)\s+", Opcoes);
    private static readonly Regex Espacos = new(@"\s+", Opcoes);

    /// <param name="texto">Texto completo da lista (uma linha por item).</param>
    /// <param name="marcasConhecidas">Marcas cadastradas pela empresa, para reconhecê-las no meio do texto.</param>
    public static IReadOnlyList<ItemInterpretado> Interpretar(string texto, IEnumerable<string> marcasConhecidas)
    {
        // Marcas mais longas primeiro: "Faber-Castell" antes de "Faber".
        var marcas = marcasConhecidas
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(m => m.Length)
            .ToList();

        var itens = new List<ItemInterpretado>();
        foreach (var linhaOriginal in texto.Replace("\r", string.Empty).Split('\n'))
        {
            var item = InterpretarLinha(linhaOriginal, marcas);
            if (item is not null)
                itens.Add(item);
            if (itens.Count >= MaximoItens)
                break;
        }
        return itens;
    }

    private static ItemInterpretado? InterpretarLinha(string linhaOriginal, IReadOnlyList<string> marcas)
    {
        var linha = Limpar(linhaOriginal);
        if (!DeveSerConsiderada(linha))
            return null;

        decimal? quantidade = null;
        string? unidade = null;
        var descricao = linha;

        // 1) Tenta encontrar a quantidade (e a unidade) no início ou no fim da linha.
        if (TentarQuantidadeNoInicio(linha, out var q1, out var u1, out var d1))
            (quantidade, unidade, descricao) = (q1, u1, d1);
        else if (TentarQuantidadeNoFim(linha, out var q2, out var u2, out var d2))
            (quantidade, unidade, descricao) = (q2, u2, d2);

        // 2) Marca: escrita como "marca X" ou uma das marcas cadastradas.
        var marca = ExtrairMarca(ref descricao, marcas);

        descricao = Finalizar(descricao);
        if (descricao.Length == 0 || !descricao.Any(char.IsLetter))
            return null; // ex.: linha só com "20 cx"

        // 3) Confiança: quanto mais coisas reconhecemos, mais confiável.
        var confianca = 0.4m;
        if (quantidade is not null) confianca += 0.3m;
        if (unidade is not null) confianca += 0.1m;
        if (marca is not null) confianca += 0.05m;

        return new ItemInterpretado(
            TextoOriginal: linhaOriginal.Trim(),
            Descricao: Cortar(descricao, TamanhoMaximoDescricao),
            Quantidade: quantidade ?? 1m,
            UnidadeMedida: unidade ?? UnidadesDeMedida.Padrao,
            Marca: marca is null ? null : Cortar(marca, TamanhoMaximoMarca),
            Confianca: Math.Min(confianca, 0.95m),
            QuantidadeInformada: quantidade is not null);
    }

    private static bool TentarQuantidadeNoInicio(string linha, out decimal? quantidade, out string? unidade, out string descricao)
    {
        quantidade = null; unidade = null; descricao = linha;

        var grudada = QuantidadeGrudadaNaUnidade.Match(linha);
        if (grudada.Success && UnidadesDeMedida.Reconhecer(grudada.Groups["un"].Value) is string unidadeGrudada
            && LerNumero(grudada.Groups["qtd"].Value) is decimal qtdGrudada)
        {
            (quantidade, unidade, descricao) = (qtdGrudada, unidadeGrudada, grudada.Groups["resto"].Value);
            return true;
        }

        var inicio = QuantidadeNoInicio.Match(linha);
        if (!inicio.Success || LerNumero(inicio.Groups["qtd"].Value) is not decimal qtd)
            return false;

        quantidade = qtd;
        descricao = inicio.Groups["resto"].Value;

        // A primeira palavra depois do número pode ser a unidade: "20 cx caneta".
        var partes = descricao.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 2 && UnidadesDeMedida.Reconhecer(partes[0]) is string un)
        {
            unidade = un;
            descricao = partes[1];
        }
        return true;
    }

    private static bool TentarQuantidadeNoFim(string linha, out decimal? quantidade, out string? unidade, out string descricao)
    {
        quantidade = null; unidade = null; descricao = linha;

        var comSeparador = QuantidadeNoFimComSeparador.Match(linha);
        if (comSeparador.Success && LerNumero(comSeparador.Groups["qtd"].Value) is decimal q1)
        {
            var palavraUnidade = comSeparador.Groups["un"].Value;
            var un = UnidadesDeMedida.Reconhecer(palavraUnidade);
            // Se veio uma palavra depois do número e ela NÃO é unidade ("200 folhas"), não é quantidade.
            if (palavraUnidade.Length == 0 || un is not null)
            {
                (quantidade, unidade, descricao) = (q1, un, comSeparador.Groups["desc"].Value);
                return true;
            }
        }

        var comUnidade = QuantidadeNoFimComUnidade.Match(linha);
        if (comUnidade.Success && UnidadesDeMedida.Reconhecer(comUnidade.Groups["un"].Value) is string un2
            && LerNumero(comUnidade.Groups["qtd"].Value) is decimal q2)
        {
            (quantidade, unidade, descricao) = (q2, un2, comUnidade.Groups["desc"].Value);
            return true;
        }

        return false;
    }

    private static string? ExtrairMarca(ref string descricao, IReadOnlyList<string> marcas)
    {
        var explicita = MarcaExplicita.Match(descricao);
        if (explicita.Success && explicita.Groups["marca"].Value.Trim().Length > 0)
        {
            descricao = descricao[..explicita.Index];
            return explicita.Groups["marca"].Value.Trim();
        }

        foreach (var marca in marcas)
        {
            // A marca precisa aparecer como palavra inteira ("BIC" não casa com "bicicleta").
            var padrao = new Regex($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(marca)}(?![\p{{L}}\p{{N}}])", Opcoes);
            var achou = padrao.Match(descricao);
            if (achou.Success)
            {
                descricao = descricao.Remove(achou.Index, achou.Length);
                return marca;
            }
        }
        return null;
    }

    /// <summary>Remove o prefixo do WhatsApp, marcadores de lista, emojis e espaços repetidos.</summary>
    private static string Limpar(string linha)
    {
        var texto = RemoverEmojis(linha).Trim();
        texto = PrefixoWhatsAppCopiado.Replace(texto, string.Empty);
        texto = PrefixoWhatsAppExportado.Replace(texto, string.Empty);
        texto = Marcador.Replace(texto.Trim(), string.Empty);
        return Espacos.Replace(texto, " ").Trim();
    }

    private static bool DeveSerConsiderada(string linha)
    {
        if (linha.Length < 2 || !linha.Any(char.IsLetter))
            return false;

        var temNumero = linha.Any(char.IsDigit);
        if (!temNumero && FraseDeConversa.IsMatch(linha))
            return false;
        if (!temNumero && linha.EndsWith(':'))
            return false; // título, ex.: "Material escolar:"
        return true;
    }

    private static string Finalizar(string descricao)
    {
        // Parênteses NÃO são removidos das pontas: "Papel A4 (500 folhas)" deve continuar com os dois.
        var texto = Espacos.Replace(descricao, " ").Trim().Trim('-', '–', ':', ';', ',', '.', '=', ' ');
        if (texto.EndsWith('(')) texto = texto.TrimEnd('(', ' '); // sobra de "caneta (20 un)"
        return PreposicaoInicial.Replace(texto, string.Empty).Trim();
    }

    /// <summary>Lê "20", "1,5", "2.5" e "1.000" (mil, no formato brasileiro).</summary>
    private static decimal? LerNumero(string valor)
    {
        var texto = Regex.IsMatch(valor, @"^\d{1,3}(?:\.\d{3})+(?:,\d+)?$")
            ? valor.Replace(".", string.Empty).Replace(',', '.')
            : valor.Replace(',', '.');

        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero) && numero > 0
            ? numero
            : null;
    }

    private static string RemoverEmojis(string texto)
    {
        var resultado = new StringBuilder(texto.Length);
        foreach (var caractere in texto)
        {
            if (char.IsSurrogate(caractere))
                continue; // a maioria dos emojis
            var categoria = CharUnicodeInfo.GetUnicodeCategory(caractere);
            if (categoria is UnicodeCategory.OtherSymbol or UnicodeCategory.Format)
                continue; // ✅ ☑ ✔ e marcadores invisíveis
            resultado.Append(caractere);
        }
        return resultado.ToString();
    }

    private static string Cortar(string texto, int tamanho) => texto.Length <= tamanho ? texto : texto[..tamanho].Trim();
}
