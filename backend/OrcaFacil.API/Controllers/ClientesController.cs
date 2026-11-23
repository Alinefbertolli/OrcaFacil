using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrcaFacil.API.Configuracao;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Application.Comum;

namespace OrcaFacil.API.Controllers;

/// <summary>Clientes da empresa (para quem os orçamentos são feitos).</summary>
[ApiController]
[Route("api/v1/clientes")]
[Produces("application/json")]
public class ClientesController(ServicoClientes servico) : ControllerBase
{
    /// <summary>Lista os clientes ativos (busca por nome, e-mail ou CPF/CNPJ).</summary>
    [HttpGet]
    [ProducesResponseType<PaginaResultado<ClienteResposta>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResultado<ClienteResposta>>> Listar(
        [FromQuery] string? busca, [FromQuery] bool incluirInativos = false,
        [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20, CancellationToken ct = default) =>
        Ok(await servico.ListarAsync(new ParametrosPaginacao(pagina, tamanhoPagina), busca, incluirInativos, ct));

    /// <summary>Consulta um cliente.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClienteResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteResposta>> ObterPorId(Guid id, CancellationToken ct) =>
        Ok(await servico.ObterPorIdAsync(id, ct));

    /// <summary>Cadastra um cliente.</summary>
    [HttpPost]
    [ProducesResponseType<ClienteResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClienteResposta>> Criar(ClienteRequisicao requisicao, CancellationToken ct)
    {
        var criado = await servico.CriarAsync(requisicao, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Altera um cliente.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ClienteResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClienteResposta>> Atualizar(Guid id, ClienteRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.AtualizarAsync(id, requisicao, ct));

    /// <summary>[Gerente] Desativa o cliente (exclusão lógica).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        await servico.DesativarAsync(id, ct);
        return NoContent();
    }

    /// <summary>[Gerente] Reativa um cliente.</summary>
    [HttpPost("{id:guid}/reativar")]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken ct)
    {
        await servico.ReativarAsync(id, ct);
        return NoContent();
    }
}
