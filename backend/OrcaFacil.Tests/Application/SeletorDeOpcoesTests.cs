using AwesomeAssertions;
using OrcaFacil.Application.Orcamentos;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Application;

/// <summary>As regras de escolha de cada opção (funções puras: não precisam de fakes).</summary>
public class SeletorDeOpcoesTests
{
    private static ProdutoCandidato Produto(string codigo, decimal? preco, string? marca, int? nivel) =>
        new(Guid.NewGuid(), codigo, $"Caderno 10 matérias {marca}".Trim(), "UN", preco, true, marca, nivel);

    // Catálogo de exemplo de uma papelaria: 4 cadernos equivalentes.
    private static readonly ProdutoCandidato SemMarca = Produto("CAD-1", 12m, null, null);
    private static readonly ProdutoCandidato Foroni = Produto("CAD-2", 20m, "Foroni", 3);
    private static readonly ProdutoCandidato Tilibra = Produto("CAD-3", 30m, "Tilibra", 5);
    private static readonly ProdutoCandidato SemPreco = Produto("CAD-4", null, "Jandaia", 4);
    private static readonly ProdutoCandidato[] Catalogo = [SemMarca, Foroni, Tilibra, SemPreco];

    [Fact]
    public void Economica_escolhe_o_menor_preco() =>
        SeletorDeOpcoes.Escolher(TipoOpcaoOrcamento.MenorPreco, Catalogo).Should().Be(SemMarca);

    [Fact]
    public void Custo_beneficio_escolhe_o_mais_barato_com_marca_de_nivel_3_ou_mais() =>
        SeletorDeOpcoes.Escolher(TipoOpcaoOrcamento.CustoBeneficio, Catalogo).Should().Be(Foroni);

    [Fact]
    public void Marcas_reconhecidas_escolhe_a_marca_de_maior_nivel_com_preco() =>
        SeletorDeOpcoes.Escolher(TipoOpcaoOrcamento.MarcasReconhecidas, Catalogo)
            .Should().Be(Tilibra, "a Jandaia (nível 4) não tem preço de venda e por isso não pode ser escolhida");

    [Fact]
    public void Custo_beneficio_sem_marcas_boas_fica_com_o_mais_barato() =>
        SeletorDeOpcoes.Escolher(TipoOpcaoOrcamento.CustoBeneficio, [SemMarca, Produto("CAD-9", 15m, "Genérica", 2)])
            .Should().Be(SemMarca);

    [Fact]
    public void Sem_nenhum_preco_nao_escolhe_nada_nunca_inventa_preco() =>
        SeletorDeOpcoes.Escolher(TipoOpcaoOrcamento.MenorPreco, [SemPreco]).Should().BeNull();

    [Fact]
    public void Empate_de_preco_prefere_a_marca_melhor()
    {
        var bic = Produto("CAN-2", 2m, "BIC", 5);
        var generica = Produto("CAN-1", 2m, null, null);

        SeletorDeOpcoes.Escolher(TipoOpcaoOrcamento.MenorPreco, [generica, bic]).Should().Be(bic);
    }

    [Fact]
    public void Marca_pedida_pelo_cliente_e_respeitada_mesmo_com_acento_e_maiusculas()
    {
        var filtrados = SeletorDeOpcoes.FiltrarPorMarca(Catalogo, "TILIBRÁ", out var encontrada);

        encontrada.Should().BeTrue();
        filtrados.Should().ContainSingle().Which.Should().Be(Tilibra);
    }

    [Fact]
    public void Marca_pedida_inexistente_mantem_todos_e_avisa()
    {
        var filtrados = SeletorDeOpcoes.FiltrarPorMarca(Catalogo, "Cadersil", out var encontrada);

        encontrada.Should().BeFalse();
        filtrados.Should().HaveCount(Catalogo.Length);
    }
}
