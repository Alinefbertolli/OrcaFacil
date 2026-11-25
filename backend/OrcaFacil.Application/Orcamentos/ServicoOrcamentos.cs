using FluentValidation;
using Microsoft.Extensions.Logging;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Application.Estoques;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Application.Orcamentos;

// ---------------------------------------------------------------- DTOs

/// <param name="ListaImportadaId">Lista já REVISADA e confirmada (Fase 6).</param>
/// <param name="Modo">Unico ou Comparativo. Vazio = o padrão da empresa.</param>
/// <param name="Opcoes">Só no Comparativo: quais opções gerar (MenorPreco, CustoBeneficio, MarcasReconhecidas). Vazio = as três.</param>
/// <param name="ClienteId">Vazio = o cliente informado na importação da lista.</param>
/// <param name="ValidadeDias">Por quantos dias o orçamento vale (padrão 7).</param>
public sealed record GerarOrcamentoRequisicao(
    Guid ListaImportadaId,
    ModoOrcamento? Modo = null,
    IReadOnlyList<TipoOpcaoOrcamento>? Opcoes = null,
    Guid? ClienteId = null,
    int? ValidadeDias = null);

public sealed record InformarPrecoRequisicao(decimal PrecoUnitario);

public sealed record AlterarStatusRequisicao(StatusOrcamento Status);

/// <param name="PrecoUnitario">Null quando o item está "sem preço".</param>
/// <param name="Disponibilidade">Situação ATUAL no estoque (calculada na hora da consulta). Null = item sem produto.</param>
/// <param name="SaldoEmEstoque">Saldo atual do produto. Null = sem controle de estoque ou sem produto.</param>
public sealed record ItemOrcamentoResposta(
    Guid Id, int Ordem, Guid? ItemListaImportadaId, Guid? ProdutoId, string Descricao, string? Marca,
    decimal Quantidade, string UnidadeMedida, decimal? PrecoUnitario, decimal? PrecoTotal, OrigemPreco? OrigemPreco,
    bool SemPreco, string? Observacao, DisponibilidadeEstoque? Disponibilidade, decimal? SaldoEmEstoque);

/// <param name="ItensSemPreco">Maior que zero = o ValorTotal ainda está incompleto.</param>
public sealed record OpcaoOrcamentoResposta(
    Guid Id, TipoOpcaoOrcamento Tipo, string Nome, decimal ValorTotal, int ItensSemPreco, bool Selecionada,
    IReadOnlyList<ItemOrcamentoResposta> Itens);

public sealed record OrcamentoResposta(
    Guid Id, string Numero, ModoOrcamento Modo, StatusOrcamento Status, Guid? ClienteId, string? ClienteNome,
    Guid? ListaImportadaId, DateOnly ValidadeAte, DateTime CriadoEm, decimal Economia,
    IReadOnlyList<OpcaoOrcamentoResposta> Opcoes, IReadOnlyList<string> Avisos);

public sealed record OrcamentoResumo(
    Guid Id, string Numero, ModoOrcamento Modo, StatusOrcamento Status, Guid? ClienteId, DateOnly ValidadeAte, DateTime CriadoEm);

/// <summary>Comparação lado a lado: uma coluna por opção, uma linha por item da lista.</summary>
public sealed record ComparacaoResposta(
    Guid OrcamentoId, string Numero, IReadOnlyList<ColunaComparacao> Opcoes, IReadOnlyList<LinhaComparacao> Linhas);

/// <param name="DiferencaParaMaisBarata">Quanto esta opção custa a mais que a mais barata (R$).</param>
/// <param name="DiferencaPercentual">A mesma diferença em % (ex.: 25,0 = 25% mais cara).</param>
public sealed record ColunaComparacao(
    Guid OpcaoId, TipoOpcaoOrcamento Tipo, string Nome, decimal ValorTotal, int ItensSemPreco,
    decimal DiferencaParaMaisBarata, decimal DiferencaPercentual, bool MaisBarata, bool Selecionada);

public sealed record LinhaComparacao(int Ordem, IReadOnlyList<CelulaComparacao> Celulas);

