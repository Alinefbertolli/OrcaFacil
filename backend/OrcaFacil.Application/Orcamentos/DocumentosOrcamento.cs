using System.Globalization;
using System.Text;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Application.Orcamentos;

// ---------------------------------------------------------------- O "conteúdo" do orçamento para o cliente

/// <summary>
/// Tudo o que vai para o CLIENTE FINAL, já filtrado e conferido. O PDF e o texto do WhatsApp
/// são duas "aparências" diferentes deste mesmo conteúdo (assim os dois nunca discordam).
/// Só tem o que o cliente pode ver: nada de custo, fornecedor, estoque ou observações internas.
/// </summary>
/// <param name="EmitidoEm">Data e hora de criação do orçamento, já no horário de Brasília.</param>
public sealed record DocumentoOrcamento(
    string Numero,
    DateTime EmitidoEm,
    DateOnly ValidadeAte,
    LojaDocumento Loja,
    ClienteDocumento? Cliente,
    IReadOnlyList<OpcaoDocumento> Opcoes,
    bool OpcaoJaEscolhida);

public sealed record LojaDocumento(string Nome, string Cnpj, string? Telefone, string? Email, string? Endereco);

public sealed record ClienteDocumento(string Nome, string? Telefone);

public sealed record OpcaoDocumento(string Nome, decimal ValorTotal, IReadOnlyList<ItemDocumento> Itens);

public sealed record ItemDocumento(string Descricao, string? Marca, decimal Quantidade, string UnidadeMedida,
    decimal PrecoUnitario, decimal PrecoTotal);

/// <param name="Link">Abre o WhatsApp já com o texto (e com o número do cliente, se ele tiver telefone cadastrado).</param>
public sealed record MensagemWhatsAppResposta(string Texto, string Link, string? TelefoneCliente);

/// <summary>Gera o arquivo PDF (implementado na Infrastructure com PDFsharp/MigraDoc, licença MIT).</summary>
public interface IGeradorPdfOrcamento
{
    byte[] Gerar(DocumentoOrcamento documento);
}

// ---------------------------------------------------------------- Formatação brasileira

/// <summary>Valores no padrão brasileiro: R$ 1.234,56 · 2,5 · 02/12/2026. Usado no PDF e no WhatsApp.</summary>
public static class FormatoBr
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    // Montado à mão (e não com "C") para usar um espaço comum: o .NET põe um espaço "especial" depois do R$.
    public static string Dinheiro(decimal valor) => $"R$ {valor.ToString("N2", PtBr)}";

    public static string Quantidade(decimal valor) => valor.ToString("0.###", PtBr);

    public static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", PtBr);

    public static string DataHora(DateTime dataHora) => dataHora.ToString("dd/MM/yyyy 'às' HH:mm", PtBr);

    /// <summary>
    /// O banco guarda as datas em UTC (horário mundial). Para o cliente, mostramos o horário de Brasília.
    /// (Lojas em outros fusos — ex.: Manaus — poderão ter o fuso nas configurações da empresa no futuro.)
    /// </summary>
    public static DateTime HorarioDeBrasilia(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), FusoBrasilia.Value);

    private static readonly Lazy<TimeZoneInfo> FusoBrasilia = new(() =>
    {
        // "America/Sao_Paulo" é o nome padrão (Linux e Windows atual); o segundo é o nome antigo do Windows.
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        // Último recurso: UTC-3 fixo (o Brasil não tem horário de verão desde 2019).
        return TimeZoneInfo.CreateCustomTimeZone("Brasilia", TimeSpan.FromHours(-3), "Brasília", "Brasília");
    });

    public static string Cnpj(string cnpj) =>
        cnpj.Length == 14 ? $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}" : cnpj;

    public static string? Endereco(Endereco? e)
    {
        if (e is null) return null;
        var rua = string.Join(", ", new[] { e.Logradouro, e.Numero, e.Complemento }.Where(p => !string.IsNullOrWhiteSpace(p)));
        var cidade = string.Join(" - ", new[] { e.Bairro, e.Cidade, e.Uf }.Where(p => !string.IsNullOrWhiteSpace(p)));
        var partes = new[] { rua, cidade, e.Cep is { Length: 8 } cep ? $"CEP {cep[..5]}-{cep[5..]}" : e.Cep }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        var texto = string.Join(" · ", partes);
        return texto.Length == 0 ? null : texto;
    }
}

