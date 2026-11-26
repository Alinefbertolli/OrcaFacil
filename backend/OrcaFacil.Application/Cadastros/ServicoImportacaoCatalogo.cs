using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Application.Estoques;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Application.Orcamentos;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Application.Cadastros;

// ---------------------------------------------------------------- DTOs

public enum SituacaoLinhaImportacao
{
    Criado = 1,
    Atualizado = 2,
    SemAlteracao = 3,
    Erro = 4
}

/// <param name="Detalhes">O que mudou (ex.: "preço R$ 12,00 → R$ 12,50") ou o motivo do erro.</param>
public sealed record LinhaImportacaoCatalogo(
    int Linha, string? Codigo, string? Nome, SituacaoLinhaImportacao Situacao, IReadOnlyList<string> Detalhes);

/// <param name="Simulacao">true = nada foi gravado: é só a prévia do que vai acontecer.</param>
public sealed record ResultadoImportacaoCatalogo(
    bool Simulacao,
    string NomeArquivo,
    IReadOnlyList<string> ColunasReconhecidas,
    int TotalLinhas,
    int Criados,
    int Atualizados,
    int SemAlteracao,
    int ComErro,
    int EstoquesAjustados,
    IReadOnlyList<string> CategoriasCriadas,
    IReadOnlyList<string> MarcasCriadas,
    IReadOnlyList<LinhaImportacaoCatalogo> Linhas);

// ---------------------------------------------------------------- Caso de uso

