using AwesomeAssertions;
using FluentValidation;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Tests.Application;

public class ServicoCategoriasTests
{
    private readonly FakeRepositorio<Categoria> _categorias = new();
    private readonly FakeRepositorio<Produto> _produtos = new();
    private ServicoCategorias Servico() => new(_categorias, _produtos, new FakeUnitOfWork(), new CategoriaValidador());

    [Fact]
    public async Task Nome_repetido_gera_conflito_mesmo_com_maiusculas_diferentes()
    {
        await Servico().CriarAsync(new CategoriaRequisicao("Cadernos", null, null));

        await FluentActions.Awaiting(() => Servico().CriarAsync(new CategoriaRequisicao("  CADERNOS ", null, null)))
            .Should().ThrowAsync<ConflitoException>();
    }

    [Fact]
    public async Task Subcategoria_mostra_o_nome_da_categoria_pai()
    {
        var escrita = await Servico().CriarAsync(new CategoriaRequisicao("Escrita", null, null));

        var canetas = await Servico().CriarAsync(new CategoriaRequisicao("Canetas", null, escrita.Id));

        canetas.CategoriaPaiNome.Should().Be("Escrita");
    }

    [Fact]
    public async Task Nao_permite_ciclo_entre_categorias()
    {
        var a = await Servico().CriarAsync(new CategoriaRequisicao("A", null, null));
        var b = await Servico().CriarAsync(new CategoriaRequisicao("B", null, a.Id)); // B dentro de A

        // Agora tentar colocar A dentro de B fecharia um ciclo.
        await FluentActions.Awaiting(() => Servico().AtualizarAsync(a.Id, new CategoriaRequisicao("A", null, b.Id)))
            .Should().ThrowAsync<DomainException>().WithMessage("*ciclo*");
    }

    [Fact]
    public async Task Categoria_pai_inexistente_e_erro_de_validacao() =>
        await FluentActions.Awaiting(() => Servico().CriarAsync(new CategoriaRequisicao("X", null, Guid.NewGuid())))
            .Should().ThrowAsync<ValidationException>();

    [Fact]
    public async Task Categoria_com_produtos_nao_pode_ser_excluida()
    {
        var categoria = await Servico().CriarAsync(new CategoriaRequisicao("Cadernos", null, null));
        _produtos.Adicionar(new Produto { Codigo = "001", Nome = "Caderno", UnidadeMedida = "UN", CategoriaId = categoria.Id });

        await FluentActions.Awaiting(() => Servico().ExcluirAsync(categoria.Id))
            .Should().ThrowAsync<ConflitoException>().WithMessage("*possui produtos*");
    }
}

public class ServicoMarcasTests
{
    private readonly FakeRepositorio<Marca> _marcas = new();
    private readonly FakeRepositorio<Produto> _produtos = new();
    private ServicoMarcas Servico() => new(_marcas, _produtos, new FakeUnitOfWork(), new MarcaValidador());

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Nivel_de_reconhecimento_fora_de_1_a_5_e_recusado(int nivel) =>
        await FluentActions.Awaiting(() => Servico().CriarAsync(new MarcaRequisicao("Faber-Castell", nivel)))
            .Should().ThrowAsync<ValidationException>();

    [Fact]
    public async Task Marca_em_uso_nao_pode_ser_excluida()
    {
        var marca = await Servico().CriarAsync(new MarcaRequisicao("Tilibra", 4));
        _produtos.Adicionar(new Produto { Codigo = "1", Nome = "Caderno", UnidadeMedida = "UN", MarcaId = marca.Id });

        await FluentActions.Awaiting(() => Servico().ExcluirAsync(marca.Id)).Should().ThrowAsync<ConflitoException>();
    }
}

