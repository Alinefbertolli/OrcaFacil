namespace OrcaFacil.Domain.Comum;

/// <summary>Marca entidades cujos dados pertencem a uma única empresa (cliente do SaaS).</summary>
public interface IPertenceAEmpresa
{
    Guid EmpresaId { get; }
}

/// <summary>
/// Base das entidades isoladas por empresa (multi-tenancy).
/// O EmpresaId é preenchido uma única vez, pelo DbContext ao salvar (com a empresa do usuário logado)
/// ou explicitamente (ex.: no cadastro de uma nova empresa). Depois disso não pode mudar:
/// um registro nunca é "transferido" de uma empresa para outra.
/// </summary>
public abstract class EntidadeDaEmpresa : EntidadeBase, IPertenceAEmpresa
{
    public Guid EmpresaId { get; private set; }

    public void VincularEmpresa(Guid empresaId)
    {
        if (empresaId == Guid.Empty)
            throw new DomainException("Empresa inválida.");

        if (EmpresaId != Guid.Empty && EmpresaId != empresaId)
            throw new DomainException("O registro já pertence a outra empresa e não pode ser transferido.");

        EmpresaId = empresaId;
    }

    /// <summary>Repassa a empresa deste registro a um registro filho criado por ele (se já conhecida).</summary>
    protected void PropagarEmpresaPara(EntidadeDaEmpresa filho)
    {
        if (EmpresaId != Guid.Empty)
            filho.VincularEmpresa(EmpresaId);
    }
}
