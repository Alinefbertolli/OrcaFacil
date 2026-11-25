using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrcaFacil.API.Configuracao;
using OrcaFacil.Application.Comum;
using OrcaFacil.Application.Estoques;

namespace OrcaFacil.API.Controllers;

/// <summary>Estoque dos produtos: saldo, entradas, saídas, inventário e histórico.</summary>
/// <remarks>
/// Um produto passa a ter controle de estoque na primeira entrada, ajuste ou configuração.
/// Produtos sem controle aparecem como "NaoControlado" no orçamento e não bloqueiam nada.
/// A venda de um orçamento FINALIZADO dá baixa automática no estoque.
/// </remarks>
[ApiController]
[Route("api/v1/estoque")]
[Produces("application/json")]
public class EstoqueController(ServicoEstoque servico) : ControllerBase
{
    /// <summary>Lista os produtos com controle de estoque (use abaixoDoMinimo=true para ver o que precisa repor).</summary>
    [HttpGet]
    [ProducesResponseType<PaginaResultado<EstoqueResumo>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaResultado<EstoqueResumo>>> Listar([FromQuery] bool abaixoDoMinimo = false,
        [FromQuery] string? busca = null, [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default) =>
        Ok(await servico.ListarAsync(new ParametrosPaginacao(pagina, tamanhoPagina), abaixoDoMinimo, busca, ct));

    /// <summary>Saldo de um produto.</summary>
    [HttpGet("{produtoId:guid}")]
    [ProducesResponseType<EstoqueResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstoqueResposta>> Obter(Guid produtoId, CancellationToken ct) =>
        Ok(await servico.ObterAsync(produtoId, ct));

    /// <summary>Registra uma entrada (compra, devolução...). Ex.: { "quantidade": 50, "motivo": "Compra NF 123" }.</summary>
    [HttpPost("{produtoId:guid}/entradas")]
    [ProducesResponseType<EstoqueResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstoqueResposta>> Entrada(Guid produtoId, MovimentarEstoqueRequisicao requisicao,
        CancellationToken ct) =>
        Ok(await servico.RegistrarEntradaAsync(produtoId, requisicao, ct));

    /// <summary>Registra uma saída manual (perda, uso interno...). Nunca deixa o saldo negativo.</summary>
    /// <response code="422">Saldo insuficiente ou produto sem controle de estoque.</response>
    [HttpPost("{produtoId:guid}/saidas")]
    [ProducesResponseType<EstoqueResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EstoqueResposta>> Saida(Guid produtoId, MovimentarEstoqueRequisicao requisicao,
        CancellationToken ct) =>
        Ok(await servico.RegistrarSaidaAsync(produtoId, requisicao, ct));

    /// <summary>[Gerente] Inventário: informa o saldo contado. Ex.: { "saldoContado": 48, "motivo": "Inventário de novembro" }.</summary>
    [HttpPost("{produtoId:guid}/ajustes")]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType<EstoqueResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EstoqueResposta>> Ajuste(Guid produtoId, AjustarEstoqueRequisicao requisicao,
        CancellationToken ct) =>
        Ok(await servico.AjustarAsync(produtoId, requisicao, ct));

    /// <summary>[Gerente] Define o estoque mínimo (alerta de reposição) e a localização na loja.</summary>
    [HttpPut("{produtoId:guid}/configuracao")]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType<EstoqueResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EstoqueResposta>> Configurar(Guid produtoId, ConfigurarEstoqueRequisicao requisicao,
        CancellationToken ct) =>
        Ok(await servico.ConfigurarAsync(produtoId, requisicao, ct));

    /// <summary>Histórico de movimentações do produto (mais recentes primeiro).</summary>
    [HttpGet("{produtoId:guid}/movimentacoes")]
    [ProducesResponseType<PaginaResultado<MovimentacaoResposta>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaginaResultado<MovimentacaoResposta>>> Movimentacoes(Guid produtoId,
        [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20, CancellationToken ct = default) =>
        Ok(await servico.ListarMovimentacoesAsync(produtoId, new ParametrosPaginacao(pagina, tamanhoPagina), ct));
}
