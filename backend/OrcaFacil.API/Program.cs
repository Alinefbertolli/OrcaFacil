using System.Text.Json.Serialization;
using Microsoft.OpenApi.Models;
using OrcaFacil.API.Configuracao;
using OrcaFacil.API.Erros;
using OrcaFacil.Application;
using OrcaFacil.Infrastructure;
using OrcaFacil.Infrastructure.Persistencia;
using Serilog;

// Logger provisório: registra erros que aconteçam antes de a configuração completa ser lida.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Variáveis de ambiente com prefixo ORCAFACIL_ sobrescrevem o appsettings
    // (ex.: ORCAFACIL_ConnectionStrings__Postgres). Segredos nunca vão para o repositório.
    builder.Configuration.AddEnvironmentVariables(prefix: "ORCAFACIL_");

    // ---------- Logs (Serilog: console + arquivo em logs/, configurados no appsettings) ----------
    builder.Services.AddSerilog((services, configuracao) => configuracao
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ---------- Camadas da aplicação ----------
    builder.Services.AddAplicacao();
    builder.Services.AddInfraestrutura(builder.Configuration);

    // ---------- Tratamento global de erros (respostas no padrão ProblemDetails) ----------
    builder.Services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
        // traceId: código que liga a resposta de erro à linha correspondente no log.
        contexto.ProblemDetails.Extensions["traceId"] = contexto.HttpContext.TraceIdentifier);
    builder.Services.AddExceptionHandler<TratadorGlobalDeExcecoes>();

    // ---------- Segurança ----------
    // Login com JWT, usuário/empresa logados (multiempresa) e permissões por perfil.
    builder.Services.AddAutenticacaoJwt(builder.Configuration);
    builder.Services.AddCorsFrontend(builder.Configuration);
    builder.Services.AddLimitesDeRequisicao(builder.Configuration);

    // ---------- API, health check e Swagger ----------
    builder.Services.AddControllers()
        // Enums em texto no JSON: "perfil": "Administrador" em vez de "perfil": 3.
        .AddJsonOptions(opcoes => opcoes.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddHealthChecks().AddDbContextCheck<OrcaFacilDbContext>("postgres");
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opcoes =>
    {
        opcoes.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "OrçaFácil API",
            Version = "v1",
            Description = "SaaS multiempresa de gestão e automação de orçamentos para varejistas."
        });

        // Usa os comentários /// dos controllers como descrição no Swagger.
        var arquivoXml = Path.Combine(AppContext.BaseDirectory, "OrcaFacil.API.xml");
        if (File.Exists(arquivoXml))
            opcoes.IncludeXmlComments(arquivoXml);

        opcoes.AdicionarAutenticacaoNoSwagger(); // botão "Authorize" 🔒
    });

    var app = builder.Build();

    // ---------- Pipeline: a ordem importa! Cada requisição passa por estas etapas, de cima para baixo ----------
    app.UseSerilogRequestLogging();  // 1. uma linha de log por requisição (método, caminho, status, tempo).
                                     //    Fica ANTES do tratador de erros para registrar o código FINAL
                                     //    (ex.: 404), e não o erro "em trânsito" (que apareceria como 500).
    app.UseExceptionHandler();       // 2. "rede de segurança": captura erros de todas as etapas seguintes
    app.UseStatusCodePages();        // 3. respostas de erro sem corpo (ex.: 404 de rota) também viram ProblemDetails

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(opcoes =>
        {
            opcoes.DocumentTitle = "OrçaFácil API";
            opcoes.EnablePersistAuthorization(); // o login no Swagger sobrevive ao F5
        });
    }

    app.UseHttpsRedirection();       // 4. força HTTPS
    app.UseCors(PoliticasApi.CorsFrontend); // 5. libera o frontend
    app.UseRateLimiter();            // 6. aplica os limites de tentativas
    app.UseAuthentication();         // 7. lê o token: QUEM é o usuário?
    app.UseAuthorization();          // 8. confere a permissão: ele PODE fazer isto?

    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException é esperada quando o "dotnet ef" inicializa a API só para ler o modelo:
    // não é um erro real, por isso não é registrada.
    Log.Fatal(ex, "A API foi encerrada por um erro na inicialização.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Exposto para testes de integração com WebApplicationFactory (Fase 12).
public partial class Program { }
