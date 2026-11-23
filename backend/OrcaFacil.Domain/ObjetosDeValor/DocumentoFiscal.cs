namespace OrcaFacil.Domain.ObjetosDeValor;

/// <summary>
/// Validação de CPF e CNPJ pelos dígitos verificadores (algoritmo oficial da Receita Federal).
/// Trabalha somente com dígitos; máscara é responsabilidade da interface.
/// </summary>
public static class DocumentoFiscal
{
    public static string SomenteDigitos(string? valor) =>
        valor is null ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());

    public static bool CpfValido(string? valor)
    {
        var cpf = SomenteDigitos(valor);
        if (cpf.Length != 11 || TodosDigitosIguais(cpf)) return false;

        var d1 = CalcularDigito(cpf[..9], [10, 9, 8, 7, 6, 5, 4, 3, 2]);
        var d2 = CalcularDigito(cpf[..9] + d1, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);
        return cpf[9] - '0' == d1 && cpf[10] - '0' == d2;
    }

    public static bool CnpjValido(string? valor)
    {
        var cnpj = SomenteDigitos(valor);
        if (cnpj.Length != 14 || TodosDigitosIguais(cnpj)) return false;

        var d1 = CalcularDigito(cnpj[..12], [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var d2 = CalcularDigito(cnpj[..12] + d1, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return cnpj[12] - '0' == d1 && cnpj[13] - '0' == d2;
    }

    // Soma ponderada módulo 11: resto < 2 → dígito 0; senão 11 - resto.
    private static int CalcularDigito(string baseNumerica, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++)
            soma += (baseNumerica[i] - '0') * pesos[i];

        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static bool TodosDigitosIguais(string s) => s.All(c => c == s[0]);
}
