using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Application.Orcamentos;
using OrcaFacil.Application.Estoques;
using OrcaFacil.Infrastructure.Importacao;
using OrcaFacil.Infrastructure.Orcamentos;
using OrcaFacil.Infrastructure.Estoques;
using OrcaFacil.Infrastructure.Email;
using OrcaFacil.Infrastructure.Seguranca;
using OrcaFacil.Infrastructure.Persistencia;
using OrcaFacil.Infrastructure.Persistencia.Repositorios;

namespace OrcaFacil.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Postgres' não configurada. Use: dotnet user-secrets set " +
                "\"ConnectionStrings:Postgres\" \"Host=localhost;Database=orcafacil;Username=orcafacil;Password=...\" " +
                "(na pasta OrcaFacil.API) ou a variável de ambiente ORCAFACIL_ConnectionStrings__Postgres.");
        }

        services.AddDbContext<OrcaFacilDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(OrcaFacilDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            }));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IGeradorNumeroOrcamento, GeradorNumeroOrcamento>();

        // Repositório genérico: IRepositorio<Produto>, IRepositorio<Cliente>... todos atendidos por Repositorio<T>.
        services.AddScoped(typeof(IRepositorio<>), typeof(Repositorio<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRepositorioAutenticacao, RepositorioAutenticacao>();

        // Segurança e e-mail
        services.AddSingleton<IHasherSenha, HasherSenha>();
        services.AddScoped<IEnvioEmail, EnvioEmailSimulado>(); // ⚠️ simulado: só escreve no log

        // Importação de listas
        services.AddScoped<IRepositorioListasImportadas, RepositorioListasImportadas>();
        services.AddScoped<IExtratorTexto, ExtratorTexto>();
        services.AddSingleton<IServicoOcr, ServicoOcrTesseract>();
        services.AddScoped<IArmazenamentoArquivos, ArmazenamentoLocal>();
        services.AddScoped<IBuscaProdutosSimilares, BuscaProdutosSimilares>();

        // Motor de orçamentação
        services.AddScoped<IRepositorioOrcamentos, RepositorioOrcamentos>();
        services.AddScoped<ICatalogoEquivalentes, CatalogoEquivalentes>();

        // Estoque
        services.AddScoped<IRepositorioEstoques, RepositorioEstoques>();

        return services;
    }
}
