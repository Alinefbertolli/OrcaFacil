using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using OrcaFacil.Application.Orcamentos;
using PdfSharp.Fonts;

namespace OrcaFacil.Infrastructure.Documentos;

/// <summary>
/// Monta o PDF do orçamento com o MigraDoc (licença MIT). O MigraDoc funciona como um "Word por código":
/// criamos seções, parágrafos e tabelas, e ele cuida das quebras de página.
/// </summary>
public sealed class GeradorPdfOrcamento : IGeradorPdfOrcamento
{
    private const string Fonte = "Arial";
    private static readonly Color CorDestaque = new(0x1F, 0x4E, 0x79); // azul escuro
    private static readonly Color CorCabecalhoTabela = new(0xDD, 0xE6, 0xF0);
    private static readonly object Trava = new();

    public GeradorPdfOrcamento(string? pastaFontes)
    {
        // O PDFsharp só aceita registrar o resolvedor de fontes UMA vez por processo.
        lock (Trava)
        {
            GlobalFontSettings.FontResolver ??= new ResolvedorDeFontes(pastaFontes);
        }
    }

    public byte[] Gerar(DocumentoOrcamento doc)
    {
        var documento = new Document();
        var info = documento.Info!;
        info.Title = $"Orçamento {doc.Numero}";
        info.Author = doc.Loja.Nome;

        var normal = documento.Styles[StyleNames.Normal]!;
        normal.Font.Name = Fonte;
        normal.Font.Size = 10;

        var secao = documento.AddSection();
        var pagina = secao.PageSetup!; // A4 é o padrão do MigraDoc
        pagina.TopMargin = Unit.FromCentimeter(1.8);
        pagina.BottomMargin = Unit.FromCentimeter(1.8);
        pagina.LeftMargin = Unit.FromCentimeter(2);
        pagina.RightMargin = Unit.FromCentimeter(2);

        Cabecalho(secao, doc);
        foreach (var opcao in doc.Opcoes)
            TabelaDaOpcao(secao, opcao, mostrarTitulo: doc.Opcoes.Count > 1);
        Rodape(secao, doc);

        var renderizador = new PdfDocumentRenderer { Document = documento };
        renderizador.RenderDocument();
        using var memoria = new MemoryStream();
        renderizador.PdfDocument!.Save(memoria, false);
        return memoria.ToArray();
    }

    private static void Cabecalho(Section secao, DocumentoOrcamento doc)
    {
        var loja = secao.AddParagraph();
        loja.Format.Font.Size = 16;
        loja.Format.Font.Bold = true;
        loja.Format.Font.Color = CorDestaque;
        loja.AddText(doc.Loja.Nome);

        var dadosLoja = new List<string> { $"CNPJ {doc.Loja.Cnpj}" };
        if (doc.Loja.Telefone is not null) dadosLoja.Add($"Tel./WhatsApp {doc.Loja.Telefone}");
        if (doc.Loja.Email is not null) dadosLoja.Add(doc.Loja.Email);
        var contato = secao.AddParagraph(string.Join("   ·   ", dadosLoja));
        contato.Format.Font.Size = 9;
        if (doc.Loja.Endereco is not null)
        {
            var endereco = secao.AddParagraph(doc.Loja.Endereco);
            endereco.Format.Font.Size = 9;
        }

        var titulo = secao.AddParagraph();
        titulo.Format.SpaceBefore = Unit.FromCentimeter(0.6);
        titulo.Format.Font.Size = 14;
        titulo.Format.Font.Bold = true;
        titulo.AddText($"Orçamento {doc.Numero}");

        var datas = secao.AddParagraph(
            $"Emitido em {FormatoBr.Data(doc.Emissao)}   ·   Válido até {FormatoBr.Data(doc.ValidadeAte)}");
        datas.Format.SpaceAfter = Unit.FromCentimeter(0.2);

        if (doc.Cliente is not null)
        {
            var cliente = secao.AddParagraph();
            cliente.AddFormattedText("Cliente: ", TextFormat.Bold);
            cliente.AddText(doc.Cliente.Telefone is null ? doc.Cliente.Nome : $"{doc.Cliente.Nome} · {doc.Cliente.Telefone}");
        }

        if (doc.Opcoes.Count > 1)
        {
            var explicacao = secao.AddParagraph(
                "Preparamos opções com produtos equivalentes de marcas e preços diferentes. Escolha a que preferir.");
            explicacao.Format.SpaceBefore = Unit.FromCentimeter(0.2);
            explicacao.Format.Font.Italic = true;
        }
    }

