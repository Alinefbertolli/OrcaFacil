using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrcaFacil.API.Configuracao;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Application.Comum;

namespace OrcaFacil.API.Controllers;

/// <summary>Produtos, fornecedores de cada produto e histórico de preços.</summary>
[ApiController]
[Route("api/v1/produtos")]
[Produces("application/json")]
public class ProdutosController(ServicoProdutos servico) : ControllerBase
{
    /// <summary>Lista os produtos ativos (busca por nome ou código, filtros por categoria e marca).</summary>
    /// <param name="busca">Parte do nome ou do código.</param>
    /// <param name="categoriaId">Só produtos desta categoria.</param>
    /// <param name="marcaId">Só produtos desta marca.</param>
    /// <param name="incluirInativos">true para mostrar também os desativados.</param>
    /// <param name="pagina">Número da página (começa em 1).</param>
    /// <param name="tamanhoPagina">Itens por página (máximo 100).</param>
    /// <param name="ct">Cancelamento da requisição.</param>
    [HttpGet]
    [ProducesResponseType<PaginaResultado<ProdutoResumo>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResultado<ProdutoResumo>>> Listar(
        [FromQuery] string? busca, [FromQuery] Guid? categoriaId, [FromQuery] Guid? marcaId,
        [FromQuery] bool incluirInativos = false, [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default) =>
        Ok(await servico.ListarAsync(new FiltroProdutos(busca, categoriaId, marcaId, incluirInativos),
            new ParametrosPaginacao(pagina, tamanhoPagina), ct));

    /// <summary>Consulta um produto, com a lista de fornecedores e seus preços.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProdutoDetalhe>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoDetalhe>> ObterPorId(Guid id, CancellationToken ct) =>
        Ok(await servico.ObterPorIdAsync(id, ct));

    /// <summary>Cadastra um produto.</summary>
    [HttpPost]
    [ProducesResponseType<ProdutoDetalhe>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProdutoDetalhe>> Criar(ProdutoRequisicao requisicao, CancellationToken ct)
    {
        var criado = await servico.CriarAsync(requisicao, ct);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Altera um produto.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ProdutoDetalhe>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProdutoDetalhe>> Atualizar(Guid id, ProdutoRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.AtualizarAsync(id, requisicao, ct));

    /// <summary>[Gerente] Desativa o produto (exclusão lógica: o histórico é preservado).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Desativar(Guid id, CancellationToken ct)
    {
        await servico.DesativarAsync(id, ct);
        return NoContent();
    }

    /// <summary>[Gerente] Reativa um produto desativado.</summary>
    [HttpPost("{id:guid}/reativar")]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reativar(Guid id, CancellationToken ct)
    {
        await servico.ReativarAsync(id, ct);
        return NoContent();
    }

    /// <summary>Liga um fornecedor ao produto (ou atualiza) com preço, código e prazo dele.</summary>
    /// <remarks>Se o preço mudar, a alteração fica registrada no histórico de preços.</remarks>
    [HttpPut("{id:guid}/fornecedores/{fornecedorId:guid}")]
    [ProducesResponseType<ProdutoDetalhe>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoDetalhe>> VincularFornecedor(Guid id, Guid fornecedorId,
        VinculoFornecedorRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.VincularFornecedorAsync(id, fornecedorId, requisicao, ct));

    /// <summary>[Gerente] Desfaz a ligação entre produto e fornecedor (o histórico de preços é mantido).</summary>
    [HttpDelete("{id:guid}/fornecedores/{fornecedorId:guid}")]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoverFornecedor(Guid id, Guid fornecedorId, CancellationToken ct)
    {
        await servico.RemoverFornecedorAsync(id, fornecedorId, ct);
        return NoContent();
    }

    /// <summary>Histórico de preços do produto (mais recentes primeiro).</summary>
    [HttpGet("{id:guid}/historico-precos")]
    [ProducesResponseType<PaginaResultado<HistoricoPrecoResposta>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginaResultado<HistoricoPrecoResposta>>> HistoricoPrecos(Guid id,
        [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20, CancellationToken ct = default) =>
        Ok(await servico.ListarHistoricoPrecosAsync(id, new ParametrosPaginacao(pagina, tamanhoPagina), ct));
}
