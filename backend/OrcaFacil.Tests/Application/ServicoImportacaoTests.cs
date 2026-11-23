using System.Text;
using AwesomeAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Application;

public class ServicoImportacaoTests
{
    // ------------------------------------------------------------ fakes específicos da importação

    private sealed class FakeListasComItens(FakeRepositorio<ListaImportada> listas) : IRepositorioListasImportadas
    {
        public Task<ListaImportada?> ObterComItensAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(listas.Itens.FirstOrDefault(l => l.Id == id));
    }

    private sealed class FakeExtrator : IExtratorTexto
    {
        public string Texto { get; set; } = "";
        public bool Falhar { get; set; }

        public Task<string> ExtrairAsync(Stream conteudo, FormatoImportacao formato, CancellationToken ct = default) =>
            Falhar ? throw new DomainException("Nenhum texto foi encontrado no arquivo.") : Task.FromResult(Texto);
    }

    private sealed class FakeArmazenamento : IArmazenamentoArquivos
    {
        public int ArquivosSalvos { get; private set; }

        public Task<string> SalvarAsync(Stream conteudo, string extensao, CancellationToken ct = default)
        {
            ArquivosSalvos++;
            return Task.FromResult($"empresa/2026/11/arquivo{extensao}");
        }
    }

    /// <summary>"Similar" = o nome do produto contém a primeira palavra da descrição.</summary>
    private sealed class FakeBusca(FakeRepositorio<Produto> produtos) : IBuscaProdutosSimilares
    {
        public Task<IReadOnlyList<ProdutoSimilar>> BuscarAsync(string descricao, int limite, CancellationToken ct = default)
        {
            var palavra = descricao.Split(' ')[0];
            IReadOnlyList<ProdutoSimilar> achados = produtos.Itens
                .Where(p => p.Nome.Contains(palavra, StringComparison.OrdinalIgnoreCase))
                .Select(p => new ProdutoSimilar(p.Id, p.Codigo, p.Nome, p.UnidadeMedida, 0.8))
                .Take(limite).ToList();
            return Task.FromResult(achados);
        }
    }

    // ------------------------------------------------------------ cenário

    private readonly FakeRepositorio<ListaImportada> _listas = new();
    private readonly FakeRepositorio<Produto> _produtos = new();
    private readonly FakeRepositorio<Marca> _marcas = new();
    private readonly FakeExtrator _extrator = new();
    private readonly FakeArmazenamento _armazenamento = new();
    private readonly FakeUnitOfWork _unidadeDeTrabalho = new();
    private readonly Produto _caneta = new() { Codigo = "CAN-01", Nome = "Caneta esferográfica azul", UnidadeMedida = "CX" };
    private readonly ServicoImportacao _servico;

    public ServicoImportacaoTests()
    {
        _produtos.Adicionar(_caneta);
        _marcas.Adicionar(new Marca { Nome = "BIC", NivelReconhecimento = 5 });

        _servico = new ServicoImportacao(_listas, new FakeListasComItens(_listas), _marcas, _produtos,
            new FakeRepositorio<Cliente>(), _extrator, _armazenamento, new FakeBusca(_produtos),
            new FakeUsuarioAtual(Guid.NewGuid()), _unidadeDeTrabalho,
            new ImportarTextoValidador(), new ItemListaValidador(), NullLogger<ServicoImportacao>.Instance);
    }

    private const string ListaWhatsApp = """
        [10:32, 18/11/2026] Cliente: Bom dia! Segue a lista:
        [10:32, 18/11/2026] Cliente: - 20 cx caneta azul BIC
        [10:33, 18/11/2026] Cliente: - 3 rolos de fita durex
        """;

    private Task<ListaImportadaResposta> ImportarWhatsApp() =>
        _servico.ImportarTextoAsync(new ImportarTextoRequisicao(ListaWhatsApp, FormatoImportacao.WhatsApp));

    // ------------------------------------------------------------ testes

    [Fact]
    public async Task Texto_do_whatsapp_vira_itens_aguardando_revisao()
    {
        var lista = await ImportarWhatsApp();

        lista.Status.Should().Be(StatusImportacao.AguardandoRevisao);
        lista.Itens.Select(i => i.Descricao).Should().Equal("caneta azul", "fita durex");
        lista.Itens[0].Marca.Should().Be("BIC", "marcas cadastradas são reconhecidas");
        lista.Itens[1].UnidadeMedida.Should().Be("RL");
    }

