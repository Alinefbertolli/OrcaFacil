using AwesomeAssertions;
using OrcaFacil.Application.Orcamentos;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Domain.ObjetosDeValor;
using OrcaFacil.Tests.Infrastructure;

namespace OrcaFacil.Tests.Application;

public class TextoWhatsAppTests
{
    private static DocumentoOrcamento Documento(int quantidadeDeOpcoes) => new(
        "ORC-2026-000001", new DateOnly(2026, 11, 25), new DateOnly(2026, 12, 2),
        new LojaDocumento("Papelaria Exemplo", "11.222.333/0001-81", "(11) 3333-4444", null, null),
        new ClienteDocumento("Maria", "(11) 98765-4321"),
        Enumerable.Range(1, quantidadeDeOpcoes).Select(n => new OpcaoDocumento(n == 1 ? "Econômica" : "Marcas reconhecidas",
            24m * n, [new ItemDocumento("Caderno 10 matérias", n == 1 ? null : "Tilibra", 2, "UN", 12m * n, 24m * n)])).ToList(),
        OpcaoJaEscolhida: quantidadeDeOpcoes == 1);

    [Fact]
    public void Com_varias_opcoes_cada_uma_tem_titulo_e_total()
    {
        var texto = TextoWhatsApp.Montar(Documento(2));

        texto.Should().Contain("*Orçamento ORC-2026-000001*")
            .And.Contain("Válido até 02/12/2026")
            .And.Contain("*Opção Econômica*")
            .And.Contain("• 2 UN Caderno 10 matérias — R$ 24,00")
            .And.Contain("• 2 UN Caderno 10 matérias (Tilibra) — R$ 48,00")
            .And.Contain("*Total: R$ 48,00*")
            .And.Contain("responda com o nome da opção")
            .And.Contain("Contato: (11) 3333-4444");
    }

    [Fact]
    public void Nao_repete_a_marca_quando_ela_ja_esta_no_nome_e_usa_quebra_de_linha_simples()
    {
        var doc = Documento(1) with
        {
            Opcoes = [new OpcaoDocumento("Orçamento", 60m, [new ItemDocumento("Caderno Tilibra", "Tilibra", 2, "UN", 30m, 60m)])]
        };

        var texto = TextoWhatsApp.Montar(doc);

        texto.Should().Contain("• 2 UN Caderno Tilibra — R$ 60,00").And.NotContain("(Tilibra)").And.NotContain("\r");
    }

    [Fact]
    public void Com_uma_opcao_nao_mostra_titulo_de_opcao()
    {
        var texto = TextoWhatsApp.Montar(Documento(1));

        texto.Should().NotContain("*Opção").And.Contain("Para confirmar o pedido");
    }

    [Theory]
    [InlineData("(11) 98765-4321", "5511987654321")]
    [InlineData("11 3333-4444", "551133334444")]
    [InlineData("+55 11 98765-4321", "5511987654321")]
    [InlineData("98765-4321", "")]   // sem DDD: não dá para saber o número completo
    [InlineData(null, "")]
    public void Telefone_vira_numero_internacional(string? telefone, string esperado) =>
        TextoWhatsApp.NumeroInternacional(telefone).Should().Be(esperado);

    [Fact]
    public void Link_abre_a_conversa_com_o_texto_codificado()
    {
        var link = TextoWhatsApp.Link("Olá! Total: R$ 10,00", "(11) 98765-4321");

        link.Should().Be("https://wa.me/5511987654321?text=Ol%C3%A1%21%20Total%3A%20R%24%2010%2C00");
    }

    [Fact]
    public void Valores_no_formato_brasileiro()
    {
        FormatoBr.Dinheiro(1234.5m).Should().Be("R$ 1.234,50");
        FormatoBr.Quantidade(2.5m).Should().Be("2,5");
        FormatoBr.Cnpj("11222333000181").Should().Be("11.222.333/0001-81");
        FormatoBr.Endereco(new Endereco("Rua A", "10", null, "Centro", "Campinas", "SP", "13010000"))
            .Should().Be("Rua A, 10 · Centro - Campinas - SP · CEP 13010-000");
    }
}

public class ServicoDocumentosOrcamentoTests
{
    private sealed class FakeOrcamentosCompletos(FakeRepositorio<Orcamento> orcamentos) : IRepositorioOrcamentos
    {
        public Task<Orcamento?> ObterCompletoAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(orcamentos.Itens.FirstOrDefault(o => o.Id == id));
    }

    private sealed class FakeGeradorPdf : IGeradorPdfOrcamento
    {
        public DocumentoOrcamento? Recebido { get; private set; }
        public byte[] Gerar(DocumentoOrcamento documento)
        {
            Recebido = documento;
            return [0x25, 0x50, 0x44, 0x46]; // "%PDF"
        }
    }

