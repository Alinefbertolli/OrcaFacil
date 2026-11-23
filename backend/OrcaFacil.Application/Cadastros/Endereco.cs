using FluentValidation;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Application.Cadastros;

/// <summary>Validação e padronização do endereço (usado em Fornecedor e Cliente).</summary>
public sealed class EnderecoValidador : AbstractValidator<Endereco>
{
    public EnderecoValidador()
    {
        RuleFor(e => e.Logradouro).MaximumLength(200);
        RuleFor(e => e.Numero).MaximumLength(20);
        RuleFor(e => e.Complemento).MaximumLength(100);
        RuleFor(e => e.Bairro).MaximumLength(100);
        RuleFor(e => e.Cidade).MaximumLength(100);
        RuleFor(e => e.Uf).Length(2).WithMessage("A UF deve ter 2 letras (ex.: SP).")
            .When(e => !string.IsNullOrWhiteSpace(e.Uf));
        RuleFor(e => e.Cep).Must(cep => DocumentoFiscal.SomenteDigitos(cep).Length == 8)
            .WithMessage("O CEP deve ter 8 números.")
            .When(e => !string.IsNullOrWhiteSpace(e.Cep));
    }
}

public static class EnderecoPadronizado
{
    /// <summary>
    /// Remove espaços, põe a UF em maiúsculas e deixa o CEP só com números.
    /// Um endereço todo em branco vira null (o cadastro fica "sem endereço").
    /// </summary>
    public static Endereco? Padronizar(Endereco? endereco)
    {
        if (endereco is null)
            return null;

        var resultado = new Endereco(
            Limpar(endereco.Logradouro), Limpar(endereco.Numero), Limpar(endereco.Complemento),
            Limpar(endereco.Bairro), Limpar(endereco.Cidade), Limpar(endereco.Uf)?.ToUpperInvariant(),
            string.IsNullOrWhiteSpace(endereco.Cep) ? null : DocumentoFiscal.SomenteDigitos(endereco.Cep));

        var vazio = resultado is { Logradouro: null, Numero: null, Complemento: null, Bairro: null, Cidade: null, Uf: null, Cep: null };
        return vazio ? null : resultado;
    }

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