    [Fact]
    public async Task Produto_parecido_do_catalogo_e_sugerido_automaticamente()
    {
        var lista = await ImportarWhatsApp();

        var caneta = lista.Itens[0];
        caneta.ProdutoId.Should().Be(_caneta.Id);
        caneta.ProdutoNome.Should().Be("Caneta esferográfica azul");
        caneta.PrecisaAtencao.Should().BeFalse();
    }

    [Fact]
    public async Task Item_sem_produto_no_catalogo_precisa_de_atencao()
    {
        var lista = await ImportarWhatsApp();

        lista.Itens[1].ProdutoId.Should().BeNull();
        lista.Itens[1].PrecisaAtencao.Should().BeTrue();
        lista.ItensQuePrecisamAtencao.Should().Be(1);
    }

    [Fact]
    public async Task Revisao_permite_corrigir_excluir_e_adicionar_itens()
    {
        var lista = await ImportarWhatsApp();
        var fita = lista.Itens[1];

        await _servico.AtualizarItemAsync(lista.Id, fita.Id, new ItemListaRequisicao("fita adesiva 12mm", 5, "rl", null, null));
        await _servico.ExcluirItemAsync(lista.Id, lista.Itens[0].Id);
        var revisada = await _servico.AdicionarItemAsync(lista.Id,
            new ItemListaRequisicao("caneta vermelha", 2, "cx", "BIC", _caneta.Id));

        revisada.Itens.Select(i => i.Descricao).Should().Equal("fita adesiva 12mm", "caneta vermelha");
        revisada.Itens[0].Quantidade.Should().Be(5);
        revisada.Itens[0].UnidadeMedida.Should().Be("RL");
        revisada.Itens[1].PrecisaAtencao.Should().BeFalse("o usuário escolheu o produto");
    }

    [Fact]
    public async Task Depois_de_confirmada_a_lista_nao_aceita_mais_alteracoes()
    {
        var lista = await ImportarWhatsApp();

        var confirmada = await _servico.ConfirmarRevisaoAsync(lista.Id);

        confirmada.Status.Should().Be(StatusImportacao.Revisada);
        await FluentActions.Awaiting(() => _servico.ExcluirItemAsync(lista.Id, lista.Itens[0].Id))
            .Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task Produto_inexistente_na_revisao_e_erro_de_validacao()
    {
        var lista = await ImportarWhatsApp();

        await FluentActions.Awaiting(() => _servico.AdicionarItemAsync(lista.Id,
                new ItemListaRequisicao("x", 1, "un", null, Guid.NewGuid())))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Arquivo_valido_e_guardado_e_interpretado()
    {
        _extrator.Texto = "10 resmas papel A4";
        using var conteudo = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 conteúdo"));

        var lista = await _servico.ImportarArquivoAsync(new ArquivoEnviado("pedido.pdf", conteudo.Length, conteudo), null);

        lista.Formato.Should().Be(FormatoImportacao.Pdf);
        lista.NomeArquivoOriginal.Should().Be("pedido.pdf");
        lista.Itens.Should().ContainSingle(i => i.Descricao == "papel A4" && i.Quantidade == 10);
        _armazenamento.ArquivosSalvos.Should().Be(1);
    }

    [Fact]
    public async Task Arquivo_de_tipo_proibido_e_recusado_antes_de_qualquer_gravacao()
    {
        using var conteudo = new MemoryStream([0x4D, 0x5A, 0x90, 0x00]);

        await FluentActions.Awaiting(() =>
                _servico.ImportarArquivoAsync(new ArquivoEnviado("virus.exe", conteudo.Length, conteudo), null))
            .Should().ThrowAsync<ValidationException>();

        _listas.Itens.Should().BeEmpty();
        _armazenamento.ArquivosSalvos.Should().Be(0);
    }

    [Fact]
    public async Task Arquivo_ilegivel_fica_registrado_com_erro()
    {
        _extrator.Falhar = true;
        using var conteudo = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7 só imagem"));

        await FluentActions.Awaiting(() =>
                _servico.ImportarArquivoAsync(new ArquivoEnviado("escaneado.pdf", conteudo.Length, conteudo), null))
            .Should().ThrowAsync<DomainException>();

        var registrada = _listas.Itens.Should().ContainSingle().Subject;
        registrada.Status.Should().Be(StatusImportacao.Erro);
        registrada.MensagemErro.Should().Contain("Nenhum texto");
    }
}
