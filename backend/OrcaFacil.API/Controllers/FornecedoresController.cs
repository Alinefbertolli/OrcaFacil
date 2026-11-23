using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrcaFacil.API.Configuracao;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Application.Comum;

namespace OrcaFacil.API.Controllers;

/// <summary>Fornecedores da empresa.</summary>
[ApiController]
[Route("api/v1/fornecedores")]
[Produces("application/json")]
public class FornecedoresController(ServicoFornecedores servico) : ControllerBase
{
    /// <summary>Lista os fornecedores ativos (busca por razão social, nome fantasia ou CNPJ).</summary>
    [HttpGet]
    [ProducesResponseType<PaginaResultado<FornecedorResposta>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResultado<FornecedorResposta>>> Listar(
        [FromQuery] string? busca, [FromQuery] bool incluirInativos = false,
        [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20, CancellationToken ct = default) =>
        Ok(await servico.ListarAsync(new ParametrosPaginacao(pagina, tamanhoPagina), busca, incluirInativos, ct));

    /// <summary>Consulta um fornecedor.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<FornecedorResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FornecedorResposta>> ObterPorId(Guid id, CancellationToken ct) =>
        Ok(await servico.ObterPorIdAsync(id, ct));

    /// <summary>Cadastra um fornecedor.</summary>
    [HttpPost]
    [ProducesResponseType<FornecedorResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FornecedorResposta>> Criar(FornecedorRequisicao requisicao, CancellationToken ct)
    {
        var criado = await servico.CriarAsync(requisicao, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Altera um fornecedor (inclusive a avaliação de 0 a 5).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<FornecedorResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FornecedorResposta>> Atualizar(Guid id, FornecedorRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.AtualizarAsync(id, requisicao, ct));

    /// <summary>[Gerente] Desativa o fornecedor (exclusão lógica).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        await servico.DesativarAsync(id, ct);
        return NoContent();
    }

    /// <summary>[Gerente] Reativa um fornecedor.</summary>
    [HttpPost("{id:guid}/reativar")]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken ct)
    {
        await servico.ReativarAsync(id, ct);
        return NoContent();
    }

    /// <summary>Produtos que o fornecedor vende, com os preços dele.</summary>
    [HttpGet("{id:guid}/produtos")]
    [ProducesResponseType<IReadOnlyList<ProdutoDoFornecedorResposta>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProdutoDoFornecedorResposta>>> Produtos(Guid id, CancellationToken ct) =>
        Ok(await servico.ListarProdutosAsync(id, ct));
}
