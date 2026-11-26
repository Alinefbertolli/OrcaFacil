using AwesomeAssertions;
using FluentValidation;
using OrcaFacil.Application.Cadastros;

namespace OrcaFacil.Tests.Application;

public class InterpretadorPlanilhaCatalogoTests
{
    private static IReadOnlyList<IReadOnlyList<string>> Planilha(params string[][] linhas) => linhas;

    [Theory]
    [InlineData("12,50", 12.50)]
    [InlineData("R$ 12,50", 12.50)]
    [InlineData("R$ 1.234,56", 1234.56)]
    [InlineData("1234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("12.5", 12.5)]
    [InlineData("1.500", 1500)]
    [InlineData("1.234.567", 1234567)]
    [InlineData("0.500", 0.5)]
    [InlineData("  30 ", 30)]
    public void Le_numeros_no_jeito_brasileiro_e_no_neutro(string texto, double esperado)
    {
        InterpretadorPlanilhaCatalogo.TentarLerNumero(texto, out var valor).Should().BeTrue();
        valor.Should().Be((decimal)esperado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("R$")]
    public void Celula_vazia_vira_null(string texto)
    {
        InterpretadorPlanilhaCatalogo.TentarLerNumero(texto, out var valor).Should().BeTrue();
        valor.Should().BeNull();
    }

    [Fact]
    public void Texto_que_nao_e_numero_e_recusado() =>
        InterpretadorPlanilhaCatalogo.TentarLerNumero("doze", out _).Should().BeFalse();

    [Fact]
    public void Reconhece_varios_nomes_de_coluna_com_acento_e_maiusculas()
    {
        var resultado = InterpretadorPlanilhaCatalogo.Interpretar(Planilha(
            ["SKU", "Descrição do Produto", "Preço de Venda", "Saldo", "UN", "Departamento", "Fabricante", "Estoque Mínimo"],
            ["CAD-1", "Caderno 10 matérias", "29,90", "12", "un", "Cadernos", "Tilibra", "5"]));

        resultado.Erros.Should().BeEmpty();
        resultado.Linhas.Should().ContainSingle().Which.Should().BeEquivalentTo(new LinhaCatalogo(
            2, "CAD-1", "Caderno 10 matérias", null, 29.90m, 12m, 5m, "UN", "Cadernos", "Tilibra", null));
    }

    [Fact]
    public void Com_nome_e_descricao_o_nome_vira_nome_e_a_descricao_vira_descricao()
    {
        var linha = InterpretadorPlanilhaCatalogo.Interpretar(Planilha(
            ["Código", "Descrição", "Nome"],
            ["A1", "Capa dura, 200 folhas", "Caderno"])).Linhas.Single();

        linha.Nome.Should().Be("Caderno");
        linha.Descricao.Should().Be("Capa dura, 200 folhas");
    }

    [Fact]
    public void Preco_de_custo_nao_e_confundido_com_preco_de_venda()
    {
        var resultado = InterpretadorPlanilhaCatalogo.Interpretar(Planilha(
            ["Código", "Nome", "Preço de custo"],
            ["A1", "Caderno", "10,00"]));

        resultado.ColunasReconhecidas.Should().NotContain(CampoPlanilha.PrecoVenda);
        resultado.Linhas.Single().PrecoVenda.Should().BeNull();
    }

    [Fact]
    public void Acha_o_cabecalho_mesmo_depois_de_linhas_de_titulo()
    {
        var resultado = InterpretadorPlanilhaCatalogo.Interpretar(Planilha(
            ["Relatório de produtos - ERP"],
            [],
            ["Código", "Nome"],
            ["A1", "Caderno"]));

        resultado.Linhas.Single().NumeroLinha.Should().Be(4); // linha 4 do Excel
    }

    [Fact]
    public void Sem_cabecalho_reconhecivel_explica_o_que_falta()
    {
        var acao = () => InterpretadorPlanilhaCatalogo.Interpretar(Planilha(["Coluna A", "Coluna B"], ["1", "2"]));

        acao.Should().Throw<ValidationException>().WithMessage("*Código*Nome*");
    }

    [Fact]
    public void Linhas_com_problema_viram_erro_e_as_outras_continuam()
    {
        var resultado = InterpretadorPlanilhaCatalogo.Interpretar(Planilha(
            ["Código", "Nome", "Preço", "Estoque"],
            ["A1", "Caderno", "29,90", "10"],
            ["", "Sem código", "1", ""],
            ["A2", "Preço errado", "abc", ""],
            ["A3", "Estoque negativo", "1", "-5"],
            ["a1", "Código repetido", "1", ""],
            ["", "", "", ""],
            ["A4", "Lápis", "", ""]));

        resultado.Linhas.Select(l => l.Codigo).Should().Equal("A1", "A4");
        resultado.Erros.Select(e => $"{e.NumeroLinha}: {e.Mensagem}").Should().Equal(
            "3: Linha sem código (SKU).",
            "4: Preço de venda inválido: \"abc\".",
            "5: Estoque não pode ser negativo.",
            "6: Código repetido na planilha (já apareceu na linha 2).");
    }

    [Fact]
    public void Celula_vazia_significa_nao_mexer()
    {
        var linha = InterpretadorPlanilhaCatalogo.Interpretar(Planilha(
            ["Código", "Nome", "Preço", "Estoque", "Marca"],
            ["A1", "", "", "", ""])).Linhas.Single();

        linha.Nome.Should().BeNull();
        linha.PrecoVenda.Should().BeNull();
        linha.Estoque.Should().BeNull();
        linha.Marca.Should().BeNull();
    }
}
