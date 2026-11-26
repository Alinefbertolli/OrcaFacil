using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Domain.Comum;

namespace OrcaFacil.Infrastructure.Importacao;

/// <summary>
/// Lê planilhas de catálogo (.xlsx com ClosedXML, licença MIT; .csv com um leitor próprio) e gera a planilha modelo.
/// </summary>
public sealed class PlanilhasCatalogo : IPlanilhasCatalogo
{
    public IReadOnlyList<IReadOnlyList<string>> Ler(Stream conteudo, FormatoPlanilha formato) => formato switch
    {
        FormatoPlanilha.Xlsx => LerExcel(conteudo),
        FormatoPlanilha.Csv => LerCsv(conteudo),
        _ => throw new DomainException("Formato de planilha não suportado.")
    };

    // ------------------------------------------------------------ Excel

    private static List<IReadOnlyList<string>> LerExcel(Stream conteudo)
    {
        XLWorkbook pasta;
        try
        {
            pasta = new XLWorkbook(conteudo);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            throw new DomainException("Não foi possível abrir a planilha. O arquivo pode estar corrompido ou protegido por senha.");
        }

        using (pasta)
        {
            var planilha = pasta.Worksheets.FirstOrDefault(p => p.LastRowUsed(XLCellsUsedOptions.Contents) is not null)
                ?? throw new DomainException("A planilha está vazia.");

            // "Contents": só células com conteúdo contam (uma coluna inteira formatada não vira 1 milhão de linhas).
            var ultimaLinha = planilha.LastRowUsed(XLCellsUsedOptions.Contents)!.RowNumber();
            var ultimaColuna = planilha.LastColumnUsed(XLCellsUsedOptions.Contents)!.ColumnNumber();
            var linhas = new List<IReadOnlyList<string>>(ultimaLinha);

            // Começamos na linha 1 (mesmo vazia) para que a posição na lista seja o número da linha no Excel.
            for (var l = 1; l <= ultimaLinha; l++)
            {
                var celulas = new string[ultimaColuna];
                for (var c = 1; c <= ultimaColuna; c++)
                    celulas[c - 1] = TextoDaCelula(planilha.Cell(l, c));
                linhas.Add(celulas);
            }

            return linhas;
        }
    }

    /// <summary>
    /// Números saem sem a formatação visual da célula ("R$ 1.234,50" vira "1234,5"): sem separador de milhar
    /// e com vírgula decimal. Assim "12,345" nunca é confundido com "doze mil trezentos e quarenta e cinco".
    /// </summary>
    private static string TextoDaCelula(IXLCell celula)
    {
        if (celula.IsEmpty()) return string.Empty;
        if (celula.DataType == XLDataType.Number)
            return ((decimal)celula.GetDouble()).ToString("0.##########", CultureInfo.InvariantCulture).Replace('.', ',');
        return celula.GetFormattedString().Trim();
    }

    // ------------------------------------------------------------ CSV

    private static List<IReadOnlyList<string>> LerCsv(Stream conteudo)
    {
        using var memoria = new MemoryStream();
        conteudo.CopyTo(memoria);
        return LerCsv(Decodificar(memoria.ToArray()));
    }

    /// <summary>
    /// CSV do Excel brasileiro costuma vir em "Windows-1252" (ANSI), e não em UTF-8.
    /// Tentamos UTF-8 "estrito" (que falha com bytes inválidos); se falhar, usamos Latin-1, que cobre os acentos.
    /// </summary>
    public static string Decodificar(byte[] bytes)
    {
        try
        {
            var texto = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            return texto.TrimStart('﻿'); // marca BOM que o Excel coloca no início
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    /// <summary>
    /// Lê CSV com separador ";" (padrão do Excel no Brasil), "," ou tabulação, descoberto pela primeira linha.
    /// Aceita campos entre aspas, com o separador ou quebra de linha dentro e aspas dobradas ("").
    /// </summary>
    public static List<IReadOnlyList<string>> LerCsv(string texto)
    {
        var separador = DescobrirSeparador(texto);
        var linhas = new List<IReadOnlyList<string>>();
        var linhaAtual = new List<string>();
        var campo = new StringBuilder();
        var entreAspas = false;

        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (entreAspas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"') { campo.Append('"'); i++; }
                else if (c == '"') entreAspas = false;
                else campo.Append(c);
                continue;
            }

            if (c == '"') entreAspas = true;
            else if (c == separador) { linhaAtual.Add(campo.ToString().Trim()); campo.Clear(); }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n') i++;
                linhaAtual.Add(campo.ToString().Trim());
                campo.Clear();
                linhas.Add(linhaAtual);
                linhaAtual = [];
            }
            else campo.Append(c);
        }

        if (campo.Length > 0 || linhaAtual.Count > 0)
        {
            linhaAtual.Add(campo.ToString().Trim());
            linhas.Add(linhaAtual);
        }

        return linhas;
    }

