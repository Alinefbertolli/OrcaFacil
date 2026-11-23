using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrcaFacil.Application.Abstracoes;
using OrcaFacil.Application.Comum;

namespace OrcaFacil.Infrastructure.Persistencia;

/// <summary>
/// Grava todas as alterações da requisição numa única transação e traduz erros
/// técnicos do banco em exceções que a aplicação entende (e que a API transforma em HTTP 409).
/// </summary>
public sealed class UnitOfWork(OrcaFacilDbContext contexto) : IUnitOfWork
{
    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Duas pessoas alteraram o mesmo registro ao mesmo tempo (ex.: saldo de estoque).
            throw new ConflitoException(
                "O registro foi alterado por outra pessoa enquanto você editava. Recarregue os dados e tente novamente.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Violação de índice único (ex.: código de produto já usado nesta empresa).
            throw new ConflitoException("Já existe um registro com esses dados.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // Ex.: tentar excluir um fornecedor que já aparece em orçamentos.
            throw new ConflitoException(
                "O registro está vinculado a outros dados e não pode ser excluído ou alterado dessa forma.", ex);
        }
    }
}
