using System.Threading.RateLimiting;

namespace OrcaFacil.API.Configuracao;

/// <summary>Nomes das políticas de segurança, usados nos atributos dos controllers.</summary>
public static class PoliticasApi
{
    /// <summary>CORS: quais sites (origens) podem chamar a API pelo navegador.</summary>
    public const string CorsFrontend = "Frontend";

    /// <summary>Limite de tentativas para login e recuperação de senha (contra "força bruta").</summary>
    public const string LimiteAutenticacao = "autenticacao";

    /// <summary>Permissão: Gerente ou Administrador.</summary>
    public const string Gerente = "Gerente";

    /// <summary>Permissão: somente Administrador.</summary>
    public const string Administrador = "Administrador";
}

public static class ConfiguracaoSegurancaExtensions
{
    /// <summary>
    /// CORS: por padrão, o navegador bloqueia um site de chamar uma API de outro endereço.
    /// Aqui liberamos apenas o endereço do nosso frontend (configurado em Frontend:Origem).
    /// </summary>
    public static IServiceCollection AddCorsFrontend(this IServiceCollection services, IConfiguration configuration)
    {
        var origem = configuration["Frontend:Origem"] ?? "http://localhost:5173";

        return services.AddCors(opcoes => opcoes.AddPolicy(PoliticasApi.CorsFrontend, politica =>
            politica.WithOrigins(origem).AllowAnyHeader().AllowAnyMethod()));
    }

    /// <summary>
    /// Limite de requisições: no máximo 5 tentativas por minuto por endereço IP nos endpoints
    /// marcados com [EnableRateLimiting(PoliticasApi.LimiteAutenticacao)]. A 6ª recebe HTTP 429.
    /// O número pode ser trocado em "Seguranca:TentativasDeAutenticacaoPorMinuto" (os testes de integração
    /// aumentam o limite, porque fazem muitos logins seguidos do mesmo "endereço").
    /// </summary>
    public static IServiceCollection AddLimitesDeRequisicao(this IServiceCollection services, IConfiguration configuration)
    {
        var tentativas = configuration.GetValue("Seguranca:TentativasDeAutenticacaoPorMinuto", 5);
        return services.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.AddPolicy(PoliticasApi.LimiteAutenticacao, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = tentativas,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });
    }
}
