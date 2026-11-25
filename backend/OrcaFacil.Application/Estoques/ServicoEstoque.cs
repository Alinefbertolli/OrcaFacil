using FluentValidation;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Application.Estoques;

// ---------------------------------------------------------------- DTOs

/// <param name="Controlado">False = a loja ainda não controla o estoque deste produto (nenhuma entrada/ajuste feito).</param>
public sealed record EstoqueResposta(
    Guid ProdutoId, string ProdutoCodigo, string ProdutoNome, string UnidadeMedida, bool Controlado,
    decimal QuantidadeDisponivel, decimal EstoqueMinimo, bool AbaixoDoMinimo, string? Localizacao);

public sealed record MovimentarEstoqueRequisicao(decimal Quantidade, string? Motivo);

public sealed record AjustarEstoqueRequisicao(decimal SaldoContado, string Motivo);

public sealed record ConfigurarEstoqueRequisicao(decimal EstoqueMinimo, string? Localizacao);

// ---------------------------------------------------------------- Validadores

public sealed class MovimentarEstoqueValidador : AbstractValidator<MovimentarEstoqueRequisicao>
{
    public MovimentarEstoqueValidador()
    {
        RuleFor(r => r.Quantidade).GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.");
        RuleFor(r => r.Motivo).MaximumLength(500);
    }
}

public sealed class AjustarEstoqueValidador : AbstractValidator<AjustarEstoqueRequisicao>
{
    public AjustarEstoqueValidador()
    {
        RuleFor(r => r.SaldoContado).GreaterThanOrEqualTo(0).WithMessage("O saldo contado não pode ser negativo.");
        RuleFor(r => r.Motivo).NotEmpty().WithMessage("Informe o motivo do ajuste (ex.: inventário de novembro).")
            .MaximumLength(500);
    }
}

public sealed class ConfigurarEstoqueValidador : AbstractValidator<ConfigurarEstoqueRequisicao>
{
    public ConfigurarEstoqueValidador()
    {
        RuleFor(r => r.EstoqueMinimo).GreaterThanOrEqualTo(0).WithMessage("O estoque mínimo não pode ser negativo.");
        RuleFor(r => r.Localizacao).MaximumLength(Estoque.TamanhoMaximoLocalizacao);
    }
}

// ---------------------------------------------------------------- Caso de uso

