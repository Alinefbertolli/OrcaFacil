using Microsoft.EntityFrameworkCore;
using OrcaFacil.Application.Abstracoes;

namespace OrcaFacil.Infrastructure.Persistencia;

/// <summary>
/// Gera ORC-AAAA-NNNNNN por empresa. O comando INSERT ... ON CONFLICT DO UPDATE ... RETURNING
/// é atômico no PostgreSQL: dois usuários da mesma empresa nunca recebem o mesmo número.
/// Números "pulados" (número gerado, mas orçamento não salvo) são aceitáveis.
/// </summary>
public class GeradorNumeroOrcamento(OrcaFacilDbContext contexto, IEmpresaAtual empresaAtual, TimeProvider relogio)
    : IGeradorNumeroOrcamento
{
    public async Task<string> GerarAsync(CancellationToken cancellationToken = default)
    {
        var empresaId = empresaAtual.EmpresaId
            ?? throw new InvalidOperationException("Não há empresa identificada para gerar o número do orçamento.");

        var chave = $"ORC-{relogio.GetUtcNow().Year}";

        // Os valores entre chaves viram parâmetros SQL (@p0, @p1): sem risco de SQL injection.
        // ToListAsync (sem Single/First) envia o SQL exatamente como escrito, sem o EF "embrulhá-lo".
        var resultado = await contexto.Database.SqlQuery<long>($"""
            INSERT INTO "ContadoresNumeracao" ("EmpresaId", "Chave", "Ultimo")
            VALUES ({empresaId}, {chave}, 1)
            ON CONFLICT ("EmpresaId", "Chave")
            DO UPDATE SET "Ultimo" = "ContadoresNumeracao"."Ultimo" + 1
            RETURNING "Ultimo" AS "Value"
            """).ToListAsync(cancellationToken);

        return $"{chave}-{resultado.Single():D6}";
    }
}