// ---------------------------------------------------------------- Texto do WhatsApp

/// <summary>
/// Monta o texto para colar no WhatsApp. Usa a formatação do próprio WhatsApp: *negrito*.
/// Função pura (sem banco): fácil de testar.
/// </summary>
public static class TextoWhatsApp
{
    public static string Montar(DocumentoOrcamento doc)
    {
        // Quebra de linha sempre "\n": o AppendLine do Windows usaria "\r\n", que alguns celulares mostram errado.
        var texto = new StringBuilder();
        void Linha(string conteudo = "") => texto.Append(conteudo).Append('\n');

        Linha($"*{doc.Loja.Nome}*");
        Linha($"*Orçamento {doc.Numero}*");
        if (doc.Cliente is not null)
            Linha($"Cliente: {doc.Cliente.Nome}");
        Linha($"Válido até {FormatoBr.Data(doc.ValidadeAte)}");

        foreach (var opcao in doc.Opcoes)
        {
            Linha();
            // Com várias opções, cada uma ganha um título; com uma só, basta a lista.
            if (doc.Opcoes.Count > 1)
                Linha($"*Opção {opcao.Nome}*");

            foreach (var item in opcao.Itens)
                Linha($"• {FormatoBr.Quantidade(item.Quantidade)} {item.UnidadeMedida} {DescricaoComMarca(item)} — " +
                      FormatoBr.Dinheiro(item.PrecoTotal));
            Linha($"*Total: {FormatoBr.Dinheiro(opcao.ValorTotal)}*");
        }

        Linha();
        Linha(doc.Opcoes.Count > 1
            ? "Para confirmar, responda com o nome da opção escolhida. 😊"
            : "Para confirmar o pedido, é só responder esta mensagem. 😊");
        if (doc.Loja.Telefone is not null)
            Linha($"Contato: {doc.Loja.Telefone}");

        return texto.ToString().TrimEnd();
    }

    /// <summary>"Caderno (Tilibra)", mas sem repetir a marca quando ela já está no nome ("Caderno Tilibra").</summary>
    private static string DescricaoComMarca(ItemDocumento item) =>
        item.Marca is null || item.Descricao.Contains(item.Marca, StringComparison.OrdinalIgnoreCase)
            ? item.Descricao
            : $"{item.Descricao} ({item.Marca})";

    /// <summary>
    /// Link "clique para conversar" do WhatsApp (wa.me). Com o telefone do cliente, abre a conversa com ele;
    /// sem telefone, o WhatsApp pede para escolher o contato.
    /// </summary>
    public static string Link(string texto, string? telefoneCliente)
    {
        var numero = NumeroInternacional(telefoneCliente);
        return $"https://wa.me/{numero}?text={Uri.EscapeDataString(texto)}";
    }

    /// <summary>"(11) 98765-4321" → "5511987654321". Retorna "" se não der para usar o número.</summary>
    public static string NumeroInternacional(string? telefone)
    {
        var digitos = DocumentoFiscal.SomenteDigitos(telefone ?? "");
        return digitos.Length switch
        {
            10 or 11 => "55" + digitos,                                  // DDD + número (Brasil)
            12 or 13 when digitos.StartsWith("55") => digitos,           // já veio com o 55
            _ => ""
        };
    }
}

// ---------------------------------------------------------------- Caso de uso

