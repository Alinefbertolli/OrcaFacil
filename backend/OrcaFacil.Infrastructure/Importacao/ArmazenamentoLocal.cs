using Microsoft.Extensions.Configuration;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Importacao;

namespace OrcaFacil.Infrastructure.Importacao;

/// <summary>
/// Guarda os arquivos enviados numa pasta do servidor (Uploads:Diretorio), FORA da pasta pública,
/// com nome aleatório (o nome original nunca é usado no disco: evita sobrescrever arquivos e ataques
/// com nomes como "..\..\Program.cs"). Organização: {empresa}/{ano}/{mês}/{guid}.{extensão}.
/// No futuro pode ser trocado por um armazenamento em nuvem sem mudar o resto do sistema.
/// </summary>
public sealed class ArmazenamentoLocal(IConfiguration configuracao, IEmpresaAtual empresaAtual, TimeProvider relogio)
    : IArmazenamentoArquivos
{
    public async Task<string> SalvarAsync(Stream conteudo, string extensao, CancellationToken ct = default)
    {
        var raiz = Path.GetFullPath(configuracao["Uploads:Diretorio"] ?? "storage/uploads");
        var agora = relogio.GetUtcNow();
        var empresa = empresaAtual.EmpresaId?.ToString() ?? "sem-empresa";

        var pasta = Path.Combine(raiz, empresa, agora.ToString("yyyy"), agora.ToString("MM"));
        Directory.CreateDirectory(pasta);

        // A extensão também é "limpa": só letras e números.
        var extensaoSegura = new string(extensao.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var caminho = Path.Combine(pasta, $"{Guid.NewGuid():N}.{extensaoSegura}");

        await using var arquivo = File.Create(caminho);
        await conteudo.CopyToAsync(arquivo, ct);

        // Guardamos o caminho relativo à raiz (independe de onde o servidor está instalado).
        return Path.GetRelativePath(raiz, caminho);
    }
}
