using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrcaFacil.API.Configuracao;
using OrcaFacil.Application.Comum;
using OrcaFacil.Application.Usuarios;

namespace OrcaFacil.API.Controllers;

/// <summary>Perfil do usuário logado e gestão dos usuários da empresa.</summary>
/// <remarks>Todos os endpoints exigem login. Alguns exigem perfil Gerente ou Administrador.</remarks>
[ApiController]
[Route("api/v1/usuarios")]
[Produces("application/json")]
public class UsuariosController(ServicoUsuarios servico) : ControllerBase
{
    /// <summary>Dados do usuário logado.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UsuarioResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UsuarioResposta>> ObterMeuPerfil(CancellationToken ct) =>
        Ok(await servico.ObterMeuPerfilAsync(ct));

    /// <summary>Atualiza o nome do usuário logado.</summary>
    [HttpPut("me")]
    [ProducesResponseType<UsuarioResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UsuarioResposta>> AtualizarMeuPerfil(AtualizarMeuPerfilRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.AtualizarMeuPerfilAsync(requisicao, ct));

    /// <summary>Troca a senha do usuário logado (encerra as sessões em outros dispositivos).</summary>
    [HttpPut("me/senha")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AlterarMinhaSenha(AlterarMinhaSenhaRequisicao requisicao, CancellationToken ct)
    {
        await servico.AlterarMinhaSenhaAsync(requisicao, ct);
        return NoContent();
    }

    /// <summary>[Gerente] Lista os usuários da empresa, com busca por nome ou e-mail.</summary>
    [HttpGet]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType<PaginaResultado<UsuarioResposta>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginaResultado<UsuarioResposta>>> Listar(
        [FromQuery] string? busca, [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 20,
        CancellationToken ct = default) =>
        Ok(await servico.ListarAsync(new ParametrosPaginacao(pagina, tamanhoPagina), busca, ct));

    /// <summary>[Gerente] Dados de um usuário da empresa.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PoliticasApi.Gerente)]
    [ProducesResponseType<UsuarioResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioResposta>> ObterPorId(Guid id, CancellationToken ct) =>
        Ok(await servico.ObterPorIdAsync(id, ct));

    /// <summary>[Administrador] Cria um usuário na empresa.</summary>
    [HttpPost]
    [Authorize(Policy = PoliticasApi.Administrador)]
    [ProducesResponseType<UsuarioResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioResposta>> Criar(CriarUsuarioRequisicao requisicao, CancellationToken ct)
    {
        var criado = await servico.CriarAsync(requisicao, ct);
        // 201 Created + cabeçalho "Location" apontando para GET /api/v1/usuarios/{id}
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>[Administrador] Muda o perfil (Operador, Gerente, Administrador) de um usuário.</summary>
    [HttpPatch("{id:guid}/perfil")]
    [Authorize(Policy = PoliticasApi.Administrador)]
    [ProducesResponseType<UsuarioResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UsuarioResposta>> AlterarPerfil(Guid id, AlterarPerfilRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.AlterarPerfilAsync(id, requisicao, ct));

    /// <summary>[Administrador] Ativa ou desativa um usuário (desativar encerra as sessões dele na hora).</summary>
    [HttpPatch("{id:guid}/situacao")]
    [Authorize(Policy = PoliticasApi.Administrador)]
    [ProducesResponseType<UsuarioResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UsuarioResposta>> AlterarSituacao(Guid id, AlterarSituacaoRequisicao requisicao, CancellationToken ct) =>
        Ok(await servico.AlterarSituacaoAsync(id, requisicao, ct));
}
