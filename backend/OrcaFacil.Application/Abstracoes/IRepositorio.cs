using System.Linq.Expressions;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;

namespace OrcaFacil.Application.Abstracoes;

/// <summary>
/// Acesso genérico aos dados de uma entidade. Os casos de uso dependem desta interface,
/// não do EF Core: assim a regra de negócio não sabe (nem precisa saber) qual banco é usado.
/// O isolamento por empresa é aplicado automaticamente pelo DbContext em todas as operações.
/// Agregados com filhos (ex.: Orçamento → Opções → Itens) terão repositórios específicos.
/// </summary>
public interface IRepositorio<T> where T : EntidadeBase
{
    Task<T?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Como ObterPorIdAsync, mas lança NaoEncontradoException (→ HTTP 404) se não existir.</summary>
    Task<T> ObterPorIdOuFalharAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExisteAsync(Expression<Func<T, bool>> condicao, CancellationToken cancellationToken = default);

    /// <summary>Primeiro registro que atende à condição (ou null).</summary>
    Task<T?> ObterAsync(Expression<Func<T, bool>> condicao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Todos os registros que atendem ao filtro, sem paginação.
    /// Use só quando o resultado for naturalmente pequeno (ex.: fornecedores de UM produto).
    /// </summary>
    Task<IReadOnlyList<T>> ListarTodosAsync(Expression<Func<T, bool>> filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca vários registros de uma vez pelos Ids (uma única consulta: WHERE Id IN (...)).
    /// Evita o "problema N+1": buscar o nome da categoria de 20 produtos com 1 consulta, e não com 20.
    /// </summary>
    Task<IReadOnlyList<T>> ListarPorIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista com filtro, ordenação e paginação. Sem ordenação informada, traz os mais recentes primeiro.
    /// </summary>
    Task<PaginaResultado<T>> ListarAsync(
        ParametrosPaginacao paginacao,
        Expression<Func<T, bool>>? filtro = null,
        Expression<Func<T, object>>? ordenarPor = null,
        bool decrescente = false,
        CancellationToken cancellationToken = default);

    /// <summary>Marca para inclusão. Só é gravado ao chamar IUnitOfWork.SalvarAlteracoesAsync.</summary>
    void Adicionar(T entidade);

    /// <summary>Marca para exclusão. Só é gravado ao chamar IUnitOfWork.SalvarAlteracoesAsync.</summary>
    void Remover(T entidade);
}

/// <summary>
/// "Unidade de trabalho": grava de uma só vez (numa única transação) tudo o que foi alterado
/// durante a requisição. Ou tudo é salvo, ou nada é.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default);
}
