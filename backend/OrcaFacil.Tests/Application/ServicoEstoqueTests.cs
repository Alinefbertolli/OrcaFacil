using AwesomeAssertions;
using FluentValidation;
using OrcaFacil.Application.Comum;
using OrcaFacil.Application.Estoques;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Application;

public class ServicoEstoqueTests
{
    private readonly FakeRepositorio<Produto> _produtos = new();
    private readonly FakeEstoques _estoques = new();
    private readonly FakeUnitOfWork _unidadeDeTrabalho = new();
    private readonly Produto _caderno = new() { Codigo = "CAD10-2", Nome = "Caderno 10 matérias Foroni", UnidadeMedida = "UN" };
    private readonly ServicoEstoque _servico;

    public ServicoEstoqueTests()
    {
        _produtos.Adicionar(_caderno);
        _servico = new ServicoEstoque(_estoques, _produtos, new FakeUsuarioAtual(Guid.NewGuid()), _unidadeDeTrabalho,
            new MovimentarEstoqueValidador(), new AjustarEstoqueValidador(), new ConfigurarEstoqueValidador());
    }

    [Fact]
    public async Task Produto_sem_movimentacao_aparece_como_nao_controlado()
    {
        var estoque = await _servico.ObterAsync(_caderno.Id);

        estoque.Controlado.Should().BeFalse();
        estoque.QuantidadeDisponivel.Should().Be(0);
    }

    [Fact]
    public async Task Primeira_entrada_cria_o_controle_de_estoque()
    {
        var estoque = await _servico.RegistrarEntradaAsync(_caderno.Id, new MovimentarEstoqueRequisicao(50, "Compra NF 123"));

        estoque.Controlado.Should().BeTrue();
        estoque.QuantidadeDisponivel.Should().Be(50);
        _estoques.Itens.Should().ContainSingle().Which.Movimentacoes.Should().ContainSingle()
            .Which.Tipo.Should().Be(TipoMovimentacaoEstoque.Entrada);
        _unidadeDeTrabalho.VezesSalvo.Should().Be(1);
    }

    [Fact]
    public async Task Saida_de_produto_sem_controle_e_recusada() =>
        await FluentActions.Awaiting(() => _servico.RegistrarSaidaAsync(_caderno.Id, new MovimentarEstoqueRequisicao(1, null)))
            .Should().ThrowAsync<DomainException>().WithMessage("*não tem controle de estoque*");

    [Fact]
    public async Task Saida_maior_que_o_saldo_e_recusada_e_o_saldo_nao_muda()
    {
        _estoques.ComSaldo(_caderno.Id, 3);

        await FluentActions.Awaiting(() => _servico.RegistrarSaidaAsync(_caderno.Id, new MovimentarEstoqueRequisicao(5, "Perda")))
            .Should().ThrowAsync<DomainException>().WithMessage("Saldo insuficiente*");
        (await _servico.ObterAsync(_caderno.Id)).QuantidadeDisponivel.Should().Be(3);
    }

    [Fact]
    public async Task Ajuste_de_inventario_define_o_saldo_contado()
    {
        _estoques.ComSaldo(_caderno.Id, 50);

        var estoque = await _servico.AjustarAsync(_caderno.Id, new AjustarEstoqueRequisicao(48, "Inventário de novembro"));

        estoque.QuantidadeDisponivel.Should().Be(48);
    }

    [Fact]
    public async Task Ajuste_sem_motivo_e_recusado() =>
        await FluentActions.Awaiting(() => _servico.AjustarAsync(_caderno.Id, new AjustarEstoqueRequisicao(10, "")))
            .Should().ThrowAsync<ValidationException>();

    [Fact]
    public async Task Configurar_minimo_mostra_alerta_de_reposicao()
    {
        _estoques.ComSaldo(_caderno.Id, 4);

        var estoque = await _servico.ConfigurarAsync(_caderno.Id, new ConfigurarEstoqueRequisicao(10, " Prateleira B2 "));

        estoque.AbaixoDoMinimo.Should().BeTrue();
        estoque.Localizacao.Should().Be("Prateleira B2");
    }

    [Fact]
    public async Task Produto_inexistente_da_404() =>
        await FluentActions.Awaiting(() => _servico.RegistrarEntradaAsync(Guid.NewGuid(), new MovimentarEstoqueRequisicao(1, null)))
            .Should().ThrowAsync<NaoEncontradoException>();

    [Theory]
    [InlineData(null, 5, DisponibilidadeEstoque.NaoControlado)]
    [InlineData(10, 5, DisponibilidadeEstoque.Disponivel)]
    [InlineData(5, 5, DisponibilidadeEstoque.Disponivel)]
    [InlineData(2, 5, DisponibilidadeEstoque.Parcial)]
    [InlineData(0, 5, DisponibilidadeEstoque.Indisponivel)]
    public void Disponibilidade_compara_saldo_com_a_quantidade_pedida(int? saldo, int pedido, DisponibilidadeEstoque esperado) =>
        Disponibilidade.Avaliar(saldo, pedido).Should().Be(esperado);
}
