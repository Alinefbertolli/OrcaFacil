using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Infrastructure.Persistencia.Configuracoes;

/// <summary>Mapeamento do objeto de valor Endereço, reaproveitado por Cliente e Fornecedor.</summary>
internal static class EnderecoMapeamento
{
    public static void Configurar<T>(OwnedNavigationBuilder<T, Endereco> e) where T : class
    {
        e.Property(x => x.Logradouro).HasColumnName("EnderecoLogradouro").HasMaxLength(200);
        e.Property(x => x.Numero).HasColumnName("EnderecoNumero").HasMaxLength(20);
        e.Property(x => x.Complemento).HasColumnName("EnderecoComplemento").HasMaxLength(100);
        e.Property(x => x.Bairro).HasColumnName("EnderecoBairro").HasMaxLength(100);
        e.Property(x => x.Cidade).HasColumnName("EnderecoCidade").HasMaxLength(100);
        e.Property(x => x.Uf).HasColumnName("EnderecoUf").HasMaxLength(2);
        e.Property(x => x.Cep).HasColumnName("EnderecoCep").HasMaxLength(8);
    }
}

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");
        builder.Property(c => c.Nome).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Documento).HasMaxLength(14);
        builder.Property(c => c.Telefone).HasMaxLength(20);
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.Observacoes).HasMaxLength(2000);
        builder.OwnsOne(c => c.Endereco, e => EnderecoMapeamento.Configurar(e));

        // Documento é opcional, mas quando informado não pode se repetir.
        builder.HasIndex(c => new { c.EmpresaId, c.Documento }).IsUnique().HasFilter("\"Documento\" IS NOT NULL");
        builder.HasIndex(c => new { c.EmpresaId, c.Nome });
    }
}

public class FornecedorConfiguration : IEntityTypeConfiguration<Fornecedor>
{
    public void Configure(EntityTypeBuilder<Fornecedor> builder)
    {
        builder.ToTable("Fornecedores", t =>
            t.HasCheckConstraint("CK_Fornecedor_Avaliacao", "\"Avaliacao\" IS NULL OR (\"Avaliacao\" >= 0 AND \"Avaliacao\" <= 5)"));

        builder.Property(f => f.RazaoSocial).HasMaxLength(200).IsRequired();
        builder.Property(f => f.NomeFantasia).HasMaxLength(200);
        builder.Property(f => f.Cnpj).HasMaxLength(14).IsFixedLength().IsRequired();
        builder.Property(f => f.Contato).HasMaxLength(150);
        builder.Property(f => f.Telefone).HasMaxLength(20);
        builder.Property(f => f.Email).HasMaxLength(200);
        builder.Property(f => f.Avaliacao).HasPrecision(3, 2);
        builder.OwnsOne(f => f.Endereco, e => EnderecoMapeamento.Configurar(e));

        builder.HasIndex(f => new { f.EmpresaId, f.Cnpj }).IsUnique();
        builder.HasIndex(f => new { f.EmpresaId, f.RazaoSocial });
    }
}
