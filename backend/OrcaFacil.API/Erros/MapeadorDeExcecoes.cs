using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;

namespace OrcaFacil.API.Erros;

/// <summary>
/// Traduz exceções em respostas HTTP no padrão ProblemDetails (RFC 7807), um formato JSON
/// conhecido por qualquer cliente HTTP:
/// { "type": "...", "title": "...", "status": 404, "detail": "...", "traceId": "..." }
///
/// É uma classe estática e pura (sem dependências) para poder ser testada isoladamente.
/// </summary>
public static class MapeadorDeExcecoes
{
    public static ProblemDetails Mapear(Exception excecao, bool mostrarDetalhesTecnicos) => excecao switch
    {
        // 400 — os dados enviados estão incompletos ou em formato inválido.
        ValidationException validacao => new ValidationProblemDetails(AgruparErros(validacao))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Os dados enviados são inválidos.",
            Type = "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1"
        },

        // 401 — não autenticado: credenciais inválidas, sessão expirada ou acesso suspenso.
        NaoAutorizadoException naoAutorizado => Criar(StatusCodes.Status401Unauthorized,
            "Não autorizado.", naoAutorizado.Message, "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2"),

        // 404 — o registro não existe (ou é de outra empresa).
        NaoEncontradoException naoEncontrado => Criar(StatusCodes.Status404NotFound,
            "Registro não encontrado.", naoEncontrado.Message, "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5"),

        // 409 — conflito com o estado atual (duplicado, alterado ao mesmo tempo, vinculado).
        ConflitoException conflito => Criar(StatusCodes.Status409Conflict,
            "Conflito com os dados existentes.", conflito.Message, "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.10"),

        // 422 — os dados são válidos, mas a operação viola uma regra de negócio.
        DomainException regra => Criar(StatusCodes.Status422UnprocessableEntity,
            "A operação não é permitida pelas regras do negócio.", regra.Message,
            "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.21"),

        // 500 — erro inesperado (bug). Em produção a mensagem técnica é escondida:
        // ela pode revelar detalhes internos úteis para um atacante.
        _ => Criar(StatusCodes.Status500InternalServerError,
            "Ocorreu um erro inesperado.",
            mostrarDetalhesTecnicos
                ? $"{excecao.GetType().Name}: {excecao.Message}"
                : "Tente novamente. Se o problema continuar, informe o código de rastreio (traceId) ao suporte.",
            "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1")
    };

    private static ProblemDetails Criar(int status, string titulo, string detalhe, string tipo) =>
        new() { Status = status, Title = titulo, Detail = detalhe, Type = tipo };

    /// <summary>Agrupa os erros por campo: { "Nome": ["obrigatório"], "Preco": ["deve ser positivo"] }.</summary>
    private static Dictionary<string, string[]> AgruparErros(ValidationException validacao) =>
        validacao.Errors
            .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "geral" : e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
}
