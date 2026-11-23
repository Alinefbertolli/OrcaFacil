using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Application.Importacao;

/// <summary>
/// Confere o arquivo enviado ANTES de processá-lo. Segurança:
/// 1) limite de tamanho (não deixar alguém enviar 2 GB);
/// 2) extensão permitida;
/// 3) "assinatura" (magic bytes): os primeiros bytes do arquivo precisam corresponder ao tipo.
///    Renomear um programa malicioso para "lista.pdf" não engana esta verificação.
/// </summary>
public static class ValidadorArquivo
{
    public const int TamanhoMaximoPadraoMb = 10;

    private static readonly Dictionary<string, FormatoImportacao> Extensoes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = FormatoImportacao.Texto,
        [".pdf"] = FormatoImportacao.Pdf,
        [".xlsx"] = FormatoImportacao.Excel,
        [".docx"] = FormatoImportacao.Word,
        [".png"] = FormatoImportacao.Imagem,
        [".jpg"] = FormatoImportacao.Imagem,
        [".jpeg"] = FormatoImportacao.Imagem,
        [".bmp"] = FormatoImportacao.Imagem,
        [".tif"] = FormatoImportacao.Imagem,
        [".tiff"] = FormatoImportacao.Imagem,
    };

    /// <summary>Valida e devolve o formato. Lança ValidationException (HTTP 400) com a explicação.</summary>
    public static FormatoImportacao Validar(string nomeArquivo, long tamanho, ReadOnlySpan<byte> inicio, int tamanhoMaximoMb)
    {
        if (tamanho <= 0)
            throw Validacao.Erro("arquivo", "O arquivo está vazio.");

        if (tamanho > tamanhoMaximoMb * 1024L * 1024L)
            throw Validacao.Erro("arquivo", $"O arquivo passa do limite de {tamanhoMaximoMb} MB.");

        var extensao = Path.GetExtension(nomeArquivo);
        if (extensao.Equals(".xls", StringComparison.OrdinalIgnoreCase) || extensao.Equals(".doc", StringComparison.OrdinalIgnoreCase))
            throw Validacao.Erro("arquivo",
                "Formato antigo do Office não suportado. Abra o arquivo e use \"Salvar como\" .xlsx (Excel) ou .docx (Word).");

        if (!Extensoes.TryGetValue(extensao, out var formato))
            throw Validacao.Erro("arquivo",
                "Tipo de arquivo não suportado. Envie PDF, Excel (.xlsx), Word (.docx), imagem (PNG, JPG) ou texto (.txt).");

        if (!AssinaturaConfere(extensao.ToLowerInvariant(), inicio))
            throw Validacao.Erro("arquivo", "O conteúdo do arquivo não corresponde à extensão informada.");

        return formato;
    }

    private static bool AssinaturaConfere(string extensao, ReadOnlySpan<byte> b) => extensao switch
    {
        ".pdf" => b.StartsWith("%PDF"u8),
        ".xlsx" or ".docx" => b.StartsWith("PK\u0003\u0004"u8), // são arquivos ZIP por dentro
        ".png" => b.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        ".jpg" or ".jpeg" => b.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
        ".bmp" => b.StartsWith("BM"u8),
        ".tif" or ".tiff" => b.StartsWith(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) || b.StartsWith(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }),
        ".txt" => !b.Contains((byte)0), // texto não tem bytes zero
        _ => false
    };
}
