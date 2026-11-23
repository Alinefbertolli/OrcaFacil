using AwesomeAssertions;
using FluentValidation;
using OrcaFacil.Application.Comum;
using OrcaFacil.Application.Usuarios;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Application;

public class ServicoUsuariosTests
{
    private readonly FakeRepositorio<Usuario> _usuarios = new();
    private readonly FakeRepositorioAutenticacao _repositorioAutenticacao = new();
    private readonly RelogioFixo _relogio = new(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly Usuario _administrador = NovoUsuario("Ana", "ana@loja.com", PerfilUsuario.Administrador);
    private readonly Usuario _operador = NovoUsuario("Bruno", "bruno@loja.com", PerfilUsuario.Operador);

    public ServicoUsuariosTests()
    {
        foreach (var usuario in new[] { _administrador, _operador })
        {
            _usuarios.Adicionar(usuario);
            _repositorioAutenticacao.Adicionar(usuario);
        }
    }

    private static Usuario NovoUsuario(string nome, string email, PerfilUsuario perfil) =>
        new(nome, email, "hash:Senha123", perfil);

    private ServicoUsuarios Servico(Guid? usuarioLogado) =>
        new(_usuarios, _repositorioAutenticacao, new FakeUnitOfWork(), new FakeUsuarioAtual(usuarioLogado),
            new FakeHasherSenha(), _relogio,
            new AtualizarMeuPerfilValidador(), new AlterarMinhaSenhaValidador(), new CriarUsuarioValidador());

    [Fact]
    public async Task Sem_login_nao_e_possivel_ver_o_perfil() =>
        await FluentActions.Awaiting(() => Servico(usuarioLogado: null).ObterMeuPerfilAsync())
            .Should().ThrowAsync<NaoAutorizadoException>();

    [Fact]
    public async Task Usuario_ve_o_proprio_perfil()
    {
        var perfil = await Servico(_operador.Id).ObterMeuPerfilAsync();

        perfil.Email.Should().Be("bruno@loja.com");
    }

    [Fact]
    public async Task Administrador_nao_pode_remover_o_proprio_acesso() =>
        await FluentActions.Awaiting(() => Servico(_administrador.Id)
                .AlterarPerfilAsync(_administrador.Id, new AlterarPerfilRequisicao(PerfilUsuario.Operador)))
            .Should().ThrowAsync<DomainException>().WithMessage("*próprio acesso*");

    [Fact]
    public async Task Administrador_nao_pode_se_desativar() =>
        await FluentActions.Awaiting(() => Servico(_administrador.Id)
                .AlterarSituacaoAsync(_administrador.Id, new AlterarSituacaoRequisicao(Ativo: false)))
            .Should().ThrowAsync<DomainException>();

    [Fact]
    public async Task Administrador_promove_outro_usuario()
    {
        var resposta = await Servico(_administrador.Id)
            .AlterarPerfilAsync(_operador.Id, new AlterarPerfilRequisicao(PerfilUsuario.Gerente));

        resposta.Perfil.Should().Be(PerfilUsuario.Gerente);
    }

    [Fact]
    public async Task Desativar_usuario_encerra_as_sessoes_dele()
    {
        var sessao = new RefreshToken(_operador.Id, "h:refresh-x", _relogio.Agora.UtcDateTime.AddDays(7));
        _repositorioAutenticacao.Adicionar(sessao);

        await Servico(_administrador.Id).AlterarSituacaoAsync(_operador.Id, new AlterarSituacaoRequisicao(Ativo: false));

        _operador.Ativo.Should().BeFalse();
        sessao.RevogadoEm.Should().NotBeNull();
    }

    [Fact]
    public async Task Trocar_senha_exige_a_senha_atual_correta() =>
        await FluentActions.Awaiting(() => Servico(_operador.Id)
                .AlterarMinhaSenhaAsync(new AlterarMinhaSenhaRequisicao("SenhaErrada1", "NovaSenha9")))
            .Should().ThrowAsync<ValidationException>();

    [Fact]
    public async Task Trocar_senha_grava_o_novo_hash()
    {
        await Servico(_operador.Id).AlterarMinhaSenhaAsync(new AlterarMinhaSenhaRequisicao("Senha123", "NovaSenha9"));

        _operador.SenhaHash.Should().Be("hash:NovaSenha9");
    }

    [Fact]
    public async Task Criar_usuario_com_email_em_uso_gera_conflito() =>
        await FluentActions.Awaiting(() => Servico(_administrador.Id)
                .CriarAsync(new CriarUsuarioRequisicao("Outro", "BRUNO@loja.com", "Senha123", PerfilUsuario.Operador)))
            .Should().ThrowAsync<ConflitoException>();

    [Fact]
    public async Task Listar_busca_por_nome_ou_email()
    {
        var pagina = await Servico(_administrador.Id).ListarAsync(new ParametrosPaginacao(), busca: "bru");

        pagina.Itens.Should().ContainSingle().Which.Nome.Should().Be("Bruno");
    }
}
