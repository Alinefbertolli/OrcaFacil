using AwesomeAssertions;
using OrcaFacil.Application.Autenticacao;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Application;

public class ValidadoresAutenticacaoTests
{
    private static RegistrarEmpresaRequisicao Registro(string senha = "Senha123", string cnpj = "11.222.333/0001-81",
        string email = "ana@loja.com") =>
        new("Loja Exemplo Ltda", null, cnpj, SegmentoEmpresa.Outro, "Ana", email, senha);

    [Theory]
    [InlineData("Senha123", true)]       // letras + números, 8 caracteres
    [InlineData("Abc1234", false)]       // só 7 caracteres
    [InlineData("somenteletras", false)] // sem número
    [InlineData("12345678", false)]      // sem letra
    [InlineData("", false)]              // vazia
    public void Politica_de_senha(string senha, bool valida) =>
        new RegistrarEmpresaValidador().Validate(Registro(senha: senha))
            .Errors.Any(e => e.PropertyName == "Senha").Should().Be(!valida);

    [Fact]
    public void Cnpj_invalido_e_recusado() =>
        new RegistrarEmpresaValidador().Validate(Registro(cnpj: "11.222.333/0001-00"))
            .Errors.Should().Contain(e => e.PropertyName == "Cnpj");

    [Fact]
    public void Email_invalido_e_recusado() =>
        new RegistrarEmpresaValidador().Validate(Registro(email: "nao-e-email"))
            .Errors.Should().Contain(e => e.PropertyName == "Email");

    [Fact]
    public void Registro_completo_e_valido() =>
        new RegistrarEmpresaValidador().Validate(Registro()).IsValid.Should().BeTrue();
}
