using PdfSharp.Fonts;

namespace OrcaFacil.Infrastructure.Documentos;

/// <summary>
/// Diz ao PDFsharp onde estão os arquivos de fonte. A documentação do PDFsharp recomenda um resolvedor próprio
/// em produção (o atalho "usar as fontes do Windows" é só para testes).
/// Procura, nesta ordem: Arial (Windows) → Liberation Sans → DejaVu Sans (Linux/Docker: pacote fonts-dejavu).
/// Itálico é simulado pelo próprio PDFsharp.
/// </summary>
internal sealed class ResolvedorDeFontes : IFontResolver
{
    private const string FaceNormal = "OrcaFacil#Normal";
    private const string FaceNegrito = "OrcaFacil#Negrito";

    private static readonly string[] CandidatosNormal = ["arial.ttf", "LiberationSans-Regular.ttf", "DejaVuSans.ttf"];
    private static readonly string[] CandidatosNegrito = ["arialbd.ttf", "LiberationSans-Bold.ttf", "DejaVuSans-Bold.ttf"];

    private readonly Lazy<byte[]> _normal;
    private readonly Lazy<byte[]> _negrito;

    public ResolvedorDeFontes(string? pastaConfigurada)
    {
        var pastas = PastasParaProcurar(pastaConfigurada).ToList();
        _normal = new Lazy<byte[]>(() => Carregar(pastas, CandidatosNormal));
        _negrito = new Lazy<byte[]>(() => Carregar(pastas, CandidatosNegrito));
    }

    // Qualquer família pedida (o documento usa "Arial") é atendida pelas nossas duas fontes.
    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        new(bold ? FaceNegrito : FaceNormal, false, italic);

    public byte[]? GetFont(string faceName) => faceName == FaceNegrito ? _negrito.Value : _normal.Value;

    private static IEnumerable<string> PastasParaProcurar(string? pastaConfigurada)
    {
        if (!string.IsNullOrWhiteSpace(pastaConfigurada)) yield return pastaConfigurada;
        var fontesDoSistema = Environment.GetFolderPath(Environment.SpecialFolder.Fonts); // Windows: C:\Windows\Fonts
        if (!string.IsNullOrEmpty(fontesDoSistema)) yield return fontesDoSistema;
        yield return "/usr/share/fonts";
        yield return "/usr/local/share/fonts";
    }

    private static byte[] Carregar(IReadOnlyList<string> pastas, string[] nomes)
    {
        var opcoes = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MatchCasing = MatchCasing.CaseInsensitive,
            IgnoreInaccessible = true
        };

        foreach (var nome in nomes)
        foreach (var pasta in pastas.Where(Directory.Exists))
        {
            var arquivo = Directory.EnumerateFiles(pasta, nome, opcoes).FirstOrDefault();
            if (arquivo is not null)
                return File.ReadAllBytes(arquivo);
        }

        throw new InvalidOperationException(
            "Nenhuma fonte encontrada para gerar o PDF (procuradas: " + string.Join(", ", nomes) + "). " +
            "No Linux/Docker instale o pacote 'fonts-dejavu-core' ou informe a pasta em 'Pdf:PastaFontes'.");
    }
}
