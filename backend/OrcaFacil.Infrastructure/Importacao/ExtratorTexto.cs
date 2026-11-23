using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Enums;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace OrcaFacil.Infrastructure.Importacao;

/// <summary>
/// Lê o texto de cada tipo de arquivo e devolve UMA LINHA POR ITEM, pronto para o InterpretadorLista.
/// Erros das bibliotecas (arquivo corrompido, protegido por senha...) viram DomainException com
/// uma mensagem amigável (HTTP 422).
/// </summary>
public sealed class ExtratorTexto(IServicoOcr ocr, ILogger<ExtratorTexto> logger) : IExtratorTexto
{
    /// <summary>Abaixo disto, consideramos que o PDF é escaneado (só imagem) e partimos para o OCR.</summary>
    private const int MinimoDeLetrasParaPdfComTexto = 20;

    public async Task<string> ExtrairAsync(Stream conteudo, FormatoImportacao formato, CancellationToken ct = default)
    {
        string texto;
        try
        {
            texto = formato switch
            {
                FormatoImportacao.Texto or FormatoImportacao.WhatsApp => await LerTextoAsync(conteudo, ct),
                FormatoImportacao.Excel => LerExcel(conteudo),
                FormatoImportacao.Word => LerWord(conteudo),
                FormatoImportacao.Pdf => await LerPdfAsync(conteudo, ct),
                FormatoImportacao.Imagem => await ocr.ReconhecerTextoAsync(await LerBytesAsync(conteudo, ct), ct),
                _ => throw new DomainException($"Formato {formato} não suportado.")
            };
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            logger.LogWarning(erro, "Falha ao ler arquivo do formato {Formato}", formato);
            throw new DomainException(
                "Não foi possível ler o arquivo. Verifique se ele não está corrompido ou protegido por senha.");
        }

        if (string.IsNullOrWhiteSpace(texto) || !texto.Any(char.IsLetter))
            throw new DomainException("Nenhum texto foi encontrado no arquivo.");

        return texto;
    }

