using FluentValidation;
using OrcaFacil.Domain.ObjetosDeValor;

namespace OrcaFacil.Application.Autenticacao;

/// <summary>Regras reutilizáveis entre validadores.</summary>
public static class RegrasComuns
{
    public const int TamanhoMinimoSenha = 8;

    /// <summary>Senha com pelo menos 8 caracteres, uma letra e um número.</summary>
    public static IRuleBuilderOptions<T, string> SenhaForte<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("Informe a senha.")
            .MinimumLength(TamanhoMinimoSenha).WithMessage($"A senha deve ter pelo menos {TamanhoMinimoSenha} caracteres.")
            .MaximumLength(128).WithMessage("A senha deve ter no máximo 128 caracteres.")
            .Matches("[A-Za-z]").WithMessage("A senha deve conter pelo menos uma letra.")
            .Matches("[0-9]").WithMessage("A senha deve conter pelo menos um número.");

    public static IRuleBuilderOptions<T, string> EmailValido<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("Informe o e-mail.")
            .MaximumLength(200).WithMessage("O e-mail deve ter no máximo 200 caracteres.")
            .EmailAddress().WithMessage("Informe um e-mail válido.");
}

public sealed class RegistrarEmpresaValidador : AbstractValidator<RegistrarEmpresaRequisicao>
{
    public RegistrarEmpresaValidador()
    {
        RuleFor(r => r.RazaoSocial).NotEmpty().WithMessage("Informe a razão social.")
            .MaximumLength(200);
        RuleFor(r => r.NomeFantasia).MaximumLength(200);
        RuleFor(r => r.Cnpj).NotEmpty().WithMessage("Informe o CNPJ.")
            .Must(DocumentoFiscal.CnpjValido).WithMessage("CNPJ inválido.");
        RuleFor(r => r.Segmento).IsInEnum().WithMessage("Segmento inválido.");
        RuleFor(r => r.NomeAdministrador).NotEmpty().WithMessage("Informe o seu nome.")
            .MaximumLength(150);
        RuleFor(r => r.Email).EmailValido();
        RuleFor(r => r.Senha).SenhaForte();
    }
}

public sealed class LoginValidador : AbstractValidator<LoginRequisicao>
{
    public LoginValidador()
    {
        // No login não exigimos "senha forte": só que os campos venham preenchidos.
        RuleFor(r => r.Email).NotEmpty().WithMessage("Informe o e-mail.");
        RuleFor(r => r.Senha).NotEmpty().WithMessage("Informe a senha.");
    }
}

public sealed class EsqueciSenhaValidador : AbstractValidator<EsqueciSenhaRequisicao>
{
    public EsqueciSenhaValidador() => RuleFor(r => r.Email).EmailValido();
}

public sealed class RedefinirSenhaValidador : AbstractValidator<RedefinirSenhaRequisicao>
{
    public RedefinirSenhaValidador()
    {
        RuleFor(r => r.Token).NotEmpty().WithMessage("Link de recuperação inválido.");
        RuleFor(r => r.NovaSenha).SenhaForte();
    }
}
