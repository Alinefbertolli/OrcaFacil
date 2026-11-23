using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrcaFacil.Application.Abstracoes;

namespace OrcaFacil.Infrastructure.Email;

/// <summary>
/// ⚠️ SIMULAÇÃO: não envia e-mail de verdade. Escreve no log o conteúdo que seria enviado,
/// incluindo o link de redefinição de senha, para permitir testar o fluxo em desenvolvimento.
/// Antes de colocar o sistema em produção, trocar por um envio real (SMTP ou serviço de e-mail).
/// </summary>
public sealed class EnvioEmailSimulado(IConfiguration configuracao, ILogger<EnvioEmailSimulado> logger) : IEnvioEmail
{
    public Task EnviarRecuperacaoSenhaAsync(string destinatario, string nome, string token, DateTime expiraEm,
        CancellationToken cancellationToken = default)
    {
        var origemFrontend = configuracao["Frontend:Origem"] ?? "http://localhost:5173";
        var link = $"{origemFrontend}/redefinir-senha?token={Uri.EscapeDataString(token)}";

        logger.LogWarning(
            "[E-MAIL SIMULADO - NÃO ENVIADO] Para: {Destinatario} | Assunto: Redefinição de senha | " +
            "Olá {Nome}, use o link a seguir até {ExpiraEm:dd/MM/yyyy HH:mm} (UTC): {Link} | Token: {Token}",
            destinatario, nome, expiraEm, link, token);

        return Task.CompletedTask;
    }
}
