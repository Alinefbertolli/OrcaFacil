namespace OrcaFacil.Application.Abstracoes;

/// <summary>
/// Gera números de orçamento sequenciais por empresa (ex.: ORC-2026-000123), reiniciando a cada ano.
/// Implementado na Infrastructure com um contador atômico no PostgreSQL,
/// o que garante unicidade mesmo com vários usuários criando orçamentos ao mesmo tempo.
/// </summary>
public interface IGeradorNumeroOrcamento
{
    Task<string> GerarAsync(CancellationToken cancellationToken = default);
}
