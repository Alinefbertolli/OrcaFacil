using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrcaFacil.API.Configuracao;
using OrcaFacil.Application.Empresas;

namespace OrcaFacil.API.Controllers;

/// <summary>Configurações da empresa logada.</summary>
[ApiController]
[Route("api/v1/empresa")]
[Produces("application/json")]
public class EmpresaController(ServicoConfiguracoesEmpresa servico) : ControllerBase
{
    /// <summary>Consulta as configurações (ex.: modo padrão de orçamento).</summary>
    [HttpGet("configuracoes")]
    [ProducesResponseType<ConfiguracoesEmpresaResposta>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfiguracoesEmpresaResposta>> Obter(CancellationToken ct) =>
        Ok(await servico.ObterAsync(ct));

    /// <summary>[Administrador] Altera o modo padrão de orçamento: Unico ou Comparativo.</summary>
    [HttpPut("configuracoes")]
    [Authorize(Policy = PoliticasApi.Administrador)]
    [ProducesResponseType<ConfiguracoesEmpresaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ConfiguracoesEmpresaResposta>> Atualizar(ConfiguracoesEmpresaRequisicao requisicao,
        CancellationToken ct) =>
        Ok(await servico.AtualizarAsync(requisicao, ct));
}
