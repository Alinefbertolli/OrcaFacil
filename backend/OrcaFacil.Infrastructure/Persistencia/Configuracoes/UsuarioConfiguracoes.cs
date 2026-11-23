using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Infrastructure.Persistencia.Configuracoes;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.Property(u => u.Nome).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();
        builder.Property(u => u.SenhaHash).HasMaxLength(500).IsRequired();
        // E-mail único no sistema inteiro: o login identifica a pessoa e, por ela, a empresa.
        builder.HasIndex(u => u.Email).IsUnique();
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.Property(t => t.TokenHash).HasMaxLength(200).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasOne(t => t.Usuario).WithMany().HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TokenRecuperacaoSenhaConfiguration : IEntityTypeConfiguration<TokenRecuperacaoSenha>
{
    public void Configure(EntityTypeBuilder<TokenRecuperacaoSenha> builder)
    {
        builder.ToTable("TokensRecuperacaoSenha");
        builder.Property(t => t.TokenHash).HasMaxLength(200).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasOne(t => t.Usuario).WithMany().HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LogSistemaConfiguration : IEntityTypeConfiguration<LogSistema>
{
    public void Configure(EntityTypeBuilder<LogSistema> builder)
    {
        builder.ToTable("LogsSistema");
        builder.Property(l => l.Acao).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Entidade).HasMaxLength(100);
        builder.Property(l => l.Ip).HasMaxLength(45); // IPv6
        builder.Property(l => l.DetalhesJson).HasColumnName("Detalhes").HasColumnType("jsonb");
        builder.HasIndex(l => new { l.EmpresaId, l.CriadoEm });
        builder.HasOne<Empresa>().WithMany().HasForeignKey(l => l.EmpresaId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(l => new { l.Entidade, l.EntidadeId });
        builder.HasOne(l => l.Usuario).WithMany().HasForeignKey(l => l.UsuarioId).OnDelete(DeleteBehavior.SetNull);
    }
}