    private readonly FakeRepositorio<Orcamento> _orcamentos = new();
    private readonly FakeRepositorio<Empresa> _empresas = new();
    private readonly FakeRepositorio<Cliente> _clientes = new();
    private readonly FakeGeradorPdf _pdf = new();
    private readonly Empresa _empresa = new("Papelaria Exemplo Ltda", "11.222.333/0001-81", SegmentoEmpresa.Papelaria, "Papelaria Exemplo");
    private readonly ServicoDocumentosOrcamento _servico;

    public ServicoDocumentosOrcamentoTests()
    {
        _empresa.AtualizarContato("(11) 3333-4444", "Contato@Papelaria.com", null);
        _empresas.Adicionar(_empresa);
        _servico = new ServicoDocumentosOrcamento(new FakeOrcamentosCompletos(_orcamentos), _empresas, _clientes,
            new EmpresaAtualFixa(_empresa.Id), _pdf);
    }

    private Orcamento OrcamentoComparativo(Guid? clienteId = null, bool comItemSemPreco = false)
    {
        var orcamento = new Orcamento("ORC-2026-000001", Guid.NewGuid(), clienteId, new DateOnly(2026, 12, 2));
        foreach (var (tipo, preco) in new[] { (TipoOpcaoOrcamento.MarcasReconhecidas, 30m), (TipoOpcaoOrcamento.MenorPreco, 12m) })
        {
            var opcao = orcamento.AdicionarOpcao(tipo);
            opcao.AdicionarItem(new ItemOrcamento("Caderno 10 matérias", null, 2, "UN", preco, OrigemPreco.PrecoVenda));
            if (comItemSemPreco && tipo == TipoOpcaoOrcamento.MenorPreco)
                opcao.AdicionarItem(ItemOrcamento.SemPreco("borracha", null, 3, "UN"));
        }
        _orcamentos.Adicionar(orcamento);
        return orcamento;
    }

    [Fact]
    public async Task Antes_da_escolha_vao_todas_as_opcoes_na_ordem_da_tela()
    {
        var orcamento = OrcamentoComparativo();

        var (conteudo, nome) = await _servico.GerarPdfAsync(orcamento.Id);

        conteudo.Should().NotBeEmpty();
        nome.Should().Be("ORC-2026-000001.pdf");
        _pdf.Recebido!.Opcoes.Select(o => o.Nome).Should().Equal("Econômica", "Marcas reconhecidas");
        _pdf.Recebido!.OpcaoJaEscolhida.Should().BeFalse();
        _pdf.Recebido!.Loja.Should().Be(new LojaDocumento("Papelaria Exemplo", "11.222.333/0001-81", "(11) 3333-4444",
            "contato@papelaria.com", null));
    }

    [Fact]
    public async Task Depois_da_escolha_vai_so_a_opcao_escolhida()
    {
        var orcamento = OrcamentoComparativo();
        orcamento.SelecionarOpcao(orcamento.Opcoes.Single(o => o.Tipo == TipoOpcaoOrcamento.MarcasReconhecidas).Id);

        var documento = await _servico.MontarAsync(orcamento.Id, default);

        documento.Opcoes.Should().ContainSingle().Which.ValorTotal.Should().Be(60m);
        documento.OpcaoJaEscolhida.Should().BeTrue();
    }

    [Fact]
    public async Task Item_sem_preco_bloqueia_o_documento()
    {
        var orcamento = OrcamentoComparativo(comItemSemPreco: true);

        await FluentActions.Awaiting(() => _servico.GerarWhatsAppAsync(orcamento.Id))
            .Should().ThrowAsync<DomainException>().WithMessage("*sem preço*\"Econômica\"*");
    }

    [Fact]
    public async Task Item_sem_preco_em_opcao_nao_escolhida_nao_bloqueia()
    {
        var orcamento = OrcamentoComparativo(comItemSemPreco: true);
        orcamento.SelecionarOpcao(orcamento.Opcoes.Single(o => o.Tipo == TipoOpcaoOrcamento.MarcasReconhecidas).Id);

        var mensagem = await _servico.GerarWhatsAppAsync(orcamento.Id);

        mensagem.Texto.Should().Contain("R$ 60,00").And.NotContain("borracha");
    }

    [Fact]
    public async Task WhatsApp_usa_o_telefone_do_cliente_no_link()
    {
        var cliente = new Cliente { Nome = "Maria", TipoPessoa = TipoPessoa.Fisica, Telefone = "(11) 98765-4321" };
        _clientes.Adicionar(cliente);
        var orcamento = OrcamentoComparativo(cliente.Id);

        var mensagem = await _servico.GerarWhatsAppAsync(orcamento.Id);

        mensagem.Link.Should().StartWith("https://wa.me/5511987654321?text=");
        mensagem.Texto.Should().Contain("Cliente: Maria");
        mensagem.TelefoneCliente.Should().Be("(11) 98765-4321");
    }
}
