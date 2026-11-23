using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Infrastructure;

/// <summary>
/// Testa o isolamento entre empresas (multi-tenancy) sem banco de dados:
/// - as consultas geradas incluem o filtro por EmpresaId;
/// - as regras executadas antes de salvar preenchem e protegem o EmpresaId.
/// </summary>
public class IsolamentoEmpresasTests
{
    private static readonly Guid EmpresaA = Guid.NewGuid();
    private static readonly Guid EmpresaB = Guid.NewGuid();

    private static Categoria NovaCategoria() => new() { Nome = "Cadernos" };

    [Fact]
    public void Consultas_de_dados_da_empresa_sao_filtradas_automaticamente()
    {
        using var contexto = ContextoDeTeste.Criar(EmpresaA);

        // ToQueryString mostra o SQL que seria enviado ao banco, sem executá-lo.
        var sql = contexto.Produtos.Where(p => p.Ativo).ToQueryString();

        sql.Should().Contain("\"EmpresaId\" = @");
    }

    [Fact]
    public void Tabela_de_empresas_nao_e_filtrada()
    {
        using var contexto = ContextoDeTeste.Criar(EmpresaA);

        contexto.Empresas.ToQueryString().Should().NotContain("WHERE");
    }

    [Fact]
    public void Registro_novo_recebe_a_empresa_logada()
    {
        using var contexto = ContextoDeTeste.Criar(EmpresaA);
        var categoria = NovaCategoria();
        contexto.Categorias.Add(categoria);

        contexto.AplicarRegrasAntesDeSalvar();

        categoria.EmpresaId.Should().Be(EmpresaA);
        categoria.CriadoEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Gravar_registro_de_outra_empresa_e_bloqueado()
    {
        using var contexto = ContextoDeTeste.Criar(EmpresaA);
        var categoria = NovaCategoria();
        categoria.VincularEmpresa(EmpresaB);
        contexto.Categorias.Add(categoria);

        FluentActions.Invoking(contexto.AplicarRegrasAntesDeSalvar)
            .Should().Throw<InvalidOperationException>().WithMessage("*outra empresa*");
    }

    [Fact]
    public void Sem_empresa_identificada_nao_e_possivel_gravar()
    {
        using var contexto = ContextoDeTeste.Criar(empresaId: null);
        contexto.Categorias.Add(NovaCategoria());

        FluentActions.Invoking(contexto.AplicarRegrasAntesDeSalvar)
            .Should().Throw<InvalidOperationException>().WithMessage("*Não há empresa identificada*");
    }

    [Fact]
    public void Cadastro_de_nova_empresa_pode_informar_a_empresa_explicitamente()
    {
        // Cenário do "criar conta" (Fase 4): ainda não há usuário logado,
        // então o primeiro usuário é vinculado à empresa recém-criada.
        using var contexto = ContextoDeTeste.Criar(empresaId: null);
        var empresa = new Empresa("Papelaria Exemplo Ltda", "11.222.333/0001-81", SegmentoEmpresa.Papelaria);
        var usuario = new Usuario("Ana", "ana@exemplo.com", "hash-ficticio", PerfilUsuario.Administrador);
        usuario.VincularEmpresa(empresa.Id);
        contexto.Empresas.Add(empresa);
        contexto.Usuarios.Add(usuario);

        FluentActions.Invoking(contexto.AplicarRegrasAntesDeSalvar).Should().NotThrow();
        usuario.EmpresaId.Should().Be(empresa.Id);
    }
}
