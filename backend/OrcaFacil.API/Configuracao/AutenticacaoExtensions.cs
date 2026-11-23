using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OrcaFacil.API.Seguranca;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Domain.Enums;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OrcaFacil.API.Configuracao;

public static class AutenticacaoExtensions
{
    /// <summary>
    /// Autenticação (QUEM é você?) com JWT e autorização (O QUE você pode fazer?) por perfil.
    /// Regra geral: todo endpoint exige login, exceto os marcados com [AllowAnonymous].
    /// </summary>
    public static IServiceCollection AddAutenticacaoJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var secao = configuration.GetSection(ConfiguracaoJwt.Secao);
        var configuracao = secao.Get<ConfiguracaoJwt>() ?? new ConfiguracaoJwt();
        configuracao.Validar();
        services.Configure<ConfiguracaoJwt>(secao);

        // Quem está logado e de qual empresa (lido do token a cada requisição).
        services.AddHttpContextAccessor();
        services.AddScoped<UsuarioLogadoPorToken>();
        services.AddScoped<IEmpresaAtual>(sp => sp.GetRequiredService<UsuarioLogadoPorToken>());
        services.AddScoped<IUsuarioAtual>(sp => sp.GetRequiredService<UsuarioLogadoPorToken>());
        services.AddScoped<IServicoTokens, ServicoTokensJwt>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opcoes =>
            {
                // Mantém os nomes curtos dos claims ("sub", "role", "empresa_id") sem convertê-los.
                opcoes.MapInboundClaims = false;
                opcoes.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuracao.Emissor,
                    ValidateAudience = true,
                    ValidAudience = configuracao.Audiencia,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = configuracao.ObterChaveAssinatura(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30), // tolerância para relógios levemente diferentes
                    NameClaimType = ClaimsOrcaFacil.Nome,
                    RoleClaimType = ClaimsOrcaFacil.Perfil
                };
            });

        services.AddAuthorizationBuilder()
            // Sem [AllowAnonymous], todo endpoint exige um usuário logado.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(PoliticasApi.Gerente, politica =>
                politica.RequireRole(nameof(PerfilUsuario.Gerente), nameof(PerfilUsuario.Administrador)))
            .AddPolicy(PoliticasApi.Administrador, politica =>
                politica.RequireRole(nameof(PerfilUsuario.Administrador)));

        return services;
    }

    /// <summary>
    /// Botão "Authorize" 🔒 do Swagger com duas formas de entrar:
    /// 1) "EmailESenha": digitar e-mail e senha (o Swagger chama /auth/token-swagger e guarda o token);
    /// 2) "Bearer": colar um token já obtido.
    /// </summary>
    public static void AdicionarAutenticacaoNoSwagger(this SwaggerGenOptions opcoes)
    {
        const string esquemaLogin = "EmailESenha";
        const string esquemaToken = "Bearer";

        opcoes.AddSecurityDefinition(esquemaLogin, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Digite o seu e-mail em username e a senha em password. " +
                          "Deixe client_id e client_secret em branco.",
            Flows = new OpenApiOAuthFlows
            {
                Password = new OpenApiOAuthFlow
                {
                    TokenUrl = new Uri("/api/v1/auth/token-swagger", UriKind.Relative),
                    Scopes = new Dictionary<string, string>()
                }
            }
        });

        opcoes.AddSecurityDefinition(esquemaToken, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Alternativa: cole aqui o tokenAcesso recebido no login (sem a palavra Bearer)."
        });

        // Dois "requisitos" separados = basta UM deles (um OU outro).
        opcoes.AddSecurityRequirement(Requisito(esquemaLogin));
        opcoes.AddSecurityRequirement(Requisito(esquemaToken));
    }

    private static OpenApiSecurityRequirement Requisito(string esquema) => new()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = esquema }
            },
            Array.Empty<string>()
        }
    };
}
