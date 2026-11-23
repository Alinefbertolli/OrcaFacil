using AwesomeAssertions;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Tests.Domain;

public class ListaImportadaTests
{
    private static ListaImportada ListaEmRevisao()
    {
        var lista = new ListaImportada(Guid.NewGuid(), FormatoImportacao.WhatsApp, null);
        lista.ConcluirExtracao("10 cx caneta azul\n2 pct papel sulfite",
        [
            new ItemListaImportada("caneta azul", 10, "cx", null, "10 cx caneta azul", 0.9m),
            new ItemListaImportada("papel sulfite", 2, "pct", null, "2 pct papel sulfite", 0.6m)
        ]);
        return lista;
    }

    [Fact]
    public void Lista_nao_revisada_nao_pode_gerar_orcamento()
    {
        var lista = ListaEmRevisao();

        lista.Status.Should().Be(StatusImportacao.AguardandoRevisao);
        FluentActions.Invoking(lista.GarantirProntaParaOrcamento)
            .Should().Throw<DomainException>().WithMessage("*revisada*");
    }

    [Fact]
    public void Itens_excluidos_na_revisao_nao_seguem_para_orcamento()
    {
        var lista = ListaEmRevisao();
        var papel = lista.Itens.Single(i => i.Descricao == "papel sulfite");

        lista.ExcluirItem(papel.Id);
        lista.ConfirmarRevisao();

        lista.ItensValidos.Should().ContainSingle(i => i.Descricao == "caneta azul");
        FluentActions.Invoking(lista.GarantirProntaParaOrcamento).Should().NotThrow();
    }

    [Fact]
    public void Nao_confirma_revisao_sem_itens_validos()
    {
        var lista = ListaEmRevisao();
        foreach (var item in lista.Itens.ToList()) lista.ExcluirItem(item.Id);

        FluentActions.Invoking(lista.ConfirmarRevisao).Should().Throw<DomainException>();
    }

    [Fact]
    public void Correcao_manual_normaliza_unidade_e_eleva_confianca()
    {
        var lista = ListaEmRevisao();
        var papel = lista.Itens.Single(i => i.Descricao == "papel sulfite");

        lista.EditarItem(papel.Id, "papel sulfite A4 75g", 3, "resma", null, null);

        papel.UnidadeMedida.Should().Be("RESMA");
        papel.Quantidade.Should().Be(3);
        papel.ConfiancaInterpretacao.Should().Be(1m);
    }
}

public class DocumentoFiscalTests
{
    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("111.111.111-11", false)]
    [InlineData("529.982.247-24", false)]
    public void Valida_cpf(string cpf, bool esperado) =>
        DocumentoFiscal.CpfValido(cpf).Should().Be(esperado);

    [Theory]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11.222.333/0001-80", false)]
    [InlineData("00.000.000/0000-00", false)]
    public void Valida_cnpj(string cnpj, bool esperado) =>
        DocumentoFiscal.CnpjValido(cnpj).Should().Be(esperado);
}