/// <summary>
/// Importa produtos, preços de venda e estoque de uma planilha (.xlsx ou .csv) exportada do ERP da loja.
/// Regras (decididas com a usuária em 26/11/2026):
/// - o produto é encontrado pelo CÓDIGO (SKU); código novo = produto novo;
/// - categoria e marca que não existem são criadas (marca nova entra com nível 3);
/// - produtos que não estão na planilha ficam como estão (dá para importar planilhas parciais);
/// - a coluna de estoque vira o saldo atual (um ajuste de inventário, com histórico);
/// - célula vazia = "não mexer" naquele dado;
/// - linhas com erro são puladas; as outras são importadas.
/// Sempre faça primeiro a SIMULAÇÃO (prévia): nada é gravado e a tela mostra o que vai acontecer.
/// </summary>
public sealed class ServicoImportacaoCatalogo(
    IPlanilhasCatalogo planilhas,
    IRepositorio<Produto> produtos,
    IRepositorio<Categoria> categorias,
    IRepositorio<Marca> marcas,
    IRepositorio<HistoricoPreco> historicos,
    IRepositorioEstoques estoques,
    IUsuarioAtual usuarioAtual,
    IUnitOfWork unidadeDeTrabalho,
    TimeProvider relogio)
{
    public const string NomeCategoriaPadrao = "Sem categoria";
    public const string UnidadePadrao = "UN";

    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    public byte[] GerarModelo() => planilhas.GerarModelo();

    public async Task<ResultadoImportacaoCatalogo> ImportarAsync(ArquivoEnviado arquivo, bool simular,
        CancellationToken ct = default)
    {
        using var memoria = new MemoryStream();
        await arquivo.Conteudo.CopyToAsync(memoria, ct);
        var bytes = memoria.ToArray();
        var nomeArquivo = Path.GetFileName(arquivo.NomeArquivo);
        var formato = ValidarArquivo(nomeArquivo, bytes);

        var celulas = planilhas.Ler(new MemoryStream(bytes), formato);
        var planilha = InterpretadorPlanilhaCatalogo.Interpretar(celulas);
        if (planilha.Linhas.Count == 0 && planilha.Erros.Count == 0)
            throw Validacao.Erro("arquivo", "A planilha não tem nenhum produto abaixo do cabeçalho.");

        var contexto = await CarregarAsync(planilha.Linhas, ct);
        var motivoAjuste = $"Importação de planilha ({nomeArquivo})";
        if (motivoAjuste.Length > 500) motivoAjuste = motivoAjuste[..500];

        var linhas = planilha.Erros
            .Select(e => new LinhaImportacaoCatalogo(e.NumeroLinha, e.Codigo, e.Nome, SituacaoLinhaImportacao.Erro, [e.Mensagem]))
            .ToList();
        var estoquesAjustados = 0;

        foreach (var linha in planilha.Linhas)
        {
            try
            {
                var (resultado, ajustouEstoque) = Aplicar(linha, contexto, motivoAjuste);
                linhas.Add(resultado);
                if (ajustouEstoque) estoquesAjustados++;
            }
            catch (DomainException erro)
            {
                linhas.Add(new LinhaImportacaoCatalogo(linha.NumeroLinha, linha.Codigo, linha.Nome,
                    SituacaoLinhaImportacao.Erro, [erro.Message]));
            }
        }

        linhas.Sort((a, b) => a.Linha.CompareTo(b.Linha));
        var houveMudanca = linhas.Any(l => l.Situacao is SituacaoLinhaImportacao.Criado or SituacaoLinhaImportacao.Atualizado);

        // Na simulação NÃO chamamos o SaveChanges: tudo o que foi preparado é descartado no fim da requisição.
        if (!simular && houveMudanca)
            await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);

        return new ResultadoImportacaoCatalogo(
            simular, nomeArquivo,
            planilha.ColunasReconhecidas.Select(InterpretadorPlanilhaCatalogo.NomeDoCampo).ToList(),
            linhas.Count,
            linhas.Count(l => l.Situacao == SituacaoLinhaImportacao.Criado),
            linhas.Count(l => l.Situacao == SituacaoLinhaImportacao.Atualizado),
            linhas.Count(l => l.Situacao == SituacaoLinhaImportacao.SemAlteracao),
            linhas.Count(l => l.Situacao == SituacaoLinhaImportacao.Erro),
            estoquesAjustados,
            contexto.CategoriasCriadas, contexto.MarcasCriadas, linhas);
    }

    // ------------------------------------------------------------ Uma linha

    private (LinhaImportacaoCatalogo Resultado, bool AjustouEstoque) Aplicar(LinhaCatalogo linha, Contexto contexto,
        string motivoAjuste)
    {
        var detalhes = new List<string>();
        var existente = contexto.Produtos.GetValueOrDefault(linha.Codigo);
        Produto produto;
        SituacaoLinhaImportacao situacao;

        if (existente is null)
        {
            if (linha.Nome is null)
                return (Erro(linha, "Produto novo precisa de nome."), false);

            produto = new Produto
            {
                Codigo = linha.Codigo,
                Nome = linha.Nome,
                Descricao = linha.Descricao,
                UnidadeMedida = linha.Unidade ?? UnidadePadrao,
                CategoriaId = ObterOuCriarCategoria(linha.Categoria ?? NomeCategoriaPadrao, contexto).Id,
                MarcaId = linha.Marca is null ? null : ObterOuCriarMarca(linha.Marca, contexto).Id,
            };
            produto.DefinirGrupoEquivalencia(linha.GrupoEquivalencia);
            if (linha.PrecoVenda is decimal preco)
            {
                produto.DefinirPrecoVenda(preco);
                historicos.Adicionar(new HistoricoPreco(produto.Id, null, preco, OrigemPreco.PrecoVenda, Agora));
            }
            else
            {
                detalhes.Add("sem preço de venda: vai aparecer como \"sem preço\" no orçamento");
            }

            produtos.Adicionar(produto);
            contexto.Produtos[produto.Codigo] = produto;
            situacao = SituacaoLinhaImportacao.Criado;
        }
        else
        {
            produto = existente;
            AtualizarProduto(produto, linha, contexto, detalhes);
            situacao = detalhes.Count > 0 ? SituacaoLinhaImportacao.Atualizado : SituacaoLinhaImportacao.SemAlteracao;
        }

        var ajustouEstoque = AtualizarEstoque(produto, linha, contexto, motivoAjuste, detalhes);
        if (situacao == SituacaoLinhaImportacao.SemAlteracao && detalhes.Count > 0)
            situacao = SituacaoLinhaImportacao.Atualizado;

        return (new LinhaImportacaoCatalogo(linha.NumeroLinha, produto.Codigo, produto.Nome, situacao, detalhes),
            ajustouEstoque);
    }

    /// <summary>Muda só o que veio preenchido E está diferente; anota cada mudança para mostrar na tela.</summary>
    private void AtualizarProduto(Produto produto, LinhaCatalogo linha, Contexto contexto, List<string> detalhes)
    {
        if (!produto.Ativo)
        {
            produto.Ativo = true;
            detalhes.Add("produto reativado");
        }

        if (linha.Nome is not null && linha.Nome != produto.Nome)
        {
            detalhes.Add($"nome \"{produto.Nome}\" → \"{linha.Nome}\"");
            produto.Nome = linha.Nome;
        }

        if (linha.Descricao is not null && linha.Descricao != produto.Descricao)
        {
            detalhes.Add("descrição alterada");
            produto.Descricao = linha.Descricao;
        }

        if (linha.Unidade is not null && linha.Unidade != produto.UnidadeMedida)
        {
            detalhes.Add($"unidade {produto.UnidadeMedida} → {linha.Unidade}");
            produto.UnidadeMedida = linha.Unidade;
        }

        if (linha.Categoria is not null)
        {
            var categoria = ObterOuCriarCategoria(linha.Categoria, contexto);
            if (categoria.Id != produto.CategoriaId)
            {
                detalhes.Add($"categoria → {categoria.Nome}");
                produto.CategoriaId = categoria.Id;
            }
        }

        if (linha.Marca is not null)
        {
            var marca = ObterOuCriarMarca(linha.Marca, contexto);
            if (marca.Id != produto.MarcaId)
            {
                detalhes.Add($"marca → {marca.Nome}");
                produto.MarcaId = marca.Id;
            }
        }

        if (linha.GrupoEquivalencia is not null &&
            Produto.NormalizarGrupo(linha.GrupoEquivalencia) != produto.GrupoEquivalencia)
        {
            produto.DefinirGrupoEquivalencia(linha.GrupoEquivalencia);
            detalhes.Add($"grupo de equivalência → \"{produto.GrupoEquivalencia}\"");
        }

        if (linha.PrecoVenda is decimal preco && preco != produto.PrecoVenda)
        {
            detalhes.Add(produto.PrecoVenda is decimal anterior
                ? $"preço {FormatoBr.Dinheiro(anterior)} → {FormatoBr.Dinheiro(preco)}"
                : $"preço definido: {FormatoBr.Dinheiro(preco)}");
            produto.DefinirPrecoVenda(preco);
            historicos.Adicionar(new HistoricoPreco(produto.Id, null, preco, OrigemPreco.PrecoVenda, Agora));
        }
    }

    /// <returns>true quando o saldo foi ajustado.</returns>
    private bool AtualizarEstoque(Produto produto, LinhaCatalogo linha, Contexto contexto, string motivoAjuste,
        List<string> detalhes)
    {
        if (linha.Estoque is null && linha.EstoqueMinimo is null) return false;

        var estoque = contexto.Estoques.GetValueOrDefault(produto.Id);
        if (estoque is null)
        {
            estoque = new Estoque(produto.Id, linha.EstoqueMinimo ?? 0);
            estoques.Adicionar(estoque);
            contexto.Estoques[produto.Id] = estoque;
            detalhes.Add("estoque passa a ser controlado");
        }
        else if (linha.EstoqueMinimo is decimal minimo && minimo != estoque.EstoqueMinimo)
        {
            detalhes.Add($"estoque mínimo {FormatoBr.Quantidade(estoque.EstoqueMinimo)} → {FormatoBr.Quantidade(minimo)}");
            estoque.DefinirEstoqueMinimo(minimo);
        }

        if (linha.Estoque is not decimal saldo || saldo == estoque.QuantidadeDisponivel)
            return false;

        var anterior = estoque.QuantidadeDisponivel;
        estoque.AjustarSaldo(saldo, IdUsuarioLogado, motivoAjuste);
        detalhes.Add($"estoque {FormatoBr.Quantidade(anterior)} → {FormatoBr.Quantidade(saldo)}");
        return true;
    }

    // ------------------------------------------------------------ Apoio

    private Guid IdUsuarioLogado =>
        usuarioAtual.UsuarioId ?? throw new NaoAutorizadoException("Faça login para continuar.");

    /// <summary>Tudo o que a importação consulta, buscado de uma vez (poucas consultas, mesmo com milhares de linhas).</summary>
    private sealed class Contexto
    {
        public required Dictionary<string, Produto> Produtos { get; init; }
        public required Dictionary<string, Categoria> Categorias { get; init; }
        public required Dictionary<string, Marca> Marcas { get; init; }
        public required Dictionary<Guid, Estoque> Estoques { get; init; }
        public List<string> CategoriasCriadas { get; } = [];
        public List<string> MarcasCriadas { get; } = [];
    }

    private async Task<Contexto> CarregarAsync(IReadOnlyList<LinhaCatalogo> linhas, CancellationToken ct)
    {
        // Comparação sem diferenciar maiúsculas: "cad10-1" na planilha encontra "CAD10-1" no sistema.
        var codigos = linhas.Select(l => l.Codigo.ToLower()).Distinct().ToList();
        IReadOnlyList<Produto> encontrados = codigos.Count == 0
            ? []
            : await produtos.ListarTodosAsync(p => codigos.Contains(p.Codigo.ToLower()), ct);

        IReadOnlyList<Estoque> listaEstoques = encontrados.Count == 0
            ? []
            : await estoques.ListarPorProdutosAsync(encontrados.Select(p => p.Id), ct);

        var todasCategorias = await categorias.ListarTodosAsync(_ => true, ct);
        var todasMarcas = await marcas.ListarTodosAsync(_ => true, ct);

        return new Contexto
        {
            Produtos = PorNome(encontrados, p => p.Codigo),
            Categorias = PorNome(todasCategorias, c => c.Nome),
            Marcas = PorNome(todasMarcas, m => m.Nome),
            Estoques = listaEstoques.ToDictionary(e => e.ProdutoId),
        };
    }

    private static Dictionary<string, T> PorNome<T>(IEnumerable<T> itens, Func<T, string> nome)
    {
        var dicionario = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in itens) dicionario.TryAdd(nome(item).Trim(), item);
        return dicionario;
    }

    private Categoria ObterOuCriarCategoria(string nome, Contexto contexto)
    {
        if (contexto.Categorias.TryGetValue(nome, out var existente)) return existente;

        var categoria = new Categoria { Nome = nome };
        categorias.Adicionar(categoria);
        contexto.Categorias[nome] = categoria;
        contexto.CategoriasCriadas.Add(nome);
        return categoria;
    }

    private Marca ObterOuCriarMarca(string nome, Contexto contexto)
    {
        if (contexto.Marcas.TryGetValue(nome, out var existente)) return existente;

        var marca = new Marca { Nome = nome }; // nível 3 (médio) por padrão: a loja ajusta depois
        marcas.Adicionar(marca);
        contexto.Marcas[nome] = marca;
        contexto.MarcasCriadas.Add(nome);
        return marca;
    }

    private static LinhaImportacaoCatalogo Erro(LinhaCatalogo linha, string mensagem) =>
        new(linha.NumeroLinha, linha.Codigo, linha.Nome, SituacaoLinhaImportacao.Erro, [mensagem]);

    /// <summary>Mesmas proteções do upload de listas: tamanho, extensão e "assinatura" do arquivo.</summary>
    private static FormatoPlanilha ValidarArquivo(string nomeArquivo, byte[] bytes)
    {
        if (bytes.Length == 0)
            throw Validacao.Erro("arquivo", "O arquivo está vazio.");

        if (bytes.LongLength > ValidadorArquivo.TamanhoMaximoPadraoMb * 1024L * 1024L)
            throw Validacao.Erro("arquivo", $"O arquivo passa do limite de {ValidadorArquivo.TamanhoMaximoPadraoMb} MB.");

        var extensao = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        var inicio = bytes.AsSpan(0, Math.Min(bytes.Length, 1024));
        return extensao switch
        {
            ".xlsx" when inicio.StartsWith("PK\u0003\u0004"u8) => FormatoPlanilha.Xlsx,
            ".csv" when !inicio.Contains((byte)0) => FormatoPlanilha.Csv,
            ".xlsx" or ".csv" => throw Validacao.Erro("arquivo", "O conteúdo do arquivo não corresponde à extensão informada."),
            ".xls" => throw Validacao.Erro("arquivo",
                "Formato antigo do Excel não suportado. Abra o arquivo e use \"Salvar como\" .xlsx."),
            _ => throw Validacao.Erro("arquivo", "Envie uma planilha Excel (.xlsx) ou CSV (.csv).")
        };
    }
}
