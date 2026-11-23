using Microsoft.AspNetCore.Mvc;
using OrcaFacil.Application.Comum;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.API.Controllers;

/// <summary>Importação inteligente de listas de produtos e revisão dos itens.</summary>
/// <remarks>
/// Fluxo: 1) importar (texto ou arquivo) → 2) revisar os itens (corrigir, excluir, adicionar,
/// escolher o produto do catálogo) → 3) confirmar a revisão. Só depois a lista pode virar orçamento.
/// </remarks>
[ApiController]
[Route("api/v1/importacoes")]
[Produces("application/json")]
public class ImportacoesController(ServicoImportacao servico) : ControllerBase
{
    private const long LimiteRequisicaoBytes = 12 * 1024 * 1024; // 10 MB do arquivo + folga do formulário

    /// <summary>Importa uma lista digitada ou colada do WhatsApp.</summary>
    /// <response code="201">Lista interpretada e aguardando revisão.</response>
    [HttpPost("texto")]
    [ProducesResponseType<ListaImportadaResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ListaImportadaResposta>> ImportarTexto(ImportarTextoRequisicao requisicao, CancellationToken ct)
    {
        var lista = await servico.ImportarTextoAsync(requisicao, ct);
        return CreatedAtAction(nameof(Obter), new { id = lista.Id }, lista);
    }

    /// <summary>Importa um arquivo: PDF, Excel (.xlsx), Word (.docx), imagem (PNG/JPG) ou texto (.txt). Máximo 10 MB.</summary>
    /// <param name="arquivo">O arquivo da lista.</param>
    /// <param name="clienteId">(Opcional) cliente para quem é a lista.</param>
    /// <param name="ct">Cancelamento da requisição.</param>
    /// <response code="201">Arquivo lido e lista aguardando revisão.</response>
    /// <response code="400">Arquivo vazio, grande demais ou de tipo não suportado.</response>
    /// <response code="422">Não foi possível ler o arquivo (corrompido, sem texto ou OCR não configurado).</response>
    [HttpPost("arquivo")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteRequisicaoBytes)]
    [ProducesResponseType<ListaImportadaResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ListaImportadaResposta>> ImportarArquivo(IFormFile arquivo, [FromForm] Guid? clienteId,
        CancellationToken ct)
    {
        await using var conteudo = arquivo.OpenReadStream();
        var lista = await servico.ImportarArquivoAsync(new ArquivoEnviado(arquivo.FileName, arquivo.Length, conteudo), clienteId, ct);
        return CreatedAtAction(nameof(Obter), new { id = lista.Id }, lista);
    }

    /// <summary>Lista as importações da empresa (mais recentes primeiro), com filtro opcional por status.</summary>
    [HttpGet]
    [ProducesResponseType<PaginaResultado<ListaImportadaResumo>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResultado<ListaImportadaResumo>>> Listar([FromQuery] StatusImportacao? status,
        [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20, CancellationToken ct = default) =>
        Ok(await servico.ListarAsync(new ParametrosPaginacao(pagina, tamanhoPagina), status, ct));

    /// <summary>Consulta uma lista importada com todos os itens.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ListaImportadaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ListaImportadaResposta>> Obter(Guid id, CancellationToken ct) =>
        Ok(await servico.ObterAsync(id, ct));

    /// <summary>Revisão: corrige um item (descrição, quantidade, unidade, marca e produto do catálogo).</summary>
    /// <remarks>Envie produtoId = null para desfazer a associação com o catálogo.</remarks>
    [HttpPut("{id:guid}/itens/{itemId:guid}")]
    [ProducesResponseType<ListaImportadaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ListaImportadaResposta>> AtualizarItem(Guid id, Guid itemId, ItemListaRequisicao requisicao,
        CancellationToken ct) =>
        Ok(await servico.AtualizarItemAsync(id, itemId, requisicao, ct));

    /// <summary>Revisão: adiciona um item que não foi reconhecido.</summary>
    [HttpPost("{id:guid}/itens")]
    [ProducesResponseType<ListaImportadaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ListaImportadaResposta>> AdicionarItem(Guid id, ItemListaRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.AdicionarItemAsync(id, requisicao, ct));

    /// <summary>Revisão: exclui um item que não deve ir para o orçamento.</summary>
    [HttpDelete("{id:guid}/itens/{itemId:guid}")]
    [ProducesResponseType<ListaImportadaResposta>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ListaImportadaResposta>> ExcluirItem(Guid id, Guid itemId, CancellationToken ct) =>
        Ok(await servico.ExcluirItemAsync(id, itemId, ct));

    /// <summary>Revisão: sugere até 5 produtos do catálogo parecidos com o item.</summary>
    [HttpGet("{id:guid}/itens/{itemId:guid}/sugestoes")]
    [ProducesResponseType<IReadOnlyList<ProdutoSimilar>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProdutoSimilar>>> Sugestoes(Guid id, Guid itemId, CancellationToken ct) =>
        Ok(await servico.SugerirProdutosAsync(id, itemId, ct));

    /// <summary>Conclui a revisão. A partir daí a lista pode gerar orçamentos e não aceita mais alterações.</summary>
    /// <response code="422">A lista não está em revisão ou não tem itens.</response>
    [HttpPost("{id:guid}/confirmar-revisao")]
    [ProducesResponseType<ListaImportadaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ListaImportadaResposta>> ConfirmarRevisao(Guid id, CancellationToken ct) =>
        Ok(await servico.ConfirmarRevisaoAsync(id, ct));
}
