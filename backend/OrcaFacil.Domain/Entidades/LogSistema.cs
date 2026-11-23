using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Domain.Entidades;

/// <summary>
/// Trilha de auditoria de negócio (quem fez o quê). Logs técnicos ficam no Serilog.
/// Nunca registrar senhas, tokens ou documentos completos em Detalhes.
/// EmpresaId é opcional porque alguns eventos acontecem antes de se saber a empresa
/// (ex.: tentativa de login com e-mail inexistente).
/// </summary>
public class LogSistema : EntidadeBase
{
    private LogSistema() { }

    public LogSistema(NivelLog nivel, string acao, Guid? empresaId = null, Guid? usuarioId = null,
        string? entidade = null, Guid? entidadeId = null, string? detalhesJson = null, string? ip = null)
    {
        Nivel = nivel;
        Acao = Guarda.TextoObrigatorio(acao, "Ação", 100);
        EmpresaId = empresaId;
        UsuarioId = usuarioId;
        Entidade = entidade;
        EntidadeId = entidadeId;
        DetalhesJson = detalhesJson;
        Ip = ip;
    }

    public Guid? EmpresaId { get; private set; }
    public Guid? UsuarioId { get; private set; }
    public Usuario? Usuario { get; private set; }
    public NivelLog Nivel { get; private set; }
    public string Acao { get; private set; } = null!;
    public string? Entidade { get; private set; }
    public Guid? EntidadeId { get; private set; }

    /// <summary>Persistido como jsonb.</summary>
    public string? DetalhesJson { get; private set; }
    public string? Ip { get; private set; }
}
