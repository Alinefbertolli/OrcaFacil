using FluentValidation;
using Microsoft.Extensions.Logging;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Application.Importacao;

// ---------------------------------------------------------------- DTOs

/// <param name="Formato">Texto (digitado) ou WhatsApp (colado de uma conversa).</param>
public sealed record ImportarTextoRequisicao(string Texto, FormatoImportacao Formato = FormatoImportacao.Texto, Guid? ClienteId = null);

public sealed record ItemListaRequisicao(string Descricao, decimal Quantidade, string UnidadeMedida, string? Marca, Guid? ProdutoId);

public sealed record ItemImportadoResposta(
    Guid Id, int Ordem, string? TextoOriginal, string Descricao, decimal Quantidade, string UnidadeMedida, string? Marca,
    Guid? ProdutoId, string? ProdutoCodigo, string? ProdutoNome, decimal Confianca, bool PrecisaAtencao);

public sealed record ListaImportadaResposta(
    Guid Id, FormatoImportacao Formato, string? NomeArquivoOriginal, StatusImportacao Status, string? MensagemErro,
    Guid? ClienteId, DateTime CriadoEm, int TotalItens, int ItensQuePrecisamAtencao,
    IReadOnlyList<ItemImportadoResposta> Itens);

public sealed record ListaImportadaResumo(
    Guid Id, FormatoImportacao Formato, string? NomeArquivoOriginal, StatusImportacao Status, Guid? ClienteId, DateTime CriadoEm);

// ---------------------------------------------------------------- Validadores

public sealed class ImportarTextoValidador : AbstractValidator<ImportarTextoRequisicao>
{
    public ImportarTextoValidador()
    {
        RuleFor(r => r.Texto).NotEmpty().WithMessage("Cole ou digite a lista.")
            .MaximumLength(100_000).WithMessage("O texto é grande demais (máximo de 100 mil caracteres).");
        RuleFor(r => r.Formato).Must(f => f is FormatoImportacao.Texto or FormatoImportacao.WhatsApp)
            .WithMessage("Para texto, use o formato Texto ou WhatsApp. Arquivos vão em /importacoes/arquivo.");
    }
}

public sealed class ItemListaValidador : AbstractValidator<ItemListaRequisicao>
{
    public ItemListaValidador()
    {
        RuleFor(r => r.Descricao).NotEmpty().WithMessage("Informe a descrição.").MaximumLength(300);
        RuleFor(r => r.Quantidade).GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.");
        RuleFor(r => r.UnidadeMedida).NotEmpty().WithMessage("Informe a unidade.").MaximumLength(10);
        RuleFor(r => r.Marca).MaximumLength(100);
    }
}

// ---------------------------------------------------------------- Caso de uso