public sealed record CelulaComparacao(
    TipoOpcaoOrcamento Tipo, Guid ItemId, string Descricao, string? Marca, decimal Quantidade, string UnidadeMedida,
    decimal? PrecoUnitario, decimal? PrecoTotal, bool SemPreco, DisponibilidadeEstoque? Disponibilidade);

// ---------------------------------------------------------------- Validadores

public sealed class GerarOrcamentoValidador : AbstractValidator<GerarOrcamentoRequisicao>
{
    public GerarOrcamentoValidador()
    {
        RuleFor(r => r.ListaImportadaId).NotEmpty().WithMessage("Informe a lista importada.");
        RuleFor(r => r.Modo).IsInEnum().WithMessage("Modo inválido: use Unico ou Comparativo.");
        RuleFor(r => r.ValidadeDias).InclusiveBetween(1, 90).WithMessage("A validade deve ser de 1 a 90 dias.");

        // "?? []" porque a lista é opcional; o nome do campo é informado à mão para a mensagem de erro.
        RuleForEach(r => r.Opcoes ?? Array.Empty<TipoOpcaoOrcamento>())
            .Must(o => SeletorDeOpcoes.OpcoesComparativas.Contains(o))
            .WithMessage("Opção inválida: use MenorPreco, CustoBeneficio ou MarcasReconhecidas.")
            .OverridePropertyName(nameof(GerarOrcamentoRequisicao.Opcoes));
        RuleFor(r => r.Opcoes)
            .Must(o => o is null || o.Distinct().Count() == o.Count).WithMessage("Não repita opções.");
        RuleFor(r => r.Opcoes)
            .Must(o => o is null || o.Count == 0)
            .When(r => r.Modo == ModoOrcamento.Unico)
            .WithMessage("No modo Unico não se escolhem opções: o orçamento usa os produtos exatos da lista.");
    }
}

public sealed class InformarPrecoValidador : AbstractValidator<InformarPrecoRequisicao>
{
    public InformarPrecoValidador()
    {
        RuleFor(r => r.PrecoUnitario).GreaterThanOrEqualTo(0).WithMessage("O preço não pode ser negativo.");
    }
}

// ---------------------------------------------------------------- Caso de uso

