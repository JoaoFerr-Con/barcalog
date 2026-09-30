using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BarcaLog.Infrastructure.Idempotencia;

/// <summary>Remove chaves de idempotência com mais de 24h, uma vez por hora.</summary>
public class LimpezaIdempotencia(IServiceScopeFactory escopos, ILogger<LimpezaIdempotencia> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                var removidas = await escopo.ServiceProvider.GetRequiredService<ServicoIdempotencia>().RemoverExpiradasAsync(stoppingToken);
                if (removidas > 0) logger.LogInformation("Limpeza de idempotência: {Removidas} chaves expiradas removidas", removidas);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Falha aqui não pode derrubar a API; fica registrada e tenta de novo na próxima hora.
                logger.LogError(ex, "Falha na limpeza de chaves de idempotência");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
