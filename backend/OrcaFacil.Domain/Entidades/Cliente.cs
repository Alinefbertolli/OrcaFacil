using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Domain.Entidades;

public class Cliente : EntidadeDaEmpresa
{
    public string Nome { get; set; } = null!;
    public TipoPessoa TipoPessoa { get; set; }

    /// <summary>CPF ou CNPJ, somente dígitos. Opcional (orçamento rápido para pessoa sem cadastro completo).</summary>
    public string? Documento { get; private set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public Endereco? Endereco { get; set; }
    public string? Observacoes { get; set; }
    public bool Ativo { get; set; } = true;

    public ICollection<Orcamento> Orcamentos { get; private set; } = [];

    public void DefinirDocumento(string? documento)
    {
        var digitos = DocumentoFiscal.SomenteDigitos(documento);
        if (digitos.Length == 0)
        {
            Documento = null;
            return;
        }

        var valido = TipoPessoa == TipoPessoa.Fisica
            ? DocumentoFiscal.CpfValido(digitos)
            : DocumentoFiscal.CnpjValido(digitos);

        if (!valido)
            throw new DomainException(TipoPessoa == TipoPessoa.Fisica ? "CPF inválido." : "CNPJ inválido.");

        Documento = digitos;
    }
}