/// <summary>
/// Importação inteligente de listas:
/// recebe → extrai o texto → interpreta as linhas → sugere produtos do catálogo → aguarda REVISÃO.
/// Nenhum orçamento é gerado sem que o usuário confirme a revisão (regra do domínio).
/// </summary>
public sealed class ServicoImportacao(
    IRepositorio<ListaImportada> listas,
    IRepositorioListasImportadas listasComItens,
    IRepositorio<Marca> marcas,
    IRepositorio<Produto> produtos,
    IRepositorio<Cliente> clientes,
    IExtratorTexto extrator,
    IArmazenamentoArquivos armazenamento,
    IBuscaProdutosSimilares buscaSimilares,
    IUsuarioAtual usuarioAtual,
    IUnitOfWork unidadeDeTrabalho,
    IValidator<ImportarTextoRequisicao> validadorTexto,
    IValidator<ItemListaRequisicao> validadorItem,
    ILogger<ServicoImportacao> logger)
{
    /// <summary>A partir desta similaridade (0 a 1), o produto do catálogo é sugerido automaticamente.</summary>
    public const double SimilaridadeMinimaParaSugerir = 0.5;
    public const int TamanhoMaximoArquivoMb = ValidadorArquivo.TamanhoMaximoPadraoMb;

    private Guid IdUsuarioLogado =>
        usuarioAtual.UsuarioId ?? throw new NaoAutorizadoException("Faça login para continuar.");

    public async Task<ListaImportadaResposta> ImportarTextoAsync(ImportarTextoRequisicao requisicao, CancellationToken ct = default)
    {
        await validadorTexto.ValidateAndThrowAsync(requisicao, ct);
        await ValidarClienteAsync(requisicao.ClienteId, ct);

        var lista = new ListaImportada(IdUsuarioLogado, requisicao.Formato, nomeArquivoOriginal: null, requisicao.ClienteId);
        listas.Adicionar(lista);
        await InterpretarEConcluirAsync(lista, requisicao.Texto, ct);
        return await MontarRespostaAsync(lista, ct);
    }

    public async Task<ListaImportadaResposta> ImportarArquivoAsync(ArquivoEnviado arquivo, Guid? clienteId, CancellationToken ct = default)
    {
        await ValidarClienteAsync(clienteId, ct);

        // Copiamos para a memória: o arquivo é lido mais de uma vez (assinatura, armazenamento, extração).
        using var memoria = new MemoryStream();
        await arquivo.Conteudo.CopyToAsync(memoria, ct);
        var bytes = memoria.ToArray();

        var formato = ValidadorArquivo.Validar(arquivo.NomeArquivo, bytes.LongLength,
            bytes.AsSpan(0, Math.Min(bytes.Length, 1024)), TamanhoMaximoArquivoMb);

        var nomeOriginal = Path.GetFileName(arquivo.NomeArquivo);
        var lista = new ListaImportada(IdUsuarioLogado, formato, nomeOriginal, clienteId);
        lista.CaminhoArmazenamento = await armazenamento.SalvarAsync(new MemoryStream(bytes), Path.GetExtension(nomeOriginal), ct);
        listas.Adicionar(lista);

        string texto;
        try
        {
            texto = await extrator.ExtrairAsync(new MemoryStream(bytes), formato, ct);
        }
        catch (DomainException erro)
        {
            // Guardamos a tentativa com status "Erro" (fica no histórico) e avisamos o usuário.
            lista.RegistrarFalha(erro.Message);
            await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
            logger.LogWarning("Não foi possível ler o arquivo {Arquivo}: {Motivo}", nomeOriginal, erro.Message);
            throw;
        }

        await InterpretarEConcluirAsync(lista, texto, ct);
        return await MontarRespostaAsync(lista, ct);
    }

    public async Task<PaginaResultado<ListaImportadaResumo>> ListarAsync(ParametrosPaginacao paginacao, StatusImportacao? status,
        CancellationToken ct = default)
    {
        var pagina = await listas.ListarAsync(paginacao,
            filtro: l => status == null || l.Status == status, cancellationToken: ct);
        return pagina.Mapear(l => new ListaImportadaResumo(l.Id, l.Formato, l.NomeArquivoOriginal, l.Status, l.ClienteId, l.CriadoEm));
    }

    public async Task<ListaImportadaResposta> ObterAsync(Guid id, CancellationToken ct = default) =>
        await MontarRespostaAsync(await ObterListaAsync(id, ct), ct);

    public async Task<ListaImportadaResposta> AtualizarItemAsync(Guid listaId, Guid itemId, ItemListaRequisicao requisicao,
        CancellationToken ct = default)
    {
        await validadorItem.ValidateAndThrowAsync(requisicao, ct);
        await ValidarProdutoAsync(requisicao.ProdutoId, ct);

        var lista = await ObterListaAsync(listaId, ct);
        lista.EditarItem(itemId, requisicao.Descricao, requisicao.Quantidade, requisicao.UnidadeMedida, requisicao.Marca,
            requisicao.ProdutoId);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await MontarRespostaAsync(lista, ct);
    }

    public async Task<ListaImportadaResposta> AdicionarItemAsync(Guid listaId, ItemListaRequisicao requisicao, CancellationToken ct = default)
    {
        await validadorItem.ValidateAndThrowAsync(requisicao, ct);
        await ValidarProdutoAsync(requisicao.ProdutoId, ct);

        var lista = await ObterListaAsync(listaId, ct);
        var item = lista.AdicionarItemManual(requisicao.Descricao, requisicao.Quantidade, requisicao.UnidadeMedida, requisicao.Marca);
        if (requisicao.ProdutoId is Guid produtoId)
            item.SugerirProduto(produtoId, 1m); // escolhido pelo usuário: confiança total

        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await MontarRespostaAsync(lista, ct);
    }

    public async Task<ListaImportadaResposta> ExcluirItemAsync(Guid listaId, Guid itemId, CancellationToken ct = default)
    {
        var lista = await ObterListaAsync(listaId, ct);
        lista.ExcluirItem(itemId);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await MontarRespostaAsync(lista, ct);
    }

    /// <summary>O usuário revisou tudo: a lista fica liberada para gerar orçamentos.</summary>
    public async Task<ListaImportadaResposta> ConfirmarRevisaoAsync(Guid listaId, CancellationToken ct = default)
    {
        var lista = await ObterListaAsync(listaId, ct);
        lista.ConfirmarRevisao();
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await MontarRespostaAsync(lista, ct);
    }

    /// <summary>Até 5 produtos do catálogo parecidos com o item, para o usuário escolher na revisão.</summary>
    public async Task<IReadOnlyList<ProdutoSimilar>> SugerirProdutosAsync(Guid listaId, Guid itemId, CancellationToken ct = default)
    {
        var lista = await ObterListaAsync(listaId, ct);
        var item = lista.Itens.FirstOrDefault(i => i.Id == itemId && !i.Excluido)
            ?? throw new NaoEncontradoException("Item da lista", itemId);
        return await buscaSimilares.BuscarAsync(item.Descricao, 5, ct);
    }

    // ---------------------------------------------------------------- auxiliares

    private async Task InterpretarEConcluirAsync(ListaImportada lista, string texto, CancellationToken ct)
    {
        var nomesMarcas = (await marcas.ListarTodosAsync(_ => true, ct)).Select(m => m.Nome).ToList();
        var interpretados = InterpretadorLista.Interpretar(texto, nomesMarcas);

        var itens = new List<ItemListaImportada>();
        foreach (var interpretado in interpretados)
        {
            var item = new ItemListaImportada(interpretado.Descricao, interpretado.Quantidade, interpretado.UnidadeMedida,
                interpretado.Marca, interpretado.TextoOriginal, interpretado.Confianca);

            // Procura no catálogo um produto com nome parecido.
            var melhor = (await buscaSimilares.BuscarAsync(interpretado.Descricao, 1, ct)).FirstOrDefault();
            if (melhor is not null && melhor.Similaridade >= SimilaridadeMinimaParaSugerir)
                item.SugerirProduto(melhor.Id, Math.Min(interpretado.Confianca + 0.15m, 0.95m));

            itens.Add(item);
        }

        lista.ConcluirExtracao(texto, itens);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);

        logger.LogInformation("Lista {ListaId} importada ({Formato}): {Quantidade} itens reconhecidos.",
            lista.Id, lista.Formato, itens.Count);
    }

    private async Task<ListaImportada> ObterListaAsync(Guid id, CancellationToken ct) =>
        await listasComItens.ObterComItensAsync(id, ct) ?? throw new NaoEncontradoException("Lista importada", id);

    private async Task ValidarClienteAsync(Guid? clienteId, CancellationToken ct)
    {
        if (clienteId is Guid id && !await clientes.ExisteAsync(c => c.Id == id, ct))
            throw Validacao.Erro("ClienteId", "Cliente não encontrado.");
    }

    private async Task ValidarProdutoAsync(Guid? produtoId, CancellationToken ct)
    {
        if (produtoId is Guid id && !await produtos.ExisteAsync(p => p.Id == id, ct))
            throw Validacao.Erro("ProdutoId", "Produto não encontrado.");
    }

    private async Task<ListaImportadaResposta> MontarRespostaAsync(ListaImportada lista, CancellationToken ct)
    {
        var itens = lista.ItensValidos.ToList();
        var dadosProdutos = (await produtos.ListarPorIdsAsync(
                itens.Where(i => i.ProdutoId.HasValue).Select(i => i.ProdutoId!.Value), ct))
            .ToDictionary(p => p.Id);

        var respostas = itens.Select(i =>
        {
            Produto? produto = i.ProdutoId is Guid pid && dadosProdutos.TryGetValue(pid, out var p) ? p : null;
            var precisaAtencao = i.ProdutoId is null || i.ConfiancaInterpretacao < InterpretadorLista.ConfiancaParaDispensarAtencao;
            return new ItemImportadoResposta(i.Id, i.Ordem, i.TextoOriginal, i.Descricao, i.Quantidade, i.UnidadeMedida,
                i.Marca, i.ProdutoId, produto?.Codigo, produto?.Nome, i.ConfiancaInterpretacao, precisaAtencao);
        }).ToList();

        return new ListaImportadaResposta(lista.Id, lista.Formato, lista.NomeArquivoOriginal, lista.Status, lista.MensagemErro,
            lista.ClienteId, lista.CriadoEm, respostas.Count, respostas.Count(r => r.PrecisaAtencao), respostas);
    }
}
