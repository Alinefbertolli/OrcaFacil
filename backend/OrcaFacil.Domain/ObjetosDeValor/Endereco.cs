namespace OrcaFacil.Domain.ObjetosDeValor;

/// <summary>Endereço como objeto de valor (mapeado como owned type no EF Core).</summary>
public sealed record Endereco(
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? Cep);
