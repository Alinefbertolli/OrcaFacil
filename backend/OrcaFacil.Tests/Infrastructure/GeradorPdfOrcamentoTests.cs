using AwesomeAssertions;
using OrcaFacil.Application.Orcamentos;
using OrcaFacil.Infrastructure.Documentos;
using UglyToad.PdfPig;

namespace OrcaFacil.Tests.Infrastructure;

/// <summary>Gera um PDF de verdade e lê o texto de volta com o PdfPig (a mesma biblioteca da importação).</summary>
public class GeradorPdfOrcamentoTests
{
    [Fact]
    public void Pdf_tem_o_numero_os_itens_e_os_totais()
    {
        var documento = new DocumentoOrcamento("ORC-2026-000001", new DateOnly(2026, 11, 25), new DateOnly(2026, 12, 2),
            new LojaDocumento("Papelaria Exemplo", "11.222.333/0001-81", "(11) 3333-4444", "contato@papelaria.com",
                "Rua A, 10 · Centro - Campinas - SP"),
            new ClienteDocumento("Maria", null),
            [
                new OpcaoDocumento("Econômica", 28.50m,
                [
                    new ItemDocumento("Caderno 10 matérias", null, 2, "UN", 12m, 24m),
                    new ItemDocumento("Borracha branca", "Mercur", 3, "UN", 1.50m, 4.50m)
                ]),
                new OpcaoDocumento("Marcas reconhecidas", 64.50m,
                [
                    new ItemDocumento("Caderno 10 matérias Tilibra", "Tilibra", 2, "UN", 30m, 60m),
                    new ItemDocumento("Borracha branca", "Mercur", 3, "UN", 1.50m, 4.50m)
                ])
            ],
            OpcaoJaEscolhida: false);

        var bytes = new GeradorPdfOrcamento(pastaFontes: null).Gerar(documento);

        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
        using var pdf = PdfDocument.Open(bytes);
        // Junta o texto e tira os espaços: a posição exata dos espaços varia conforme a extração.
        var texto = string.Concat(string.Join("", pdf.GetPages().Select(p => p.Text)).Where(c => !char.IsWhiteSpace(c)));
        texto.Should().Contain("ORC-2026-000001")
            .And.Contain("PapelariaExemplo")
            .And.Contain("OpçãoEconômica")
            .And.Contain("R$28,50")
            .And.Contain("R$64,50")
            .And.Contain("02/12/2026");
    }
}
