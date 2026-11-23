using Microsoft.EntityFrameworkCore;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Domain.Entidades;

namespace OrcaFacil.Infrastructure.Persistencia.Repositorios;

/// <summary>
/// Consultas do login e da recuperação de senha. Acontecem ANTES de existir uma empresa logada,
/// por isso usam IgnoreQueryFilters() — o único lugar do sistema que desliga o filtro por empresa.
/// Cada consulta busca por um dado único (e-mail, hash de token, Id), então não há risco de
/// misturar dados de empresas diferentes.
/// </summary>
public sealed class RepositorioAutenticacao(OrcaFacilDbContext contexto) : IRepositorioAutenticacao
{
    private IQueryable<Usuario> UsuariosSemFiltro => contexto.Usuarios.IgnoreQueryFilters();

    public Task<Usuario?> ObterUsuarioPorEmailAsync(string emailNormalizado, CancellationToken cancellationToken = default) =>
        UsuariosSemFiltro.FirstOrDefaultAsync(u => u.Email == emailNormalizado, cancellationToken);

    public Task<Usuario?> ObterUsuarioPorIdAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        UsuariosSemFiltro.FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken);

    public Task<bool> EmailEmUsoAsync(string emailNormalizado, CancellationToken cancellationToken = default) =>
        UsuariosSemFiltro.AnyAsync(u => u.Email == emailNormalizado, cancellationToken);

    public Task<Empresa?> ObterEmpresaAsync(Guid empresaId, CancellationToken cancellationToken = default) =>
        contexto.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, cancellationToken);

    public Task<bool> CnpjEmUsoAsync(string cnpj, CancellationToken cancellationToken = default) =>
        contexto.Empresas.AnyAsync(e => e.Cnpj == cnpj, cancellationToken);

    public Task<RefreshToken?> ObterRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        contexto.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListarRefreshTokensAtivosAsync(Guid usuarioId, DateTime agoraUtc,
        CancellationToken cancellationToken = default) =>
        await contexto.RefreshTokens
            .Where(t => t.UsuarioId == usuarioId && t.RevogadoEm == null && t.ExpiraEm > agoraUtc)
            .ToListAsync(cancellationToken);

    public Task<TokenRecuperacaoSenha?> ObterTokenRecuperacaoAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        contexto.TokensRecuperacaoSenha.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public void Adicionar(Empresa empresa) => contexto.Empresas.Add(empresa);
    public void Adicionar(Usuario usuario) => contexto.Usuarios.Add(usuario);
    public void Adicionar(RefreshToken refreshToken) => contexto.RefreshTokens.Add(refreshToken);
    public void Adicionar(TokenRecuperacaoSenha token) => contexto.TokensRecuperacaoSenha.Add(token);
    public void Adicionar(LogSistema log) => contexto.Logs.Add(log);
}
