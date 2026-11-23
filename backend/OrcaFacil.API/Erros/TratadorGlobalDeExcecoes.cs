using Microsoft.AspNetCore.Diagnostics;

namespace OrcaFacil.API.Erros;

/// <summary>
/// Captura QUALQUER exceção que escape de um controller e devolve uma resposta padronizada.
/// Assim nenhum controller precisa de try/catch: ele lança a exceção e este tratador responde.
/// </summary>
public sealed class TratadorGlobalDeExcecoes(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment ambiente,
    ILogger<TratadorGlobalDeExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problema = MapeadorDeExcecoes.Mapear(exception, mostrarDetalhesTecnicos: ambiente.IsDevelopment());
        var status = problema.Status ?? StatusCodes.Status500InternalServerError;

        // Erros 4xx são do cliente (dado errado, regra violada): registramos como aviso.
        // Erros 5xx são nossos (bug): registramos como erro, com o stack trace completo.
        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Erro inesperado ao processar {Metodo} {Caminho}",
                httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogWarning("Requisição recusada ({Status}) em {Metodo} {Caminho}: {Mensagem}",
                status, httpContext.Request.Method, httpContext.Request.Path, exception.Message);

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problema,
            Exception = exception
        });
    }
}
