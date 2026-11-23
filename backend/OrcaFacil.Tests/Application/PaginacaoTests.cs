using AwesomeAssertions;
using OrcaFacil.Application.Comum;

namespace OrcaFacil.Tests.Application;

public class PaginacaoTests
{
    [Theory]
    [InlineData(0, 20, 1, 20)]      // página 0 vira 1
    [InlineData(-5, 20, 1, 20)]     // página negativa vira 1
    [InlineData(2, 0, 2, 1)]        // tamanho 0 vira 1
    [InlineData(1, 5000, 1, 100)]   // tamanho acima do máximo vira 100
    [InlineData(3, 50, 3, 50)]      // valores válidos são mantidos
    public void Parametros_fora_dos_limites_sao_corrigidos(int pagina, int tamanho, int paginaEsperada, int tamanhoEsperado)
    {
        var parametros = new ParametrosPaginacao(pagina, tamanho);

        parametros.Pagina.Should().Be(paginaEsperada);
        parametros.TamanhoPagina.Should().Be(tamanhoEsperado);
    }

    [Fact]
    public void Saltar_calcula_quantos_registros_pular() =>
        new ParametrosPaginacao(pagina: 3, tamanhoPagina: 20).Saltar.Should().Be(40);

    [Theory]
    [InlineData(0L, 0, false, false)]   // lista vazia
    [InlineData(45L, 3, true, false)]   // 45 itens / 20 por página = 3 páginas; estamos na última
    [InlineData(45L, 1, false, true)]   // primeira página
    [InlineData(45L, 2, true, true)]    // página do meio
    public void Informacoes_de_navegacao(long total, int pagina, bool temAnterior, bool temProxima)
    {
        var resultado = new PaginaResultado<string>([], pagina, 20, total);

        resultado.TemAnterior.Should().Be(temAnterior);
        resultado.TemProxima.Should().Be(temProxima);
    }

    [Fact]
    public void Total_de_paginas_arredonda_para_cima() =>
        new PaginaResultado<string>([], 1, 20, 41).TotalPaginas.Should().Be(3);

    [Fact]
    public void Mapear_converte_itens_e_mantem_paginacao()
    {
        var resultado = new PaginaResultado<int>([1, 2, 3], 2, 3, 9);

        var convertido = resultado.Mapear(n => $"Item {n}");

        convertido.Itens.Should().Equal("Item 1", "Item 2", "Item 3");
        convertido.Pagina.Should().Be(2);
        convertido.TotalItens.Should().Be(9);
    }
}
