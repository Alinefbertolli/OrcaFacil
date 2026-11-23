using System.Reflection;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Infrastructure.Persistencia;

/// <summary>
/// Contexto do EF Core. Além do mapeamento, garante o isolamento entre empresas (multi-tenancy):
/// 1) toda consulta a uma entidade da empresa recebe automaticamente "WHERE EmpresaId = empresa atual";
/// 2) todo registro novo recebe a empresa atual ao ser salvo, e gravar dados de outra empresa é bloqueado.
/// </summary>
public class OrcaFacilDbContext(DbContextOptions<OrcaFacilDbContext> options, IEmpresaAtual empresaAtual)
    : DbContext(options)
{
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TokenRecuperacaoSenha> TokensRecuperacaoSenha => Set<TokenRecuperacaoSenha>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<ProdutoFornecedor> ProdutosFornecedores => Set<ProdutoFornecedor>();
    public DbSet<HistoricoPreco> HistoricoPrecos => Set<HistoricoPreco>();
    public DbSet<Cotacao> Cotacoes => Set<Cotacao>();
    public DbSet<ListaImportada> ListasImportadas => Set<ListaImportada>();
    public DbSet<ItemListaImportada> ItensListaImportada => Set<ItemListaImportada>();
    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<OpcaoOrcamento> OpcoesOrcamento => Set<OpcaoOrcamento>();
    public DbSet<ItemOrcamento> ItensOrcamento => Set<ItemOrcamento>();
    public DbSet<Estoque> Estoques => Set<Estoque>();
    public DbSet<MovimentacaoEstoque> MovimentacoesEstoque => Set<MovimentacaoEstoque>();
    public DbSet<LogSistema> Logs => Set<LogSistema>();

    /// <summary>
    /// Lida a cada consulta. Precisa ser um membro do DbContext para o EF trocar o valor
    /// por instância (cada requisição tem seu próprio contexto e sua própria empresa).
    /// </summary>
    private Guid? EmpresaIdAtual => empresaAtual.EmpresaId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Busca aproximada de produtos por nome (usada na importação de listas).
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrcaFacilDbContext).Assembly);

        // Os IDs são gerados no domínio (Guid v7). Sem ValueGeneratedNever, o EF trataria
        // um filho novo adicionado a um agregado já carregado como "Modified" em vez de "Added".
        foreach (var entidade in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(EntidadeBase).IsAssignableFrom(t.ClrType)))
        {
            modelBuilder.Entity(entidade.ClrType).Property(nameof(EntidadeBase.Id)).ValueGeneratedNever();
        }

        // Para cada entidade da empresa: chave estrangeira para Empresa + filtro global.
        // Usamos um método genérico chamado por reflexão porque o filtro precisa ser uma
        // expressão tipada (e => e.EmpresaId == ...) para cada classe.
        var configurar = typeof(OrcaFacilDbContext)
            .GetMethod(nameof(ConfigurarEntidadeDaEmpresa), BindingFlags.NonPublic | BindingFlags.Instance)!;

        var tiposDaEmpresa = modelBuilder.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(t => typeof(EntidadeDaEmpresa).IsAssignableFrom(t))
            .ToList();

        foreach (var tipo in tiposDaEmpresa)
            configurar.MakeGenericMethod(tipo).Invoke(this, [modelBuilder]);
    }

    private void ConfigurarEntidadeDaEmpresa<T>(ModelBuilder modelBuilder) where T : EntidadeDaEmpresa
    {
        modelBuilder.Entity<T>(entidade =>
        {
            entidade.HasOne<Empresa>().WithMany()
                .HasForeignKey(e => e.EmpresaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Sem empresa identificada, EmpresaIdAtual é null e nenhuma linha é retornada.
            entidade.HasQueryFilter(e => e.EmpresaId == EmpresaIdAtual);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums salvos como texto: legíveis no banco e imunes a reordenação dos valores.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);

        // Padrão monetário: 2 casas. Preços unitários (4) e quantidades (3) são ajustados nas configurações.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AplicarRegrasAntesDeSalvar();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AplicarRegrasAntesDeSalvar();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Executado antes de cada gravação. Interno para poder ser testado sem banco.</summary>
    internal void AplicarRegrasAntesDeSalvar()
    {
        GarantirIsolamentoEntreEmpresas();
        PreencherDatasDeAuditoria();
    }

    private void GarantirIsolamentoEntreEmpresas()
    {
        var empresaId = EmpresaIdAtual;

        foreach (var entrada in ChangeTracker.Entries<EntidadeDaEmpresa>())
        {
            var registro = entrada.Entity;

            if (entrada.State == EntityState.Added)
            {
                if (registro.EmpresaId == Guid.Empty)
                {
                    // Registro novo sem empresa: recebe a empresa do usuário logado.
                    if (empresaId is null)
                        throw new InvalidOperationException(
                            $"Não há empresa identificada para salvar o registro '{registro.GetType().Name}'.");
                    registro.VincularEmpresa(empresaId.Value);
                }
                else if (empresaId is not null && registro.EmpresaId != empresaId)
                {
                    // Proteção extra: um usuário nunca grava dados na empresa de outro.
                    throw new InvalidOperationException("Tentativa de gravar dados em outra empresa foi bloqueada.");
                }
            }
            else if (entrada.State == EntityState.Modified && entrada.Property(e => e.EmpresaId).IsModified)
            {
                throw new InvalidOperationException("A empresa de um registro não pode ser alterada.");
            }
        }
    }

    /// <summary>Preenche CriadoEm/AtualizadoEm em UTC para toda entidade inserida ou alterada.</summary>
    private void PreencherDatasDeAuditoria()
    {
        var agora = DateTime.UtcNow;

        foreach (var entrada in ChangeTracker.Entries<EntidadeBase>())
        {
            switch (entrada.State)
            {
                case EntityState.Added:
                    entrada.Entity.CriadoEm = agora;
                    break;
                case EntityState.Modified:
                    entrada.Entity.AtualizadoEm = agora;
                    entrada.Property(e => e.CriadoEm).IsModified = false;
                    break;
            }
        }
    }
}
