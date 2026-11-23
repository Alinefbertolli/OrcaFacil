using AwesomeAssertions;
using OrcaFacil.Infrastructure.Seguranca;

namespace OrcaFacil.Tests.Infrastructure;

/// <summary>Testa o hash de senha REAL (PBKDF2), não o fake.</summary>
public class HasherSenhaTests
{
    private readonly HasherSenha _hasher = new();

    [Fact]
    public void Hash_nao_contem_a_senha_e_confere_com_a_senha_correta()
    {
        var hash = _hasher.GerarHash("Senha123");

        hash.Should().NotContain("Senha123");
        _hasher.Verificar(hash, "Senha123").Should().BeTrue();
        _hasher.Verificar(hash, "senha123").Should().BeFalse("maiúsculas e minúsculas importam");
    }

    [Fact]
    public void Mesma_senha_gera_hashes_diferentes_por_causa_do_sal() =>
        _hasher.GerarHash("Senha123").Should().NotBe(_hasher.GerarHash("Senha123"));
}
