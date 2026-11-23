using System.Linq.Expressions;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Tests.Application;

// "Fakes": implementações simples, em memória, das interfaces que os casos de uso usam.
// Permitem testar a LÓGICA dos serviços sem banco de dados, sem rede e em milissegundos.
// Isso só é possível porque os serviços dependem de interfaces (o "D" do SOLID).

/// <summary>Relógio controlado pelo teste (dá para "avançar o tempo" e testar expirações).</summary>
internal sealed class RelogioFixo(DateTimeOffset inicio) : TimeProvider
{
    public DateTimeOffset Agora { get; private set; } = inicio;
    public override DateTimeOffset GetUtcNow() => Agora;
    public void Avancar(TimeSpan intervalo) => Agora += intervalo;
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int VezesSalvo { get; private set; }

    public Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(++VezesSalvo);
}

/// <summary>"Hash" previsível para os testes: hash:senha.</summary>
internal sealed class FakeHasherSenha : IHasherSenha
{
    public string GerarHash(string senha) => $"hash:{senha}";
    public bool Verificar(string senhaHash, string senhaInformada) => senhaHash == $"hash:{senhaInformada}";
}

internal sealed class FakeServicoTokens(TimeProvider relogio) : IServicoTokens
{
    private int _contador;
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    public TokenAcessoGerado GerarTokenAcesso(Usuario usuario) =>
        new($"acesso-{usuario.Id}-{++_contador}", Agora.AddMinutes(15));

    public TokenGerado GerarRefreshToken() => Gerar("refresh", Agora.AddDays(7));
    public TokenGerado GerarTokenRecuperacaoSenha() => Gerar("recuperacao", Agora.AddMinutes(60));
    public string CalcularHash(string valor) => $"h:{valor}";

    private TokenGerado Gerar(string prefixo, DateTime expiraEm)
    {
        var valor = $"{prefixo}-{++_contador}";
        return new TokenGerado(valor, CalcularHash(valor), expiraEm);
    }
}

internal sealed class FakeEnvioEmail : IEnvioEmail
{
    public List<(string Destinatario, string Token)> Enviados { get; } = [];

    public Task EnviarRecuperacaoSenhaAsync(string destinatario, string nome, string token, DateTime expiraEm,
        CancellationToken cancellationToken = default)
    {
        Enviados.Add((destinatario, token));
        return Task.CompletedTask;
    }
}

internal sealed class FakeUsuarioAtual(Guid? usuarioId) : IUsuarioAtual
{
    public Guid? UsuarioId { get; } = usuarioId;
}

internal sealed class FakeRepositorioAutenticacao : IRepositorioAutenticacao
{
    public List<Empresa> Empresas { get; } = [];
    public List<Usuario> Usuarios { get; } = [];
    public List<RefreshToken> RefreshTokens { get; } = [];
    public List<TokenRecuperacaoSenha> TokensRecuperacao { get; } = [];
    public List<LogSistema> Logs { get; } = [];

    public Task<Usuario?> ObterUsuarioPorEmailAsync(string emailNormalizado, CancellationToken ct = default) =>
        Task.FromResult(Usuarios.FirstOrDefault(u => u.Email == emailNormalizado));

    public Task<Usuario?> ObterUsuarioPorIdAsync(Guid usuarioId, CancellationToken ct = default) =>
        Task.FromResult(Usuarios.FirstOrDefault(u => u.Id == usuarioId));

    public Task<bool> EmailEmUsoAsync(string emailNormalizado, CancellationToken ct = default) =>
        Task.FromResult(Usuarios.Any(u => u.Email == emailNormalizado));

    public Task<Empresa?> ObterEmpresaAsync(Guid empresaId, CancellationToken ct = default) =>
        Task.FromResult(Empresas.FirstOrDefault(e => e.Id == empresaId));

    public Task<bool> CnpjEmUsoAsync(string cnpj, CancellationToken ct = default) =>
        Task.FromResult(Empresas.Any(e => e.Cnpj == cnpj));

    public Task<RefreshToken?> ObterRefreshTokenAsync(string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(RefreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task<IReadOnlyList<RefreshToken>> ListarRefreshTokensAtivosAsync(Guid usuarioId, DateTime agoraUtc,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RefreshToken>>(RefreshTokens
            .Where(t => t.UsuarioId == usuarioId && t.RevogadoEm == null && t.ExpiraEm > agoraUtc)
            .ToList());

    public Task<TokenRecuperacaoSenha?> ObterTokenRecuperacaoAsync(string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(TokensRecuperacao.FirstOrDefault(t => t.TokenHash == tokenHash));

    public void Adicionar(Empresa empresa) => Empresas.Add(empresa);
    public void Adicionar(Usuario usuario) => Usuarios.Add(usuario);
    public void Adicionar(RefreshToken refreshToken) => RefreshTokens.Add(refreshToken);
    public void Adicionar(TokenRecuperacaoSenha token) => TokensRecuperacao.Add(token);
    public void Adicionar(LogSistema log) => Logs.Add(log);
}

/// <summary>Repositório genérico em memória.</summary>
internal sealed class FakeRepositorio<T> : IRepositorio<T> where T : EntidadeBase
{
    public List<T> Itens { get; } = [];

    public Task<T?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Itens.FirstOrDefault(e => e.Id == id));

    public async Task<T> ObterPorIdOuFalharAsync(Guid id, CancellationToken ct = default) =>
        await ObterPorIdAsync(id, ct) ?? throw new NaoEncontradoException(typeof(T).Name, id);

    public Task<bool> ExisteAsync(Expression<Func<T, bool>> condicao, CancellationToken ct = default) =>
        Task.FromResult(Itens.AsQueryable().Any(condicao));

    public Task<T?> ObterAsync(Expression<Func<T, bool>> condicao, CancellationToken ct = default) =>
        Task.FromResult(Itens.AsQueryable().FirstOrDefault(condicao));

    public Task<IReadOnlyList<T>> ListarTodosAsync(Expression<Func<T, bool>> filtro, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<T>>(Itens.AsQueryable().Where(filtro).ToList());

    public Task<IReadOnlyList<T>> ListarPorIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var conjunto = ids.ToHashSet();
        return Task.FromResult<IReadOnlyList<T>>(Itens.Where(e => conjunto.Contains(e.Id)).ToList());
    }

    public Task<PaginaResultado<T>> ListarAsync(ParametrosPaginacao paginacao, Expression<Func<T, bool>>? filtro = null,
        Expression<Func<T, object>>? ordenarPor = null, bool decrescente = false, CancellationToken ct = default)
    {
        var consulta = Itens.AsQueryable();
        if (filtro is not null) consulta = consulta.Where(filtro);
        if (ordenarPor is not null) consulta = decrescente ? consulta.OrderByDescending(ordenarPor) : consulta.OrderBy(ordenarPor);

        var itens = consulta.Skip(paginacao.Saltar).Take(paginacao.TamanhoPagina).ToList();
        return Task.FromResult(new PaginaResultado<T>(itens, paginacao.Pagina, paginacao.TamanhoPagina, consulta.LongCount()));
    }

    public void Adicionar(T entidade) => Itens.Add(entidade);
    public void Remover(T entidade) => Itens.Remove(entidade);
}
