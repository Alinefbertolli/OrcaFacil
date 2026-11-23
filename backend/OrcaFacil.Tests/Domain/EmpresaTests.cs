using AwesomeAssertions;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Domain;

public class EmpresaTests
{
    private const string CnpjValido = "11.222.333/0001-81";

    [Fact]
    public void Cria_empresa_com_cnpj_somente_digitos()
    {
        var empresa = new Empresa("Papelaria Exemplo Ltda", CnpjValido, SegmentoEmpresa.Papelaria, "Papelaria Exemplo");

        empresa.Cnpj.Should().Be("11222333000181");
        empresa.Ativa.Should().BeTrue();
        empresa.NomeExibicao.Should().Be("Papelaria Exemplo");
    }

    [Fact]
    public void Recusa_cnpj_invalido() =>
        FluentActions.Invoking(() => new Empresa("Loja", "11.222.333/0001-80", SegmentoEmpresa.Outro))
            .Should().Throw<DomainException>().WithMessage("CNPJ inválido.");

    [Fact]
    public void Sem_nome_fantasia_exibe_razao_social() =>
        new Empresa("Comércio Geral Ltda", CnpjValido, SegmentoEmpresa.Utilidades)
            .NomeExibicao.Should().Be("Comércio Geral Ltda");

    [Fact]
    public void Modo_padrao_de_orcamento_comeca_comparativo_e_pode_mudar()
    {
        var empresa = new Empresa("Pijamas da Ana Ltda", CnpjValido, SegmentoEmpresa.Vestuario);
        empresa.ModoOrcamentoPadrao.Should().Be(ModoOrcamento.Comparativo);

        empresa.DefinirModoOrcamentoPadrao(ModoOrcamento.Unico);

        empresa.ModoOrcamentoPadrao.Should().Be(ModoOrcamento.Unico);
        FluentActions.Invoking(() => empresa.DefinirModoOrcamentoPadrao((ModoOrcamento)99))
            .Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("  Caderno  10 Matérias ", "caderno 10 materias")]
    [InlineData("LÁPIS Nº 2", "lapis nº 2")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Grupo_de_equivalencia_do_produto_e_normalizado(string? informado, string? esperado)
    {
        var produto = new Produto { Codigo = "1", Nome = "x", UnidadeMedida = "UN" };
        produto.DefinirGrupoEquivalencia(informado);
        produto.GrupoEquivalencia.Should().Be(esperado);
    }

    [Fact]
    public void Preco_de_venda_negativo_e_recusado() =>
        FluentActions.Invoking(() => new Produto { Codigo = "1", Nome = "x", UnidadeMedida = "UN" }.DefinirPrecoVenda(-1m))
            .Should().Throw<DomainException>();
}

public class EntidadeDaEmpresaTests
{
    private static readonly Guid EmpresaA = Guid.NewGuid();
    private static readonly Guid EmpresaB = Guid.NewGuid();

    [Fact]
    public void Registro_nao_pode_ser_transferido_para_outra_empresa()
    {
        var produto = new Produto { Codigo = "001", Nome = "Lápis HB", UnidadeMedida = "UN" };
        produto.VincularEmpresa(EmpresaA);

        FluentActions.Invoking(() => produto.VincularEmpresa(EmpresaB))
            .Should().Throw<DomainException>().WithMessage("*outra empresa*");
        produto.EmpresaId.Should().Be(EmpresaA);
    }

    [Fact]
    public void Vincular_de_novo_a_mesma_empresa_e_permitido()
    {
        var produto = new Produto { Codigo = "001", Nome = "Lápis HB", UnidadeMedida = "UN" };
        produto.VincularEmpresa(EmpresaA);

        FluentActions.Invoking(() => produto.VincularEmpresa(EmpresaA)).Should().NotThrow();
    }

    [Fact]
    public void Movimentacao_de_estoque_herda_a_empresa_do_estoque()
    {
        var estoque = new Estoque(Guid.NewGuid(), estoqueMinimo: 0);
        estoque.VincularEmpresa(EmpresaA);

        var movimentacao = estoque.RegistrarEntrada(5, Guid.NewGuid(), "Compra");

        movimentacao.EmpresaId.Should().Be(EmpresaA);
    }

    [Fact]
    public void Orcamento_duplicado_pertence_a_mesma_empresa()
    {
        var original = new Orcamento("ORC-2026-000001", Guid.NewGuid(), null, DateOnly.FromDateTime(DateTime.Today));
        original.VincularEmpresa(EmpresaA);

        var copia = original.Duplicar("ORC-2026-000002", Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today));

        copia.EmpresaId.Should().Be(EmpresaA);
    }

    [Fact]
    public void Historico_de_preco_herda_a_empresa_da_tabela_do_fornecedor()
    {
        var tabela = new ProdutoFornecedor(Guid.NewGuid(), Guid.NewGuid());
        tabela.VincularEmpresa(EmpresaA);

        var historico = tabela.AtualizarPreco(12.5m, DateTime.UtcNow);

        historico.EmpresaId.Should().Be(EmpresaA);
    }
}
