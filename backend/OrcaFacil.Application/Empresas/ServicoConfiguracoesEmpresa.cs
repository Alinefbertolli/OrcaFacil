using FluentValidation;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Application.Empresas;

public sealed record ConfiguracoesEmpresaResposta(Guid EmpresaId, string Nome, SegmentoEmpresa Segmento,
    ModoOrcamento ModoOrcamentoPadrao);

/// <param name="ModoOrcamentoPadrao">Unico (itens exatos, ex.: loja de pijamas) ou Comparativo (ex.: papelaria).</param>
public sealed record ConfiguracoesEmpresaRequisicao(ModoOrcamento ModoOrcamentoPadrao);

public sealed class ConfiguracoesEmpresaValidador : AbstractValidator<ConfiguracoesEmpresaRequisicao>
{
    public ConfiguracoesEmpresaValidador()
    {
        RuleFor(r => r.ModoOrcamentoPadrao).IsInEnum().WithMessage("Modo inválido: use Unico ou Comparativo.");
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
        new(empresa.Id, empresa.NomeExibicao, empresa.Segmento, empresa.ModoOrcamentoPadrao);
}
