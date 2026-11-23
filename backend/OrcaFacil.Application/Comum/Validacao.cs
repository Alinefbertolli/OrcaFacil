using FluentValidation;
using FluentValidation.Results;

namespace OrcaFacil.Application.Comum;

public static class Validacao
{
    /// <summary>
    /// Cria um erro de validação (HTTP 400) ligado a um campo específico.
    /// Uso: throw Validacao.Erro("CategoriaId", "Categoria não encontrada.");
    /// </summary>
    public static ValidationException Erro(string campo, string mensagem) =>
        new([new ValidationFailure(campo, mensagem)]);

    /// <summary>Texto de busca sem espaços nas pontas e em minúsculas ("" quando vazio).</summary>
    public static string NormalizarBusca(string? busca) => busca?.Trim().ToLower() ?? string.Empty;

    /// <summary>Texto opcional: vazio vira null e o restante perde os espaços das pontas.</summary>
    public static string? TextoOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
