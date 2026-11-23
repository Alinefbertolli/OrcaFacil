using FluentValidation;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Application.Cadastros;

// ---------------------------------------------------------------- DTOs

/// <param name="PrecoVenda">Preço de VENDA da loja (o que o cliente final paga). Vazio = "sem preço" no orçamento.</param>
/// <param name="GrupoEquivalencia">(Opcional) produtos com o mesmo grupo são substitutos no orçamento comparativo.
/// Ex.: "caderno 10 materias".</param>
public sealed record ProdutoRequisicao(
    string Codigo,
    string Nome,
    string? Descricao,
    string UnidadeMedida,
    Guid CategoriaId,
    Guid? MarcaId,
    decimal? PrecoVenda,
    string? GrupoEquivalencia = null);

public sealed record FiltroProdutos(string? Busca, Guid? CategoriaId, Guid? MarcaId, bool IncluirInativos = false);

public sealed record ProdutoResumo(
    Guid Id, string Codigo, string Nome, string UnidadeMedida,
    Guid CategoriaId, string? CategoriaNome, Guid? MarcaId, string? MarcaNome,
    decimal? PrecoVenda, string? GrupoEquivalencia, bool Ativo);

public sealed record ProdutoDetalhe(
    Guid Id, string Codigo, string Nome, string? Descricao, string UnidadeMedida,
    Guid CategoriaId, string? CategoriaNome, Guid? MarcaId, string? MarcaNome,
    decimal? PrecoVenda, string? GrupoEquivalencia, bool Ativo,
    IReadOnlyList<ProdutoFornecedorResposta> Fornecedores);

/// <summary>Um fornecedor que vende o produto, com a tabela de preço dele.</summary>
public sealed record ProdutoFornecedorResposta(
    Guid FornecedorId, string? FornecedorNome, string? CodigoNoFornecedor,
    decimal? PrecoAtual, int? PrazoEntregaDias, bool Disponivel, DateTime? PrecoAtualizadoEm);

public sealed record VinculoFornecedorRequisicao(decimal? Preco, string? CodigoNoFornecedor, int? PrazoEntregaDias,
    bool Disponivel = true);

public sealed record HistoricoPrecoResposta(decimal Preco, OrigemPreco Origem, Guid? FornecedorId, string? FornecedorNome,
    DateTime RegistradoEm);

// ---------------------------------------------------------------- Validadores

public sealed class ProdutoValidador : AbstractValidator<ProdutoRequisicao>
{
    public ProdutoValidador()
    {
        RuleFor(r => r.Codigo).NotEmpty().WithMessage("Informe o código do produto.").MaximumLength(50);
        RuleFor(r => r.Nome).NotEmpty().WithMessage("Informe o nome do produto.").MaximumLength(200);
        RuleFor(r => r.Descricao).MaximumLength(2000);
        RuleFor(r => r.UnidadeMedida).NotEmpty().WithMessage("Informe a unidade de medida (ex.: UN, CX, KG).")
            .MaximumLength(10);
        RuleFor(r => r.CategoriaId).NotEmpty().WithMessage("Informe a categoria.");
        RuleFor(r => r.PrecoVenda).GreaterThanOrEqualTo(0).WithMessage("O preço não pode ser negativo.");
        RuleFor(r => r.GrupoEquivalencia).MaximumLength(Produto.TamanhoMaximoGrupo);
    }
}

public sealed class VinculoFornecedorValidador : AbstractValidator<VinculoFornecedorRequisicao>
{
    public VinculoFornecedorValidador()
    {
        RuleFor(r => r.Preco).GreaterThanOrEqualTo(0).WithMessage("O preço não pode ser negativo.");
        RuleFor(r => r.CodigoNoFornecedor).MaximumLength(50);
        RuleFor(r => r.PrazoEntregaDias).InclusiveBetween(0, 365).WithMessage("O prazo deve ser de 0 a 365 dias.");
    }
}

// ---------------------------------------------------------------- Casos de uso

