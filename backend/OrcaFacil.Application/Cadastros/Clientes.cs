using FluentValidation;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Autenticacao;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Application.Cadastros;

/// <param name="Documento">CPF (pessoa física) ou CNPJ (pessoa jurídica). Opcional; com ou sem pontuação.</param>
public sealed record ClienteRequisicao(
    string Nome,
    TipoPessoa TipoPessoa,
    string? Documento,
    string? Telefone,
    string? Email,
    Endereco? Endereco,
    string? Observacoes);

public sealed record ClienteResposta(
    Guid Id, string Nome, TipoPessoa TipoPessoa, string? Documento, string? Telefone, string? Email,
    Endereco? Endereco, string? Observacoes, bool Ativo)
{
    public static ClienteResposta De(Cliente c) =>
        new(c.Id, c.Nome, c.TipoPessoa, c.Documento, c.Telefone, c.Email, c.Endereco, c.Observacoes, c.Ativo);
}

public sealed class ClienteValidador : AbstractValidator<ClienteRequisicao>
{
    public ClienteValidador()
    {
        RuleFor(r => r.Nome).NotEmpty().WithMessage("Informe o nome ou a razão social.").MaximumLength(200);
        RuleFor(r => r.TipoPessoa).IsInEnum().WithMessage("Tipo de pessoa inválido (Fisica ou Juridica).");

        // O documento é opcional; se vier, é conferido pelo dígito verificador conforme o tipo de pessoa.
        RuleFor(r => r.Documento)
            .Must((requisicao, documento) => requisicao.TipoPessoa == TipoPessoa.Fisica
                ? DocumentoFiscal.CpfValido(documento)
                : DocumentoFiscal.CnpjValido(documento))
            .WithMessage(r => r.TipoPessoa == TipoPessoa.Fisica ? "CPF inválido." : "CNPJ inválido.")
            .When(r => !string.IsNullOrWhiteSpace(r.Documento));

        RuleFor(r => r.Telefone).MaximumLength(20);
        RuleFor(r => r.Email!).EmailValido().When(r => !string.IsNullOrWhiteSpace(r.Email));
        RuleFor(r => r.Endereco!).SetValidator(new EnderecoValidador()).When(r => r.Endereco is not null);
        RuleFor(r => r.Observacoes).MaximumLength(2000);
    }
}

public sealed class ServicoClientes(
    IRepositorio<Cliente> clientes,
    IUnitOfWork unidadeDeTrabalho,
    IValidator<ClienteRequisicao> validador)
{
    public async Task<PaginaResultado<ClienteResposta>> ListarAsync(ParametrosPaginacao paginacao, string? busca,
        bool incluirInativos, CancellationToken ct = default)
    {
        var termo = Validacao.NormalizarBusca(busca);
        var digitos = DocumentoFiscal.SomenteDigitos(busca);

        var pagina = await clientes.ListarAsync(paginacao,
            filtro: c =>
                (termo == "" ||
                 c.Nome.ToLower().Contains(termo) ||
                 (c.Email != null && c.Email.Contains(termo)) ||
                 (digitos != "" && c.Documento != null && c.Documento.Contains(digitos))) &&
                (incluirInativos || c.Ativo),
            ordenarPor: c => c.Nome, cancellationToken: ct);

        return pagina.Mapear(ClienteResposta.De);
    }

    public async Task<ClienteResposta> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        ClienteResposta.De(await clientes.ObterPorIdOuFalharAsync(id, ct));

    public async Task<ClienteResposta> CriarAsync(ClienteRequisicao requisicao, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(requisicao, ct);
        var cliente = new Cliente();
        await PreencherAsync(cliente, requisicao, ct);
        clientes.Adicionar(cliente);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return ClienteResposta.De(cliente);
    }

    public async Task<ClienteResposta> AtualizarAsync(Guid id, ClienteRequisicao requisicao, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(requisicao, ct);
        var cliente = await clientes.ObterPorIdOuFalharAsync(id, ct);
        await PreencherAsync(cliente, requisicao, ct);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return ClienteResposta.De(cliente);
    }

    public async Task DesativarAsync(Guid id, CancellationToken ct = default)
    {
        var cliente = await clientes.ObterPorIdOuFalharAsync(id, ct);
        cliente.Ativo = false;
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    public async Task ReativarAsync(Guid id, CancellationToken ct = default)
    {
        var cliente = await clientes.ObterPorIdOuFalharAsync(id, ct);
        cliente.Ativo = true;
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    private async Task PreencherAsync(Cliente cliente, ClienteRequisicao requisicao, CancellationToken ct)
    {
        var documento = DocumentoFiscal.SomenteDigitos(requisicao.Documento);
        var idAtual = cliente.Id;

        if (documento.Length > 0 && await clientes.ExisteAsync(c => c.Id != idAtual && c.Documento == documento, ct))
            throw new ConflitoException("Já existe um cliente com este CPF/CNPJ.");

        cliente.Nome = requisicao.Nome.Trim();
        cliente.TipoPessoa = requisicao.TipoPessoa; // precisa vir antes do documento (define CPF ou CNPJ)
        cliente.DefinirDocumento(documento);
        cliente.Telefone = Validacao.TextoOpcional(requisicao.Telefone);
        cliente.Email = Validacao.TextoOpcional(requisicao.Email)?.ToLowerInvariant();
        cliente.Endereco = EnderecoPadronizado.Padronizar(requisicao.Endereco);
        cliente.Observacoes = Validacao.TextoOpcional(requisicao.Observacoes);
    }
}