/// <summary>
/// Gera o PDF e o texto do WhatsApp de um orçamento. Regras (decisões da usuária, 25/11/2026):
/// 1) opções "automático": antes da escolha do cliente vão TODAS; depois, só a escolhida;
/// 2) se alguma opção que vai para o cliente tiver item sem preço, NÃO gera (nunca mandar valor incompleto).
/// </summary>
public sealed class ServicoDocumentosOrcamento(
    IRepositorioOrcamentos orcamentos,
    IRepositorio<Empresa> empresas,
    IRepositorio<Cliente> clientes,
    IEmpresaAtual empresaAtual,
    IGeradorPdfOrcamento geradorPdf)
{
    public async Task<(byte[] Conteudo, string NomeArquivo)> GerarPdfAsync(Guid orcamentoId, CancellationToken ct = default)
    {
        var documento = await MontarAsync(orcamentoId, ct);
        return (geradorPdf.Gerar(documento), $"{documento.Numero}.pdf");
    }

    public async Task<MensagemWhatsAppResposta> GerarWhatsAppAsync(Guid orcamentoId, CancellationToken ct = default)
    {
        var documento = await MontarAsync(orcamentoId, ct);
        var texto = TextoWhatsApp.Montar(documento);
        return new MensagemWhatsAppResposta(texto, TextoWhatsApp.Link(texto, documento.Cliente?.Telefone),
            documento.Cliente?.Telefone);
    }

    public async Task<DocumentoOrcamento> MontarAsync(Guid orcamentoId, CancellationToken ct)
    {
        var orcamento = await orcamentos.ObterCompletoAsync(orcamentoId, ct)
            ?? throw new NaoEncontradoException("Orçamento", orcamentoId);

        // Regra 1: escolhida → só ela; senão → todas (na ordem Econômica, Custo-benefício, Marcas).
        var selecionada = orcamento.OpcaoSelecionada;
        var opcoes = selecionada is not null
            ? new List<OpcaoOrcamento> { selecionada }
            : orcamento.Opcoes.OrderBy(o => PosicaoNaTela(o.Tipo)).ToList();

        if (opcoes.Count == 0 || opcoes.All(o => o.Itens.Count == 0))
            throw new DomainException("O orçamento não tem itens para enviar ao cliente.");

        // Regra 2: nada de valor incompleto para o cliente.
        var comPendencia = opcoes.Where(o => o.ItensSemPreco > 0).ToList();
        if (comPendencia.Count > 0)
            throw new DomainException(
                "Não é possível gerar o documento: há item(ns) sem preço em " +
                string.Join(", ", comPendencia.Select(o => $"\"{SeletorDeOpcoes.NomeDaOpcao(o.Tipo)}\"")) +
                ". Informe os preços e tente de novo.");

        var empresaId = empresaAtual.EmpresaId ?? throw new NaoAutorizadoException("Faça login para continuar.");
        var empresa = await empresas.ObterPorIdOuFalharAsync(empresaId, ct);
        var cliente = orcamento.ClienteId is Guid cid ? await clientes.ObterPorIdAsync(cid, ct) : null;

        return new DocumentoOrcamento(
            orcamento.Numero,
            FormatoBr.HorarioDeBrasilia(orcamento.CriadoEm),
            orcamento.ValidadeAte,
            new LojaDocumento(empresa.NomeExibicao, FormatoBr.Cnpj(empresa.Cnpj), empresa.Telefone, empresa.Email,
                FormatoBr.Endereco(empresa.Endereco)),
            cliente is null ? null : new ClienteDocumento(cliente.Nome, cliente.Telefone),
            opcoes.Select(o => new OpcaoDocumento(SeletorDeOpcoes.NomeDaOpcao(o.Tipo), o.ValorTotal,
                o.Itens.OrderBy(i => i.Ordem).Select(i => new ItemDocumento(i.Descricao, i.Marca, i.Quantidade,
                    i.UnidadeMedida, i.PrecoUnitario, i.PrecoTotal)).ToList())).ToList(),
            selecionada is not null);
    }

    private static int PosicaoNaTela(TipoOpcaoOrcamento tipo)
    {
        var posicao = Array.IndexOf(SeletorDeOpcoes.OpcoesComparativas, tipo);
        return posicao >= 0 ? posicao : 99;
    }
}
