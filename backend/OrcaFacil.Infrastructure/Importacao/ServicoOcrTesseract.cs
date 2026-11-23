using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Domain.Comum;
using Tesseract;

namespace OrcaFacil.Infrastructure.Importacao;

/// <summary>
/// OCR (reconhecimento óptico de caracteres) com o Tesseract, gratuito e executado no próprio servidor.
/// Precisa do arquivo de idioma "por.traineddata" na pasta configurada em Ocr:CaminhoTessdata.
///
/// Limitações honestas: funciona bem com texto impresso e fotos nítidas; letra cursiva e fotos
/// tortas ou escuras dão resultados ruins. Por isso a revisão do usuário é obrigatória.
/// </summary>
public sealed class ServicoOcrTesseract(IConfiguration configuracao, ILogger<ServicoOcrTesseract> logger) : IServicoOcr
{
    private string PastaIdiomas => Path.GetFullPath(configuracao["Ocr:CaminhoTessdata"] ?? "tessdata");
    private string Idioma => configuracao["Ocr:Idioma"] ?? "por";

    public Task<string> ReconhecerTextoAsync(byte[] imagem, CancellationToken ct = default)
    {
        var arquivoIdioma = Path.Combine(PastaIdiomas, $"{Idioma}.traineddata");
        if (!File.Exists(arquivoIdioma))
            throw new DomainException(
                $"O leitor de imagens (OCR) ainda não está configurado: falta o arquivo {arquivoIdioma}. " +
                "Veja no README a seção \"OCR\" (download do por.traineddata).");

        // O OCR usa bastante processador: rodamos fora da thread da requisição.
        return Task.Run(() => Reconhecer(imagem), ct);
    }

    private string Reconhecer(byte[] imagem)
    {
        try
        {
            // Um "motor" por chamada: o TesseractEngine não pode ser usado por duas requisições ao mesmo tempo.
            using var motor = new TesseractEngine(PastaIdiomas, Idioma, EngineMode.Default);
            using var figura = Pix.LoadFromMemory(imagem);
            using var pagina = motor.Process(figura);

            logger.LogInformation("OCR concluído (confiança média do Tesseract: {Confianca:P0}).", pagina.GetMeanConfidence());
            return pagina.GetText();
        }
        catch (DllNotFoundException erro)
        {
            logger.LogError(erro, "Bibliotecas nativas do Tesseract não encontradas.");
            throw new DomainException(
                "O leitor de imagens (OCR) não conseguiu iniciar neste computador. " +
                "Instale o \"Microsoft Visual C++ Redistributable\" (x64) e tente novamente.");
        }
    }
}
