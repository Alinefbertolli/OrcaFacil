using System.Text;
using AwesomeAssertions;
using FluentValidation;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Application;

public class ServicoImportacaoCatalogoTests
{
    /// <summary>Devolve as "células" que o teste montou, sem ler arquivo de verdade.</summary>
    private sealed class PlanilhaFake : IPlanilhasCatalogo
    {
        public IReadOnlyList<IReadOnlyList<string>> Celulas { get; set; } = [];
        public IReadOnlyList<IReadOnlyList<string>> Ler(Stream conteudo, FormatoPlanilha formato) => Celulas;
        public byte[] GerarModelo() => [];
    }

    private static readonly string[] Cabecalho = ["Código", "Nome", "Preço", "Estoque", "Categoria", "Marca"];

    private readonly PlanilhaFake _planilha = new();
    private readonly FakeRepositorio<Produto> _produtos = new();
    private readonly FakeRepositorio<Categoria> _categorias = new();
    private readonly FakeRepositorio<Marca> _marcas = new();
    private readonly FakeRepositorio<HistoricoPreco> _historicos = new();
    private readonly FakeEstoques _estoques = new();
    private readonly FakeUnitOfWork _unidadeDeTrabalho = new();
    private readonly Categoria _cadernos = new() { Nome = "Cadernos" };
    private readonly Produto _caderno;
    private readonly ServicoImportacaoCatalogo _servico;

