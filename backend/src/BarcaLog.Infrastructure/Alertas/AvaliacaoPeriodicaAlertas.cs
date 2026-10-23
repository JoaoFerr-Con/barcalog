using BarcaLog.Application.Servicos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BarcaLog.Infrastructure.Alertas;

/// <summary>
/// Reavalia as regras de alerta ao subir e depois a cada
/// <c>Alertas:IntervaloMinutos</c> (padrão 10; 0 desliga — usado nos testes).
/// Previsões dependem da hora do dia, por isso a reavaliação periódica.
/// </summary>
public class AvaliacaoPeriodicaAlertas(IServiceScopeFactory escopos, IConfiguration configuracao, ILogger<AvaliacaoPeriodicaAlertas> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutos = configuracao.GetValue("Alertas:IntervaloMinutos", 10);
        if (minutos <= 0) return;
        // Espera a API terminar de subir (migrations/seed) antes da primeira avaliação.
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch (OperationCanceledException) { return; }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutos));
        do
        {
            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                var r = await escopo.ServiceProvider.GetRequiredService<AlertasServico>().AvaliarAsync(Domain.Enums.OrigemAvaliacao.Automatica, stoppingToken);
                if (r.Criados + r.Alterados + r.Encerrados > 0)
                    logger.LogInformation("Alertas reavaliados: {Criados} novo(s), {Alterados} alterado(s), {Encerrados} encerrado(s)", r.Criados, r.Alterados, r.Encerrados);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Falha aqui não derruba a API; tenta de novo no próximo ciclo.
                logger.LogError(ex, "Falha ao reavaliar alertas");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
