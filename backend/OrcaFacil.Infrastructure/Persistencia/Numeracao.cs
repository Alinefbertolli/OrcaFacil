using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Infrastructure.Persistencia;

/// <summary>
/// Contador de numeração por empresa e por chave (ex.: "ORC-2026").
/// Cada empresa tem sua própria sequência, que recomeça a cada ano.
/// Não é uma entidade de domínio: é um detalhe técnico da persistência.
/// </summary>
internal sealed class ContadorNumeracao
{
    public Guid EmpresaId { get; set; }
    public string Chave { get; set; } = null!;
    public long Ultimo { get; set; }
}

internal sealed class ContadorNumeracaoConfiguration : IEntityTypeConfiguration<ContadorNumeracao>
{
    public void Configure(EntityTypeBuilder<ContadorNumeracao> builder)
    {
        builder.ToTable("ContadoresNumeracao");
        builder.HasKey(c => new { c.EmpresaId, c.Chave });
        builder.Property(c => c.Chave).HasMaxLength(30);
        builder.HasOne<Empresa>().WithMany().HasForeignKey(c => c.EmpresaId).OnDelete(DeleteBehavior.Cascade);
    }
}
