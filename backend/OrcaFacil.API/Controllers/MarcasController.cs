using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrcaFacil.API.Configuracao;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Application.Comum;

namespace OrcaFacil.API.Controllers;

/// <summary>Marcas, com o nível de reconhecimento (1 a 5) usado nos orçamentos.</summary>
[ApiController]
[Route("api/v1/marcas")]
[Produces("application/json")]
public class MarcasController(ServicoMarcas servico) : ControllerBase
{
    /// <summary>Lista as marcas (com busca por nome e paginação).</summary>
    [HttpGet]
    [ProducesResponseType<PaginaResultado<MarcaResposta>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResultado<MarcaResposta>>> Listar(
        [FromQuery] string? busca, [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default) =>
        Ok(await servico.ListarAsync(new ParametrosPaginacao(pagina, tamanhoPagina), busca, ct));

    /// <summary>Consulta uma marca.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<MarcaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarcaResposta>> ObterPorId(Guid id, CancellationToken ct) =>
        Ok(await servico.ObterPorIdAsync(id, ct));

    /// <summary>Cria uma marca.</summary>
    [HttpPost]
    [ProducesResponseType<MarcaResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MarcaResposta>> Criar(MarcaRequisicao requisicao, CancellationToken ct)
    {
        var criada = await servico.CriarAsync(requisicao, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    /// <summary>Altera uma marca.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<MarcaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MarcaResposta>> Atualizar(Guid id, MarcaRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.AtualizarAsync(id, requisicao, ct));

    /// <summary>[Gerente] Exclui uma marca que não esteja em uso.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        await servico.ExcluirAsync(id, ct);
        return NoContent();
    }
}
