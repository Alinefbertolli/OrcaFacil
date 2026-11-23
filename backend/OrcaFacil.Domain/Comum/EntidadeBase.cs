namespace OrcaFacil.Domain.Comum;

/// <summary>
/// Base de todas as entidades persistidas.
/// As datas são preenchidas pelo DbContext (Infrastructure) no SaveChanges,
/// sempre em UTC, para que nenhuma camada precise lembrar de fazê-lo.
/// </summary>
public abstract class EntidadeBase
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
