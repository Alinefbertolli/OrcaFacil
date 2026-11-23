using FluentValidation;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Application.Cadastros;

public sealed record CategoriaRequisicao(string Nome, string? Descricao, Guid? CategoriaPaiId);

public sealed record CategoriaResposta(Guid Id, string Nome, string? Descricao, Guid? CategoriaPaiId, string? CategoriaPaiNome)
{
    public static CategoriaResposta De(Categoria c, string? nomePai) =>
        new(c.Id, c.Nome, c.Descricao, c.CategoriaPaiId, nomePai);
}

public sealed class CategoriaValidador : AbstractValidator<CategoriaRequisicao>
{
    public CategoriaValidador()
    {
        RuleFor(r => r.Nome).NotEmpty().WithMessage("Informe o nome da categoria.").MaximumLength(100);
        RuleFor(r => r.Descricao).MaximumLength(500);
    }
}

/// <summary>
/// Categorias de produtos. Podem ter subcategorias (ex.: "Escrita" → "Canetas").
/// Cada empresa tem as suas: o filtro por empresa é automático.
/// </summary>
public sealed class ServicoCategorias(
    IRepositorio<Categoria> categorias,
    IRepositorio<Produto> produtos,
    IUnitOfWork unidadeDeTrabalho,
    IValidator<CategoriaRequisicao> validador)
{
    public async Task<PaginaResultado<CategoriaResposta>> ListarAsync(ParametrosPaginacao paginacao, string? busca,
        CancellationToken ct = default)
    {
        var termo = Validacao.NormalizarBusca(busca);
        var pagina = await categorias.ListarAsync(paginacao,
            filtro: c => termo == "" || c.Nome.ToLower().Contains(termo),
            ordenarPor: c => c.Nome, cancellationToken: ct);

        // Nomes das categorias "pai" com UMA consulta só (e não uma por item).
        var pais = await categorias.ListarPorIdsAsync(
            pagina.Itens.Where(c => c.CategoriaPaiId.HasValue).Select(c => c.CategoriaPaiId!.Value), ct);
        var nomes = pais.ToDictionary(p => p.Id, p => p.Nome);

        return pagina.Mapear(c => CategoriaResposta.De(c,
            c.CategoriaPaiId is Guid paiId && nomes.TryGetValue(paiId, out var nome) ? nome : null));
    }

    public async Task<CategoriaResposta> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        await MontarRespostaAsync(await categorias.ObterPorIdOuFalharAsync(id, ct), ct);

    public async Task<CategoriaResposta> CriarAsync(CategoriaRequisicao requisicao, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(requisicao, ct);

        var categoria = new Categoria();
        await PreencherAsync(categoria, requisicao, ct);
        categorias.Adicionar(categoria);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await MontarRespostaAsync(categoria, ct);
    }

    public async Task<CategoriaResposta> AtualizarAsync(Guid id, CategoriaRequisicao requisicao, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(requisicao, ct);

        var categoria = await categorias.ObterPorIdOuFalharAsync(id, ct);
        await PreencherAsync(categoria, requisicao, ct);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await MontarRespostaAsync(categoria, ct);
    }

    /// <summary>Exclusão definitiva, permitida só para categorias que não estão em uso.</summary>
    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var categoria = await categorias.ObterPorIdOuFalharAsync(id, ct);

        if (await produtos.ExisteAsync(p => p.CategoriaId == id, ct))
            throw new ConflitoException("A categoria possui produtos. Mova os produtos para outra categoria antes de excluí-la.");
        if (await categorias.ExisteAsync(c => c.CategoriaPaiId == id, ct))
            throw new ConflitoException("A categoria possui subcategorias. Exclua ou mova as subcategorias antes.");

        categorias.Remover(categoria);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    private async Task PreencherAsync(Categoria categoria, CategoriaRequisicao requisicao, CancellationToken ct)
    {
        var nome = requisicao.Nome.Trim();
        var nomeMinusculo = nome.ToLower();
        var idAtual = categoria.Id;

        if (await categorias.ExisteAsync(c => c.Id != idAtual && c.Nome.ToLower() == nomeMinusculo, ct))
            throw new ConflitoException($"Já existe uma categoria chamada \"{nome}\".");

        await ValidarCategoriaPaiAsync(categoria.Id, requisicao.CategoriaPaiId, ct);

        categoria.Nome = nome;
        categoria.Descricao = Validacao.TextoOpcional(requisicao.Descricao);
        categoria.CategoriaPaiId = requisicao.CategoriaPaiId;
    }

    /// <summary>
    /// A categoria pai precisa existir e não pode criar um "ciclo"
    /// (A dentro de B e B dentro de A). Para isso subimos a árvore a partir do pai.
    /// </summary>
    private async Task ValidarCategoriaPaiAsync(Guid idCategoria, Guid? idPai, CancellationToken ct)
    {
        if (idPai is null)
            return;

        if (idPai == idCategoria)
            throw new DomainException("Uma categoria não pode ser pai de si mesma.");

        var atual = await categorias.ObterPorIdAsync(idPai.Value, ct)
            ?? throw Validacao.Erro(nameof(CategoriaRequisicao.CategoriaPaiId), "Categoria pai não encontrada.");

        for (var nivel = 0; atual?.CategoriaPaiId is Guid acima && nivel < 50; nivel++)
        {
            if (acima == idCategoria)
                throw new DomainException("Essa escolha criaria um ciclo: a categoria ficaria dentro dela mesma.");
            atual = await categorias.ObterPorIdAsync(acima, ct);
        }
    }

    private async Task<CategoriaResposta> MontarRespostaAsync(Categoria categoria, CancellationToken ct)
    {
        string? nomePai = null;
        if (categoria.CategoriaPaiId is Guid paiId)
            nomePai = (await categorias.ObterPorIdAsync(paiId, ct))?.Nome;
        return CategoriaResposta.De(categoria, nomePai);
    }
}
