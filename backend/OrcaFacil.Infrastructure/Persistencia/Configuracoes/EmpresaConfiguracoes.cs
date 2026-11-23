using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Infrastructure.Persistencia.Configuracoes;

public class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("Empresas");
        builder.Property(e => e.RazaoSocial).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NomeFantasia).HasMaxLength(200);
        builder.Property(e => e.Cnpj).HasMaxLength(14).IsFixedLength().IsRequired();
        builder.Ignore(e => e.NomeExibicao); // calculada

        // Uma empresa (CNPJ) só pode ter uma conta no OrçaFácil.
        builder.HasIndex(e => e.Cnpj).IsUnique();
    }
}