public class ServicoProdutosTests
{
    private readonly FakeRepositorio<Produto> _produtos = new();
    private readonly FakeRepositorio<Categoria> _categorias = new();
    private readonly FakeRepositorio<Marca> _marcas = new();
    private readonly FakeRepositorio<Fornecedor> _fornecedores = new();
    private readonly FakeRepositorio<ProdutoFornecedor> _vinculos = new();
    private readonly FakeRepositorio<HistoricoPreco> _historicos = new();
    private readonly RelogioFixo _relogio = new(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly Categoria _cadernos = new() { Nome = "Cadernos" };
    private readonly Fornecedor _distribuidora = new() { RazaoSocial = "Distribuidora Ltda", Cnpj = "11222333000181" };

    public ServicoProdutosTests()
    {
        _categorias.Adicionar(_cadernos);
        _fornecedores.Adicionar(_distribuidora);
    }

    private ServicoProdutos Servico() => new(_produtos, _categorias, _marcas, _fornecedores, _vinculos, _historicos,
        new FakeUnitOfWork(), _relogio, new ProdutoValidador(), new VinculoFornecedorValidador());

    private ProdutoRequisicao Requisicao(string codigo = "CAD-001", decimal? preco = null) =>
        new(codigo, "Caderno universitário 10 matérias", null, " un ", _cadernos.Id, null, preco);

    [Fact]
    public async Task Cadastro_padroniza_a_unidade_e_mostra_a_categoria()
    {
        var produto = await Servico().CriarAsync(Requisicao());

        produto.UnidadeMedida.Should().Be("UN");
        produto.CategoriaNome.Should().Be("Cadernos");
    }

    [Fact]
    public async Task Codigo_repetido_gera_conflito() =>
        await FluentActions.Awaiting(async () =>
            {
                await Servico().CriarAsync(Requisicao("cad-001"));
                await Servico().CriarAsync(Requisicao("CAD-001"));
            })
            .Should().ThrowAsync<ConflitoException>();

    [Fact]
    public async Task Categoria_inexistente_e_erro_de_validacao() =>
        await FluentActions.Awaiting(() => Servico().CriarAsync(Requisicao() with { CategoriaId = Guid.NewGuid() }))
            .Should().ThrowAsync<ValidationException>();

    [Fact]
    public async Task Preco_de_venda_vai_para_o_historico_com_origem_preco_de_venda()
    {
        var produto = await Servico().CriarAsync(Requisicao(preco: 24.90m));

        produto.PrecoVenda.Should().Be(24.90m);
        _historicos.Itens.Should().ContainSingle(h => h.Preco == 24.90m && h.Origem == OrigemPreco.PrecoVenda);
    }

    [Fact]
    public async Task Grupo_de_equivalencia_e_guardado_normalizado()
    {
        var produto = await Servico().CriarAsync(Requisicao() with { GrupoEquivalencia = "  Caderno  10 Matérias " });

        produto.GrupoEquivalencia.Should().Be("caderno 10 materias");
    }

    [Fact]
    public async Task Vincular_fornecedor_registra_preco_e_nao_duplica_historico_se_o_preco_nao_mudar()
    {
        var produto = await Servico().CriarAsync(Requisicao());

        await Servico().VincularFornecedorAsync(produto.Id, _distribuidora.Id, new VinculoFornecedorRequisicao(19.50m, "X1", 5));
        var detalhe = await Servico().VincularFornecedorAsync(produto.Id, _distribuidora.Id,
            new VinculoFornecedorRequisicao(19.50m, "X1", 3));

        detalhe.Fornecedores.Should().ContainSingle().Which.PrazoEntregaDias.Should().Be(3);
        _historicos.Itens.Should().ContainSingle(h => h.FornecedorId == _distribuidora.Id && h.Preco == 19.50m);
    }

    [Fact]
    public async Task Produto_desativado_some_da_listagem_padrao()
    {
        var produto = await Servico().CriarAsync(Requisicao());
        await Servico().DesativarAsync(produto.Id);

        var padrao = await Servico().ListarAsync(new FiltroProdutos(null, null, null), new ParametrosPaginacao());
        var comInativos = await Servico().ListarAsync(new FiltroProdutos(null, null, null, IncluirInativos: true),
            new ParametrosPaginacao());

        padrao.Itens.Should().BeEmpty();
        comInativos.Itens.Should().ContainSingle();
    }

    [Fact]
    public async Task Busca_encontra_pelo_codigo()
    {
        await Servico().CriarAsync(Requisicao("CAD-001"));

        var resultado = await Servico().ListarAsync(new FiltroProdutos("cad-0", null, null), new ParametrosPaginacao());

        resultado.Itens.Should().ContainSingle();
    }
}

public class ServicoFornecedoresTests
{
    private readonly FakeRepositorio<Fornecedor> _fornecedores = new();
    private ServicoFornecedores Servico() => new(_fornecedores, new FakeRepositorio<ProdutoFornecedor>(),
        new FakeRepositorio<Produto>(), new FakeUnitOfWork(), new FornecedorValidador());

