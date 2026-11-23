using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Infrastructure.Persistencia.Configuracoes;

public class EstoqueConfiguration : IEntityTypeConfiguration<Estoque>
{
    public void Configure(EntityTypeBuilder<Estoque> builder)
    {
        builder.ToTable("Estoques", t =>
        {
            t.HasCheckConstraint("CK_Estoque_Saldo", "\"QuantidadeDisponivel\" >= 0");
            t.HasCheckConstraint("CK_Estoque_Minimo", "\"EstoqueMinimo\" >= 0");
        });

        builder.Property(e => e.QuantidadeDisponivel).HasPrecision(18, 3);
        builder.Property(e => e.EstoqueMinimo).HasPrecision(18, 3);
        builder.Property(e => e.Localizacao).HasMaxLength(100);
        builder.Ignore(e => e.AbaixoDoMinimo);

        // Concorrência otimista usando a coluna de sistema xmin do PostgreSQL:
        // duas saídas simultâneas do mesmo produto não conseguem gravar sobre saldo desatualizado.
        builder.Property(e => e.Versao).IsRowVersion();

        // 1:1 com Produto
        builder.HasIndex(e => e.ProdutoId).IsUnique();
        builder.HasOne(e => e.Produto).WithOne(p => p.Estoque)
            .HasForeignKey<Estoque>(e => e.ProdutoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Movimentacoes).WithOne(m => m.Estoque)
            .HasForeignKey(m => m.EstoqueId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(e => e.Movimentacoes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class MovimentacaoEstoqueConfiguration : IEntityTypeConfiguration<MovimentacaoEstoque>
{
    public void Configure(EntityTypeBuilder<MovimentacaoEstoque> builder)
    {
        builder.ToTable("MovimentacoesEstoque", t =>
            t.HasCheckConstraint("CK_Movimentacao_Quantidade", "\"Quantidade\" > 0"));

        builder.Property(m => m.Quantidade).HasPrecision(18, 3);
        builder.Property(m => m.SaldoAnterior).HasPrecision(18, 3);
        builder.Property(m => m.SaldoPosterior).HasPrecision(18, 3);
        builder.Property(m => m.Motivo).HasMaxLength(500);
        builder.HasIndex(m => new { m.EstoqueId, m.CriadoEm });
        builder.HasIndex(m => new { m.EmpresaId, m.CriadoEm }); // relatório de movimentações por período

        builder.HasOne(m => m.Usuario).WithMany()
            .HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Orcamento).WithMany()
            .HasForeignKey(m => m.OrcamentoId).OnDelete(DeleteBehavior.Restrict);
    }
}
