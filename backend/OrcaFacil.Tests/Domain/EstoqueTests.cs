using AwesomeAssertions;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Domain;

public class EstoqueTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    [Fact]
    public void Entrada_deve_somar_saldo_e_registrar_movimentacao()
    {
        var estoque = new Estoque(Guid.NewGuid(), estoqueMinimo: 5);

        var mov = estoque.RegistrarEntrada(10, UsuarioId, "Compra");

        estoque.QuantidadeDisponivel.Should().Be(10);
        mov.Tipo.Should().Be(TipoMovimentacaoEstoque.Entrada);
        mov.SaldoAnterior.Should().Be(0);
        mov.SaldoPosterior.Should().Be(10);
        estoque.Movimentacoes.Should().ContainSingle();
    }

    [Fact]
    public void Saida_maior_que_saldo_deve_ser_recusada()
    {
        var estoque = new Estoque(Guid.NewGuid(), 0);
        estoque.RegistrarEntrada(3, UsuarioId, null);

        var acao = () => estoque.RegistrarSaida(4, UsuarioId, null);

        acao.Should().Throw<DomainException>().WithMessage("Saldo insuficiente*");
        estoque.QuantidadeDisponivel.Should().Be(3);
    }

    [Fact]
    public void Deve_sinalizar_estoque_abaixo_do_minimo()
    {
        var estoque = new Estoque(Guid.NewGuid(), estoqueMinimo: 5);
        estoque.RegistrarEntrada(6, UsuarioId, null);
        estoque.RegistrarSaida(2, UsuarioId, null);

        estoque.AbaixoDoMinimo.Should().BeTrue();
    }

    [Fact]
    public void Ajuste_define_saldo_contado_e_exige_motivo()
    {
        var estoque = new Estoque(Guid.NewGuid(), 0);
        estoque.RegistrarEntrada(10, UsuarioId, null);

        var mov = estoque.AjustarSaldo(7, UsuarioId, "Inventário mensal");

        estoque.QuantidadeDisponivel.Should().Be(7);
        mov.Quantidade.Should().Be(3);
        FluentActions.Invoking(() => estoque.AjustarSaldo(5, UsuarioId, ""))
            .Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantidade_deve_ser_positiva(decimal quantidade)
    {
        var estoque = new Estoque(Guid.NewGuid(), 0);
        FluentActions.Invoking(() => estoque.RegistrarEntrada(quantidade, UsuarioId, null))
            .Should().Throw<DomainException>();
    }
}
