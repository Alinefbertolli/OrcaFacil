using OrcaFacil.Application.Abstracoes;

namespace OrcaFacil.API.Seguranca;

/// <summary>
/// Lê, do token JWT da requisição, quem está logado e de qual empresa ele é.
/// Um "claim" é uma informação gravada dentro do token no momento do login.
/// Sem login (ou sem o claim), devolve null — e o filtro do banco não retorna nenhum dado.
/// </summary>
public sealed class UsuarioLogadoPorToken(IHttpContextAccessor httpContextAccessor) : IEmpresaAtual, IUsuarioAtual
{
    public Guid? EmpresaId => LerGuid(ClaimsOrcaFacil.EmpresaId);

    public Guid? UsuarioId => LerGuid(ClaimsOrcaFacil.UsuarioId);

    private Guid? LerGuid(string claim)
    {
        var valor = httpContextAccessor.HttpContext?.User.FindFirst(claim)?.Value;
        return Guid.TryParse(valor, out var id) ? id : null;
    }
}
