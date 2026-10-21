namespace BarcaLog.Application.Metricas;

/// <summary>
/// Estatísticas de espera (marcação → liberação) de um conjunto de movimentos.
/// Esperas só existem para movimentos liberados; os ainda abertos são contados à parte.
/// </summary>
/// <param name="Movimentacoes">Marcações no período (liberadas + ainda sem liberação).</param>
/// <param name="Liberadas">Movimentos já liberados (base das esperas).</param>
/// <param name="SemLiberacao">Marcados e ainda não liberados.</param>
/// <param name="MediaDiaria">Movimentações ÷ dias corridos do período.</param>
/// <param name="DiasComOperacao">Dias com ao menos uma marcação.</param>
/// <param name="EsperaMediana">Mediana da espera (h).</param>
/// <param name="EsperaP90">90º percentil da espera (h).</param>
/// <param name="EsperaMedia">Média da espera (h) — puxada por casos extremos.</param>
/// <param name="LiberadasAte24hPct">% das liberadas com espera ≤ 24 h.</param>
/// <param name="EsperasLongas">Liberadas com espera acima do limite de espera longa (padrão 72 h).</param>
public sealed record EstatisticasEspera(
    int Movimentacoes,
    int Liberadas,
    int SemLiberacao,
    double? MediaDiaria,
    int DiasComOperacao,
    double? EsperaMediana,
    double? EsperaP90,
    double? EsperaMedia,
    double? LiberadasAte24hPct,
    int EsperasLongas);

public static class Indicadores
{
    /// <summary>
    /// Percentil por interpolação linear entre os vizinhos (mesmo método padrão do
    /// pandas/NumPy e do PERCENTIL.INC do Excel). <paramref name="ordenados"/> em ordem crescente.
    /// </summary>
    public static double? Percentil(IReadOnlyList<double> ordenados, double p)
    {
        if (ordenados.Count == 0) return null;
        if (p is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(p));
        var pos = (ordenados.Count - 1) * p;
        var baixo = (int)Math.Floor(pos);
        var alto = (int)Math.Ceiling(pos);
        return ordenados[baixo] + (ordenados[alto] - ordenados[baixo]) * (pos - baixo);
    }

    public static EstatisticasEspera Calcular(
        IReadOnlyCollection<RegistroMarcacao> liberadas, int semLiberacao, int diasDoPeriodo, double limiteEsperaLonga)
    {
        var esperas = liberadas.Select(r => r.EsperaHoras).OrderBy(e => e).ToList();
        var total = liberadas.Count + semLiberacao;
        return new EstatisticasEspera(
            total,
            liberadas.Count,
            semLiberacao,
            diasDoPeriodo > 0 ? (double)total / diasDoPeriodo : null,
            liberadas.Select(r => r.MarcadoEm.Date).Distinct().Count(),
            Percentil(esperas, 0.5),
            Percentil(esperas, 0.9),
            esperas.Count == 0 ? null : esperas.Average(),
            esperas.Count == 0 ? null : 100.0 * esperas.Count(e => e <= 24) / esperas.Count,
            esperas.Count(e => e > limiteEsperaLonga));
    }

    /// <summary>Dias corridos de <paramref name="de"/> a <paramref name="ate"/>, inclusive.</summary>
    public static int DiasCorridos(DateOnly de, DateOnly ate) => ate.DayNumber - de.DayNumber + 1;
}