    private static FornecedorRequisicao Requisicao(string cnpj = "11.222.333/0001-81", decimal? avaliacao = 4,
        Endereco? endereco = null) =>
        new("Distribuidora Ltda", "Distribuidora", cnpj, "Carlos", "(11) 99999-0000", "Vendas@Distribuidora.com",
            endereco, avaliacao);

    [Fact]
    public async Task Cnpj_e_guardado_so_com_numeros_e_email_em_minusculas()
    {
        var fornecedor = await Servico().CriarAsync(Requisicao());

        fornecedor.Cnpj.Should().Be("11222333000181");
        fornecedor.Email.Should().Be("vendas@distribuidora.com");
    }

    [Fact]
    public async Task Cnpj_repetido_gera_conflito_mesmo_com_outra_formatacao()
    {
        await Servico().CriarAsync(Requisicao("11.222.333/0001-81"));

        await FluentActions.Awaiting(() => Servico().CriarAsync(Requisicao("11222333000181")))
            .Should().ThrowAsync<ConflitoException>();
    }

    [Theory]
    [InlineData("11.222.333/0001-80")]
    [InlineData("123")]
    public async Task Cnpj_invalido_e_recusado(string cnpj) =>
        await FluentActions.Awaiting(() => Servico().CriarAsync(Requisicao(cnpj)))
            .Should().ThrowAsync<ValidationException>();

    [Fact]
    public async Task Avaliacao_acima_de_5_e_recusada() =>
        await FluentActions.Awaiting(() => Servico().CriarAsync(Requisicao(avaliacao: 7)))
            .Should().ThrowAsync<ValidationException>();

    [Fact]
    public async Task Endereco_e_padronizado()
    {
        var endereco = new Endereco(" Rua A ", "10", null, "Centro", "São Paulo", "sp", "01001-000");

        var fornecedor = await Servico().CriarAsync(Requisicao(endereco: endereco));

        var gravado = fornecedor.Endereco!;
        gravado.Uf.Should().Be("SP");
        gravado.Cep.Should().Be("01001000");
        gravado.Logradouro.Should().Be("Rua A");
    }

    [Fact]
    public async Task Endereco_todo_em_branco_vira_sem_endereco()
    {
        var fornecedor = await Servico().CriarAsync(Requisicao(endereco: new Endereco(" ", null, null, "", null, null, null)));

        fornecedor.Endereco.Should().BeNull();
    }
}

public class ServicoClientesTests
{
    private readonly FakeRepositorio<Cliente> _clientes = new();
    private ServicoClientes Servico() => new(_clientes, new FakeUnitOfWork(), new ClienteValidador());

    private static ClienteRequisicao PessoaFisica(string? cpf) =>
        new("Maria Souza", TipoPessoa.Fisica, cpf, null, null, null, null);

    [Fact]
    public async Task Cliente_sem_documento_e_permitido()
    {
        var cliente = await Servico().CriarAsync(PessoaFisica(null));

        cliente.Documento.Should().BeNull();
    }

    [Fact]
    public async Task Cpf_e_guardado_so_com_numeros()
    {
        var cliente = await Servico().CriarAsync(PessoaFisica("529.982.247-25"));

        cliente.Documento.Should().Be("52998224725");
    }

    [Fact]
    public async Task Cpf_invalido_e_recusado() =>
        await FluentActions.Awaiting(() => Servico().CriarAsync(PessoaFisica("529.982.247-24")))
            .Should().ThrowAsync<ValidationException>();

    [Fact]
    public async Task Cnpj_informado_como_pessoa_fisica_e_recusado() =>
        await FluentActions.Awaiting(() => Servico().CriarAsync(PessoaFisica("11.222.333/0001-81")))
            .Should().ThrowAsync<ValidationException>().WithMessage("*CPF inválido*");

    [Fact]
    public async Task Documento_repetido_gera_conflito()
    {
        await Servico().CriarAsync(PessoaFisica("529.982.247-25"));

        await FluentActions.Awaiting(() => Servico().CriarAsync(PessoaFisica("52998224725")))
            .Should().ThrowAsync<ConflitoException>();
    }
}