    private static char DescobrirSeparador(string texto)
    {
        var fim = texto.IndexOfAny(['\r', '\n']);
        var primeira = fim < 0 ? texto : texto[..fim];
        char[] candidatos = [';', '\t', ','];
        return candidatos.OrderByDescending(s => primeira.Count(c => c == s)).First();
    }

    // ------------------------------------------------------------ Modelo

    public byte[] GerarModelo()
    {
        using var pasta = new XLWorkbook();

        var produtos = pasta.Worksheets.Add("Produtos");
        string[] titulos = ["Código", "Nome", "Preço de venda", "Estoque", "Estoque mínimo", "Unidade", "Categoria",
            "Marca", "Grupo de equivalência", "Descrição"];
        for (var i = 0; i < titulos.Length; i++)
            produtos.Cell(1, i + 1).Value = titulos[i];

        var cabecalho = produtos.Range(1, 1, 1, titulos.Length);
        cabecalho.Style.Font.Bold = true;
        cabecalho.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
        cabecalho.Style.Font.FontColor = XLColor.White;
        produtos.Column(1).Style.NumberFormat.Format = "@"; // código como texto: preserva zeros à esquerda
        produtos.Column(3).Style.NumberFormat.Format = "#,##0.00";
        produtos.SheetView.FreezeRows(1);
        produtos.Columns().Width = 18;
        produtos.Column(2).Width = 40;

        var instrucoes = pasta.Worksheets.Add("Instruções");
        string[] textos =
        [
            "Como preencher a planilha de produtos do OrçaFácil",
            "",
            "• Preencha a aba \"Produtos\" a partir da linha 2. Só Código e Nome são obrigatórios (Nome só para produtos novos).",
            "• O produto é encontrado pelo Código (SKU). Código que não existe no OrçaFácil = produto novo.",
            "• Célula vazia = o OrçaFácil não mexe naquele dado.",
            "• Preço de venda: o valor que o cliente paga. Ex.: 12,50.",
            "• Estoque: o saldo atual. Vira um ajuste de inventário, com histórico.",
            "• Unidade: UN, CX, PCT, KG, M... (vazio = UN).",
            "• Categoria e Marca que não existem são criadas automaticamente (marca nova com nível 3).",
            "• Grupo de equivalência: produtos com o mesmo grupo são substitutos no orçamento comparativo. Ex.: caderno 10 materias.",
            "• Produtos que não estão na planilha continuam como estão.",
            "",
            "Exemplo:",
        ];
        for (var i = 0; i < textos.Length; i++)
            instrucoes.Cell(i + 1, 1).Value = textos[i];
        instrucoes.Cell(1, 1).Style.Font.Bold = true;
        instrucoes.Cell(1, 1).Style.Font.FontSize = 14;

        var linhaExemplo = textos.Length + 1;
        string[][] exemplo =
        [
            titulos,
            ["CAD10-1", "Caderno 10 matérias Tilibra", "29,90", "12", "5", "UN", "Cadernos", "Tilibra", "caderno 10 materias", ""],
            ["LAP-HB", "Lápis preto HB", "1,50", "200", "50", "UN", "Escrita", "Faber-Castell", "", ""],
        ];
        for (var l = 0; l < exemplo.Length; l++)
            for (var c = 0; c < exemplo[l].Length; c++)
                instrucoes.Cell(linhaExemplo + l, c + 1).Value = exemplo[l][c];
        instrucoes.Range(linhaExemplo, 1, linhaExemplo, titulos.Length).Style.Font.Bold = true;
        instrucoes.Column(1).Width = 18;

        using var saida = new MemoryStream();
        pasta.SaveAs(saida);
        return saida.ToArray();
    }
}
