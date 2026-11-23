using AwesomeAssertions;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Domain;

public class OrcamentoTests
{
    private static Orcamento NovoOrcamento() =>
        new("ORC-2026-000001", Guid.NewGuid(), clienteId: null, DateOnly.FromDateTime(DateTime.Today.AddDays(15)));

    private static ItemOrcamento Item(decimal qtd, decimal preco, decimal? frete = null,
        OrigemPreco origem = OrigemPreco.FornecedorCadastrado) =>
        new("Caderno universitário 10 matérias", "Marca X", qtd, "un", preco, origem, frete: frete);

    [Fact]
    public void Totais_devem_ser_calculados_a_partir_dos_itens()
    {
        var orcamento = NovoOrcamento();
        var opcao = orcamento.AdicionarOpcao(TipoOpcaoOrcamento.MenorPreco);

        opcao.AdicionarItem(Item(10, 32.90m, frete: 15m));
        opcao.AdicionarItem(Item(3, 1.335m)); // 4,005 → 4,01 (arredondamento comercial)

        opcao.Subtotal.Should().Be(333.01m);
        opcao.FreteTotal.Should().Be(15m);
        opcao.ValorTotal.Should().Be(348.01m);
    }

    [Fact]
    public void Economia_compara_selecionada_com_opcao_mais_cara()
    {
        var orcamento = NovoOrcamento();
        var cara = orcamento.AdicionarOpcao(TipoOpcaoOrcamento.MarcasReconhecidas);
        cara.AdicionarItem(Item(10, 40m));
        var barata = orcamento.AdicionarOpcao(TipoOpcaoOrcamento.MenorPreco);
        barata.AdicionarItem(Item(10, 30m));

        orcamento.SelecionarOpcao(barata.Id);

        orcamento.CalcularEconomia().Should().Be(100m);
        orcamento.OpcaoSelecionada.Should().Be(barata);
    }