    private static void TabelaDaOpcao(Section secao, OpcaoDocumento opcao, bool mostrarTitulo)
    {
        var titulo = secao.AddParagraph(mostrarTitulo ? $"Opção {opcao.Nome}" : "Itens");
        titulo.Format.SpaceBefore = Unit.FromCentimeter(0.6);
        titulo.Format.SpaceAfter = Unit.FromCentimeter(0.15);
        titulo.Format.Font.Size = 12;
        titulo.Format.Font.Bold = true;
        titulo.Format.Font.Color = CorDestaque;
        titulo.Format.KeepWithNext = true;

        var tabela = secao.AddTable();
        tabela.Borders.Width = 0.5;
        tabela.Borders.Color = Colors.Gray;
        tabela.Format.Font.Size = 9;

        // Largura útil da página A4 com margens de 2 cm: 17 cm.
        tabela.AddColumn(Unit.FromCentimeter(6.6));                                         // Produto
        tabela.AddColumn(Unit.FromCentimeter(2.8));                                         // Marca
        tabela.AddColumn(Unit.FromCentimeter(1.6)).Format.Alignment = ParagraphAlignment.Right; // Qtd
        tabela.AddColumn(Unit.FromCentimeter(1.2)).Format.Alignment = ParagraphAlignment.Center; // Un
        tabela.AddColumn(Unit.FromCentimeter(2.4)).Format.Alignment = ParagraphAlignment.Right; // Preço unit.
        tabela.AddColumn(Unit.FromCentimeter(2.4)).Format.Alignment = ParagraphAlignment.Right; // Total

        var cabecalho = tabela.AddRow();
        cabecalho.HeadingFormat = true; // repete o cabeçalho se a tabela continuar na outra página
        cabecalho.Format.Font.Bold = true;
        cabecalho.Shading.Color = CorCabecalhoTabela;
        string[] titulos = ["Produto", "Marca", "Qtd", "Un", "Preço unit.", "Total"];
        for (var i = 0; i < titulos.Length; i++)
            cabecalho.Cells[i]!.AddParagraph(titulos[i]);

        foreach (var item in opcao.Itens)
        {
            var linha = tabela.AddRow();
            linha.Cells[0]!.AddParagraph(item.Descricao);
            linha.Cells[1]!.AddParagraph(item.Marca ?? "—");
            linha.Cells[2]!.AddParagraph(FormatoBr.Quantidade(item.Quantidade));
            linha.Cells[3]!.AddParagraph(item.UnidadeMedida);
            linha.Cells[4]!.AddParagraph(FormatoBr.Dinheiro(item.PrecoUnitario));
            linha.Cells[5]!.AddParagraph(FormatoBr.Dinheiro(item.PrecoTotal));
        }

        var total = tabela.AddRow();
        total.Format.Font.Bold = true;
        total.Cells[0]!.MergeRight = 4;
        total.Cells[0]!.Format.Alignment = ParagraphAlignment.Right;
        total.Cells[0]!.AddParagraph("Total");
        total.Cells[5]!.AddParagraph(FormatoBr.Dinheiro(opcao.ValorTotal));
    }

    private static void Rodape(Section secao, DocumentoOrcamento doc)
    {
        var aviso = secao.AddParagraph(
            $"Orçamento válido até {FormatoBr.Data(doc.ValidadeAte)}. Após essa data, preços e disponibilidade podem mudar.");
        aviso.Format.SpaceBefore = Unit.FromCentimeter(0.8);
        aviso.Format.Font.Size = 9;

        var rodape = secao.Footers!.Primary!.AddParagraph();
        rodape.Format.Font.Size = 8;
        rodape.Format.Font.Color = Colors.Gray;
        rodape.Format.Alignment = ParagraphAlignment.Center;
        rodape.AddText($"{doc.Loja.Nome} · Orçamento {doc.Numero} · Página ");
        rodape.AddPageField();
        rodape.AddText(" de ");
        rodape.AddNumPagesField();
    }
}
