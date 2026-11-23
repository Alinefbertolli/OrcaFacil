using AwesomeAssertions;
using OrcaFacil.Application.Importacao;

namespace OrcaFacil.Tests.Application;

public class InterpretadorListaTests
{
    private static readonly string[] Marcas = ["BIC", "Tilibra", "Faber-Castell"];

    private static ItemInterpretado Unico(string linha) =>
        InterpretadorLista.Interpretar(linha, Marcas).Should().ContainSingle().Subject;

    [Theory]
    [InlineData("20 cx caneta azul", 20.0, "CX", "caneta azul")]
    [InlineData("20x caneta azul", 20.0, "UN", "caneta azul")]
    [InlineData("20cx caneta azul", 20.0, "CX", "caneta azul")]
    [InlineData("caneta azul - 20 cx", 20.0, "CX", "caneta azul")]
    [InlineData("caneta azul (20 un)", 20.0, "UN", "caneta azul")]
    [InlineData("caneta azul: 20", 20.0, "UN", "caneta azul")]
    [InlineData("caneta azul 20 caixas", 20.0, "CX", "caneta azul")]
    [InlineData("5 resmas de papel A4", 5.0, "RESMA", "papel A4")]
    [InlineData("1,5 kg de massinha", 1.5, "KG", "massinha")]
    [InlineData("1.000 un clips", 1000.0, "UN", "clips")]
    public void Reconhece_quantidade_unidade_e_descricao(string linha, double quantidade, string unidade, string descricao)
    {
        var item = Unico(linha);

        item.Quantidade.Should().Be((decimal)quantidade);
        item.UnidadeMedida.Should().Be(unidade);
        item.Descricao.Should().Be(descricao);
        item.QuantidadeInformada.Should().BeTrue();
    }

    [Fact]
    public void Numero_que_faz_parte_da_descricao_nao_vira_quantidade()
    {
        var item = Unico("caderno 200 folhas");

        item.Quantidade.Should().Be(1);
        item.QuantidadeInformada.Should().BeFalse();
        item.Descricao.Should().Be("caderno 200 folhas");
    }

    [Fact]
    public void Parenteses_da_descricao_sao_preservados() =>
        Unico("Papel A4 (500 folhas)").Descricao.Should().Be("Papel A4 (500 folhas)");

    [Theory]
    [InlineData("20 cx caneta azul BIC", "BIC", "caneta azul")]
    [InlineData("12 lápis Faber-Castell", "Faber-Castell", "lápis")]
    [InlineData("3 kits de pintura marca Acrilex", "Acrilex", "pintura")]
    public void Reconhece_a_marca(string linha, string marca, string descricao)
    {
        var item = Unico(linha);

        item.Marca.Should().Be(marca);
        item.Descricao.Should().Be(descricao);
    }

    [Fact]
    public void Marca_so_e_reconhecida_como_palavra_inteira() =>
        Unico("2 bicicletas infantis").Marca.Should().BeNull("'BIC' não pode casar com 'bicicletas'");

    [Fact]
    public void Lista_do_whatsapp_ignora_conversa_prefixos_marcadores_e_emojis()
    {
        const string conversa = """
            [10:32, 18/11/2026] Cliente: Bom dia! Preciso de:
            [10:32, 18/11/2026] Cliente: - 20 cx caneta azul BIC
            18/11/2026 10:33 - Cliente: • 10 cadernos Tilibra ✅
            Material escolar:
            <Mídia oculta>
            Obrigada!
            """;

        var itens = InterpretadorLista.Interpretar(conversa, Marcas);

        itens.Should().HaveCount(2);
        itens[0].Descricao.Should().Be("caneta azul");
        itens[1].Descricao.Should().Be("cadernos");
        itens[1].Marca.Should().Be("Tilibra");
        itens[0].TextoOriginal.Should().Contain("[10:32", "o texto original é guardado para conferência");
    }

    [Fact]
    public void Linha_numerada_nao_confunde_numero_do_item_com_quantidade() =>
        Unico("1. 3 pacotes de etiqueta").Quantidade.Should().Be(3);

    [Fact]
    public void Linha_sem_quantidade_tem_confianca_menor()
    {
        var comQuantidade = Unico("20 cx caneta azul");
        var semQuantidade = Unico("caneta azul");

        semQuantidade.Confianca.Should().BeLessThan(comQuantidade.Confianca);
        semQuantidade.Confianca.Should().BeLessThan(InterpretadorLista.ConfiancaParaDispensarAtencao);
    }

    [Fact]
    public void Linhas_vazias_ou_sem_letras_sao_ignoradas() =>
        InterpretadorLista.Interpretar("\n\n   \n---\n123\n", Marcas).Should().BeEmpty();
}