    private static async Task<string> LerTextoAsync(Stream conteudo, CancellationToken ct)
    {
        // detectEncodingFromByteOrderMarks: aceita arquivos UTF-8 com ou sem BOM.
        using var leitor = new StreamReader(conteudo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await leitor.ReadToEndAsync(ct);
    }

    /// <summary>
    /// Excel: procura uma linha de cabeçalho (Descrição, Quantidade, Unidade, Marca).
    /// Se achar, monta cada linha como "quantidade unidade descrição marca: X".
    /// Se não achar, junta as células de cada linha com espaço.
    /// </summary>
    private static string LerExcel(Stream conteudo)
    {
        using var pasta = new XLWorkbook(conteudo);
        var planilha = pasta.Worksheets.FirstOrDefault(p => p.RangeUsed() is not null)
            ?? throw new DomainException("A planilha está vazia.");

        var linhas = planilha.RangeUsed()!.RowsUsed().ToList();
        var colunas = linhas.Count > 0 ? IdentificarColunas(linhas[0]) : null;
        var resultado = new StringBuilder();

        foreach (var linha in colunas is null ? linhas : linhas.Skip(1))
        {
            if (colunas is null)
            {
                var celulas = linha.CellsUsed().Select(c => c.GetFormattedString().Trim()).Where(t => t.Length > 0);
                resultado.AppendLine(string.Join(' ', celulas));
                continue;
            }

            var descricao = Celula(linha, colunas.Descricao);
            if (descricao.Length == 0)
                continue;

            var partes = new[] { Celula(linha, colunas.Quantidade), Celula(linha, colunas.Unidade), descricao }
                .Where(p => p.Length > 0);
            var texto = string.Join(' ', partes);
            var marca = Celula(linha, colunas.Marca);
            resultado.AppendLine(marca.Length > 0 ? $"{texto} marca: {marca}" : texto);
        }

        return resultado.ToString();
    }

    /// <summary>Texto de uma célula da linha (coluna relativa ao início da área usada da planilha).</summary>
    private static string Celula(IXLRangeRow linha, int? coluna) =>
        coluna is int n ? linha.Cell(n).GetFormattedString().Trim() : string.Empty;

    private sealed record ColunasExcel(int Descricao, int? Quantidade, int? Unidade, int? Marca);

    private static ColunasExcel? IdentificarColunas(IXLRangeRow cabecalho)
    {
        int? descricao = null, quantidade = null, unidade = null, marca = null;
        var posicao = 0;
        foreach (var celula in cabecalho.Cells())
        {
            posicao++;
            var titulo = celula.GetFormattedString().Trim().ToLowerInvariant();
            if (titulo.Length == 0) continue;

            if (descricao is null && (titulo.Contains("descri") || titulo.Contains("produto") || titulo.Contains("item") || titulo.Contains("material")))
                descricao = posicao;
            else if (quantidade is null && (titulo.StartsWith("qt") || titulo.Contains("quant")))
                quantidade = posicao;
            else if (unidade is null && (titulo.StartsWith("un") || titulo.Contains("medida")))
                unidade = posicao;
            else if (marca is null && titulo.Contains("marca"))
                marca = posicao;
        }

        return descricao is int d ? new ColunasExcel(d, quantidade, unidade, marca) : null;
    }

    /// <summary>Word: cada parágrafo vira uma linha; cada linha de tabela vira uma linha (células unidas).</summary>
    private static string LerWord(Stream conteudo)
    {
        using var documento = WordprocessingDocument.Open(conteudo, isEditable: false);
        var corpo = documento.MainDocumentPart?.Document?.Body ?? throw new DomainException("O documento está vazio.");
        var resultado = new StringBuilder();

        foreach (var elemento in corpo.ChildElements)
        {
            switch (elemento)
            {
                case Paragraph paragrafo:
                    resultado.AppendLine(paragrafo.InnerText);
                    break;
                case Table tabela:
                    foreach (var linha in tabela.Elements<TableRow>())
                    {
                        var celulas = linha.Elements<TableCell>().Select(c => c.InnerText.Trim()).Where(t => t.Length > 0);
                        resultado.AppendLine(string.Join(' ', celulas));
                    }
                    break;
            }
        }

        return resultado.ToString();
    }

    /// <summary>PDF com texto: extrai direto. PDF escaneado (só imagens): passa as imagens pelo OCR.</summary>
    private async Task<string> LerPdfAsync(Stream conteudo, CancellationToken ct)
    {
        using var pdf = PdfDocument.Open(conteudo);
        var resultado = new StringBuilder();
        foreach (var pagina in pdf.GetPages())
            resultado.AppendLine(ContentOrderTextExtractor.GetText(pagina));

        var texto = resultado.ToString();
        if (texto.Count(char.IsLetter) >= MinimoDeLetrasParaPdfComTexto)
            return texto;

        logger.LogInformation("PDF sem texto suficiente: tentando OCR nas imagens das páginas.");
        var textoOcr = new StringBuilder();
        foreach (var pagina in pdf.GetPages())
        {
            foreach (var imagem in pagina.GetImages())
            {
                // PNG quando a biblioteca consegue converter; senão, os bytes originais (normalmente JPEG).
                var bytes = imagem.TryGetPng(out var png) ? png : imagem.RawBytes.ToArray();
                try
                {
                    textoOcr.AppendLine(await ocr.ReconhecerTextoAsync(bytes, ct));
                }
                catch (DomainException)
                {
                    throw; // OCR não configurado: repassa a instrução ao usuário
                }
                catch (Exception erro) when (erro is not OperationCanceledException)
                {
                    logger.LogDebug(erro, "Imagem do PDF ignorada (formato não reconhecido pelo OCR).");
                }
            }
        }
        return textoOcr.ToString();
    }

    private static async Task<byte[]> LerBytesAsync(Stream conteudo, CancellationToken ct)
    {
        using var memoria = new MemoryStream();
        await conteudo.CopyToAsync(memoria, ct);
        return memoria.ToArray();
    }
}
