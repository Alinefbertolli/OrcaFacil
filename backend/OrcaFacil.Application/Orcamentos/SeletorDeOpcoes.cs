using System.Globalization;
using System.Text;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Application.Orcamentos;

/// <summary>
/// As REGRAS de escolha de cada opção do orçamento comparativo, isoladas aqui para ficarem
/// fáceis de entender e de testar (são funções puras: mesma entrada → mesma saída, sem banco).
///
/// Para cada item da lista, o motor recebe os produtos equivalentes do catálogo e escolhe:
/// - Econômica (MenorPreco): o de menor preço de venda;
/// - Custo-benefício: o mais barato entre as marcas de nível 3 ou mais ("marca boa, preço justo");
///   se nenhum tiver marca de nível 3+, fica o mais barato;
/// - Marcas reconhecidas: o de marca com MAIOR nível (de preferência 4 ou 5); empate → o mais barato.
/// Produtos sem preço de venda nunca são escolhidos: não inventamos preço.
/// </summary>
public static class SeletorDeOpcoes
{
    /// <summary>Nível usado para produtos sem marca cadastrada (o mais baixo).</summary>
    public const int NivelSemMarca = 1;

    /// <summary>Nível mínimo de marca considerado no Custo-benefício.</summary>
    public const int NivelMinimoCustoBeneficio = 3;

    /// <summary>As três opções do modo comparativo, na ordem em que aparecem para o cliente.</summary>
    public static readonly TipoOpcaoOrcamento[] OpcoesComparativas =
        [TipoOpcaoOrcamento.MenorPreco, TipoOpcaoOrcamento.CustoBeneficio, TipoOpcaoOrcamento.MarcasReconhecidas];

    public static string NomeDaOpcao(TipoOpcaoOrcamento tipo) => tipo switch
    {
        TipoOpcaoOrcamento.MenorPreco => "Econômica",
        TipoOpcaoOrcamento.CustoBeneficio => "Custo-benefício",
        TipoOpcaoOrcamento.MarcasReconhecidas => "Marcas reconhecidas",
        TipoOpcaoOrcamento.Unica => "Orçamento",
        _ => "Manual"
    };

    /// <summary>Escolhe o produto da opção. Null = nenhum candidato com preço de venda.</summary>
    public static ProdutoCandidato? Escolher(TipoOpcaoOrcamento tipo, IEnumerable<ProdutoCandidato> candidatos)
    {
        var comPreco = candidatos.Where(c => c.PrecoVenda is not null).ToList();
        if (comPreco.Count == 0) return null;

        // Desempates sempre pelo Código: assim o resultado é o mesmo toda vez (previsível).
        return tipo switch
        {
            TipoOpcaoOrcamento.MenorPreco => comPreco
                .OrderBy(c => c.PrecoVenda).ThenByDescending(Nivel).ThenBy(c => c.Codigo).First(),

            TipoOpcaoOrcamento.CustoBeneficio => (comPreco.Any(c => Nivel(c) >= NivelMinimoCustoBeneficio)
                    ? comPreco.Where(c => Nivel(c) >= NivelMinimoCustoBeneficio)
                    : comPreco)
                .OrderBy(c => c.PrecoVenda).ThenByDescending(Nivel).ThenBy(c => c.Codigo).First(),

            TipoOpcaoOrcamento.MarcasReconhecidas => comPreco
                .OrderByDescending(Nivel).ThenBy(c => c.PrecoVenda).ThenBy(c => c.Codigo).First(),

            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de opção sem regra automática.")
        };
    }

    /// <summary>
    /// Marca pedida pelo cliente ("caderno Tilibra"): fica só com os produtos dessa marca.
    /// Se nenhum for da marca, devolve todos e avisa (marcaEncontrada = false).
    /// </summary>
    public static IReadOnlyList<ProdutoCandidato> FiltrarPorMarca(IReadOnlyList<ProdutoCandidato> candidatos,
        string? marcaPedida, out bool marcaEncontrada)
    {
        marcaEncontrada = true;
        var marca = Normalizar(marcaPedida);
        if (marca.Length == 0) return candidatos;

        var daMarca = candidatos
            .Where(c => Normalizar(c.MarcaNome) == marca || Normalizar(c.Nome).Contains(marca))
            .ToList();

        if (daMarca.Count > 0) return daMarca;

        marcaEncontrada = false;
        return candidatos;
    }

    private static int Nivel(ProdutoCandidato c) => c.NivelMarca ?? NivelSemMarca;

    /// <summary>Minúsculas e sem acentos, para comparar "Tilibrá" com "tilibra".</summary>
    internal static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        var decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(decomposto.Length);
        foreach (var letra in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)
                resultado.Append(letra);
        }
        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }
}
