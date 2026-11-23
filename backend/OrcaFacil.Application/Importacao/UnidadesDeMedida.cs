namespace OrcaFacil.Application.Importacao;

/// <summary>
/// Dicionário de unidades de medida e seus sinônimos: "cx", "caixa", "caixas" → CX.
/// Usado para reconhecer a unidade nas listas e padronizá-la.
/// </summary>
public static class UnidadesDeMedida
{
    public const string Padrao = "UN";

    private static readonly Dictionary<string, string> Sinonimos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["un"] = "UN", ["und"] = "UN", ["unid"] = "UN", ["unidade"] = "UN", ["unidades"] = "UN",
        ["pc"] = "UN", ["pç"] = "UN", ["pcs"] = "UN", ["pçs"] = "UN",
        ["peca"] = "UN", ["peça"] = "UN", ["pecas"] = "UN", ["peças"] = "UN",
        ["cx"] = "CX", ["cxs"] = "CX", ["caixa"] = "CX", ["caixas"] = "CX",
        ["pct"] = "PCT", ["pcts"] = "PCT", ["pacote"] = "PCT", ["pacotes"] = "PCT",
        ["kg"] = "KG", ["kgs"] = "KG", ["quilo"] = "KG", ["quilos"] = "KG", ["kilo"] = "KG", ["kilos"] = "KG",
        ["g"] = "G", ["gr"] = "G", ["grama"] = "G", ["gramas"] = "G",
        ["m"] = "M", ["mt"] = "M", ["mts"] = "M", ["metro"] = "M", ["metros"] = "M",
        ["m2"] = "M2", ["m²"] = "M2", ["m3"] = "M3", ["m³"] = "M3",
        ["l"] = "L", ["lt"] = "L", ["lts"] = "L", ["litro"] = "L", ["litros"] = "L",
        ["ml"] = "ML",
        ["resma"] = "RESMA", ["resmas"] = "RESMA",
        ["rl"] = "RL", ["rolo"] = "RL", ["rolos"] = "RL",
        ["sc"] = "SC", ["saco"] = "SC", ["sacos"] = "SC",
        ["dz"] = "DZ", ["duzia"] = "DZ", ["dúzia"] = "DZ", ["duzias"] = "DZ", ["dúzias"] = "DZ",
        ["par"] = "PAR", ["pares"] = "PAR",
        ["kit"] = "KIT", ["kits"] = "KIT",
        ["fd"] = "FD", ["fardo"] = "FD", ["fardos"] = "FD",
        ["gl"] = "GL", ["galao"] = "GL", ["galão"] = "GL", ["galoes"] = "GL", ["galões"] = "GL",
        ["lata"] = "LATA", ["latas"] = "LATA",
        ["tb"] = "TB", ["tubo"] = "TB", ["tubos"] = "TB",
        ["bl"] = "BL", ["bloco"] = "BL", ["blocos"] = "BL",
        ["cj"] = "CJ", ["conjunto"] = "CJ", ["conjuntos"] = "CJ",
        ["fr"] = "FR", ["frasco"] = "FR", ["frascos"] = "FR",
    };

    /// <summary>Devolve a unidade padronizada ("caixas" → "CX") ou null se a palavra não for uma unidade.</summary>
    public static string? Reconhecer(string? palavra)
    {
        if (string.IsNullOrWhiteSpace(palavra))
            return null;
        return Sinonimos.TryGetValue(palavra.Trim().Trim('.', ',', ';', ':'), out var unidade) ? unidade : null;
    }
}
