namespace OrcaFacil.Domain.Comum;

/// <summary>Verificações de invariantes reutilizadas pelas entidades.</summary>
public static class Guarda
{
    public static string TextoObrigatorio(string? valor, string campo, int tamanhoMaximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new DomainException($"{campo} é obrigatório.");

        var texto = valor.Trim();
        if (texto.Length > tamanhoMaximo)
            throw new DomainException($"{campo} deve ter no máximo {tamanhoMaximo} caracteres.");

        return texto;
    }

    public static decimal Positivo(decimal valor, string campo)
    {
        if (valor <= 0)
            throw new DomainException($"{campo} deve ser maior que zero.");
        return valor;
    }

    public static decimal NaoNegativo(decimal valor, string campo)
    {
        if (valor < 0)
            throw new DomainException($"{campo} não pode ser negativo.");
        return valor;
    }
}
