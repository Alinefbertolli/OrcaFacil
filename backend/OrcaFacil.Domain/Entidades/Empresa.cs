using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Domain.Entidades;

/// <summary>
/// Cliente do SaaS (o varejista que assina o OrçaFácil: uma papelaria, uma loja de materiais etc.).
/// Todos os dados de negócio (produtos, clientes, orçamentos, estoque...) pertencem a uma Empresa.
/// </summary>
public class Empresa : EntidadeBase
{
    private Empresa() { } // EF Core

    public Empresa(string razaoSocial, string cnpj, SegmentoEmpresa segmento, string? nomeFantasia = null)
    {
        AtualizarDados(razaoSocial, nomeFantasia, segmento);
        Cnpj = ValidarCnpj(cnpj);
        Ativa = true;
    }

    public string RazaoSocial { get; private set; } = null!;
    public string? NomeFantasia { get; private set; }

    /// <summary>Somente dígitos, validado pelos dígitos verificadores.</summary>
    public string Cnpj { get; private set; } = null!;

    /// <summary>
    /// Ramo de atuação. Serve para sugerir categorias e unidades típicas do segmento;
    /// não altera regras de negócio — o sistema é o mesmo para todos.
    /// </summary>
    public SegmentoEmpresa Segmento { get; private set; }
    public bool Ativa { get; private set; }

    /// <summary>
    /// Modo usado quando o usuário não escolhe na hora de gerar o orçamento.
    /// Ex.: papelaria → Comparativo; loja de pijamas por catálogo → Unico.
    /// </summary>
    public ModoOrcamento ModoOrcamentoPadrao { get; private set; } = ModoOrcamento.Comparativo;

    /// <summary>Nome para exibição: fantasia quando houver, senão a razão social.</summary>
    public string NomeExibicao => NomeFantasia ?? RazaoSocial;

    public void AtualizarDados(string razaoSocial, string? nomeFantasia, SegmentoEmpresa segmento)
    {
        RazaoSocial = Guarda.TextoObrigatorio(razaoSocial, "Razão social", 200);
        NomeFantasia = string.IsNullOrWhiteSpace(nomeFantasia) ? null : nomeFantasia.Trim();
        Segmento = segmento;
    }

    public void DefinirModoOrcamentoPadrao(ModoOrcamento modo)
    {
        if (!Enum.IsDefined(modo))
            throw new DomainException("Modo de orçamento inválido.");
        ModoOrcamentoPadrao = modo;
    }

    public void Desativar() => Ativa = false;
    public void Ativar() => Ativa = true;

    private static string ValidarCnpj(string cnpj)
    {
        if (!DocumentoFiscal.CnpjValido(cnpj))
            throw new DomainException("CNPJ inválido.");
        return DocumentoFiscal.SomenteDigitos(cnpj);
    }
}