/// <summary>
/// Estoque da loja. Um produto passa a ter "controle de estoque" na primeira entrada, ajuste ou configuração.
/// Produtos sem controle continuam podendo ser orçados normalmente (ex.: venda sob encomenda).
/// Toda mudança de saldo gera uma movimentação (histórico que não pode ser apagado).
/// </summary>
public sealed class ServicoEstoque(
    IRepositorioEstoques estoques,
    IRepositorio<Produto> produtos,
    IUsuarioAtual usuarioAtual,
    IUnitOfWork unidadeDeTrabalho,
    IValidator<MovimentarEstoqueRequisicao> validadorMovimento,
    IValidator<AjustarEstoqueRequisicao> validadorAjuste,
    IValidator<ConfigurarEstoqueRequisicao> validadorConfiguracao)
{
    private Guid IdUsuarioLogado =>
        usuarioAtual.UsuarioId ?? throw new NaoAutorizadoException("Faça login para continuar.");

    public Task<PaginaResultado<EstoqueResumo>> ListarAsync(ParametrosPaginacao paginacao, bool somenteAbaixoDoMinimo,
        string? busca, CancellationToken ct = default) =>
        estoques.ListarAsync(paginacao, somenteAbaixoDoMinimo, Validacao.NormalizarBusca(busca), ct);

    public async Task<EstoqueResposta> ObterAsync(Guid produtoId, CancellationToken ct = default)
    {
        var produto = await produtos.ObterPorIdOuFalharAsync(produtoId, ct);
        return Mapear(produto, await estoques.ObterPorProdutoAsync(produtoId, ct));
    }

    public async Task<EstoqueResposta> ConfigurarAsync(Guid produtoId, ConfigurarEstoqueRequisicao requisicao,
        CancellationToken ct = default)
    {
        await validadorConfiguracao.ValidateAndThrowAsync(requisicao, ct);
        var (produto, estoque) = await ObterOuCriarAsync(produtoId, ct);
        estoque.DefinirEstoqueMinimo(requisicao.EstoqueMinimo);
        estoque.Localizacao = Validacao.TextoOpcional(requisicao.Localizacao);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return Mapear(produto, estoque);
    }

    /// <summary>Entrada (compra, devolução...). Na primeira entrada o produto passa a ter controle de estoque.</summary>
    public async Task<EstoqueResposta> RegistrarEntradaAsync(Guid produtoId, MovimentarEstoqueRequisicao requisicao,
        CancellationToken ct = default)
    {
        await validadorMovimento.ValidateAndThrowAsync(requisicao, ct);
        var (produto, estoque) = await ObterOuCriarAsync(produtoId, ct);
        estoque.RegistrarEntrada(requisicao.Quantidade, IdUsuarioLogado, Validacao.TextoOpcional(requisicao.Motivo));
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return Mapear(produto, estoque);
    }

    /// <summary>Saída manual (perda, uso interno, venda fora do sistema...). Nunca deixa o saldo negativo.</summary>
    public async Task<EstoqueResposta> RegistrarSaidaAsync(Guid produtoId, MovimentarEstoqueRequisicao requisicao,
        CancellationToken ct = default)
    {
        await validadorMovimento.ValidateAndThrowAsync(requisicao, ct);
        var produto = await produtos.ObterPorIdOuFalharAsync(produtoId, ct);
        var estoque = await estoques.ObterPorProdutoAsync(produtoId, ct)
            ?? throw new DomainException(
                "Este produto ainda não tem controle de estoque: registre uma entrada ou um ajuste primeiro.");

        estoque.RegistrarSaida(requisicao.Quantidade, IdUsuarioLogado, Validacao.TextoOpcional(requisicao.Motivo));
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return Mapear(produto, estoque);
    }

    /// <summary>Inventário: informa o saldo contado na prateleira; o sistema registra a diferença.</summary>
    public async Task<EstoqueResposta> AjustarAsync(Guid produtoId, AjustarEstoqueRequisicao requisicao,
        CancellationToken ct = default)
    {
        await validadorAjuste.ValidateAndThrowAsync(requisicao, ct);
        var (produto, estoque) = await ObterOuCriarAsync(produtoId, ct);
        estoque.AjustarSaldo(requisicao.SaldoContado, IdUsuarioLogado, requisicao.Motivo);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return Mapear(produto, estoque);
    }

    public async Task<PaginaResultado<MovimentacaoResposta>> ListarMovimentacoesAsync(Guid produtoId,
        ParametrosPaginacao paginacao, CancellationToken ct = default)
    {
        if (!await produtos.ExisteAsync(p => p.Id == produtoId, ct))
            throw new NaoEncontradoException("Produto", produtoId);
        return await estoques.ListarMovimentacoesAsync(produtoId, paginacao, ct);
    }

    private async Task<(Produto Produto, Estoque Estoque)> ObterOuCriarAsync(Guid produtoId, CancellationToken ct)
    {
        var produto = await produtos.ObterPorIdOuFalharAsync(produtoId, ct);
        var estoque = await estoques.ObterPorProdutoAsync(produtoId, ct);
        if (estoque is null)
        {
            estoque = new Estoque(produtoId, estoqueMinimo: 0);
            estoques.Adicionar(estoque);
        }
        return (produto, estoque);
    }

    private static EstoqueResposta Mapear(Produto produto, Estoque? estoque) =>
        new(produto.Id, produto.Codigo, produto.Nome, produto.UnidadeMedida, estoque is not null,
            estoque?.QuantidadeDisponivel ?? 0, estoque?.EstoqueMinimo ?? 0, estoque?.AbaixoDoMinimo ?? false,
            estoque?.Localizacao);
}
