using System.Text;
using AwesomeAssertions;
using FluentValidation;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Application;

public class ValidadorArquivoTests
{
    private static readonly byte[] InicioPdf = Encoding.ASCII.GetBytes("%PDF-1.7 ...");
    private static readonly byte[] InicioZip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00];
    private static readonly byte[] InicioPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A];
    private static readonly byte[] InicioJpg = [0xFF, 0xD8, 0xFF, 0xE0];

    [Theory]
    [MemberData(nameof(ArquivosValidos))]
    public void Aceita_os_formatos_suportados(string nome, byte[] inicio, FormatoImportacao esperado) =>
        ValidadorArquivo.Validar(nome, 1000, inicio, 10).Should().Be(esperado);

    public static TheoryData<string, byte[], FormatoImportacao> ArquivosValidos() => new()
    {
        { "lista.pdf", InicioPdf, FormatoImportacao.Pdf },
        { "lista.XLSX", InicioZip, FormatoImportacao.Excel },
        { "lista.docx", InicioZip, FormatoImportacao.Word },
        { "foto.png", InicioPng, FormatoImportacao.Imagem },
        { "foto.jpeg", InicioJpg, FormatoImportacao.Imagem },
        { "lista.txt", Encoding.UTF8.GetBytes("20 cx caneta"), FormatoImportacao.Texto },
    };

    [Fact]
    public void Arquivo_renomeado_e_recusado_pela_assinatura() =>
        FluentActions.Invoking(() => ValidadorArquivo.Validar("lista.pdf", 1000, InicioPng, 10))
            .Should().Throw<ValidationException>().WithMessage("*não corresponde*");

    [Fact]
    public void Extensao_nao_suportada_e_recusada() =>
        FluentActions.Invoking(() => ValidadorArquivo.Validar("programa.exe", 1000, [0x4D, 0x5A], 10))
            .Should().Throw<ValidationException>().WithMessage("*não suportado*");

    [Fact]
    public void Formato_antigo_do_office_recebe_orientacao() =>
        FluentActions.Invoking(() => ValidadorArquivo.Validar("lista.xls", 1000, [0xD0, 0xCF], 10))
            .Should().Throw<ValidationException>().WithMessage("*Salvar como*");

    [Fact]
    public void Arquivo_acima_do_limite_e_recusado() =>
        FluentActions.Invoking(() => ValidadorArquivo.Validar("lista.pdf", 11L * 1024 * 1024, InicioPdf, 10))
            .Should().Throw<ValidationException>().WithMessage("*10 MB*");

    [Fact]
    public void Arquivo_vazio_e_recusado() =>
        FluentActions.Invoking(() => ValidadorArquivo.Validar("lista.txt", 0, [], 10))
            .Should().Throw<ValidationException>();
}
