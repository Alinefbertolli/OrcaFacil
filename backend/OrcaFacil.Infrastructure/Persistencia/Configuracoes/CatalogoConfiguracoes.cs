using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Infrastructure.Persistencia.Configuracoes;

public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("Categorias");
        builder.Property(c => c.Nome).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Descricao).HasMaxLength(500);
        builder.HasIndex(c => new { c.EmpresaId, c.Nome }).IsUnique();

        builder.HasOne(c => c.CategoriaPai)
            .WithMany(c => c.Subcategorias)
            .HasForeignKey(c => c.CategoriaPaiId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sem categorias pré-cadastradas: cada empresa cria as suas (o sistema atende qualquer segmento).
    }
}

public class MarcaConfiguration : IEntityTypeConfiguration<Marca>
{
    public void Configure(EntityTypeBuilder<Marca> builder)
    {
        builder.ToTable("Marcas", t =>
            t.HasCheckConstraint("CK_Marca_NivelReconhecimento", "\"NivelReconhecimento\" BETWEEN 1 AND 5"));
        builder.Property(m => m.Nome).HasMaxLength(100).IsRequired();
        builder.HasIndex(m => new { m.EmpresaId, m.Nome }).IsUnique();
    }
}

public class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("Produtos", t =>
            t.HasCheckConstraint("CK_Produto_PrecoVenda", "\"PrecoVenda\" IS NULL OR \"PrecoVenda\" >= 0"));

        builder.Property(p => p.Codigo).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Nome).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Descricao).HasMaxLength(2000);
        builder.Property(p => p.UnidadeMedida).HasMaxLength(10).IsRequired();
        builder.Property(p => p.PrecoVenda).HasPrecision(18, 4);
        builder.Property(p => p.GrupoEquivalencia).HasMaxLength(Produto.TamanhoMaximoGrupo);

        // Código único dentro de cada empresa (duas lojas podem ter o produto "001").
        builder.HasIndex(p => new { p.EmpresaId, p.Codigo }).IsUnique();
        // Encontrar rápido os produtos equivalentes (mesmo grupo) no orçamento comparativo.
        builder.HasIndex(p => new { p.EmpresaId, p.GrupoEquivalencia });
        // Índice trigram: busca por similaridade ("cimento cp2" ≈ "Cimento CP-II 50kg").
        builder.HasIndex(p => p.Nome, "IX_Produtos_Nome_Trgm").HasMethod("gin").HasOperators("gin_trgm_ops");

        builder.HasOne(p => p.Categoria).WithMany(c => c.Produtos)
            .HasForeignKey(p => p.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Marca).WithMany(m => m.Produtos)
            .HasForeignKey(p => p.MarcaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProdutoFornecedorConfiguration : IEntityTypeConfiguration<ProdutoFornecedor>
{
    public void Configure(EntityTypeBuilder<ProdutoFornecedor> builder)
    {
        builder.ToTable("ProdutosFornecedores");
        builder.Property(pf => pf.CodigoNoFornecedor).HasMaxLength(50);
        builder.Property(pf => pf.PrecoAtual).HasPrecision(18, 4);
        builder.HasIndex(pf => new { pf.ProdutoId, pf.FornecedorId }).IsUnique();

        builder.HasOne(pf => pf.Produto).WithMany(p => p.Fornecedores)
            .HasForeignKey(pf => pf.ProdutoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(pf => pf.Fornecedor).WithMany(f => f.Produtos)
            .HasForeignKey(pf => pf.FornecedorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HistoricoPrecoConfiguration : IEntityTypeConfiguration<HistoricoPreco>
{
    public void Configure(EntityTypeBuilder<HistoricoPreco> builder)
    {
        builder.ToTable("HistoricoPrecos");
        builder.Property(h => h.Preco).HasPrecision(18, 4);
        builder.HasIndex(h => new { h.ProdutoId, h.RegistradoEm }).IsDescending(false, true);

        builder.HasOne(h => h.Produto).WithMany(p => p.HistoricoPrecos)
            .HasForeignKey(h => h.ProdutoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.Fornecedor).WithMany()
            .HasForeignKey(h => h.FornecedorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CotacaoConfiguration : IEntityTypeConfiguration<Cotacao>
{
    public void Configure(EntityTypeBuilder<Cotacao> builder)
    {
        builder.ToTable("Cotacoes");
        builder.Property(c => c.Provedor).HasMaxLength(100).IsRequired();
        builder.Property(c => c.TermoPesquisa).HasMaxLength(300).IsRequired();
        builder.Property(c => c.DescricaoEncontrada).HasMaxLength(500).IsRequired();
        builder.Property(c => c.MarcaEncontrada).HasMaxLength(100);
        builder.Property(c => c.UnidadeMedida).HasMaxLength(10);
        builder.Property(c => c.UrlOferta).HasMaxLength(1000);
        builder.Property(c => c.PrecoUnitario).HasPrecision(18, 4);

        builder.HasIndex(c => new { c.ProdutoId, c.ConsultadoEm });
        builder.HasIndex(c => new { c.EmpresaId, c.ConsultadoEm });
        builder.HasIndex(c => new { c.EmpresaId, c.TermoPesquisa }); // relatório "produtos mais pesquisados"

        builder.HasOne(c => c.Produto).WithMany()
            .HasForeignKey(c => c.ProdutoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Fornecedor).WithMany()
            .HasForeignKey(c => c.FornecedorId).OnDelete(DeleteBehavior.Restrict);
    }
}