    [Fact]
    public void Nao_deve_haver_duas_opcoes_do_mesmo_tipo()
    {
        var orcamento = NovoOrcamento();
        orcamento.AdicionarOpcao(TipoOpcaoOrcamento.CustoBeneficio);

        FluentActions.Invoking(() => orcamento.AdicionarOpcao(TipoOpcaoOrcamento.CustoBeneficio))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Envio_para_aprovacao_exige_opcao_selecionada()
    {
        var orcamento = NovoOrcamento();

        FluentActions.Invoking(() => orcamento.AlterarStatus(StatusOrcamento.AguardandoAprovacao))
            .Should().Throw<DomainException>().WithMessage("Selecione uma opção*");
    }

    [Fact]
    public void Fluxo_de_status_valido_e_finalizado_e_terminal()
    {
        var orcamento = NovoOrcamento();
        var opcao = orcamento.AdicionarOpcao(TipoOpcaoOrcamento.MenorPreco);
        opcao.AdicionarItem(Item(1, 10m));
        orcamento.SelecionarOpcao(opcao.Id);

        orcamento.AlterarStatus(StatusOrcamento.AguardandoAprovacao);
        orcamento.AlterarStatus(StatusOrcamento.Aprovado);
        orcamento.AlterarStatus(StatusOrcamento.Finalizado);

        orcamento.Status.Should().Be(StatusOrcamento.Finalizado);
        FluentActions.Invoking(() => orcamento.AlterarStatus(StatusOrcamento.EmElaboracao))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Orcamento_fora_de_elaboracao_nao_pode_ser_editado()
    {
        var orcamento = NovoOrcamento();
        var opcao = orcamento.AdicionarOpcao(TipoOpcaoOrcamento.MenorPreco);
        opcao.AdicionarItem(Item(1, 10m));
        orcamento.SelecionarOpcao(opcao.Id);
        orcamento.AlterarStatus(StatusOrcamento.AguardandoAprovacao);

        FluentActions.Invoking(() => orcamento.AdicionarOpcao(TipoOpcaoOrcamento.Manual))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void Duplicar_gera_copia_em_elaboracao_com_mesmos_itens()
    {
        var original = NovoOrcamento();
        var opcao = original.AdicionarOpcao(TipoOpcaoOrcamento.MenorPreco);
        opcao.AdicionarItem(Item(2, 50m));
        original.SelecionarOpcao(opcao.Id);
        original.AlterarStatus(StatusOrcamento.AguardandoAprovacao);

        var copia = original.Duplicar("ORC-2026-000002", Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today.AddDays(30)));

        copia.Status.Should().Be(StatusOrcamento.EmElaboracao);
        copia.OrcamentoOrigemId.Should().Be(original.Id);
        copia.OpcaoSelecionada!.ValorTotal.Should().Be(100m);
        copia.OpcaoSelecionada!.Id.Should().NotBe(opcao.Id);
    }

    [Fact]
    public void Opcao_com_preco_simulado_deve_ser_sinalizada()
    {
        var orcamento = NovoOrcamento();
        var opcao = orcamento.AdicionarOpcao(TipoOpcaoOrcamento.MenorPreco);
        opcao.AdicionarItem(Item(1, 10m, origem: OrigemPreco.Simulado));

        opcao.PossuiPrecoNaoCotado.Should().BeTrue();
    }

    // ------------------------------------------------------------ Fase 7: itens "sem preço"

    [Fact]
    public void Item_sem_preco_nao_soma_no_total_e_e_contado()
    {
        var orcamento = NovoOrcamento();
        var opcao = orcamento.AdicionarOpcao(TipoOpcaoOrcamento.Unica);
        opcao.AdicionarItem(Item(2, 10m));
        opcao.AdicionarItem(ItemOrcamento.SemPreco("Pijama Luna", null, 1, "un", observacao: "Sem preço de venda."));

        opcao.ValorTotal.Should().Be(20m);
        opcao.ItensSemPreco.Should().Be(1);
    }

    [Fact]
    public void Nao_envia_para_aprovacao_com_item_sem_preco_ate_informar_o_preco()
    {
        var orcamento = NovoOrcamento();
        var opcao = orcamento.AdicionarOpcao(TipoOpcaoOrcamento.Unica);
        var pendente = ItemOrcamento.SemPreco("Pijama Luna", null, 2, "un");
        opcao.AdicionarItem(pendente);
        orcamento.SelecionarOpcao(opcao.Id);

        FluentActions.Invoking(() => orcamento.AlterarStatus(StatusOrcamento.AguardandoAprovacao))
            .Should().Throw<DomainException>().WithMessage("*sem preço*");

        orcamento.InformarPrecoItem(opcao.Id, pendente.Id, 49.90m);

        pendente.PrecoPendente.Should().BeFalse();
        pendente.OrigemPreco.Should().Be(OrigemPreco.Manual);
        opcao.ValorTotal.Should().Be(99.80m);
        orcamento.AlterarStatus(StatusOrcamento.AguardandoAprovacao);
        orcamento.Status.Should().Be(StatusOrcamento.AguardandoAprovacao);
    }

    [Fact]
    public void Duplicar_mantem_modo_itens_sem_preco_e_ordem()
    {
        var original = new Orcamento("ORC-2026-000001", Guid.NewGuid(), null,
            DateOnly.FromDateTime(DateTime.Today.AddDays(7)), modo: ModoOrcamento.Unico);
        var opcao = original.AdicionarOpcao(TipoOpcaoOrcamento.Unica);
        var item = ItemOrcamento.SemPreco("Pijama Luna", null, 1, "un");
        item.VincularLinhaDaLista(Guid.NewGuid(), 3);
        opcao.AdicionarItem(item);

        var copia = original.Duplicar("ORC-2026-000002", Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today.AddDays(7)));

        copia.Modo.Should().Be(ModoOrcamento.Unico);
        var itemCopiado = copia.Opcoes.Single().Itens.Single();
        itemCopiado.PrecoPendente.Should().BeTrue();
        itemCopiado.Ordem.Should().Be(3);
    }

    [Fact]
    public void Observacao_longa_e_cortada_no_limite()
    {
        var item = ItemOrcamento.SemPreco("Pijama", null, 1, "un", observacao: new string('x', 600));

        item.Observacao!.Length.Should().Be(ItemOrcamento.TamanhoMaximoObservacao);
    }
}
