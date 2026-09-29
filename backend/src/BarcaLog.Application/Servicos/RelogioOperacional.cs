using BarcaLog.Application.Configuracao;
using Microsoft.Extensions.Options;

namespace BarcaLog.Application.Servicos;

/// <summary>"Agora" em UTC (timestamps operacionais) e no horário local do porto (comparação com as marcações).</summary>
public class RelogioOperacional(TimeProvider tempo, IOptions<OperacaoOptions> opcoes)
{
    private readonly TimeZoneInfo _fuso = ResolverFuso(opcoes.Value.FusoHorario);

    public DateTime AgoraUtc => tempo.GetUtcNow().UtcDateTime;

    public DateTime AgoraLocalPorto => TimeZoneInfo.ConvertTimeFromUtc(AgoraUtc, _fuso);

    public DateOnly HojeLocalPorto => DateOnly.FromDateTime(AgoraLocalPorto);

    private static TimeZoneInfo ResolverFuso(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception)
        {
            // Sem base de fusos no SO: Belém é UTC−3 fixo (sem horário de verão).
            return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "Horário de Brasília", "BRT");
        }
    }
}
