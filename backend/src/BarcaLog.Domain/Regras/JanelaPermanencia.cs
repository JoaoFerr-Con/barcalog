namespace BarcaLog.Domain.Regras;

/// <summary>Uma faixa de permanência (DataLiberacao − DataMarcacao).</summary>
public sealed record FaixaPermanencia(string Chave, string Rotulo, double Min, double Max, string Cor);

/// <summary>
/// Janelas de permanência — porte fiel de JANELAS/classificarJanela em
/// src/data/metricsEngine.js: D0 (0–24h), D1 (24–48h), D2 (48–72h),
/// D3 (72–96h), Alerta (96–144h), Estouro Crítico (&gt;144h).
/// Intervalos fechados à esquerda e abertos à direita ([min, max)).
/// </summary>
public static class JanelaPermanencia
{
    public const double SlaLimiteHoras = 144; // 6 dias = estouro crítico

    public static readonly FaixaPermanencia D0 = new("D0", "D0 (0–24h)", 0, 24, "#22C55E");
    public static readonly FaixaPermanencia D1 = new("D1", "D1 (24–48h)", 24, 48, "#F59E0B");
    public static readonly FaixaPermanencia D2 = new("D2", "D2 (48–72h)", 48, 72, "#F97316");
    public static readonly FaixaPermanencia D3 = new("D3", "D3 (72–96h)", 72, 96, "#EF4444");
    public static readonly FaixaPermanencia Alerta = new("AL", "Alerta (96–144h)", 96, 144, "#DC2626");
    public static readonly FaixaPermanencia EstouroCritico = new("EC", "Estouro Crítico (>144h)", 144, double.PositiveInfinity, "#7F1D1D");

    public static IReadOnlyList<FaixaPermanencia> Todas { get; } = [D0, D1, D2, D3, Alerta, EstouroCritico];

    /// <summary>
    /// Mesmo comportamento do JS: primeira faixa com min ≤ h &lt; max; se nenhuma
    /// casar, cai na última (Estouro Crítico).
    /// </summary>
    public static FaixaPermanencia Classificar(double horasEspera)
    {
        foreach (var faixa in Todas)
        {
            if (horasEspera >= faixa.Min && horasEspera < faixa.Max) return faixa;
        }
        return EstouroCritico;
    }

    public static FaixaPermanencia Classificar(DateTime dataMarcacao, DateTime dataLiberacao) =>
        Classificar(EsperaEmHoras(dataMarcacao, dataLiberacao));

    /// <summary>
    /// (liberação − marcação) em horas, 2 casas — idêntico ao campo esperaHoras
    /// dos datasets e à coluna computada no SQL Server.
    /// </summary>
    public static double EsperaEmHoras(DateTime dataMarcacao, DateTime dataLiberacao)
    {
        var segundos = Math.Floor((dataLiberacao - dataMarcacao).TotalSeconds);
        return (double)Math.Round((decimal)segundos / 3600m, 2, MidpointRounding.AwayFromZero);
    }
}
