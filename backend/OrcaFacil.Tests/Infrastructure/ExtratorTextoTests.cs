using AwesomeAssertions;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Infrastructure.Importacao;
using PageSize = UglyToad.PdfPig.Content.PageSize; // apelido: o Word também tem uma classe PageSize
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace OrcaFacil.Tests.Infrastructure;

/// <summary>
/// Testa os leitores REAIS de arquivo: cada teste gera um arquivo de verdade em memória
/// (Excel, Word, PDF) e confere o texto extraído. O OCR é substituído por um fake.
/// </summary>
public class ExtratorTextoTests
{
    private sealed class OcrFake(string texto) : IServicoOcr
    {
        public Task<string> ReconhecerTextoAsync(byte[] imagem, CancellationToken ct = default) => Task.FromResult(texto);
    }

    private static ExtratorTexto Extrator(string textoOcr = "") =>
        new(new OcrFake(textoOcr), NullLogger<ExtratorTexto>.Instance);

    [Fact]
    public async Task Excel_com_cabecalho_monta_quantidade_unidade_descricao_e_marca()
    {
        using var pasta = new XLWorkbook();
        var planilha = pasta.AddWorksheet("Lista");
        planilha.Cell(1, 1).Value = "Produto";
        planilha.Cell(1, 2).Value = "Qtd";
        planilha.Cell(1, 3).Value = "Unidade";
        planilha.Cell(1, 4).Value = "Marca";
        planilha.Cell(2, 1).Value = "Caneta azul";
        planilha.Cell(2, 2).Value = 20;
        planilha.Cell(2, 3).Value = "cx";
        planilha.Cell(2, 4).Value = "BIC";
        using var arquivo = new MemoryStream();
        pasta.SaveAs(arquivo);
        arquivo.Position = 0;

        var texto = await Extrator().ExtrairAsync(arquivo, FormatoImportacao.Excel);

        texto.Trim().Should().Be("20 cx Caneta azul marca: BIC");
        var item = InterpretadorLista.Interpretar(texto, []).Should().ContainSingle().Subject;
        item.Marca.Should().Be("BIC");
        item.UnidadeMedida.Should().Be("CX");
    }

    [Fact]
    public async Task Word_le_paragrafos_e_linhas_de_tabela()
    {
        using var arquivo = new MemoryStream();
        using (var documento = WordprocessingDocument.Create(arquivo, WordprocessingDocumentType.Document))
        {
            var principal = documento.AddMainDocumentPart();
            principal.Document = new Document(new Body(
                new Paragraph(new Run(new Text("5 resmas papel A4"))),
                new Table(new TableRow(
                    new TableCell(new Paragraph(new Run(new Text("10")))),
                    new TableCell(new Paragraph(new Run(new Text("cadernos"))))))));
        }
        arquivo.Position = 0;

        var texto = await Extrator().ExtrairAsync(arquivo, FormatoImportacao.Word);

        texto.Should().Contain("5 resmas papel A4").And.Contain("10 cadernos");
    }

    [Fact]
    public async Task Pdf_com_texto_e_lido_linha_a_linha()
    {
        var construtor = new PdfDocumentBuilder();
        var fonte = construtor.AddStandard14Font(Standard14Font.Helvetica);
        var pagina = construtor.AddPage(PageSize.A4);
        pagina.AddText("20 cx caneta azul", 12, new PdfPoint(50, 750), fonte);
        pagina.AddText("5 resmas papel A4", 12, new PdfPoint(50, 720), fonte);
        using var arquivo = new MemoryStream(construtor.Build());

        var texto = await Extrator().ExtrairAsync(arquivo, FormatoImportacao.Pdf);

        InterpretadorLista.Interpretar(texto, []).Select(i => i.Descricao)
            .Should().Equal("caneta azul", "papel A4");
    }

    [Fact]
    public async Task Imagem_usa_o_ocr()
    {
        using var arquivo = new MemoryStream([0x89, 0x50, 0x4E, 0x47]);

        var texto = await Extrator(textoOcr: "3 pct etiqueta").ExtrairAsync(arquivo, FormatoImportacao.Imagem);

        texto.Should().Be("3 pct etiqueta");
    }

    [Fact]
    public async Task Arquivo_corrompido_vira_mensagem_amigavel()
    {
        using var lixo = new MemoryStream([1, 2, 3, 4, 5]);

        await FluentActions.Awaiting(() => Extrator().ExtrairAsync(lixo, FormatoImportacao.Excel))
            .Should().ThrowAsync<DomainException>().WithMessage("*corrompido*");
    }

    [Fact]
    public async Task Ocr_sem_arquivo_de_idioma_explica_como_configurar()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ocr:CaminhoTessdata"] = "pasta-que-nao-existe" })
            .Build();
        var ocr = new ServicoOcrTesseract(configuracao, NullLogger<ServicoOcrTesseract>.Instance);

        await FluentActions.Awaiting(() => ocr.ReconhecerTextoAsync([1, 2, 3]))
            .Should().ThrowAsync<DomainException>().WithMessage("*por.traineddata*");
    }
}
