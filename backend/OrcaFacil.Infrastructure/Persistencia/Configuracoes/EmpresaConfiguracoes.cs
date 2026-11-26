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

        // Contato da loja (Fase 9: cabeçalho do PDF e texto do WhatsApp)
        builder.Property(e => e.Telefone).HasMaxLength(Empresa.TamanhoMaximoTelefone);
        builder.Property(e => e.Email).HasMaxLength(200);
        builder.OwnsOne(e => e.Endereco, e => EnderecoMapeamento.Configurar(e));

        // Uma empresa (CNPJ) só pode ter uma conta no OrçaFácil.
        builder.HasIndex(e => e.Cnpj).IsUnique();
    }
}
