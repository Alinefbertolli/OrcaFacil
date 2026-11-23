using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Application.Importacao;

/// <summary>Lê o texto de um arquivo (PDF, Excel, Word, imagem...). Implementado na Infrastructure.</summary>
public interface IExtratorTexto
{
    /// <exception cref="OrcaFacil.Domain.Comum.DomainException">Quando o arquivo não pode ser lido.</exception>
    Task<string> ExtrairAsync(Stream conteudo, FormatoImportacao formato, CancellationToken ct = default);
}

/// <summary>Reconhecimento de texto em imagens (OCR).</summary>
public interface IServicoOcr
{
    Task<string> ReconhecerTextoAsync(byte[] imagem, CancellationToken ct = default);
}

/// <summary>Guarda o arquivo original enviado (para consulta futura).</summary>
public interface IArmazenamentoArquivos
{
    /// <returns>Caminho interno onde o arquivo foi salvo.</returns>
    Task<string> SalvarAsync(Stream conteudo, string extensao, CancellationToken ct = default);
}

/// <summary>Busca no catálogo da empresa os produtos com nome parecido com a descrição.</summary>
public interface IBuscaProdutosSimilares
{
    Task<IReadOnlyList<ProdutoSimilar>> BuscarAsync(string descricao, int limite, CancellationToken ct = default);
}

/// <param name="Similaridade">De 0 (nada a ver) a 1 (igual).</param>
public sealed record ProdutoSimilar(Guid Id, string Codigo, string Nome, string UnidadeMedida, double Similaridade);

/// <summary>Acesso à lista importada junto com os itens (o repositório genérico não carrega filhos).</summary>
public interface IRepositorioListasImportadas
{
    Task<ListaImportada?> ObterComItensAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Arquivo recebido pela API, sem depender de tipos do ASP.NET.</summary>
public sealed record ArquivoEnviado(string NomeArquivo, long Tamanho, Stream Conteudo);
