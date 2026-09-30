using BarcaLog.Infrastructure.Persistencia;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BarcaLog.Api.Infra;

/// <summary>Readiness: a instância só deve receber tráfego se alcançar o banco.</summary>
public class VerificacaoBanco(BarcaLogDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            return await db.Database.CanConnectAsync(limite.Token)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Banco inacessível");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Banco inacessível", ex); // detalhe só no log, nunca na resposta
        }
    }
}
