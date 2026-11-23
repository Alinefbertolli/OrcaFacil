using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrcaFacil.API.Configuracao;
using OrcaFacil.Application.Autenticacao;

namespace OrcaFacil.API.Controllers;

/// <summary>Criação de conta, login, sessão e recuperação de senha.</summary>
/// <remarks>
/// Todos os endpoints daqui são públicos ([AllowAnonymous]): ainda não há token quando são chamados.
/// O controller é "fino": só recebe a requisição, chama o caso de uso e devolve a resposta.
/// </remarks>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[AllowAnonymous]
public class AuthController(ServicoAutenticacao servico) : ControllerBase
{
    /// <summary>Cria a conta de uma nova empresa e o seu primeiro usuário (administrador).</summary>
    /// <response code="201">Conta criada; a sessão já vem aberta (tokens na resposta).</response>
    /// <response code="400">Dados inválidos (ex.: CNPJ inválido, senha fraca).</response>
    /// <response code="409">CNPJ ou e-mail já cadastrados.</response>
    [HttpPost("registrar")]
    [EnableRateLimiting(PoliticasApi.LimiteAutenticacao)]
    [ProducesResponseType<RespostaAutenticacao>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RespostaAutenticacao>> Registrar(RegistrarEmpresaRequisicao requisicao, CancellationToken ct)
    {
        var resposta = await servico.RegistrarAsync(requisicao, ct);
        return StatusCode(StatusCodes.Status201Created, resposta);
    }

    /// <summary>Entra no sistema com e-mail e senha.</summary>
    /// <response code="200">Login realizado; use o tokenAcesso no botão Authorize.</response>
    /// <response code="401">E-mail ou senha inválidos, ou acesso desativado.</response>
    /// <response code="429">Muitas tentativas: aguarde um minuto.</response>
    [HttpPost("login")]
    [EnableRateLimiting(PoliticasApi.LimiteAutenticacao)]
    [ProducesResponseType<RespostaAutenticacao>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RespostaAutenticacao>> Login(LoginRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.LoginAsync(requisicao, ct));

    /// <summary>
    /// [Somente em desenvolvimento] Login no formato que o botão "Authorize" do Swagger usa
    /// (OAuth2 "password": formulário com username e password). Permite entrar digitando
    /// e-mail e senha, sem copiar e colar o token. Não aparece na lista do Swagger.
    /// </summary>
    [HttpPost("token-swagger")]
    [Consumes("application/x-www-form-urlencoded")]
    [EnableRateLimiting(PoliticasApi.LimiteAutenticacao)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> TokenParaSwagger([FromForm] string username, [FromForm] string password,
        [FromServices] IHostEnvironment ambiente, CancellationToken ct)
    {
        if (!ambiente.IsDevelopment())
            return NotFound();

        var sessao = await servico.LoginAsync(new LoginRequisicao(username, password), ct);

        // Nomes exigidos pelo padrão OAuth2 (o Swagger procura exatamente "access_token").
        return Ok(new
        {
            access_token = sessao.TokenAcesso,
            token_type = "Bearer",
            expires_in = (int)(sessao.TokenAcessoExpiraEm - DateTime.UtcNow).TotalSeconds
        });
    }

    /// <summary>Troca o refreshToken por uma sessão nova, sem pedir a senha de novo.</summary>
    /// <response code="200">Nova sessão (o refreshToken anterior deixa de valer).</response>
    /// <response code="401">Sessão expirada ou inválida: faça login novamente.</response>
    [HttpPost("refresh")]
    [ProducesResponseType<RespostaAutenticacao>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RespostaAutenticacao>> Renovar(RenovarSessaoRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.RenovarAsync(requisicao, ct));

    /// <summary>Encerra a sessão (invalida o refreshToken informado).</summary>
    /// <response code="204">Sessão encerrada.</response>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RenovarSessaoRequisicao requisicao, CancellationToken ct)
    {
        await servico.LogoutAsync(requisicao, ct);
        return NoContent();
    }

    /// <summary>Pede o link de redefinição de senha.</summary>
    /// <remarks>⚠️ Por enquanto o e-mail é SIMULADO: o link e o token aparecem no log da API (terminal).</remarks>
    /// <response code="200">Resposta sempre igual, exista ou não o e-mail (para não revelar quem tem conta).</response>
    [HttpPost("esqueci-senha")]
    [EnableRateLimiting(PoliticasApi.LimiteAutenticacao)]
    [ProducesResponseType<MensagemResposta>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MensagemResposta>> EsqueciSenha(EsqueciSenhaRequisicao requisicao, CancellationToken ct)
    {
        await servico.EsqueciSenhaAsync(requisicao, ct);
        return Ok(new MensagemResposta(
            "Se o e-mail estiver cadastrado, você receberá as instruções para redefinir a senha."));
    }

    /// <summary>Define uma nova senha usando o token recebido por e-mail.</summary>
    /// <response code="204">Senha alterada; todas as sessões antigas foram encerradas.</response>
    /// <response code="422">Link inválido, expirado ou já utilizado.</response>
    [HttpPost("redefinir-senha")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RedefinirSenha(RedefinirSenhaRequisicao requisicao, CancellationToken ct)
    {
        await servico.RedefinirSenhaAsync(requisicao, ct);
        return NoContent();
    }
}
