using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using OrcaFacil.API.Erros;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;

namespace OrcaFacil.Tests.Api;

public class MapeadorDeExcecoesTests
{
    [Fact]
    public void Validacao_vira_400_com_erros_agrupados_por_campo()
    {
        var excecao = new ValidationException(
        [
            new ValidationFailure("Nome", "Obrigatório."),
            new ValidationFailure("Nome", "Máximo de 200 caracteres."),
            new ValidationFailure("Preco", "Deve ser positivo.")
        ]);

        var problema = MapeadorDeExcecoes.Mapear(excecao, mostrarDetalhesTecnicos: false);

        problema.Status.Should().Be(400);
        var validacao = problema.Should().BeOfType<ValidationProblemDetails>().Subject;
        validacao.Errors["Nome"].Should().HaveCount(2);
        validacao.Errors["Preco"].Should().ContainSingle();
    }

    [Fact]
    public void Nao_autorizado_vira_401() =>
        MapeadorDeExcecoes.Mapear(new NaoAutorizadoException("E-mail ou senha inválidos."), false)
            .Status.Should().Be(401);

    [Fact]
    public void Nao_encontrado_vira_404() =>
        MapeadorDeExcecoes.Mapear(new NaoEncontradoException("Produto", Guid.Empty), false)
            .Status.Should().Be(404);

    [Fact]
    public void Conflito_vira_409_com_a_mensagem() =>
        MapeadorDeExcecoes.Mapear(new ConflitoException("Código já cadastrado."), false)
            .Detail.Should().Be("Código já cadastrado.");

    [Fact]
    public void Regra_de_negocio_vira_422()
    {
        var problema = MapeadorDeExcecoes.Mapear(new DomainException("Saldo insuficiente."), false);

        problema.Status.Should().Be(422);
        problema.Detail.Should().Be("Saldo insuficiente.");
    }

    [Fact]
    public void Erro_inesperado_em_producao_esconde_detalhes_tecnicos()
    {
        var problema = MapeadorDeExcecoes.Mapear(
            new InvalidOperationException("Connection string com senha 123"), mostrarDetalhesTecnicos: false);

        problema.Status.Should().Be(500);
        problema.Detail.Should().NotContain("senha");
    }

    [Fact]
    public void Erro_inesperado_em_desenvolvimento_mostra_o_tipo_e_a_mensagem() =>
        MapeadorDeExcecoes.Mapear(new InvalidOperationException("detalhe"), mostrarDetalhesTecnicos: true)
            .Detail.Should().Be("InvalidOperationException: detalhe");
}
