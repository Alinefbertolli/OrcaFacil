using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;

namespace OrcaFacil.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Implementação genérica de IRepositorio com EF Core.
/// O filtro por empresa é aplicado pelo próprio DbContext em todas as consultas.
/// </summary>
public class Repositorio<T>(OrcaFacilDbContext contexto) : IRepositorio<T> where T : EntidadeBase
{
    protected OrcaFacilDbContext Contexto { get; } = contexto;
    protected DbSet<T> Conjunto => Contexto.Set<T>();

    public virtual Task<T?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        // FirstOrDefaultAsync (e não FindAsync) para respeitar sempre o filtro por empresa.
        Conjunto.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<T> ObterPorIdOuFalharAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ObterPorIdAsync(id, cancellationToken)
        ?? throw new NaoEncontradoException(typeof(T).Name, id);

    public Task<bool> ExisteAsync(Expression<Func<T, bool>> condicao, CancellationToken cancellationToken = default) =>
        Conjunto.AnyAsync(condicao, cancellationToken);

    public Task<T?> ObterAsync(Expression<Func<T, bool>> condicao, CancellationToken cancellationToken = default) =>
        Conjunto.FirstOrDefaultAsync(condicao, cancellationToken);

    public async Task<IReadOnlyList<T>> ListarTodosAsync(Expression<Func<T, bool>> filtro,
        CancellationToken cancellationToken = default) =>
        await Conjunto.Where(filtro).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> ListarPorIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0)
            return [];

        return await Conjunto.AsNoTracking().Where(e => lista.Contains(e.Id)).ToListAsync(cancellationToken);
    }

    public async Task<PaginaResultado<T>> ListarAsync(
        ParametrosPaginacao paginacao,
        Expression<Func<T, bool>>? filtro = null,
        Expression<Func<T, object>>? ordenarPor = null,
        bool decrescente = false,
        CancellationToken cancellationToken = default)
    {
        // AsNoTracking: listagens são só leitura, então o EF não precisa "vigiar" as alterações (mais rápido).
        IQueryable<T> consulta = Conjunto.AsNoTracking();
        if (filtro is not null)
            consulta = consulta.Where(filtro);

        // 1ª consulta: quantos registros existem no total (para calcular o número de páginas).
        var total = await consulta.LongCountAsync(cancellationToken);

        // Ordenação estável: o Id desempata registros iguais, para nenhum item "pular" entre páginas.
        IOrderedQueryable<T> ordenada = ordenarPor is null
            ? consulta.OrderByDescending(e => e.CriadoEm)
            : decrescente ? consulta.OrderByDescending(ordenarPor) : consulta.OrderBy(ordenarPor);

        // 2ª consulta: só os registros da página pedida (LIMIT/OFFSET no SQL).
        var itens = await ordenada.ThenBy(e => e.Id)
            .Skip(paginacao.Saltar)
            .Take(paginacao.TamanhoPagina)
            .ToListAsync(cancellationToken);

        return new PaginaResultado<T>(itens, paginacao.Pagina, paginacao.TamanhoPagina, total);
    }

    public void Adicionar(T entidade) => Conjunto.Add(entidade);

    public void Remover(T entidade) => Conjunto.Remove(entidade);
}
