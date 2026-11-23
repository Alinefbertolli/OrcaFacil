namespace OrcaFacil.Application.Comum;

/// <summary>
/// Página pedida pelo cliente da API (ex.: ?pagina=2&amp;tamanhoPagina=20).
/// Valores fora dos limites são corrigidos em vez de gerar erro: página mínima 1, tamanho entre 1 e 100.
/// O limite máximo protege o servidor de alguém pedir "um milhão de linhas de uma vez".
/// </summary>
public sealed record ParametrosPaginacao
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;

    public ParametrosPaginacao(int pagina = 1, int tamanhoPagina = TamanhoPadrao)
    {
        Pagina = Math.Max(1, pagina);
        TamanhoPagina = Math.Clamp(tamanhoPagina, 1, TamanhoMaximo);
    }

    public int Pagina { get; }
    public int TamanhoPagina { get; }

    /// <summary>Quantos registros pular para chegar a esta página (ex.: página 3 de 20 → pula 40).</summary>
    public int Saltar => (Pagina - 1) * TamanhoPagina;
}

/// <summary>Resultado de uma listagem paginada, com as informações que a tela precisa para montar a navegação.</summary>
public sealed record PaginaResultado<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, long TotalItens)
{
    public int TotalPaginas => TotalItens == 0 ? 0 : (int)Math.Ceiling(TotalItens / (double)TamanhoPagina);
    public bool TemAnterior => Pagina > 1;
    public bool TemProxima => Pagina < TotalPaginas;

    /// <summary>Converte os itens (ex.: entidade → DTO) mantendo as informações de paginação.</summary>
    public PaginaResultado<TDestino> Mapear<TDestino>(Func<T, TDestino> conversor) =>
        new(Itens.Select(conversor).ToList(), Pagina, TamanhoPagina, TotalItens);
}
