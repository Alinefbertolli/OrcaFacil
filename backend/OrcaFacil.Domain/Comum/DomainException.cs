namespace OrcaFacil.Domain.Comum;

/// <summary>
/// Violação de regra de negócio. A API converte esta exceção em HTTP 422 (ProblemDetails).
/// </summary>
public sealed class DomainException(string mensagem) : Exception(mensagem);