    public ServicoImportacaoCatalogoTests()
    {
        _categorias.Adicionar(_cadernos);
        _caderno = new Produto { Codigo = "CAD10-1", Nome = "Caderno 10 matérias", UnidadeMedida = "UN", CategoriaId = _cadernos.Id };
        _caderno.DefinirPrecoVenda(12m);
        _produtos.Adicionar(_caderno);

        _servico = new ServicoImportacaoCatalogo(_planilha, _produtos, _categorias, _marcas, _historicos, _estoques,
            new FakeUsuarioAtual(Guid.NewGuid()), _unidadeDeTrabalho,
            new RelogioFixo(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero)));
    }

    private Task<ResultadoImportacaoCatalogo> Importar(bool simular, params string[][] linhas)
    {
        _planilha.Celulas = [Cabecalho, .. linhas];
        var arquivo = new ArquivoEnviado("produtos.csv", 10, new MemoryStream(Encoding.UTF8.GetBytes("Código;Nome")));
        return _servico.ImportarAsync(arquivo, simular);
    }

    [Fact]
    public async Task Codigo_novo_cria_o_produto_com_categoria_marca_preco_e_estoque()
    {
        var resultado = await Importar(false, ["LAP-HB", "Lápis HB", "1,50", "200", "Escrita", "Faber-Castell"]);

        resultado.Criados.Should().Be(1);
        resultado.CategoriasCriadas.Should().Equal("Escrita");
        resultado.MarcasCriadas.Should().Equal("Faber-Castell");
        resultado.EstoquesAjustados.Should().Be(1);

        var lapis = _produtos.Itens.Single(p => p.Codigo == "LAP-HB");
        lapis.PrecoVenda.Should().Be(1.50m);
        lapis.UnidadeMedida.Should().Be("UN");
        lapis.CategoriaId.Should().Be(_categorias.Itens.Single(c => c.Nome == "Escrita").Id);
        _marcas.Itens.Single().NivelReconhecimento.Should().Be(3);
        _historicos.Itens.Should().ContainSingle(h => h.ProdutoId == lapis.Id && h.Origem == OrigemPreco.PrecoVenda);
        _estoques.Itens.Single(e => e.ProdutoId == lapis.Id).QuantidadeDisponivel.Should().Be(200);
        _unidadeDeTrabalho.VezesSalvo.Should().Be(1);
    }

    [Fact]
    public async Task Codigo_existente_atualiza_so_o_que_mudou_e_guarda_o_historico_do_preco()
    {
        var resultado = await Importar(false, ["cad10-1", "", "12,50", "", "", ""]);

        var linha = resultado.Linhas.Single();
        linha.Situacao.Should().Be(SituacaoLinhaImportacao.Atualizado);
        linha.Detalhes.Should().Equal("preço R$ 12,00 → R$ 12,50");
        _caderno.PrecoVenda.Should().Be(12.50m);
        _caderno.Nome.Should().Be("Caderno 10 matérias"); // célula vazia: não mexe
        _historicos.Itens.Should().ContainSingle().Which.Preco.Should().Be(12.50m);
        _estoques.Itens.Should().BeEmpty(); // sem coluna de estoque preenchida, não passa a controlar
    }

    [Fact]
    public async Task Linha_igual_ao_cadastro_fica_sem_alteracao_e_nada_e_gravado()
    {
        var resultado = await Importar(false, ["CAD10-1", "Caderno 10 matérias", "12", "", "Cadernos", ""]);

        resultado.SemAlteracao.Should().Be(1);
        _unidadeDeTrabalho.VezesSalvo.Should().Be(0);
    }

    [Fact]
    public async Task Simulacao_mostra_o_resultado_mas_nao_grava()
    {
        var resultado = await Importar(true, ["CAD10-1", "", "15", "", "", ""], ["NOVO-1", "Borracha", "2", "", "", ""]);

        resultado.Simulacao.Should().BeTrue();
        resultado.Atualizados.Should().Be(1);
        resultado.Criados.Should().Be(1);
        _unidadeDeTrabalho.VezesSalvo.Should().Be(0);
    }

    [Fact]
    public async Task Estoque_da_planilha_vira_o_saldo_com_ajuste_de_inventario()
    {
        _estoques.ComSaldo(_caderno.Id, 8);

        var resultado = await Importar(false, ["CAD10-1", "", "", "10", "", ""]);

        resultado.Linhas.Single().Detalhes.Should().Equal("estoque 8 → 10");
        var estoque = _estoques.Itens.Single();
        estoque.QuantidadeDisponivel.Should().Be(10);
        estoque.Movimentacoes.Last().Tipo.Should().Be(TipoMovimentacaoEstoque.Ajuste);
        estoque.Movimentacoes.Last().Motivo.Should().Be("Importação de planilha (produtos.csv)");
    }

    [Fact]
    public async Task Produto_desativado_que_esta_na_planilha_volta_a_ficar_ativo()
    {
        _caderno.Ativo = false;

        var resultado = await Importar(false, ["CAD10-1", "", "", "", "", ""]);

        _caderno.Ativo.Should().BeTrue();
        resultado.Linhas.Single().Detalhes.Should().Contain("produto reativado");
    }

    [Fact]
    public async Task Produto_novo_sem_nome_e_erro_e_as_outras_linhas_seguem()
    {
        var resultado = await Importar(false, ["NOVO-1", "", "2", "", "", ""], ["NOVO-2", "Borracha", "2", "", "", ""]);

        resultado.ComErro.Should().Be(1);
        resultado.Criados.Should().Be(1);
        resultado.Linhas[0].Detalhes.Should().Equal("Produto novo precisa de nome.");
        _produtos.Itens.Should().Contain(p => p.Codigo == "NOVO-2").And.NotContain(p => p.Codigo == "NOVO-1");
    }

    [Fact]
    public async Task Categoria_nova_usada_em_varias_linhas_e_criada_uma_vez_so_e_sem_categoria_vai_para_a_padrao()
    {
        var resultado = await Importar(false,
            ["N1", "Régua 30 cm", "3", "", "Geometria", ""],
            ["N2", "Esquadro", "4", "", "geometria", ""],
            ["N3", "Clips", "1", "", "", ""]);

        resultado.CategoriasCriadas.Should().Equal("Geometria", ServicoImportacaoCatalogo.NomeCategoriaPadrao);
        _categorias.Itens.Should().HaveCount(3); // Cadernos + Geometria + Sem categoria
    }

    [Fact]
    public async Task Produtos_que_nao_estao_na_planilha_ficam_como_estao()
    {
        await Importar(false, ["NOVO-1", "Borracha", "2", "", "", ""]);

        _caderno.Ativo.Should().BeTrue();
        _caderno.PrecoVenda.Should().Be(12m);
    }

    [Theory]
    [InlineData("produtos.pdf")]
    [InlineData("produtos.xls")]
    public async Task Arquivo_que_nao_e_xlsx_ou_csv_e_recusado(string nome)
    {
        var arquivo = new ArquivoEnviado(nome, 3, new MemoryStream([1, 2, 3]));

        await FluentActions.Awaiting(() => _servico.ImportarAsync(arquivo, simular: true))
            .Should().ThrowAsync<ValidationException>();
    }
}
