namespace OrcaFacil.Application.Comum;

// Exceções que os casos de uso lançam e que a API traduz em respostas HTTP padronizadas:
//   NaoAutorizadoException → 401 Unauthorized
//   NaoEncontradoException → 404 Not Found
//   ConflitoException      → 409 Conflict
// (DomainException → 422 e ValidationException do FluentValidation → 400 vêm de outros lugares.)

/// <summary>
/// Credenciais inválidas, sessão expirada ou acesso suspenso.
/// A mensagem é propositalmente genérica no login ("e-mail ou senha inválidos"),
/// para não revelar a um atacante quais e-mails estão cadastrados.
/// </summary>
public sealed class NaoAutorizadoException(string mensagem) : Exception(mensagem);

/// <summary>O registro procurado não existe (ou pertence a outra empresa, o que dá no mesmo).</summary>
public sealed class NaoEncontradoException(string entidade, object id)
    : Exception($"{entidade} não encontrado(a). Identificador: {id}.")
{
    public string Entidade { get; } = entidade;
}

/// <summary>
/// A operação conflita com o estado atual dos dados: registro duplicado,
/// alterado por outra pessoa ao mesmo tempo, ou vinculado a outros registros.
/// </summary>
public sealed class ConflitoException(string mensagem, Exception? causa = null) : Exception(mensagem, causa);
