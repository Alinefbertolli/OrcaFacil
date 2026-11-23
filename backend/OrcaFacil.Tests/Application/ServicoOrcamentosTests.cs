using AwesomeAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Application.Orcamentos;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Tests.Infrastructure;

namespace OrcaFacil.Tests.Application;

public class ServicoOrcamentosTests
{
    // ------------------------------------------------------------ fakes específicos do motor

    private sealed class FakeListasComItens(FakeRepositorio<ListaImportada> listas) : IRepositorioListasImportadas
    {
        public Task<ListaImportada?> ObterComItensAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(listas.Itens.FirstOrDefault(l => l.Id == id));
    }

    private sealed class FakeOrcamentosCompletos(FakeRepositorio<Orcamento> orcamentos) : IRepositorioOrcamentos
    {
        public Task<Orcamento?> ObterCompletoAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(orcamentos.Itens.FirstOrDefault(o => o.Id == id));
    }

    private sealed class FakeGeradorNumero : IGeradorNumeroOrcamento
    {
        private int _ultimo;
        public Task<string> GerarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult($"ORC-2026-{++_ultimo:000000}");
    }

    /// <summary>Equivalentes = produtos com o mesmo "grupo" (aqui, o texto do Codigo antes do hífen).</summary>
    private sealed class FakeCatalogo(List<ProdutoCandidato> produtos) : ICatalogoEquivalentes
    {
        public Task<IReadOnlyList<ProdutoCandidato>> ObterAsync(IEnumerable<Guid> produtoIds, CancellationToken ct = default)
        {
            var ids = produtoIds.ToHashSet();
            return Task.FromResult<IReadOnlyList<ProdutoCandidato>>(produtos.Where(p => ids.Contains(p.Id)).ToList());
        }

        public Task<IReadOnlyList<ProdutoCandidato>> BuscarEquivalentesAsync(Guid produtoId, CancellationToken ct = default)
        {
            var grupo = produtos.First(p => p.Id == produtoId).Codigo.Split('-')[0];
            return Task.FromResult<IReadOnlyList<ProdutoCandidato>>(
                produtos.Where(p => p.Ativo && p.Codigo.StartsWith(grupo + "-")).ToList());
        }
    }

    // ------------------------------------------------------------ cenário: papelaria

    private static ProdutoCandidato Produto(string codigo, string nome, decimal? preco, string? marca, int? nivel,
        string unidade = "UN") =>
        new(Guid.NewGuid(), codigo, nome, unidade, preco, true, marca, nivel);

    private readonly ProdutoCandidato _cadernoGenerico = Produto("CAD-1", "Caderno 10 matérias", 12m, null, null);
    private readonly ProdutoCandidato _cadernoForoni = Produto("CAD-2", "Caderno 10 matérias Foroni", 20m, "Foroni", 3);
    private readonly ProdutoCandidato _cadernoTilibra = Produto("CAD-3", "Caderno 10 matérias Tilibra", 30m, "Tilibra", 5);
    private readonly ProdutoCandidato _lapis = Produto("LAP-1", "Lápis preto nº 2", 1.50m, "Faber-Castell", 5);
    private readonly ProdutoCandidato _pijamaSemPreco = Produto("PIJ-1", "Pijama Mayara", null, null, null);

    private readonly FakeRepositorio<ListaImportada> _listas = new();
    private readonly FakeRepositorio<Orcamento> _orcamentos = new();
    private readonly FakeRepositorio<Empresa> _empresas = new();
    private readonly FakeUnitOfWork _unidadeDeTrabalho = new();
    private readonly Empresa _empresa = new("Papelaria Exemplo Ltda", "11.222.333/0001-81", SegmentoEmpresa.Papelaria);
    private readonly ServicoOrcamentos _servico;

    public ServicoOrcamentosTests()
    {
        _empresas.Adicionar(_empresa);
        var catalogo = new FakeCatalogo([_cadernoGenerico, _cadernoForoni, _cadernoTilibra, _lapis, _pijamaSemPreco]);

        _servico = new ServicoOrcamentos(_orcamentos, new FakeOrcamentosCompletos(_orcamentos), new FakeListasComItens(_listas),
            new FakeRepositorio<Cliente>(), _empresas, catalogo, new FakeGeradorNumero(), new EmpresaAtualFixa(_empresa.Id),
            new FakeUsuarioAtual(Guid.NewGuid()), _unidadeDeTrabalho,
            new RelogioFixo(new DateTimeOffset(2026, 11, 23, 15, 0, 0, TimeSpan.Zero)),
            new GerarOrcamentoValidador(), new InformarPrecoValidador(), NullLogger<ServicoOrcamentos>.Instance);
    }

