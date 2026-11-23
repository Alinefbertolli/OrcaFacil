using Microsoft.EntityFrameworkCore;
using OrcaFacil.Application.Orcamentos;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Infrastructure.Persistencia;

namespace OrcaFacil.Infrastructure.Orcamentos;

/// <summary>Carrega o orçamento com as opções e os itens (Include + ThenInclude).</summary>
public sealed class RepositorioOrcamentos(OrcaFacilDbContext contexto) : IRepositorioOrcamentos
{
    public Task<Orcamento?> ObterCompletoAsync(Guid id, CancellationToken ct = default) =>
        contexto.Orcamentos
            .Include(o => o.Opcoes)
            .ThenInclude(op => op.Itens)
            .AsSplitQuery() // duas coleções aninhadas: consultas separadas evitam repetir linhas
            .FirstOrDefaultAsync(o => o.Id == id, ct);
}
