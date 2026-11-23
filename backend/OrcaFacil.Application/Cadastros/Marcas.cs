using FluentValidation;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Application.Cadastros;

/// <param name="NivelReconhecimento">1 (pouco conhecida) a 5 (muito reconhecida). Usado no orçamento "Marcas reconhecidas".</param>
public sealed record MarcaRequisicao(string Nome, int NivelReconhecimento = 3);

public sealed record MarcaResposta(Guid Id, string Nome, int NivelReconhecimento)
{
    public static MarcaResposta De(Marca m) => new(m.Id, m.Nome, m.NivelReconhecimento);
}

public sealed class MarcaValidador : AbstractValidator<MarcaRequisicao>
{
    public MarcaValidador()
    {
        RuleFor(r => r.Nome).NotEmpty().WithMessage("Informe o nome da marca.").MaximumLength(100);
        RuleFor(r => r.NivelReconhecimento).InclusiveBetween(Marca.NivelMinimo, Marca.NivelMaximo)
            .WithMessage($"O nível de reconhecimento deve ser de {Marca.NivelMinimo} a {Marca.NivelMaximo}.");
    }
}

public sealed class ServicoMarcas(
    IRepositorio<Marca> marcas,
    IRepositorio<Produto> produtos,
    IUnitOfWork unidadeDeTrabalho,
    IValidator<MarcaRequisicao> validador)
{
    public async Task<PaginaResultado<MarcaResposta>> ListarAsync(ParametrosPaginacao paginacao, string? busca,
        CancellationToken ct = default)
    {
        var termo = Validacao.NormalizarBusca(busca);
        var pagina = await marcas.ListarAsync(paginacao,
            filtro: m => termo == "" || m.Nome.ToLower().Contains(termo),
            ordenarPor: m => m.Nome, cancellationToken: ct);
        return pagina.Mapear(MarcaResposta.De);
    }

    public async Task<MarcaResposta> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        MarcaResposta.De(await marcas.ObterPorIdOuFalharAsync(id, ct));

    public async Task<MarcaResposta> CriarAsync(MarcaRequisicao requisicao, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(requisicao, ct);
        var marca = new Marca();
        await PreencherAsync(marca, requisicao, ct);
        marcas.Adicionar(marca);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return MarcaResposta.De(marca);
    }

    public async Task<MarcaResposta> AtualizarAsync(Guid id, MarcaRequisicao requisicao, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(requisicao, ct);
        var marca = await marcas.ObterPorIdOuFalharAsync(id, ct);
        await PreencherAsync(marca, requisicao, ct);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
        return MarcaResposta.De(marca);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var marca = await marcas.ObterPorIdOuFalharAsync(id, ct);
        if (await produtos.ExisteAsync(p => p.MarcaId == id, ct))
            throw new ConflitoException("A marca está em uso por produtos e não pode ser excluída.");

        marcas.Remover(marca);
        await unidadeDeTrabalho.SalvarAlteracoesAsync(ct);
    }

    private async Task PreencherAsync(Marca marca, MarcaRequisicao requisicao, CancellationToken ct)
    {
        var nome = requisicao.Nome.Trim();
        var nomeMinusculo = nome.ToLower();
        var idAtual = marca.Id;

        if (await marcas.ExisteAsync(m => m.Id != idAtual && m.Nome.ToLower() == nomeMinusculo, ct))
            throw new ConflitoException($"Já existe uma marca chamada \"{nome}\".");

        marca.Nome = nome;
        marca.NivelReconhecimento = requisicao.NivelReconhecimento;
    }
}