/// <summary>
/// Motor de orçamentação: transforma uma lista REVISADA em orçamento para o cliente final,
/// usando o PREÇO DE VENDA da loja. Nunca inventa preço: o que não tem preço fica "sem preço".
/// </summary>
public sealed class ServicoOrcamentos(
    IRepositorio<Orcamento> orcamentos,
    IRepositorioOrcamentos orcamentosCompletos,
    IRepositorioListasImportadas listas,
    IRepositorio<Cliente> clientes,
    IRepositorio<Empresa> empresas,
    ICatalogoEquivalentes catalogo,
    IRepositorioEstoques estoques,
    IGeradorNumeroOrcamento geradorNumero,
    IEmpresaAtual empresaAtual,
    IUsuarioAtual usuarioAtual,
    IUnitOfWork unidadeDeTrabalho,
    TimeProvider relogio,
    IValidator<GerarOrcamentoRequisicao> validadorGerar,
    IValidator<InformarPrecoRequisicao> validadorPreco,
    ILogger<ServicoOrcamentos> logger)
{
    public const int ValidadePadraoDias = 7;

    private Guid IdUsuarioLogado =>
        usuarioAtual.UsuarioId ?? throw new NaoAutorizadoException("Faça login para continuar.");

    public async Task<OrcamentoResposta> GerarAsync(GerarOrcamentoRequisicao requisicao, CancellationToken ct = default)
    {
        await validadorGerar.ValidateAndThrowAsync(requisicao, ct);

        var lista = await listas.ObterComItensAsync(requisicao.ListaImportadaId, ct)
            ?? throw new NaoEncontradoException("Lista importada", requisicao.ListaImportadaId);
        lista.GarantirProntaParaOrcamento(); // regra: só lista revisada vira orçamento (senão 422)

        var clienteId = requisicao.ClienteId ?? lista.ClienteId;
        if (clienteId is Guid idCliente && !await clientes.ExisteAsync(c => c.Id == idCliente, ct))
            throw Validacao.Erro(nameof(GerarOrcamentoRequisicao.ClienteId), "Cliente não encontrado.");

        var modo = requisicao.Modo ?? await ModoPadraoDaEmpresaAsync(ct);
        List<TipoOpcaoOrcamento> tipos;
        if (modo == ModoOrcamento.Unico)
            tipos = [TipoOpcaoOrcamento.Unica];
        else if (requisicao.Opcoes is { Count: > 0 } escolhidas)
            tipos = SeletorDeOpcoes.OpcoesComparativas.Where(t => escolhidas.Contains(t)).ToList(); // ordem fixa
        else
            tipos = [.. SeletorDeOpcoes.OpcoesComparativas];

        // 1) Para cada linha da lista, decide o produto de cada opção (ou "sem preço").
        var itensLista = lista.ItensValidos.ToList();
        var planos = new List<PlanoDoItem>();
        var associados = (await catalogo.ObterAsync(
                itensLista.Where(i => i.ProdutoId.HasValue).Select(i => i.ProdutoId!.Value), ct))
            .ToDictionary(p => p.Id);

        foreach (var linha in itensLista)
            planos.Add(await PlanejarItemAsync(linha, modo, tipos, associados, ct));

        // 2) Opções que ficaram idênticas (mesmos produtos em todas as linhas) aparecem uma vez só.
        var avisos = new List<string>();
        var tiposMantidos = RemoverOpcoesIguais(tipos, planos, avisos);

        // 3) Monta o orçamento.
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);
        var numero = await geradorNumero.GerarAsync(ct);
        var orcamento = new Orcamento(numero, IdUsuarioLogado, clienteId,
            hoje.AddDays(requisicao.ValidadeDias ?? ValidadePadraoDias), lista.Id, modo);

        foreach (var tipo in tiposMantidos)
        {
            var opcao = orcamento.AdicionarOpcao(tipo);
            foreach (var plano in planos)
                opcao.AdicionarItem(CriarItem(plano, tipo));
        }

        // Uma opção só: já fica selecionada (não há o que escolher).
        if (orcamento.Opcoes.Count == 1)
            orcamento.SelecionarOpcao(orcamento.Opcoes.First().Id);

        orcamentos.Adicionar(orcamento);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);

        logger.LogInformation("Orçamento {Numero} gerado da lista {ListaId}: modo {Modo}, {Opcoes} opção(ões), {Itens} itens.",
            orcamento.Numero, lista.Id, modo, orcamento.Opcoes.Count, planos.Count);

        return await MontarRespostaAsync(orcamento, avisos, ct);
    }

    public async Task<PaginaResultado<OrcamentoResumo>> ListarAsync(ParametrosPaginacao paginacao, StatusOrcamento? status,
        Guid? clienteId, CancellationToken ct = default)
    {
        var pagina = await orcamentos.ListarAsync(paginacao,
            filtro: o => (status == null || o.Status == status) && (clienteId == null || o.ClienteId == clienteId),
            ordenarPor: o => o.CriadoEm, decrescente: true, cancellationToken: ct);
        return pagina.Mapear(o => new OrcamentoResumo(o.Id, o.Numero, o.Modo, o.Status, o.ClienteId, o.ValidadeAte, o.CriadoEm));
    }

    public async Task<OrcamentoResposta> ObterAsync(Guid id, CancellationToken ct = default) =>
        await MontarRespostaAsync(await ObterCompletoAsync(id, ct), [], ct);

    public async Task<ComparacaoResposta> CompararAsync(Guid id, CancellationToken ct = default)
    {
        var orcamento = await ObterCompletoAsync(id, ct);
        var opcoes = OrdenarOpcoes(orcamento.Opcoes);
        var saldos = await SaldosDoOrcamentoAsync(orcamento, ct);
        var menorTotal = opcoes.Count == 0 ? 0m : opcoes.Min(o => o.ValorTotal);

        var colunas = opcoes.Select(o =>
        {
            var diferenca = o.ValorTotal - menorTotal;
            var percentual = menorTotal == 0 ? 0m : Math.Round(diferenca / menorTotal * 100, 1, MidpointRounding.AwayFromZero);
            return new ColunaComparacao(o.Id, o.Tipo, SeletorDeOpcoes.NomeDaOpcao(o.Tipo), o.ValorTotal, o.ItensSemPreco,
                diferenca, percentual, diferenca == 0, o.Selecionada);
        }).ToList();

        var linhas = opcoes
            .SelectMany(o => o.Itens.Select(i => (o.Tipo, Item: i)))
            .GroupBy(x => x.Item.Ordem)
            .OrderBy(g => g.Key)
            .Select(g => new LinhaComparacao(g.Key, g.Select(x => new CelulaComparacao(
                x.Tipo, x.Item.Id, x.Item.Descricao, x.Item.Marca, x.Item.Quantidade, x.Item.UnidadeMedida,
                x.Item.PrecoPendente ? null : x.Item.PrecoUnitario,
                x.Item.PrecoPendente ? null : x.Item.PrecoTotal,
                x.Item.PrecoPendente, AvaliarItem(x.Item, saldos))).ToList()))
            .ToList();

        return new ComparacaoResposta(orcamento.Id, orcamento.Numero, colunas, linhas);
    }

    /// <summary>Preenche à mão o preço de um item (normalmente um item "sem preço").</summary>
    public async Task<OrcamentoResposta> InformarPrecoAsync(Guid id, Guid opcaoId, Guid itemId, InformarPrecoRequisicao requisicao,
        CancellationToken ct = default)
    {
        await validadorPreco.ValidateAndThrowAsync(requisicao, ct);
        var orcamento = await ObterCompletoAsync(id, ct);
        orcamento.InformarPrecoItem(opcaoId, itemId, requisicao.PrecoUnitario);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await MontarRespostaAsync(orcamento, [], ct);
    }

    /// <summary>O cliente escolheu uma das opções.</summary>
    public async Task<OrcamentoResposta> SelecionarOpcaoAsync(Guid id, Guid opcaoId, CancellationToken ct = default)
    {
        var orcamento = await ObterCompletoAsync(id, ct);

        // O banco aceita só UMA opção marcada por orçamento. Para trocar a escolha,
        // primeiro gravamos "nenhuma marcada" e depois marcamos a nova.
        if (orcamento.OpcaoSelecionada is { } atual && atual.Id != opcaoId)
        {
            orcamento.LimparSelecao();
            await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        }

        orcamento.SelecionarOpcao(opcaoId);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await MontarRespostaAsync(orcamento, [], ct);
    }

    public async Task<OrcamentoResposta> AlterarStatusAsync(Guid id, AlterarStatusRequisicao requisicao, CancellationToken ct = default)
    {
        var orcamento = await ObterCompletoAsync(id, ct);

        // Fase 8 (decisão da usuária): a venda finalizada dá baixa no estoque.
        // Só "Aprovado" pode virar "Finalizado"; nos outros casos o AlterarStatus abaixo já recusa.
        // Tudo é gravado junto no MESMO SaveChanges: ou finaliza E baixa, ou nada acontece.
        if (requisicao.Status == StatusOrcamento.Finalizado && orcamento.Status == StatusOrcamento.Aprovado)
            await BaixarEstoqueDaVendaAsync(orcamento, ct);

        orcamento.AlterarStatus(requisicao.Status);

        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return await MontarRespostaAsync(orcamento, [], ct);
    }

    // ---------------------------------------------------------------- motor

    /// <summary>O que cada opção vai usar para uma linha da lista.</summary>
    private sealed record PlanoDoItem(
        ItemListaImportada Linha,
        Dictionary<TipoOpcaoOrcamento, ProdutoCandidato?> Escolhas,
        string? MotivoSemPreco,
        string? Observacao);

    private async Task<PlanoDoItem> PlanejarItemAsync(ItemListaImportada linha, ModoOrcamento modo,
        IReadOnlyList<TipoOpcaoOrcamento> tipos, Dictionary<Guid, ProdutoCandidato> associados, CancellationToken ct)
    {
        var escolhas = tipos.ToDictionary(t => t, _ => (ProdutoCandidato?)null);

        if (linha.ProdutoId is not Guid produtoId || !associados.TryGetValue(produtoId, out var associado))
            return new PlanoDoItem(linha, escolhas,
                "Item sem produto do catálogo: informe o preço ou associe um produto na revisão da lista.", null);

        if (modo == ModoOrcamento.Unico)
        {
            // Item exato: o produto escolhido na revisão, sem trocas.
            string? motivo = !associado.Ativo ? "Produto inativo no catálogo: informe o preço."
                : associado.PrecoVenda is null ? "Produto sem preço de venda cadastrado: informe o preço."
                : null;
            if (motivo is null) escolhas[TipoOpcaoOrcamento.Unica] = associado;
            return new PlanoDoItem(linha, escolhas, motivo, null);
        }

        // Comparativo: produtos equivalentes (grupo ou similaridade), respeitando a marca pedida.
        var equivalentes = await catalogo.BuscarEquivalentesAsync(produtoId, ct);
        var daMarca = SeletorDeOpcoes.FiltrarPorMarca(equivalentes, linha.Marca, out var marcaEncontrada);
        var candidatos = SeletorDeOpcoes.FiltrarPorEstoque(daMarca, linha.Quantidade, out var algumComEstoque);

        var observacoes = new List<string>();
        if (!marcaEncontrada)
            observacoes.Add($"Marca pedida ({linha.Marca}) não encontrada no catálogo: usamos marcas equivalentes.");
        if (!algumComEstoque)
            observacoes.Add("Nenhum produto equivalente tem estoque suficiente: confira o prazo de reposição.");
        var observacao = observacoes.Count == 0 ? null : string.Join(" ", observacoes);

        foreach (var tipo in tipos)
            escolhas[tipo] = SeletorDeOpcoes.Escolher(tipo, candidatos);

        var semNenhumPreco = escolhas.Values.All(e => e is null);
        return new PlanoDoItem(linha, escolhas,
            semNenhumPreco ? "Nenhum produto equivalente com preço de venda cadastrado: informe o preço." : null,
            observacao);
    }

    private static List<TipoOpcaoOrcamento> RemoverOpcoesIguais(List<TipoOpcaoOrcamento> tipos,
        List<PlanoDoItem> planos, List<string> avisos)
    {
        var mantidos = new List<TipoOpcaoOrcamento>();
        foreach (var tipo in tipos)
        {
            // Procura, entre as opções já mantidas, uma que usa exatamente os mesmos produtos em todas as linhas.
            TipoOpcaoOrcamento? igual = null;
            foreach (var mantido in mantidos)
            {
                if (planos.All(p => p.Escolhas[mantido]?.Id == p.Escolhas[tipo]?.Id))
                {
                    igual = mantido;
                    break;
                }
            }

            if (igual is TipoOpcaoOrcamento repetida)
                avisos.Add($"A opção \"{SeletorDeOpcoes.NomeDaOpcao(tipo)}\" ficou igual à opção " +
                           $"\"{SeletorDeOpcoes.NomeDaOpcao(repetida)}\" (mesmos produtos): mostramos só uma.");
            else
                mantidos.Add(tipo);
        }
        return mantidos;
    }

    private static ItemOrcamento CriarItem(PlanoDoItem plano, TipoOpcaoOrcamento tipo)
    {
        var linha = plano.Linha;
        var produto = plano.Escolhas[tipo];

        ItemOrcamento item;
        if (produto?.PrecoVenda is decimal preco)
        {
            item = new ItemOrcamento(produto.Nome, produto.MarcaNome, linha.Quantidade, linha.UnidadeMedida,
                preco, OrigemPreco.PrecoVenda, produto.Id);

            // A quantidade veio da lista; o preço é por unidade do produto. Se forem diferentes, avisamos.
            if (!string.Equals(produto.UnidadeMedida, linha.UnidadeMedida, StringComparison.OrdinalIgnoreCase))
                item.AdicionarObservacao(
                    $"Unidade da lista ({linha.UnidadeMedida}) diferente da unidade do produto ({produto.UnidadeMedida}): confira a quantidade.");
        }
        else
        {
            var motivo = plano.MotivoSemPreco
                         ?? "Nenhum produto com preço de venda para esta opção: informe o preço.";
            item = ItemOrcamento.SemPreco(linha.Descricao, linha.Marca, linha.Quantidade, linha.UnidadeMedida,
                linha.ProdutoId, motivo);
        }

        item.AdicionarObservacao(plano.Observacao);
        item.VincularLinhaDaLista(linha.Id, linha.Ordem);
        return item;
    }

    // ---------------------------------------------------------------- auxiliares

    /// <summary>
    /// Baixa no estoque os produtos da opção vendida. Produtos sem controle de estoque são ignorados.
    /// Se faltar saldo de qualquer produto, NADA é baixado e a finalização é recusada com a lista do que falta.
    /// </summary>
    private async Task BaixarEstoqueDaVendaAsync(Orcamento orcamento, CancellationToken ct)
    {
        var vendida = orcamento.OpcaoSelecionada
            ?? throw new DomainException("O orçamento não tem opção selecionada.");

        var porProduto = vendida.Itens
            .Where(i => i.ProdutoId.HasValue && !i.PrecoPendente)
            .GroupBy(i => i.ProdutoId!.Value)
            .Select(g => (ProdutoId: g.Key, Descricao: g.First().Descricao, Quantidade: g.Sum(i => i.Quantidade)))
            .ToList();
        if (porProduto.Count == 0) return;

        var estoquesDosProdutos = (await estoques.ListarPorProdutosAsync(porProduto.Select(p => p.ProdutoId), ct))
            .ToDictionary(e => e.ProdutoId);

        // 1) Confere TUDO antes de baixar qualquer coisa.
        var faltas = porProduto
            .Where(p => estoquesDosProdutos.TryGetValue(p.ProdutoId, out var e) && e.QuantidadeDisponivel < p.Quantidade)
            .Select(p => $"{p.Descricao} (pedido {p.Quantidade:0.###}, em estoque {estoquesDosProdutos[p.ProdutoId].QuantidadeDisponivel:0.###})")
            .ToList();
        if (faltas.Count > 0)
            throw new DomainException(
                $"Estoque insuficiente para finalizar a venda: {string.Join("; ", faltas)}. " +
                "Registre a entrada dos produtos (ou ajuste o estoque) e tente de novo.");

        // 2) Baixa: cada saída fica no histórico ligada a este orçamento.
        foreach (var (produtoId, _, quantidade) in porProduto)
        {
            if (estoquesDosProdutos.TryGetValue(produtoId, out var estoque))
                estoque.RegistrarSaida(quantidade, IdUsuarioLogado, $"Venda do orçamento {orcamento.Numero}", orcamento.Id);
        }
    }

    private async Task<IReadOnlyDictionary<Guid, decimal>> SaldosDoOrcamentoAsync(Orcamento orcamento, CancellationToken ct)
    {
        var ids = orcamento.Opcoes.SelectMany(o => o.Itens)
            .Where(i => i.ProdutoId.HasValue).Select(i => i.ProdutoId!.Value).Distinct().ToList();
        return ids.Count == 0 ? new Dictionary<Guid, decimal>() : await estoques.ObterSaldosAsync(ids, ct);
    }

    private static DisponibilidadeEstoque? AvaliarItem(ItemOrcamento item, IReadOnlyDictionary<Guid, decimal> saldos)
    {
        if (item.ProdutoId is not Guid produtoId) return null;
        return Disponibilidade.Avaliar(saldos.TryGetValue(produtoId, out var saldo) ? saldo : null, item.Quantidade);
    }

    private async Task<ModoOrcamento> ModoPadraoDaEmpresaAsync(CancellationToken ct)
    {
        var empresaId = empresaAtual.EmpresaId ?? throw new NaoAutorizadoException("Faça login para continuar.");
        var empresa = await empresas.ObterPorIdOuFalharAsync(empresaId, ct);
        return empresa.ModoOrcamentoPadrao;
    }

    private async Task<Orcamento> ObterCompletoAsync(Guid id, CancellationToken ct) =>
        await orcamentosCompletos.ObterCompletoAsync(id, ct) ?? throw new NaoEncontradoException("Orçamento", id);

    /// <summary>Ordem de exibição: Econômica, Custo-benefício, Marcas reconhecidas (e as demais depois).</summary>
    private static List<OpcaoOrcamento> OrdenarOpcoes(IEnumerable<OpcaoOrcamento> opcoes) =>
        opcoes.OrderBy(o => PosicaoNaTela(o.Tipo)).ToList();

    private static int PosicaoNaTela(TipoOpcaoOrcamento tipo)
    {
        var posicao = Array.IndexOf(SeletorDeOpcoes.OpcoesComparativas, tipo);
        return posicao >= 0 ? posicao : 99; // Unica/Manual vão para o fim
    }

    private async Task<OrcamentoResposta> MontarRespostaAsync(Orcamento orcamento, IReadOnlyList<string> avisosDaGeracao,
        CancellationToken ct)
    {
        var cliente = orcamento.ClienteId is Guid cid ? await clientes.ObterPorIdAsync(cid, ct) : null;
        var opcoes = OrdenarOpcoes(orcamento.Opcoes);
        var saldos = await SaldosDoOrcamentoAsync(orcamento, ct);

        var avisos = new List<string>(avisosDaGeracao);
        foreach (var opcao in opcoes.Where(o => o.ItensSemPreco > 0))
            avisos.Add($"Opção \"{SeletorDeOpcoes.NomeDaOpcao(opcao.Tipo)}\": {opcao.ItensSemPreco} item(ns) sem preço. " +
                       "O total está incompleto até você informar os preços.");

        // O estoque muda depois que o orçamento é gerado: a disponibilidade é sempre a de AGORA.
        if (orcamento.Status != StatusOrcamento.Finalizado)
        {
            foreach (var opcao in opcoes)
            {
                var semEstoque = opcao.Itens.Count(i => AvaliarItem(i, saldos)
                    is DisponibilidadeEstoque.Parcial or DisponibilidadeEstoque.Indisponivel);
                if (semEstoque > 0)
                    avisos.Add($"Opção \"{SeletorDeOpcoes.NomeDaOpcao(opcao.Tipo)}\": {semEstoque} item(ns) sem estoque suficiente no momento.");
            }
        }

        var respostasOpcoes = opcoes.Select(o => new OpcaoOrcamentoResposta(o.Id, o.Tipo, SeletorDeOpcoes.NomeDaOpcao(o.Tipo),
            o.ValorTotal, o.ItensSemPreco, o.Selecionada,
            o.Itens.OrderBy(i => i.Ordem).Select(i => new ItemOrcamentoResposta(i.Id, i.Ordem, i.ItemListaImportadaId,
                i.ProdutoId, i.Descricao, i.Marca, i.Quantidade, i.UnidadeMedida,
                i.PrecoPendente ? null : i.PrecoUnitario,
                i.PrecoPendente ? null : i.PrecoTotal,
                i.PrecoPendente ? null : i.OrigemPreco,
                i.PrecoPendente, i.Observacao, AvaliarItem(i, saldos),
                i.ProdutoId is Guid pid && saldos.TryGetValue(pid, out var saldo) ? saldo : null)).ToList())).ToList();

        return new OrcamentoResposta(orcamento.Id, orcamento.Numero, orcamento.Modo, orcamento.Status, orcamento.ClienteId,
            cliente?.Nome, orcamento.ListaImportadaId, orcamento.ValidadeAte, orcamento.CriadoEm, orcamento.CalcularEconomia(),
            respostasOpcoes, avisos);
    }
}
