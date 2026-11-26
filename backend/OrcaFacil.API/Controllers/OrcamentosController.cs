using Microsoft.AspNetCore.Mvc;
using OrcaFacil.Application.Comum;
using OrcaFacil.Application.Orcamentos;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.API.Controllers;

/// <summary>Motor de orçamentação: gera o orçamento a partir de uma lista revisada, compara as opções e controla o status.</summary>
/// <remarks>
/// Fluxo: 1) gerar (modo Unico ou Comparativo) → 2) preencher os itens "sem preço", se houver →
/// 3) comparar e selecionar a opção do cliente → 4) enviar para aprovação → 5) aprovado/reprovado → finalizado.
/// O preço usado é o PREÇO DE VENDA cadastrado no produto. Nenhum preço é inventado.
/// </remarks>
[ApiController]
[Route("api/v1/orcamentos")]
[Produces("application/json")]
public class OrcamentosController(ServicoOrcamentos servico, ServicoDocumentosOrcamento documentos) : ControllerBase
{
    /// <summary>Baixa o orçamento em PDF para enviar ao cliente.</summary>
    /// <remarks>
    /// Antes da escolha do cliente, o PDF traz todas as opções; depois de selecionada, só a escolhida.
    /// Não gera se houver item "sem preço" nas opções que vão para o cliente (422).
    /// </remarks>
    [HttpGet("{id:guid}/pdf")]
    [Produces("application/pdf", "application/problem+json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct)
    {
        var (conteudo, nomeArquivo) = await documentos.GerarPdfAsync(id, ct);
        return File(conteudo, "application/pdf", nomeArquivo);
    }

    /// <summary>Texto pronto para o WhatsApp e um link que abre a conversa já com a mensagem.</summary>
    /// <remarks>Mesmas regras do PDF. Com o telefone do cliente cadastrado, o link abre a conversa com ele.</remarks>
    [HttpGet("{id:guid}/whatsapp")]
    [ProducesResponseType<MensagemWhatsAppResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MensagemWhatsAppResposta>> WhatsApp(Guid id, CancellationToken ct) =>
        Ok(await documentos.GerarWhatsAppAsync(id, ct));

    /// <summary>Gera um orçamento a partir de uma lista importada e REVISADA.</summary>
    /// <remarks>
    /// Exemplo (comparativo com as 3 opções): { "listaImportadaId": "...", "modo": "Comparativo" }.
    /// Exemplo (itens exatos): { "listaImportadaId": "...", "modo": "Unico" }.
    /// Sem "modo", vale o padrão da empresa (GET /api/v1/empresa/configuracoes).
    /// </remarks>
    /// <response code="201">Orçamento gerado. Veja "avisos" (opções iguais, itens sem preço...).</response>
    /// <response code="422">A lista ainda não teve a revisão confirmada.</response>
    [HttpPost("gerar")]
    [ProducesResponseType<OrcamentoResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrcamentoResposta>> Gerar(GerarOrcamentoRequisicao requisicao, CancellationToken ct)
    {
        var orcamento = await servico.GerarAsync(requisicao, ct);
        return CreatedAtAction(nameof(Obter), new { id = orcamento.Id }, orcamento);
    }

    /// <summary>Lista os orçamentos da empresa (mais recentes primeiro), com filtros opcionais.</summary>
    [HttpGet]
    [ProducesResponseType<PaginaResultado<OrcamentoResumo>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResultado<OrcamentoResumo>>> Listar([FromQuery] StatusOrcamento? status,
        [FromQuery] Guid? clienteId, [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default) =>
        Ok(await servico.ListarAsync(new ParametrosPaginacao(pagina, tamanhoPagina), status, clienteId, ct));

    /// <summary>Consulta um orçamento com todas as opções e itens.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrcamentoResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrcamentoResposta>> Obter(Guid id, CancellationToken ct) =>
        Ok(await servico.ObterAsync(id, ct));

    /// <summary>Comparação lado a lado: totais, diferença para a opção mais barata (R$ e %) e item a item.</summary>
    [HttpGet("{id:guid}/comparacao")]
    [ProducesResponseType<ComparacaoResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComparacaoResposta>> Comparar(Guid id, CancellationToken ct) =>
        Ok(await servico.CompararAsync(id, ct));

    /// <summary>Informa à mão o preço unitário de um item (ex.: um item "sem preço").</summary>
    /// <response code="422">O orçamento não está mais em elaboração.</response>
    [HttpPut("{id:guid}/opcoes/{opcaoId:guid}/itens/{itemId:guid}/preco")]
    [ProducesResponseType<OrcamentoResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrcamentoResposta>> InformarPreco(Guid id, Guid opcaoId, Guid itemId,
        InformarPrecoRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.InformarPrecoAsync(id, opcaoId, itemId, requisicao, ct));

    /// <summary>Marca a opção escolhida pelo cliente.</summary>
    [HttpPost("{id:guid}/opcoes/{opcaoId:guid}/selecionar")]
    [ProducesResponseType<OrcamentoResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrcamentoResposta>> Selecionar(Guid id, Guid opcaoId, CancellationToken ct) =>
        Ok(await servico.SelecionarOpcaoAsync(id, opcaoId, ct));

    /// <summary>Muda o status: EmElaboracao → AguardandoAprovacao → Aprovado/Reprovado → Finalizado.</summary>
    /// <response code="422">Mudança não permitida (ex.: sem opção selecionada ou com itens sem preço).</response>
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType<OrcamentoResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrcamentoResposta>> AlterarStatus(Guid id, AlterarStatusRequisicao requisicao,
        CancellationToken ct) =>
        Ok(await servico.AlterarStatusAsync(id, requisicao, ct));
}
