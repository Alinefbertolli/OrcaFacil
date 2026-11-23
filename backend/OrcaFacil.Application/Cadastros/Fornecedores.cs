using FluentValidation;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Autenticacao;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Application.Cadastros;

/// <param name="Avaliacao">Nota de 0 a 5 dada pela equipe de compras (usada no orçamento "Custo-benefício").</param>
public sealed record FornecedorRequisicao(
    string RazaoSocial,
    string? NomeFantasia,
    string Cnpj,
    string? Contato,
    string? Telefone,
    string? Email,
    Endereco? Endereco,
    decimal? Avaliacao);

public sealed record FornecedorResposta(
    Guid Id, string RazaoSocial, string? NomeFantasia, string Cnpj, string? Contato, string? Telefone, string? Email,
    Endereco? Endereco, decimal? Avaliacao, bool Ativo)
{
    public static FornecedorResposta De(Fornecedor f) =>
        new(f.Id, f.RazaoSocial, f.NomeFantasia, f.Cnpj, f.Contato, f.Telefone, f.Email, f.Endereco, f.Avaliacao, f.Ativo);
}

/// <summary>Um produto vendido pelo fornecedor, com o preço praticado por ele.</summary>
public sealed record ProdutoDoFornecedorResposta(
    Guid ProdutoId, string? ProdutoCodigo, string? ProdutoNome, string? CodigoNoFornecedor,
    decimal? PrecoAtual, int? PrazoEntregaDias, bool Disponivel, DateTime? PrecoAtualizadoEm);

public sealed class FornecedorValidador : AbstractValidator<FornecedorRequisicao>
{
    public FornecedorValidador()
    {
        RuleFor(r => r.RazaoSocial).NotEmpty().WithMessage("Informe a razão social.").MaximumLength(200);
        RuleFor(r => r.NomeFantasia).MaximumLength(200);
        RuleFor(r => r.Cnpj).NotEmpty().WithMessage("Informe o CNPJ.")
            .Must(DocumentoFiscal.CnpjValido).WithMessage("CNPJ inválido.");
        RuleFor(r => r.Contato).MaximumLength(150);
        RuleFor(r => r.Telefone).MaximumLength(20);
        RuleFor(r => r.Email!).EmailValido().When(r => !string.IsNullOrWhiteSpace(r.Email));
        RuleFor(r => r.Endereco!).SetValidator(new EnderecoValidador()).When(r => r.Endereco is not null);
        RuleFor(r => r.Avaliacao).InclusiveBetween(0m, 5m).WithMessage("A avaliação deve ser de 0 a 5.");
    }
}

public sealed class ServicoFornecedores(
    IRepositorio<Fornecedor> fornecedores,
    IRepositorio<ProdutoFornecedor> vinculos,
    IRepositorio<Produto> produtos,
    IUnitOfWork unidadeDeTrabalho,
    IValidator<FornecedorRequisicao> validador)
{
    public async Task<PaginaResultado<FornecedorResposta>> ListarAsync(ParametrosPaginacao paginacao, string? busca,
        bool incluirInativos, CancellationToken ct = default)
    {
        var termo = Validacao.NormalizarBusca(busca);
        var digitos = DocumentoFiscal.SomenteDigitos(busca);

        var pagina = await fornecedores.ListarAsync(paginacao,
            filtro: f =>
                (termo == "" ||
                 f.RazaoSocial.ToLower().Contains(termo) ||
                 (f.NomeFantasia != null && f.NomeFantasia.ToLower().Contains(termo)) ||
                 (digitos != "" && f.Cnpj.Contains(digitos))) &&
                (incluirInativos || f.Ativo),
            ordenarPor: f => f.RazaoSocial, cancellationToken: ct);

        return pagina.Mapear(FornecedorResposta.De);
    }

    public async Task<FornecedorResposta> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        FornecedorResposta.De(await fornecedores.ObterPorIdOuFalharAsync(id, ct));

    public async Task<FornecedorResposta> CriarAsync(FornecedorRequisicao requisicao, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(requisicao, ct);
        var fornecedor = new Fornecedor();
        await PreencherAsync(fornecedor, requisicao, ct);
        fornecedores.Adicionar(fornecedor);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return FornecedorResposta.De(fornecedor);
    }

    public async Task<FornecedorResposta> AtualizarAsync(Guid id, FornecedorRequisicao requisicao, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(requisicao, ct);
        var fornecedor = await fornecedores.ObterPorIdOuFalharAsync(id, ct);
        await PreencherAsync(fornecedor, requisicao, ct);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return FornecedorResposta.De(fornecedor);
    }

    /// <summary>Exclusão lógica: o fornecedor sai das listas, mas o histórico de preços e orçamentos é preservado.</summary>
    public async Task DesativarAsync(Guid id, CancellationToken ct = default)
    {
        var fornecedor = await fornecedores.ObterPorIdOuFalharAsync(id, ct);
        fornecedor.Ativo = false;
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    public async Task ReativarAsync(Guid id, CancellationToken ct = default)
    {
        var fornecedor = await fornecedores.ObterPorIdOuFalharAsync(id, ct);
        fornecedor.Ativo = true;
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    public async Task<IReadOnlyList<ProdutoDoFornecedorResposta>> ListarProdutosAsync(Guid fornecedorId, CancellationToken ct = default)
    {
        await fornecedores.ObterPorIdOuFalharAsync(fornecedorId, ct);
        var lista = await vinculos.ListarTodosAsync(v => v.FornecedorId == fornecedorId, ct);
        var dadosProdutos = (await produtos.ListarPorIdsAsync(lista.Select(v => v.ProdutoId), ct)).ToDictionary(p => p.Id);

        return lista
            .Select(v =>
            {
                dadosProdutos.TryGetValue(v.ProdutoId, out var produto);
                return new ProdutoDoFornecedorResposta(v.ProdutoId, produto?.Codigo, produto?.Nome, v.CodigoNoFornecedor,
                    v.PrecoAtual, v.PrazoEntregaDias, v.Disponivel, v.PrecoAtualizadoEm);
            })
            .OrderBy(p => p.ProdutoNome)
            .ToList();
    }

    private async Task PreencherAsync(Fornecedor fornecedor, FornecedorRequisicao requisicao, CancellationToken ct)
    {
        var cnpj = DocumentoFiscal.SomenteDigitos(requisicao.Cnpj);
        var idAtual = fornecedor.Id;

        if (await fornecedores.ExisteAsync(f => f.Id != idAtual && f.Cnpj == cnpj, ct))
            throw new ConflitoException("Já existe um fornecedor com este CNPJ.");

        fornecedor.RazaoSocial = requisicao.RazaoSocial.Trim();
        fornecedor.NomeFantasia = Validacao.TextoOpcional(requisicao.NomeFantasia);
        fornecedor.Cnpj = cnpj;
        fornecedor.Contato = Validacao.TextoOpcional(requisicao.Contato);
        fornecedor.Telefone = Validacao.TextoOpcional(requisicao.Telefone);
        fornecedor.Email = Validacao.TextoOpcional(requisicao.Email)?.ToLowerInvariant();
        fornecedor.Endereco = EnderecoPadronizado.Padronizar(requisicao.Endereco);
        fornecedor.Avaliacao = requisicao.Avaliacao;
    }
}
