using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Application.Comum;
using OrcaFacil.Application.Estoques;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Application.Orcamentos;
using OrcaFacil.Domain.Enums;
using OrcaFacil.IntegrationTests.Infra;

namespace OrcaFacil.IntegrationTests;

/// <summary>
/// O caminho completo que a loja faz no dia a dia, de ponta a ponta, só pela API:
/// catálogo → lista do cliente → revisão → orçamento comparativo → PDF.
/// </summary>
[Collection(ColecaoApi.Nome)]
public class FluxoOrcamentoTests(ApiFixture api)
{
    [Fact]
    public async Task Da_lista_do_cliente_ao_orcamento_comparativo_em_PDF()
    {
        var loja = await api.NovaEmpresaAsync();

        // 1) Catálogo: o mesmo caderno em 3 marcas (sem marca = nível 1; Foroni = 3; Tilibra = 5).
        var cadernos = await loja.CategoriaAsync("Cadernos");
        var foroni = await loja.MarcaAsync("Foroni", 3);
        var tilibra = await loja.MarcaAsync("Tilibra", 5);
        const string grupo = "caderno 10 materias";
        var generico = await loja.ProdutoAsync("CAD-GEN", "Caderno 10 matérias simples", cadernos, 24m, null, grupo);
        await loja.ProdutoAsync("CAD-FOR", "Caderno 10 matérias Foroni", cadernos, 40m, foroni, grupo);
        await loja.ProdutoAsync("CAD-TIL", "Caderno 10 matérias Tilibra", cadernos, 60m, tilibra, grupo);

        // 2) A lista que o cliente mandou (texto colado do WhatsApp).
        var lista = await Garantir.LerAsync<ListaImportadaResposta>(await loja.Cliente.PostAsJsonAsync(
            "/api/v1/importacoes/texto", new ImportarTextoRequisicao("2 cadernos de 10 matérias"), ApiFixture.Json));
        lista.Status.Should().Be(StatusImportacao.AguardandoRevisao);
        var item = lista.Itens.Should().ContainSingle().Which;

        // 3) Revisão humana: confirma a quantidade e liga o item ao produto do catálogo.
        await Garantir.SucessoAsync(await loja.Cliente.PutAsJsonAsync($"/api/v1/importacoes/{lista.Id}/itens/{item.Id}",
            new ItemListaRequisicao("Caderno 10 matérias", 2, "UN", null, generico.Id), ApiFixture.Json));
        var revisada = await Garantir.LerAsync<ListaImportadaResposta>(
            await loja.Cliente.PostAsync($"/api/v1/importacoes/{lista.Id}/confirmar-revisao", null));
        revisada.Status.Should().Be(StatusImportacao.Revisada);

        // 4) Orçamento comparativo: o motor procura os equivalentes no PostgreSQL de verdade.
        var orcamento = await Garantir.LerAsync<OrcamentoResposta>(await loja.Cliente.PostAsJsonAsync(
            "/api/v1/orcamentos/gerar", new GerarOrcamentoRequisicao(lista.Id, ModoOrcamento.Comparativo), ApiFixture.Json));

        orcamento.Opcoes.Should().HaveCount(3);
        orcamento.Opcoes.Single(o => o.Tipo == TipoOpcaoOrcamento.MenorPreco).ValorTotal.Should().Be(48m);      // 2 × 24
        orcamento.Opcoes.Single(o => o.Tipo == TipoOpcaoOrcamento.CustoBeneficio).ValorTotal.Should().Be(80m); // 2 × 40
        orcamento.Opcoes.Single(o => o.Tipo == TipoOpcaoOrcamento.MarcasReconhecidas).ValorTotal.Should().Be(120m); // 2 × 60
        orcamento.Numero.Should().StartWith("ORC-");

        // 5) O PDF para mandar ao cliente.
        var pdf = await loja.Cliente.GetAsync($"/api/v1/orcamentos/{orcamento.Id}/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await pdf.Content.ReadAsByteArrayAsync()).Take(4).Should().Equal("%PDF"u8.ToArray());
    }

    [Fact]
    public async Task Importar_planilha_do_ERP_cria_produtos_e_estoque_de_verdade_no_banco()
    {
        var loja = await api.NovaEmpresaAsync();
        var csv = "Código;Nome;Preço de venda;Estoque;Categoria;Marca\r\n" +
                  "LAP-HB;Lápis preto HB;1,50;200;Escrita;Faber-Castell\r\n" +
                  "BOR-01;Borracha branca;2,00;80;Escrita;Faber-Castell\r\n";

        async Task<ResultadoImportacaoCatalogo> Enviar(bool simular)
        {
            using var formulario = new MultipartFormDataContent
            {
                { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "arquivo", "produtos.csv" }
            };
            return await Garantir.LerAsync<ResultadoImportacaoCatalogo>(
                await loja.Cliente.PostAsync($"/api/v1/produtos/importacao?simular={simular}", formulario));
        }

        // Prévia: diz o que vai acontecer, mas não grava.
        (await Enviar(simular: true)).Criados.Should().Be(2);
        (await Garantir.LerAsync<PaginaResultado<ProdutoResumo>>(await loja.Cliente.GetAsync("/api/v1/produtos")))
            .Itens.Should().BeEmpty();

        // Importação de verdade: tudo numa transação só.
        var resultado = await Enviar(simular: false);
        resultado.Criados.Should().Be(2);
        resultado.CategoriasCriadas.Should().Equal("Escrita");

        var produtos = await Garantir.LerAsync<PaginaResultado<ProdutoResumo>>(await loja.Cliente.GetAsync("/api/v1/produtos"));
        produtos.Itens.Select(p => (p.Codigo, p.PrecoVenda, p.MarcaNome))
            .Should().BeEquivalentTo(new[] { ("BOR-01", (decimal?)2.00m, "Faber-Castell"), ("LAP-HB", (decimal?)1.50m, "Faber-Castell") });

        var estoque = await Garantir.LerAsync<PaginaResultado<EstoqueResumo>>(await loja.Cliente.GetAsync("/api/v1/estoque"));
        estoque.Itens.Single(e => e.ProdutoCodigo == "LAP-HB").QuantidadeDisponivel.Should().Be(200);

        // Importar de novo a mesma planilha não muda nada (idempotência).
        var deNovo = await Enviar(simular: false);
        deNovo.SemAlteracao.Should().Be(2);
    }

    [Fact]
    public async Task Planilha_modelo_e_um_arquivo_Excel()
    {
        var loja = await api.NovaEmpresaAsync();

        var resposta = await loja.Cliente.GetAsync("/api/v1/produtos/importacao/modelo");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        resposta.Content.Headers.ContentDisposition!.FileName.Should().Contain("modelo-importacao-produtos.xlsx");
        (await resposta.Content.ReadAsByteArrayAsync()).Take(2).Should().Equal("PK"u8.ToArray()); // .xlsx é um zip
    }
}
