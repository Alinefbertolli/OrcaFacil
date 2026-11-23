using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Infrastructure.Persistencia.Configuracoes;

public class ListaImportadaConfiguration : IEntityTypeConfiguration<ListaImportada>
{
    public void Configure(EntityTypeBuilder<ListaImportada> builder)
    {
        builder.ToTable("ListasImportadas");
        builder.Property(l => l.NomeArquivoOriginal).HasMaxLength(255);
        builder.Property(l => l.CaminhoArmazenamento).HasMaxLength(500);
        builder.Property(l => l.MensagemErro).HasMaxLength(1000);
        builder.HasIndex(l => new { l.EmpresaId, l.CriadoEm });
        builder.HasIndex(l => new { l.UsuarioId, l.CriadoEm });

        // Propriedade calculada — não é coluna nem navegação.
        builder.Ignore(l => l.ItensValidos);

        builder.HasMany(l => l.Itens).WithOne(i => i.ListaImportada)
            .HasForeignKey(i => i.ListaImportadaId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(l => l.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne(l => l.Usuario).WithMany()
            .HasForeignKey(l => l.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.Cliente).WithMany()
            .HasForeignKey(l => l.ClienteId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ItemListaImportadaConfiguration : IEntityTypeConfiguration<ItemListaImportada>
{
    public void Configure(EntityTypeBuilder<ItemListaImportada> builder)
    {
        builder.ToTable("ItensListaImportada", t =>
        {
            t.HasCheckConstraint("CK_ItemLista_Quantidade", "\"Quantidade\" > 0");
            t.HasCheckConstraint("CK_ItemLista_Confianca", "\"ConfiancaInterpretacao\" BETWEEN 0 AND 1");
        });

        builder.Property(i => i.TextoOriginal).HasMaxLength(1000);
        builder.Property(i => i.Descricao).HasMaxLength(300).IsRequired();
        builder.Property(i => i.Quantidade).HasPrecision(18, 3);
        builder.Property(i => i.UnidadeMedida).HasMaxLength(10).IsRequired();
        builder.Property(i => i.Marca).HasMaxLength(100);
        builder.Property(i => i.ConfiancaInterpretacao).HasPrecision(3, 2);
        builder.HasIndex(i => new { i.ListaImportadaId, i.Ordem });

        builder.HasOne(i => i.Produto).WithMany()
            .HasForeignKey(i => i.ProdutoId).OnDelete(DeleteBehavior.SetNull);
    }
}
