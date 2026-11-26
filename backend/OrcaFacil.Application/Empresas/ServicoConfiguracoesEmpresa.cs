using FluentValidation;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Autenticacao;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Application.Empresas;

public sealed record ConfiguracoesEmpresaResposta(Guid EmpresaId, string Nome, string RazaoSocial, string Cnpj,
    SegmentoEmpresa Segmento, ModoOrcamento ModoOrcamentoPadrao, string? Telefone, string? Email, Endereco? Endereco);

/// <param name="ModoOrcamentoPadrao">Unico (itens exatos, ex.: loja de pijamas) ou Comparativo (ex.: papelaria).</param>
/// <param name="Telefone">Telefone/WhatsApp da loja (aparece no PDF e no texto do WhatsApp).</param>
/// <remarks>É um PUT: os campos de contato não enviados ficam vazios.</remarks>
public sealed record ConfiguracoesEmpresaRequisicao(
    ModoOrcamento ModoOrcamentoPadrao,
    string? Telefone = null,
    string? Email = null,
    Endereco? Endereco = null);

public sealed class ConfiguracoesEmpresaValidador : AbstractValidator<ConfiguracoesEmpresaRequisicao>
{
    public ConfiguracoesEmpresaValidador()
    {
        RuleFor(r => r.ModoOrcamentoPadrao).IsInEnum().WithMessage("Modo inválido: use Unico ou Comparativo.");
        RuleFor(r => r.Telefone).MaximumLength(Empresa.TamanhoMaximoTelefone);
        RuleFor(r => r.Email!).EmailValido().When(r => !string.IsNullOrWhiteSpace(r.Email));
        RuleFor(r => r.Endereco!).SetValidator(new EnderecoValidador()).When(r => r.Endereco is not null);
    }
}

/// <summary>Configurações da empresa logada (sempre a do token: uma empresa nunca vê a outra).</summary>
public sealed class ServicoConfiguracoesEmpresa(
    IRepositorio<Empresa> empresas,
    IEmpresaAtual empresaAtual,
    IUnitOfWork unidadeDeTrabalho,
    IValidator<ConfiguracoesEmpresaRequisicao> validador)
{
    public async Task<ConfiguracoesEmpresaResposta> ObterAsync(CancellationToken ct = default) =>
        Mapear(await EmpresaLogadaAsync(ct));

    public async Task<ConfiguracoesEmpresaResposta> AtualizarAsync(ConfiguracoesEmpresaRequisicao requisicao,
        CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(requisicao, ct);
        var empresa = await EmpresaLogadaAsync(ct);
        empresa.DefinirModoOrcamentoPadrao(requisicao.ModoOrcamentoPadrao);
        empresa.AtualizarContato(requisicao.Telefone, requisicao.Email, EnderecoPadronizado.Padronizar(requisicao.Endereco));
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return Mapear(empresa);
    }

    private async Task<Empresa> EmpresaLogadaAsync(CancellationToken ct)
    {
        // Empresa não tem filtro automático (ela É o "dono" dos dados): por isso usamos sempre o Id do token.
        var empresaId = empresaAtual.EmpresaId ?? throw new NaoAutorizadoException("Faça login para continuar.");
        return await empresas.ObterPorIdOuFalharAsync(empresaId, ct);
    }

    private static ConfiguracoesEmpresaResposta Mapear(Empresa empresa) =>
        new(empresa.Id, empresa.NomeExibicao, empresa.RazaoSocial, empresa.Cnpj, empresa.Segmento,
            empresa.ModoOrcamentoPadrao, empresa.Telefone, empresa.Email, empresa.Endereco);
}