    /// <summary>Cria uma lista já revisada. Cada item: (descrição, quantidade, marca pedida, produto associado).</summary>
    private ListaImportada ListaRevisada(params (string Descricao, decimal Quantidade, string? Marca, ProdutoCandidato? Produto)[] itens)
    {
        var lista = new ListaImportada(Guid.NewGuid(), FormatoImportacao.WhatsApp, null);
        var itensLista = itens.Select(i =>
        {
            var item = new ItemListaImportada(i.Descricao, i.Quantidade, "UN", i.Marca, i.Descricao, 0.9m);
            if (i.Produto is not null) item.SugerirProduto(i.Produto.Id, 0.9m);
            return item;
        }).ToList();
        lista.ConcluirExtracao("texto", itensLista);
        lista.ConfirmarRevisao();
        _listas.Adicionar(lista);
        return lista;
    }

    private Task<OrcamentoResposta> Gerar(ListaImportada lista, ModoOrcamento? modo = null,
        IReadOnlyList<TipoOpcaoOrcamento>? opcoes = null) =>
        _servico.GerarAsync(new GerarOrcamentoRequisicao(lista.Id, modo, opcoes));

    // ------------------------------------------------------------ testes

    [Fact]
    public async Task Comparativo_gera_as_tres_opcoes_com_o_preco_de_venda()
    {
        var lista = ListaRevisada(("caderno 10 materias", 2, null, _cadernoForoni), ("lapis", 3, null, _lapis));

        var orcamento = await Gerar(lista, ModoOrcamento.Comparativo);

        orcamento.Opcoes.Select(o => o.Tipo).Should().Equal(
            TipoOpcaoOrcamento.MenorPreco, TipoOpcaoOrcamento.CustoBeneficio, TipoOpcaoOrcamento.MarcasReconhecidas);
        orcamento.Opcoes.Select(o => o.ValorTotal).Should().Equal(28.50m, 44.50m, 64.50m); // 2 cadernos + 3 × 1,50
        orcamento.Opcoes[0].Itens[0].OrigemPreco.Should().Be(OrigemPreco.PrecoVenda);
        orcamento.Numero.Should().Be("ORC-2026-000001");
        orcamento.ValidadeAte.Should().Be(new DateOnly(2026, 11, 30), "a validade padrão é de 7 dias");
        _unidadeDeTrabalho.VezesSalvo.Should().Be(1);
    }

    [Fact]
    public async Task Sem_modo_informado_usa_o_padrao_da_empresa()
    {
        _empresa.DefinirModoOrcamentoPadrao(ModoOrcamento.Unico);
        var lista = ListaRevisada(("caderno", 1, null, _cadernoForoni));

        var orcamento = await Gerar(lista);

        orcamento.Modo.Should().Be(ModoOrcamento.Unico);
    }

    [Fact]
    public async Task Modo_unico_usa_exatamente_o_produto_da_revisao_e_ja_seleciona_a_opcao()
    {
        var lista = ListaRevisada(("caderno", 1, null, _cadernoForoni));

        var orcamento = await Gerar(lista, ModoOrcamento.Unico);

        var opcao = orcamento.Opcoes.Should().ContainSingle().Subject;
        opcao.Tipo.Should().Be(TipoOpcaoOrcamento.Unica);
        opcao.Selecionada.Should().BeTrue("não há o que escolher");
        opcao.Itens[0].ProdutoId.Should().Be(_cadernoForoni.Id, "no modo único não há troca por equivalente");
        opcao.ValorTotal.Should().Be(20m);
    }

    [Fact]
    public async Task Da_para_escolher_quais_opcoes_gerar()
    {
        var lista = ListaRevisada(("caderno", 1, null, _cadernoForoni));

        var orcamento = await Gerar(lista, ModoOrcamento.Comparativo,
            [TipoOpcaoOrcamento.MarcasReconhecidas, TipoOpcaoOrcamento.MenorPreco]);

        orcamento.Opcoes.Select(o => o.Tipo).Should().Equal(TipoOpcaoOrcamento.MenorPreco, TipoOpcaoOrcamento.MarcasReconhecidas);
    }

    [Fact]
    public async Task Opcoes_iguais_aparecem_uma_vez_so_com_aviso()
    {
        // O lápis só tem um produto equivalente: as três opções sairiam iguais.
        var lista = ListaRevisada(("lapis", 10, null, _lapis));

        var orcamento = await Gerar(lista, ModoOrcamento.Comparativo);

        orcamento.Opcoes.Should().ContainSingle().Which.Tipo.Should().Be(TipoOpcaoOrcamento.MenorPreco);
        orcamento.Avisos.Should().HaveCount(2).And.AllSatisfy(a => a.Should().Contain("mostramos só uma"));
    }

    [Fact]
    public async Task Marca_pedida_pelo_cliente_vale_para_todas_as_opcoes()
    {
        var lista = ListaRevisada(("caderno", 1, "Tilibra", _cadernoForoni), ("lapis", 1, null, _lapis));

        var orcamento = await Gerar(lista, ModoOrcamento.Comparativo);

        orcamento.Opcoes.Should().ContainSingle("com a marca fixa, as opções ficam iguais");
        orcamento.Opcoes[0].Itens[0].ProdutoId.Should().Be(_cadernoTilibra.Id);
    }

