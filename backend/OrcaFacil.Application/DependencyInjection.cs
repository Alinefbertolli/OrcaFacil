using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OrcaFacil.Application.Autenticacao;
using OrcaFacil.Application.Cadastros;
using OrcaFacil.Application.Empresas;
using OrcaFacil.Application.Estoques;
using OrcaFacil.Application.Importacao;
using OrcaFacil.Application.Orcamentos;
using OrcaFacil.Application.Usuarios;

namespace OrcaFacil.Application;

public static class DependencyInjection
{
    /// <summary>Registra os serviços da camada Application (casos de uso e validadores).</summary>
    public static IServiceCollection AddAplicacao(this IServiceCollection services)
    {
        // Encontra e registra automaticamente todas as classes AbstractValidator<T> deste projeto.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        // Casos de uso (um objeto novo por requisição: "Scoped").
        services.AddScoped<ServicoAutenticacao>();
        services.AddScoped<ServicoUsuarios>();
        services.AddScoped<ServicoCategorias>();
        services.AddScoped<ServicoMarcas>();
        services.AddScoped<ServicoProdutos>();
        services.AddScoped<ServicoImportacaoCatalogo>();
        services.AddScoped<ServicoFornecedores>();
        services.AddScoped<ServicoClientes>();
        services.AddScoped<ServicoImportacao>();
        services.AddScoped<ServicoOrcamentos>();
        services.AddScoped<ServicoConfiguracoesEmpresa>();
        services.AddScoped<ServicoEstoque>();
        services.AddScoped<ServicoDocumentosOrcamento>();

        return services;
    }
}
