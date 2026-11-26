using System.Text;
using AwesomeAssertions;
using ClosedXML.Excel;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Infrastructure.Importacao;

namespace OrcaFacil.Tests.Infrastructure;

/// <summary>Leitura REAL de planilhas (.xlsx gerado em memória e textos .csv).</summary>
public class PlanilhasCatalogoTests
{
    private readonly PlanilhasCatalogo _planilhas = new();

    [Fact]
    public void Csv_do_excel_brasileiro_com_ponto_e_virgula_e_aspas()
    {
        var linhas = PlanilhasCatalogo.LerCsv(
            "Código;Nome;Preço\r\n" +
            "A1;\"Caderno; capa dura\";29,90\r\n" +
            "A2;\"Régua \"\"30 cm\"\"\";3,50\r\n");

        linhas.Should().HaveCount(3);
        linhas[1].Should().Equal("A1", "Caderno; capa dura", "29,90");
        linhas[2].Should().Equal("A2", "Régua \"30 cm\"", "3,50");
    }

    [Fact]
    public void Csv_com_virgula_como_separador()
    {
        var linhas = PlanilhasCatalogo.LerCsv("Codigo,Nome,Preco\nA1,Caderno,\"29,90\"");

        linhas[1].Should().Equal("A1", "Caderno", "29,90");
    }

    [Fact]
    public void Csv_salvo_em_ANSI_mantem_os_acentos()
    {
        var bytes = Encoding.Latin1.GetBytes("Código;Descrição\nA1;Régua");

        PlanilhasCatalogo.Decodificar(bytes).Should().Be("Código;Descrição\nA1;Régua");
    }

    [Fact]
    public void Excel_numeros_saem_sem_formatacao_e_linhas_mantem_a_numeracao_do_excel()
    {
        using var pasta = new XLWorkbook();
        var aba = pasta.AddWorksheet("Produtos");
        aba.Cell(2, 1).Value = "Código";
        aba.Cell(2, 2).Value = "Nome";
        aba.Cell(2, 3).Value = "Preço";
        aba.Cell(3, 1).Value = "A1";
        aba.Cell(3, 2).Value = "Caderno";
        aba.Cell(3, 3).Value = 1234.5;
        aba.Cell(3, 3).Style.NumberFormat.Format = "R$ #,##0.00"; // o visual não pode atrapalhar
        using var memoria = new MemoryStream();
        pasta.SaveAs(memoria);

        var linhas = _planilhas.Ler(new MemoryStream(memoria.ToArray()), FormatoPlanilha.Xlsx);

        linhas.Should().HaveCount(3);
        linhas[0].Should().OnlyContain(c => c == ""); // linha 1 em branco
        linhas[2].Should().Equal("A1", "Caderno", "1234,5");

        var interpretada = InterpretadorPlanilhaCatalogo.Interpretar(linhas);
        interpretada.Linhas.Single().Should().Match<LinhaCatalogo>(l => l.NumeroLinha == 3 && l.PrecoVenda == 1234.5m);
    }

    [Fact]
    public void A_planilha_modelo_e_entendida_pela_propria_importacao()
    {
        var modelo = _planilhas.GerarModelo();

        var linhas = _planilhas.Ler(new MemoryStream(modelo), FormatoPlanilha.Xlsx);
        var acao = () => InterpretadorPlanilhaCatalogo.Interpretar(linhas);

        // Só o cabeçalho: todas as colunas reconhecidas, nenhum produto.
        var colunas = Enum.GetValues<CampoPlanilha>();
        linhas.Should().ContainSingle();
        acao().ColunasReconhecidas.Should().BeEquivalentTo(colunas);
    }

    [Fact]
    public void Arquivo_que_nao_e_excel_de_verdade_gera_erro_amigavel()
    {
        var acao = () => _planilhas.Ler(new MemoryStream("PK\u0003\u0004 lixo"u8.ToArray()), FormatoPlanilha.Xlsx);

        acao.Should().Throw<DomainException>().WithMessage("*corrompido*");
    }
}
