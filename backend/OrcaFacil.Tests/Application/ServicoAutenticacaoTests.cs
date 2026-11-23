using AwesomeAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using OrcaFacil.Application.Autenticacao;
using OrcaFacil.Application.Comum;
using OrcaFacil.Domain.Comum;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Tests.Application;

public class ServicoAutenticacaoTests
{
    private const string Email = "ana@papelaria.com";
    private const string Senha = "Senha123";

    // Cada teste do xUnit cria uma instância nova da classe: estes campos começam "zerados" em cada teste.
    private readonly FakeRepositorioAutenticacao _repositorio = new();
    private readonly FakeUnitOfWork _unidadeDeTrabalho = new();
    private readonly FakeEnvioEmail _email = new();
    private readonly RelogioFixo _relogio = new(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));
    private readonly ServicoAutenticacao _servico;

    public ServicoAutenticacaoTests()
    {
        _servico = new ServicoAutenticacao(_repositorio, _unidadeDeTrabalho, new FakeHasherSenha(),
            new FakeServicoTokens(_relogio), _email, _relogio,
            new RegistrarEmpresaValidador(), new LoginValidador(), new EsqueciSenhaValidador(),
            new RedefinirSenhaValidador(), NullLogger<ServicoAutenticacao>.Instance);
    }

    private static RegistrarEmpresaRequisicao Registro(string cnpj = "11.222.333/0001-81", string email = Email,
        string senha = Senha) =>
        new("Papelaria Exemplo Ltda", "Papelaria Exemplo", cnpj, SegmentoEmpresa.Papelaria, "Ana", email, senha);

    // ------------------------------------------------------------------ Registro

    [Fact]
    public async Task Registrar_cria_empresa_e_administrador_com_sessao_aberta()
    {
        var resposta = await _servico.RegistrarAsync(Registro());

        var empresa = _repositorio.Empresas.Should().ContainSingle().Subject;
        var usuario = _repositorio.Usuarios.Should().ContainSingle().Subject;
        usuario.Perfil.Should().Be(PerfilUsuario.Administrador);
        usuario.EmpresaId.Should().Be(empresa.Id);
        usuario.SenhaHash.Should().NotBe(Senha, "a senha nunca é guardada em texto puro");
        resposta.TokenAcesso.Should().NotBeNullOrEmpty();
        resposta.Usuario.EmpresaNome.Should().Be("Papelaria Exemplo");
        _repositorio.RefreshTokens.Should().ContainSingle();
        _unidadeDeTrabalho.VezesSalvo.Should().Be(1, "empresa, usuário e sessão são gravados juntos");
    }

    [Fact]
    public async Task Registrar_com_cnpj_ja_cadastrado_gera_conflito()
    {
        await _servico.RegistrarAsync(Registro());

        await FluentActions.Awaiting(() => _servico.RegistrarAsync(Registro(email: "outra@loja.com")))
            .Should().ThrowAsync<ConflitoException>().WithMessage("*CNPJ*");
    }

    [Fact]
    public async Task Registrar_com_email_ja_cadastrado_gera_conflito()
    {
        await _servico.RegistrarAsync(Registro());

        await FluentActions.Awaiting(() => _servico.RegistrarAsync(Registro(cnpj: "11.444.777/0001-61")))
            .Should().ThrowAsync<ConflitoException>().WithMessage("*e-mail*");
    }

    [Fact]
    public async Task Registrar_com_senha_fraca_e_recusado() =>
        await FluentActions.Awaiting(() => _servico.RegistrarAsync(Registro(senha: "somenteletras")))
            .Should().ThrowAsync<ValidationException>();

    // ------------------------------------------------------------------ Login

    [Fact]
    public async Task Login_correto_abre_sessao_e_registra_horario()
    {
        await _servico.RegistrarAsync(Registro());

        var resposta = await _servico.LoginAsync(new LoginRequisicao("  ANA@Papelaria.com ", Senha));

        resposta.Usuario.Email.Should().Be(Email, "o e-mail é comparado sem diferenciar maiúsculas e espaços");
        _repositorio.Usuarios.Single().UltimoLoginEm.Should().Be(_relogio.Agora.UtcDateTime);
    }

    [Fact]
    public async Task Senha_errada_e_email_inexistente_recebem_a_mesma_mensagem()
    {
        await _servico.RegistrarAsync(Registro());

        await FluentActions.Awaiting(() => _servico.LoginAsync(new LoginRequisicao(Email, "SenhaErrada1")))
            .Should().ThrowAsync<NaoAutorizadoException>().WithMessage(ServicoAutenticacao.MensagemCredenciaisInvalidas);
        await FluentActions.Awaiting(() => _servico.LoginAsync(new LoginRequisicao("ninguem@x.com", Senha)))
            .Should().ThrowAsync<NaoAutorizadoException>().WithMessage(ServicoAutenticacao.MensagemCredenciaisInvalidas);
    }

    [Fact]
    public async Task Usuario_desativado_nao_entra()
    {
        await _servico.RegistrarAsync(Registro());
        _repositorio.Usuarios.Single().Desativar();

        await FluentActions.Awaiting(() => _servico.LoginAsync(new LoginRequisicao(Email, Senha)))
            .Should().ThrowAsync<NaoAutorizadoException>().WithMessage("*desativado*");
    }

    [Fact]
    public async Task Empresa_suspensa_nao_entra()
    {
        await _servico.RegistrarAsync(Registro());
        _repositorio.Empresas.Single().Desativar();

        await FluentActions.Awaiting(() => _servico.LoginAsync(new LoginRequisicao(Email, Senha)))
            .Should().ThrowAsync<NaoAutorizadoException>().WithMessage("*suspenso*");
    }

    // ------------------------------------------------------------------ Renovação de sessão

    [Fact]
    public async Task Renovar_troca_o_refresh_token_e_invalida_o_anterior()
    {
        var primeira = await _servico.RegistrarAsync(Registro());

        var segunda = await _servico.RenovarAsync(new RenovarSessaoRequisicao(primeira.RefreshToken));

        segunda.RefreshToken.Should().NotBe(primeira.RefreshToken);
        var antigo = _repositorio.RefreshTokens[0];
        var novo = _repositorio.RefreshTokens[1];
        antigo.RevogadoEm.Should().NotBeNull();
        antigo.SubstituidoPorId.Should().Be(novo.Id);
    }

    [Fact]
    public async Task Reusar_refresh_token_ja_usado_encerra_todas_as_sessoes()
    {
        var primeira = await _servico.RegistrarAsync(Registro());
        await _servico.RenovarAsync(new RenovarSessaoRequisicao(primeira.RefreshToken));

        // Alguém apresenta de novo o token antigo (possível cópia roubada).
        await FluentActions.Awaiting(() => _servico.RenovarAsync(new RenovarSessaoRequisicao(primeira.RefreshToken)))
            .Should().ThrowAsync<NaoAutorizadoException>();

        _repositorio.RefreshTokens.Should().OnlyContain(t => t.RevogadoEm != null);
    }

    [Fact]
    public async Task Refresh_token_expirado_e_recusado()
    {
        var sessao = await _servico.RegistrarAsync(Registro());
        _relogio.Avancar(TimeSpan.FromDays(8));

        await FluentActions.Awaiting(() => _servico.RenovarAsync(new RenovarSessaoRequisicao(sessao.RefreshToken)))
            .Should().ThrowAsync<NaoAutorizadoException>();
    }

    [Fact]
    public async Task Logout_invalida_o_refresh_token()
    {
        var sessao = await _servico.RegistrarAsync(Registro());

        await _servico.LogoutAsync(new RenovarSessaoRequisicao(sessao.RefreshToken));

        await FluentActions.Awaiting(() => _servico.RenovarAsync(new RenovarSessaoRequisicao(sessao.RefreshToken)))
            .Should().ThrowAsync<NaoAutorizadoException>();
    }

    // ------------------------------------------------------------------ Recuperação de senha

    [Fact]
    public async Task Esqueci_senha_com_email_inexistente_nao_envia_nada_e_nao_da_erro()
    {
        await _servico.EsqueciSenhaAsync(new EsqueciSenhaRequisicao("ninguem@x.com"));

        _email.Enviados.Should().BeEmpty();
    }

    [Fact]
    public async Task Redefinir_senha_troca_a_senha_e_encerra_as_sessoes()
    {
        await _servico.RegistrarAsync(Registro());
        await _servico.EsqueciSenhaAsync(new EsqueciSenhaRequisicao(Email));
        var token = _email.Enviados.Should().ContainSingle().Subject.Token;

        await _servico.RedefinirSenhaAsync(new RedefinirSenhaRequisicao(token, "NovaSenha9"));

        _repositorio.RefreshTokens.Should().OnlyContain(t => t.RevogadoEm != null);
        await FluentActions.Awaiting(() => _servico.LoginAsync(new LoginRequisicao(Email, Senha)))
            .Should().ThrowAsync<NaoAutorizadoException>("a senha antiga deixou de valer");
        (await _servico.LoginAsync(new LoginRequisicao(Email, "NovaSenha9"))).Should().NotBeNull();
    }

    [Fact]
    public async Task Link_de_redefinicao_so_pode_ser_usado_uma_vez()
    {
        await _servico.RegistrarAsync(Registro());
        await _servico.EsqueciSenhaAsync(new EsqueciSenhaRequisicao(Email));
        var token = _email.Enviados.Single().Token;
        await _servico.RedefinirSenhaAsync(new RedefinirSenhaRequisicao(token, "NovaSenha9"));

        await FluentActions.Awaiting(() => _servico.RedefinirSenhaAsync(new RedefinirSenhaRequisicao(token, "OutraSenha8")))
            .Should().ThrowAsync<DomainException>().WithMessage("*já foi utilizado*");
    }

    [Fact]
    public async Task Link_de_redefinicao_expira_em_uma_hora()
    {
        await _servico.RegistrarAsync(Registro());
        await _servico.EsqueciSenhaAsync(new EsqueciSenhaRequisicao(Email));
        var token = _email.Enviados.Single().Token;
        _relogio.Avancar(TimeSpan.FromHours(2));

        await FluentActions.Awaiting(() => _servico.RedefinirSenhaAsync(new RedefinirSenhaRequisicao(token, "NovaSenha9")))
            .Should().ThrowAsync<DomainException>().WithMessage("*expirou*");
    }
}
