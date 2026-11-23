using Microsoft.EntityFrameworkCore;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Infrastructure.Persistencia;

namespace OrcaFacil.Tests.Infrastructure;

/// <summary>Empresa "logada" fixa, para testes. Simula o que o token JWT fará na API.</summary>
internal sealed class EmpresaAtualFixa(Guid? empresaId) : IEmpresaAtual
{
    public Guid? EmpresaId { get; } = empresaId;
}

internal static class ContextoDeTeste
{
    /// <summary>
    /// Cria um DbContext do PostgreSQL que NÃO abre conexão: serve para inspecionar o modelo,
    /// gerar SQL e testar as regras do SaveChanges sem precisar de banco rodando.
    /// </summary>
    public static OrcaFacilDbContext Criar(Guid? empresaId = null)
    {
        var options = new DbContextOptionsBuilder<OrcaFacilDbContext>()
            .UseNpgsql("Host=localhost;Database=validacao_modelo")
            .Options;
        return new OrcaFacilDbContext(options, new EmpresaAtualFixa(empresaId));
    }
}
