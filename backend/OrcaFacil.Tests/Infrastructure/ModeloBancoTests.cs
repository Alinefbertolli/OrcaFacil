using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Tests.Infrastructure;

/// <summary>
/// Valida o mapeamento EF Core sem precisar de um PostgreSQL rodando:
/// GenerateCreateScript monta o modelo (e falha se houver configuração inválida)
/// e gera o SQL de criação, que é inspecionado aqui.
/// </summary>
public class ModeloBancoTests
{
    private static readonly Lazy<string> Script = new(() =>
    {
        using var contexto = ContextoDeTeste.Criar();
        return contexto.Database.GenerateCreateScript();
    });

    [Fact]
    public void Modelo_deve_ser_valido_e_gerar_script() =>
        Script.Value.Should().Contain("CREATE TABLE");

    [Theory]
    [InlineData("\"Empresas\"")]
    [InlineData("\"Usuarios\"")]
    [InlineData("\"Orcamentos\"")]
    [InlineData("\"OpcoesOrcamento\"")]
    [InlineData("\"ItensOrcamento\"")]
    [InlineData("\"ListasImportadas\"")]
    [InlineData("\"Estoques\"")]
    [InlineData("\"MovimentacoesEstoque\"")]
    [InlineData("\"ProdutosFornecedores\"")]
    [InlineData("\"LogsSistema\"")]
    [InlineData("\"ContadoresNumeracao\"")]
    public void Deve_criar_tabela(string tabela) =>
        Script.Value.Should().Contain($"CREATE TABLE {tabela}");

    [Fact]
    public void Enums_devem_ser_gravados_como_texto() =>
        Script.Value.Should().Contain("\"Status\" character varying(40)");

    [Fact]
    public void Deve_habilitar_busca_por_similaridade() =>
        Script.Value.Should().Contain("pg_trgm").And.Contain("gin_trgm_ops");

    [Fact]
    public void Deve_garantir_uma_unica_opcao_selecionada_por_orcamento() =>
        Script.Value.Should().Contain("IX_OpcoesOrcamento_UmaSelecionada");

    [Fact]
    public void Precos_unitarios_devem_ter_quatro_casas() =>
        Script.Value.Should().Contain("\"PrecoUnitario\" numeric(18,4)");

    [Fact]
    public void Produto_tem_preco_de_venda_nao_negativo_e_grupo_de_equivalencia()
    {
        Script.Value.Should().Contain("\"PrecoVenda\" numeric(18,4)");
        Script.Value.Should().Contain("CK_Produto_PrecoVenda");
        Script.Value.Should().Contain("\"GrupoEquivalencia\" character varying(100)");
        Script.Value.Should().NotContain("PrecoReferencia");
    }

    [Fact]
    public void Empresa_tem_contato_para_o_pdf()
    {
        var empresas = Script.Value[Script.Value.IndexOf("CREATE TABLE \"Empresas\"")..];
        empresas = empresas[..empresas.IndexOf(");")];
        empresas.Should().Contain("\"Telefone\" character varying(20)")
            .And.Contain("\"Email\" character varying(200)")
            .And.Contain("\"EnderecoCidade\" character varying(100)");
    }

    [Fact]
    public void Empresa_e_orcamento_guardam_o_modo_como_texto() =>
        Script.Value.Should().Contain("\"ModoOrcamentoPadrao\" character varying(40)")
            .And.Contain("\"Modo\" character varying(40)");

    [Theory]
    [InlineData("IX_Produtos_EmpresaId_Codigo")]
    [InlineData("IX_Categorias_EmpresaId_Nome")]
    [InlineData("IX_Marcas_EmpresaId_Nome")]
    [InlineData("IX_Fornecedores_EmpresaId_Cnpj")]
    [InlineData("IX_Clientes_EmpresaId_Documento")]
    [InlineData("IX_Orcamentos_EmpresaId_Numero")]
    public void Unicidades_devem_valer_dentro_de_cada_empresa(string indice) =>
        Script.Value.Should().Contain($"CREATE UNIQUE INDEX \"{indice}\"");

    [Fact]
    public void Nao_deve_haver_dados_de_um_segmento_especifico() =>
        Script.Value.Should().NotContain("INSERT INTO \"Categorias\"");

    [Fact]
    public void Ids_sao_gerados_pelo_dominio_e_nao_pelo_banco()
    {
        using var contexto = ContextoDeTeste.Criar();

        var id = contexto.Model.FindEntityType(typeof(ItemOrcamento))!.FindPrimaryKey()!.Properties.Single();
        id.ValueGenerated.Should().Be(ValueGenerated.Never);
    }
}
