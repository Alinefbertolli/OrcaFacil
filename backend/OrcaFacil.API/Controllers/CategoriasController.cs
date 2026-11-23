using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrcaFacil.API.Configuracao;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Application.Comum;

namespace OrcaFacil.API.Controllers;

/// <summary>Categorias de produtos da empresa.</summary>
/// <remarks>
/// Padrão REST usado em todos os cadastros:
/// GET (listar/consultar) · POST (criar) · PUT (alterar) · DELETE (excluir/desativar).
/// </remarks>
[ApiController]
[Route("api/v1/categorias")]
[Produces("application/json")]
public class CategoriasController(ServicoCategorias servico) : ControllerBase
{
    /// <summary>Lista as categorias (com busca por nome e paginação).</summary>
    [HttpGet]
    [ProducesResponseType<PaginaResultado<CategoriaResposta>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResultado<CategoriaResposta>>> Listar(
        [FromQuery] string? busca, [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default) =>
        Ok(await servico.ListarAsync(new ParametrosPaginacao(pagina, tamanhoPagina), busca, ct));

    /// <summary>Consulta uma categoria.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CategoriaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoriaResposta>> ObterPorId(Guid id, CancellationToken ct) =>
        Ok(await servico.ObterPorIdAsync(id, ct));

    /// <summary>Cria uma categoria (opcionalmente dentro de outra: categoriaPaiId).</summary>
    [HttpPost]
    [ProducesResponseType<CategoriaResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoriaResposta>> Criar(CategoriaRequisicao requisicao, CancellationToken ct)
    {
        var criada = await servico.CriarAsync(requisicao, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    /// <summary>Altera uma categoria.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<CategoriaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CategoriaResposta>> Atualizar(Guid id, CategoriaRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.AtualizarAsync(id, requisicao, ct));

    /// <summary>[Gerente] Exclui uma categoria sem produtos nem subcategorias.</summary>
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