public sealed class ServicoProdutos(
    IRepositorio<Produto> produtos,
    IRepositorio<Categoria> categorias,
    IRepositorio<Marca> marcas,
    IRepositorio<Fornecedor> fornecedores,
    IRepositorio<ProdutoFornecedor> vinculos,
    IRepositorio<HistoricoPreco> historicos,
    IUnitOfWork unidadeDeTrabalho,
    TimeProvider relogio,
    IValidator<ProdutoRequisicao> validador,
    IValidator<VinculoFornecedorRequisicao> validadorVinculo)
{
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    public async Task<PaginaResultado<ProdutoResumo>> ListarAsync(FiltroProdutos filtro, ParametrosPaginacao paginacao,
        CancellationToken ct = default)
    {
        // Copiamos os filtros para variáveis locais: o EF transforma cada uma em parâmetro do SQL.
        var termo = Validacao.NormalizarBusca(filtro.Busca);
        var categoriaId = filtro.CategoriaId;
        var marcaId = filtro.MarcaId;
        var incluirInativos = filtro.IncluirInativos;

        var pagina = await produtos.ListarAsync(paginacao,
            filtro: p =>
                (termo == "" || p.Nome.ToLower().Contains(termo) || p.Codigo.ToLower().Contains(termo)) &&
                (categoriaId == null || p.CategoriaId == categoriaId) &&
                (marcaId == null || p.MarcaId == marcaId) &&
                (incluirInativos || p.Ativo),
            ordenarPor: p => p.Nome, cancellationToken: ct);

        var nomesCategorias = await NomesAsync(categorias, pagina.Itens.Select(p => p.CategoriaId), c => c.Nome, ct);
        var nomesMarcas = await NomesAsync(marcas, pagina.Itens.Where(p => p.MarcaId.HasValue).Select(p => p.MarcaId!.Value),
            m => m.Nome, ct);

        return pagina.Mapear(p => new ProdutoResumo(p.Id, p.Codigo, p.Nome, p.UnidadeMedida,
            p.CategoriaId, nomesCategorias.GetValueOrDefault(p.CategoriaId),
            p.MarcaId, p.MarcaId is Guid m ? nomesMarcas.GetValueOrDefault(m) : null,
            p.PrecoVenda, p.GrupoEquivalencia, p.Ativo));
    }

    public async Task<ProdutoDetalhe> ObterPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var produto = await produtos.ObterPorIdOuFalharAsync(id, ct);
        var categoria = await categorias.ObterPorIdAsync(produto.CategoriaId, ct);
        var marca = produto.MarcaId is Guid marcaId ? await marcas.ObterPorIdAsync(marcaId, ct) : null;

        var vinculosDoProduto = await vinculos.ListarTodosAsync(v => v.ProdutoId == id, ct);
        var nomesFornecedores = await NomesAsync(fornecedores, vinculosDoProduto.Select(v => v.FornecedorId),
            f => f.NomeFantasia ?? f.RazaoSocial, ct);

        var listaFornecedores = vinculosDoProduto
            .Select(v => new ProdutoFornecedorResposta(v.FornecedorId, nomesFornecedores.GetValueOrDefault(v.FornecedorId),
                v.CodigoNoFornecedor, v.PrecoAtual, v.PrazoEntregaDias, v.Disponivel, v.PrecoAtualizadoEm))
            .OrderBy(f => f.PrecoAtual ?? decimal.MaxValue) // mais barato primeiro
            .ToList();

        return new ProdutoDetalhe(produto.Id, produto.Codigo, produto.Nome, produto.Descricao, produto.UnidadeMedida,
            produto.CategoriaId, categoria?.Nome, produto.MarcaId, marca?.Nome,
            produto.PrecoVenda, produto.GrupoEquivalencia, produto.Ativo, listaFornecedores);
    }

    public async Task<ProdutoDetalhe> CriarAsync(ProdutoRequisicao requisicao, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(requisicao, ct);
        var produto = new Produto();
        await PreencherAsync(produto, requisicao, ct);
        produtos.Adicionar(produto);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await ObterPorIdAsync(produto.Id, ct);
    }

    public async Task<ProdutoDetalhe> AtualizarAsync(Guid id, ProdutoRequisicao requisicao, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(requisicao, ct);
        var produto = await produtos.ObterPorIdOuFalharAsync(id, ct);
        await PreencherAsync(produto, requisicao, ct);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await ObterPorIdAsync(produto.Id, ct);
    }

    /// <summary>
    /// "Exclusão lógica": o produto some das listagens, mas continua no banco,
    /// porque orçamentos e movimentações antigas apontam para ele.
    /// </summary>
    public async Task DesativarAsync(Guid id, CancellationToken ct = default)
    {
        var produto = await produtos.ObterPorIdOuFalharAsync(id, ct);
        produto.Ativo = false;
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    public async Task ReativarAsync(Guid id, CancellationToken ct = default)
    {
        var produto = await produtos.ObterPorIdOuFalharAsync(id, ct);
        produto.Ativo = true;
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    /// <summary>
    /// Liga um fornecedor ao produto (ou atualiza a ligação) com a tabela de preço dele.
    /// Toda mudança de preço fica guardada no histórico.
    /// </summary>
    public async Task<ProdutoDetalhe> VincularFornecedorAsync(Guid produtoId, Guid fornecedorId,
        VinculoFornecedorRequisicao requisicao, CancellationToken ct = default)
    {
        await validadorVinculo.ValidateAndThrowAsync(requisicao, ct);
        await produtos.ObterPorIdOuFalharAsync(produtoId, ct);
        await fornecedores.ObterPorIdOuFalharAsync(fornecedorId, ct);

        var vinculo = await vinculos.ObterAsync(v => v.ProdutoId == produtoId && v.FornecedorId == fornecedorId, ct);
        if (vinculo is null)
        {
            vinculo = new ProdutoFornecedor(produtoId, fornecedorId);
            vinculos.Adicionar(vinculo);
        }

        vinculo.CodigoNoFornecedor = Validacao.TextoOpcional(requisicao.CodigoNoFornecedor);
        vinculo.PrazoEntregaDias = requisicao.PrazoEntregaDias;
        vinculo.Disponivel = requisicao.Disponivel;

        // Só registra histórico quando o preço realmente muda.
        if (requisicao.Preco is decimal preco && preco != vinculo.PrecoAtual)
            historicos.Adicionar(vinculo.AtualizarPreco(preco, Agora));

        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await ObterPorIdAsync(produtoId, ct);
    }

    public async Task RemoverFornecedorAsync(Guid produtoId, Guid fornecedorId, CancellationToken ct = default)
    {
        var vinculo = await vinculos.ObterAsync(v => v.ProdutoId == produtoId && v.FornecedorId == fornecedorId, ct)
            ?? throw new NaoEncontradoException("Vínculo entre produto e fornecedor", $"{produtoId}/{fornecedorId}");

        vinculos.Remover(vinculo); // o histórico de preços é mantido
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    public async Task<PaginaResultado<HistoricoPrecoResposta>> ListarHistoricoPrecosAsync(Guid produtoId,
        ParametrosPaginacao paginacao, CancellationToken ct = default)
    {
        await produtos.ObterPorIdOuFalharAsync(produtoId, ct);

        var pagina = await historicos.ListarAsync(paginacao, filtro: h => h.ProdutoId == produtoId,
            ordenarPor: h => h.RegistradoEm, decrescente: true, cancellationToken: ct);

        var nomes = await NomesAsync(fornecedores,
            pagina.Itens.Where(h => h.FornecedorId.HasValue).Select(h => h.FornecedorId!.Value),
            f => f.NomeFantasia ?? f.RazaoSocial, ct);

        return pagina.Mapear(h => new HistoricoPrecoResposta(h.Preco, h.Origem, h.FornecedorId,
            h.FornecedorId is Guid f ? nomes.GetValueOrDefault(f) : null, h.RegistradoEm));
    }

    private async Task PreencherAsync(Produto produto, ProdutoRequisicao requisicao, CancellationToken ct)
    {
        var codigo = requisicao.Codigo.Trim();
        var codigoMinusculo = codigo.ToLower();
        var idAtual = produto.Id;

        if (await produtos.ExisteAsync(p => p.Id != idAtual && p.Codigo.ToLower() == codigoMinusculo, ct))
            throw new ConflitoException($"Já existe um produto com o código \"{codigo}\".");

        if (!await categorias.ExisteAsync(c => c.Id == requisicao.CategoriaId, ct))
            throw Validacao.Erro(nameof(ProdutoRequisicao.CategoriaId), "Categoria não encontrada.");

        if (requisicao.MarcaId is Guid marcaId && !await marcas.ExisteAsync(m => m.Id == marcaId, ct))
            throw Validacao.Erro(nameof(ProdutoRequisicao.MarcaId), "Marca não encontrada.");

        produto.Codigo = codigo;
        produto.Nome = requisicao.Nome.Trim();
        produto.Descricao = Validacao.TextoOpcional(requisicao.Descricao);
        produto.UnidadeMedida = requisicao.UnidadeMedida.Trim().ToUpperInvariant();
        produto.CategoriaId = requisicao.CategoriaId;
        produto.MarcaId = requisicao.MarcaId;

        produto.DefinirGrupoEquivalencia(requisicao.GrupoEquivalencia);

        // Preço de venda: toda mudança fica no histórico (sem fornecedor = preço da própria loja).
        if (requisicao.PrecoVenda != produto.PrecoVenda)
        {
            produto.DefinirPrecoVenda(requisicao.PrecoVenda);

            if (requisicao.PrecoVenda is decimal preco)
                historicos.Adicionar(new HistoricoPreco(produto.Id, null, preco, OrigemPreco.PrecoVenda, Agora));
        }
    }

    /// <summary>Busca, numa única consulta, o "nome" de vários registros pelos Ids.</summary>
    private static async Task<Dictionary<Guid, string>> NomesAsync<T>(IRepositorio<T> repositorio, IEnumerable<Guid> ids,
        Func<T, string> nome, CancellationToken ct) where T : EntidadeBase
    {
        var registros = await repositorio.ListarPorIdsAsync(ids, ct);
        return registros.ToDictionary(r => r.Id, nome);
    }
}
