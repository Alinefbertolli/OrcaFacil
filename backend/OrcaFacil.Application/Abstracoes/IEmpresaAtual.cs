namespace OrcaFacil.Application.Abstracoes;

/// <summary>
/// Informa qual empresa (cliente do SaaS) está usando o sistema nesta requisição.
/// Na API é lido do token JWT do usuário logado (Fase 4).
/// Null = nenhuma empresa identificada: as consultas filtradas não retornam nada (falha "fechada").
/// </summary>
public interface IEmpresaAtual
{
    Guid? EmpresaId { get; }
}