    [Fact]
    public async Task Item_sem_produto_ou_sem_preco_fica_sem_preco_e_nunca_tem_valor_inventado()
    {
        var lista = ListaRevisada(("pijama mayara", 1, null, _pijamaSemPreco), ("pijama luna", 2, null, null));

        var orcamento = await Gerar(lista, ModoOrcamento.Unico);

        var itens = orcamento.Opcoes[0].Itens;
        itens.Should().AllSatisfy(i =>
        {
            i.SemPreco.Should().BeTrue();
            i.PrecoUnitario.Should().BeNull();
            i.Observacao.Should().NotBeNullOrWhiteSpace();
        });
        itens[0].Observacao.Should().Contain("sem preço de venda");
        itens[1].Observacao.Should().Contain("sem produto do catálogo");
        orcamento.Opcoes[0].ItensSemPreco.Should().Be(2);
        orcamento.Avisos.Should().Contain(a => a.Contains("2 item(ns) sem preço"));
    }

    [Fact]
    public async Task Preco_informado_a_mao_completa_o_orcamento_e_libera_o_envio()
    {
        var lista = ListaRevisada(("pijama luna", 2, null, null));
        var gerado = await Gerar(lista, ModoOrcamento.Unico);
        var opcao = gerado.Opcoes[0];

        await FluentActions.Awaiting(() =>
                _servico.AlterarStatusAsync(gerado.Id, new AlterarStatusRequisicao(StatusOrcamento.AguardandoAprovacao)))
            .Should().ThrowAsync<DomainException>().WithMessage("*sem preço*");

        var completo = await _servico.InformarPrecoAsync(gerado.Id, opcao.Id, opcao.Itens[0].Id, new InformarPrecoRequisicao(89.90m));
        completo.Opcoes[0].ValorTotal.Should().Be(179.80m);
        completo.Opcoes[0].Itens[0].OrigemPreco.Should().Be(OrigemPreco.Manual);

        var enviado = await _servico.AlterarStatusAsync(gerado.Id, new AlterarStatusRequisicao(StatusOrcamento.AguardandoAprovacao));
        enviado.Status.Should().Be(StatusOrcamento.AguardandoAprovacao);
    }

    [Fact]
    public async Task Comparacao_mostra_diferenca_em_reais_e_percentual()
    {
        var lista = ListaRevisada(("caderno", 1, null, _cadernoForoni));
        var gerado = await Gerar(lista, ModoOrcamento.Comparativo);

        var comparacao = await _servico.CompararAsync(gerado.Id);

        comparacao.Opcoes.Select(c => c.DiferencaParaMaisBarata).Should().Equal(0m, 8m, 18m);   // 12 / 20 / 30
        comparacao.Opcoes.Select(c => c.DiferencaPercentual).Should().Equal(0m, 66.7m, 150m);
        comparacao.Opcoes[0].MaisBarata.Should().BeTrue();
        comparacao.Linhas.Should().ContainSingle().Which.Celulas.Should().HaveCount(3);
    }

    [Fact]
    public async Task Trocar_a_opcao_selecionada_desmarca_a_anterior()
    {
        var lista = ListaRevisada(("caderno", 1, null, _cadernoForoni));
        var gerado = await Gerar(lista, ModoOrcamento.Comparativo);

        await _servico.SelecionarOpcaoAsync(gerado.Id, gerado.Opcoes[0].Id);
        var depois = await _servico.SelecionarOpcaoAsync(gerado.Id, gerado.Opcoes[2].Id);

        depois.Opcoes.Count(o => o.Selecionada).Should().Be(1);
        depois.Opcoes[2].Selecionada.Should().BeTrue();
        depois.Economia.Should().Be(0m, "escolheu a opção mais cara");
    }

    [Fact]
    public async Task Lista_ainda_em_revisao_nao_gera_orcamento()
    {
        var lista = new ListaImportada(Guid.NewGuid(), FormatoImportacao.Texto, null);
        lista.ConcluirExtracao("x", [new ItemListaImportada("caderno", 1, "UN", null, "caderno", 1m)]);
        _listas.Adicionar(lista);

        await FluentActions.Awaiting(() => Gerar(lista, ModoOrcamento.Unico))
            .Should().ThrowAsync<DomainException>().WithMessage("*revisada*");
    }

    [Fact]
    public async Task Modo_unico_nao_aceita_escolha_de_opcoes()
    {
        var lista = ListaRevisada(("caderno", 1, null, _cadernoForoni));

        await FluentActions.Awaiting(() => Gerar(lista, ModoOrcamento.Unico, [TipoOpcaoOrcamento.MenorPreco]))
            .Should().ThrowAsync<ValidationException>();
    }
}
