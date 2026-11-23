using Microsoft.AspNetCore.Identity;
using OrcaFacil.Application.Abstracoes;

namespace OrcaFacil.Infrastructure.Seguranca;

/// <summary>
/// Usa o PasswordHasher do ASP.NET Core Identity: PBKDF2 com HMAC-SHA512, 100 mil iterações
/// e um "sal" aleatório por senha. Na prática:
/// - a mesma senha gera hashes diferentes a cada vez (por causa do sal);
/// - descobrir a senha a partir do hash exigiria testar bilhões de combinações, cada uma lenta de propósito.
/// </summary>
public sealed class HasherSenha : IHasherSenha
{
    // O PasswordHasher pede um "usuário" como parâmetro, mas não o usa no algoritmo padrão.
    private static readonly object UsuarioNaoUtilizado = new();
    private readonly PasswordHasher<object> _hasher = new();

    public string GerarHash(string senha) => _hasher.HashPassword(UsuarioNaoUtilizado, senha);

    public bool Verificar(string senhaHash, string senhaInformada) =>
        _hasher.VerifyHashedPassword(UsuarioNaoUtilizado, senhaHash, senhaInformada)
            != PasswordVerificationResult.Failed;
}
