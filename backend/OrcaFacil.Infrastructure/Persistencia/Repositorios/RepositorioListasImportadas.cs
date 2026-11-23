using Microsoft.EntityFrameworkCore;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Infrastructure.Persistencia.Repositorios;

/// <summary>Carrega a lista importada JUNTO com os itens (Include), em uma única consulta.</summary>
public sealed class RepositorioListasImportadas(OrcaFacilDbContext contexto) : IRepositorioListasImportadas
{
    public Task<ListaImportada?> ObterComItensAsync(Guid id, CancellationToken ct = default) =>
        contexto.ListasImportadas
            .Include(l => l.Itens)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
}
