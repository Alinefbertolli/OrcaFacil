using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Infrastructure.Persistencia.Configuracoes;

public class OrcamentoConfiguration : IEntityTypeConfiguration<Orcamento>
{
    public void Configure(EntityTypeBuilder<Orcamento> builder)
    {
        builder.ToTable("Orcamentos");
        builder.Property(o => o.Numero).HasMaxLength(30).IsRequired();
        builder.Property(o => o.CondicoesComerciais).HasMaxLength(2000);
        builder.Property(o => o.Observacoes).HasMaxLength(2000);

        // Numeração própria de cada empresa: o número se repete entre empresas, nunca dentro da mesma.
        builder.HasIndex(o => new { o.EmpresaId, o.Numero }).IsUnique();
        builder.HasIndex(o => new { o.EmpresaId, o.Status, o.CriadoEm });
        builder.HasIndex(o => new { o.ClienteId, o.CriadoEm });

        // Propriedades calculadas.
        builder.Ignore(o => o.OpcaoSelecionada);
        builder.Ignore(o => o.PodeSerEditado);

        builder.HasMany(o => o.Opcoes).WithOne(op => op.Orcamento)
            .HasForeignKey(op => op.OrcamentoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Opcoes).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne(o => o.Usuario).WithMany()
            .HasForeignKey(o => o.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Cliente).WithMany(c => c.Orcamentos)
            .HasForeignKey(o => o.ClienteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.ListaImportada).WithMany()
            .HasForeignKey(o => o.ListaImportadaId).OnDelete(DeleteBehavior.SetNull);

        // Duplicação: aponta para o orçamento de origem, sem navegação.
        builder.HasOne<Orcamento>().WithMany()
            .HasForeignKey(o => o.OrcamentoOrigemId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class OpcaoOrcamentoConfiguration : IEntityTypeConfiguration<OpcaoOrcamento>
{
    public void Configure(EntityTypeBuilder<OpcaoOrcamento> builder)
    {
        builder.ToTable("OpcoesOrcamento");
        builder.Ignore(o => o.PossuiPrecoNaoCotado);
        builder.Ignore(o => o.ItensSemPreco);

        builder.HasIndex(o => new { o.OrcamentoId, o.Tipo }).IsUnique();

        // No máximo uma opção selecionada por orçamento (índice único parcial).
        builder.HasIndex(o => o.OrcamentoId, "IX_OpcoesOrcamento_UmaSelecionada")
            .IsUnique()
            .HasFilter("\"Selecionada\" = TRUE");

        builder.HasMany(o => o.Itens).WithOne(i => i.OpcaoOrcamento)
            .HasForeignKey(i => i.OpcaoOrcamentoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ItemOrcamentoConfiguration : IEntityTypeConfiguration<ItemOrcamento>
{
    public void Configure(EntityTypeBuilder<ItemOrcamento> builder)
    {
        builder.ToTable("ItensOrcamento", t =>
        {
            t.HasCheckConstraint("CK_ItemOrcamento_Quantidade", "\"Quantidade\" > 0");
            t.HasCheckConstraint("CK_ItemOrcamento_Preco", "\"PrecoUnitario\" >= 0");
        });

        builder.Property(i => i.Descricao).HasMaxLength(300).IsRequired();
        builder.Property(i => i.Marca).HasMaxLength(100);
        builder.Property(i => i.UnidadeMedida).HasMaxLength(10).IsRequired();
        builder.Property(i => i.Quantidade).HasPrecision(18, 3);
        builder.Property(i => i.PrecoUnitario).HasPrecision(18, 4);
        builder.Property(i => i.Observacao).HasMaxLength(ItemOrcamento.TamanhoMaximoObservacao);
        builder.HasIndex(i => new { i.OpcaoOrcamentoId, i.Ordem });

        builder.HasOne(i => i.Produto).WithMany()
            .HasForeignKey(i => i.ProdutoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Fornecedor).WithMany()
            .HasForeignKey(i => i.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Cotacao).WithMany()
            .HasForeignKey(i => i.CotacaoId).OnDelete(DeleteBehavior.Restrict);
    }
}
