using OrcaFacil.Domain.Entidades;
using OrcaFacil.Domain.Enums;

namespace OrcaFacil.Application.Autenticacao;

// DTO = Data Transfer Object: o "formulário" que entra ou sai da API.
// Separar DTOs das entidades evita expor dados internos (ex.: o hash da senha nunca sai da API).

/// <summary>Criação da conta de uma nova empresa e do seu primeiro usuário (administrador).</summary>
public sealed record RegistrarEmpresaRequisicao(
    string RazaoSocial,
    string? NomeFantasia,
    string Cnpj,
    SegmentoEmpresa Segmento,
    string NomeAdministrador,
    string Email,
    string Senha);

public sealed record LoginRequisicao(string Email, string Senha);

public sealed record RenovarSessaoRequisicao(string RefreshToken);

public sealed record EsqueciSenhaRequisicao(string Email);

public sealed record RedefinirSenhaRequisicao(string Token, string NovaSenha);

/// <summary>Resposta de login, cadastro e renovação de sessão.</summary>
public sealed record RespostaAutenticacao(
    string TokenAcesso,
    DateTime TokenAcessoExpiraEm,
    string RefreshToken,
    DateTime RefreshTokenExpiraEm,
    UsuarioLogadoResposta Usuario);

public sealed record UsuarioLogadoResposta(
    Guid Id,
    string Nome,
    string Email,
    PerfilUsuario Perfil,
    Guid EmpresaId,
    string EmpresaNome)
{
    public static UsuarioLogadoResposta De(Usuario usuario, Empresa empresa) =>
        new(usuario.Id, usuario.Nome, usuario.Email, usuario.Perfil, empresa.Id, empresa.NomeExibicao);
}

public sealed record MensagemResposta(string Mensagem);
