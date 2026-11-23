using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;

namespace OrcaFacil.API.Controllers;

/// <summary>Informações gerais da API.</summary>
[ApiController]
[Route("api/v1/sistema")]
[AllowAnonymous] // público: não exige login
[Produces("application/json")]
public class SistemaController(IHostEnvironment ambiente, TimeProvider relogio) : ControllerBase
{
    /// <summary>Retorna nome, versão e ambiente da API. Útil para conferir se ela está no ar.</summary>
    /// <response code="200">A API está funcionando.</response>
    [HttpGet("info")]
    [ProducesResponseType<InfoSistemaResposta>(StatusCodes.Status200OK)]
    public ActionResult<InfoSistemaResposta> ObterInfo()
    {
        var versao = typeof(SistemaController).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        return Ok(new InfoSistemaResposta("OrçaFácil API", versao, ambiente.EnvironmentName, relogio.GetUtcNow()));
    }

    /// <summary>
    /// [Somente em desenvolvimento] Demonstra como a API responde a cada tipo de erro.
    /// </summary>
    /// <param name="tipo">validacao, nao-encontrado, conflito, regra ou inesperado</param>
    /// <response code="400">validacao: dados inválidos</response>
    /// <response code="404">nao-encontrado: registro inexistente</response>
    /// <response code="409">conflito: registro duplicado</response>
    /// <response code="422">regra: regra de negócio violada</response>
    /// <response code="500">inesperado: erro interno</response>
    [HttpGet("demonstracao-erro/{tipo}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public IActionResult DemonstrarErro(string tipo)
    {
        // Fora do ambiente de desenvolvimento este endpoint "não existe".
        if (!ambiente.IsDevelopment())
            return NotFound();

        // Cada caso lança a exceção que um caso de uso real lançaria; o TratadorGlobalDeExcecoes responde.
        Exception erro = tipo switch
        {
            "validacao" => new ValidationException(
            [
                new ValidationFailure("Nome", "O nome do produto é obrigatório."),
                new ValidationFailure("PrecoVenda", "O preço não pode ser negativo.")
            ]),
            "nao-encontrado" => new NaoEncontradoException("Produto", Guid.Empty),
            "conflito" => new ConflitoException("Já existe um produto com o código 001 nesta empresa."),
            "regra" => new DomainException("Saldo insuficiente. Disponível: 3, solicitado: 5."),
            _ => new InvalidOperationException("Exemplo de erro inesperado (bug).")
        };
        throw erro;
    }
}

/// <summary>Dados retornados por GET /api/v1/sistema/info.</summary>
public sealed record InfoSistemaResposta(string Nome, string Versao, string Ambiente, DateTimeOffset HorarioServidorUtc);
