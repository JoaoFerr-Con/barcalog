using System.Globalization;
using BarcaLog.Application.Metricas;

namespace BarcaLog.Application.Relatorios;

/// <summary>Período coberto pelos dados de um terminal (primeira e última marcação).</summary>
public sealed record CoberturaTerminal(string TerminalId, string Terminal, DateOnly Primeira, DateOnly Ultima);

/// <summary>Um mês do histórico. "Parcial" = algum terminal do recorte não tem dados no mês inteiro.</summary>
public sealed record MesHistorico(string Chave, string Rotulo, int Entradas, int Soja, int Milho, int Outras, double? EsperaMediana, bool Parcial);

public sealed record FaixaEspera(string Rotulo, int Quantidade, double Pct);

/// <summary>Volume de um dia num terminal comparado à referência diária.</summary>
public sealed record DiaTerminal(DateOnly Data, string TerminalId, string Terminal, int Entradas, int Referencia, bool ReferenciaInformada)
{
    public double PctReferencia => Referencia == 0 ? 0 : Entradas * 100.0 / Referencia;
    public int Diferenca => Entradas - Referencia;
}

/// <summary>Resultado do teste da estimativa simples contra o histórico.</summary>
/// <remarks>
/// <para>Disponivel: true só quando o teste passou (erro médio dentro do limite).</para>
/// <para>ErroMedioPct: Erro percentual médio absoluto nos meses testados.</para>
/// <para>MesesTestados: Meses em que a estimativa foi comparada com o realizado.</para>
/// <para>Estimativa: Estimativa do mês seguinte ao último mês completo (só quando disponível).</para>
/// </remarks>
public sealed record ResultadoProjecao(bool Disponivel, string Metodo, double? ErroMedioPct, IReadOnlyList<(string Rotulo, int Estimado, int Realizado)> MesesTestados,
    string? MesEstimado, int? Estimativa, int? Minimo, int? Maximo, string Motivo);

/// <summary>Cálculos usados só pelos relatórios (os demais vêm da Análise 360° e do motor de métricas).</summary>
public static class CalculosRelatorio
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Erro médio máximo aceito para mostrar a estimativa do próximo mês.</summary>
    public const double ErroMaximoProjecaoPct = 15;

    /// <summary>Faixas de espera (h) para a distribuição da permanência.</summary>
    public static readonly (string Rotulo, double Min, double? Max)[] Faixas =
    [
        ("Até 6 h", 0, 6), ("6 a 12 h", 6, 12), ("12 a 24 h", 12, 24), ("24 a 48 h", 24, 48), ("48 a 72 h", 48, 72), ("Mais de 72 h", 72, null)
    ];

    public static string RotuloMes(string chave)
    {
        var d = DateTime.ParseExact(chave + "-01", "yyyy-MM-dd", Br);
        return Br.TextInfo.ToTitleCase(d.ToString("MMMM 'de' yyyy", Br).ToLower(Br)).Replace(" De ", " de ");
    }

    public static string RotuloMesCurto(string chave)
    {
        var d = DateTime.ParseExact(chave + "-01", "yyyy-MM-dd", Br);
        var m = d.ToString("MMM", Br).TrimEnd('.');
        return $"{char.ToUpper(m[0], Br)}{m[1..]}/{d:yy}";
    }

    public static IReadOnlyList<CoberturaTerminal> Cobertura(IEnumerable<RegistroAnalise> regs) =>
        regs.GroupBy(r => r.TerminalId)
            .Select(g => new CoberturaTerminal(g.Key, g.First().Terminal, DateOnly.FromDateTime(g.Min(r => r.DataMarcacao)), DateOnly.FromDateTime(g.Max(r => r.DataMarcacao))))
            .OrderBy(c => c.Terminal, StringComparer.Create(Br, true))
            .ToList();

    /// <summary>Mês completo = todos os terminais do recorte têm dados do primeiro ao último dia do mês.</summary>
    public static bool MesCompleto(string chave, IReadOnlyList<CoberturaTerminal> cobertura)
    {
        if (cobertura.Count == 0) return false;
        var inicio = DateOnly.ParseExact(chave + "-01", "yyyy-MM-dd", Br);
        var fim = inicio.AddMonths(1).AddDays(-1);
        return cobertura.All(c => c.Primeira <= inicio && c.Ultima >= fim);
    }

    /// <summary>Entradas por mês de marcação, separadas por tipo de carga, até <paramref name="ate"/>.</summary>
    public static List<MesHistorico> HistoricoMensal(IEnumerable<RegistroAnalise> regs, DateOnly ate, IReadOnlyList<CoberturaTerminal> cobertura)
    {
        var limite = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return regs.Where(r => r.DataMarcacao < limite)
            .GroupBy(r => r.DataMarcacao.ToString("yyyy-MM", Br))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var esperas = g.Where(r => r.EsperaHoras is not null).Select(r => r.EsperaHoras!.Value).OrderBy(x => x).ToList();
                int soja = g.Count(r => EhSoja(r.Carga)), milho = g.Count(r => EhMilho(r.Carga));
                var mesCortado = DateOnly.ParseExact(g.Key + "-01", "yyyy-MM-dd", Br).AddMonths(1).AddDays(-1) > ate;
                return new MesHistorico(g.Key, RotuloMesCurto(g.Key), g.Count(), soja, milho, g.Count() - soja - milho,
                    Arred(Indicadores.Percentil(esperas, 0.5)), mesCortado || !MesCompleto(g.Key, cobertura));
            }).ToList();
    }

    public static bool EhSoja(string carga) => carga.StartsWith("Soja", StringComparison.OrdinalIgnoreCase);
    public static bool EhMilho(string carga) => carga.StartsWith("Milho", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Distribuição das esperas (marcação → liberação) das carretas já liberadas. Faixas fechadas
    /// à direita — "até 6 h" inclui 6 h; "mais de 72 h" começa acima de 72 h — para que a soma das
    /// três primeiras seja igual a "liberadas em até 24 h" e a última igual a "esperas longas".
    /// </summary>
    public static List<FaixaEspera> DistribuicaoEspera(IEnumerable<RegistroAnalise> entradas)
    {
        var esperas = entradas.Where(r => r.EsperaHoras is not null).Select(r => r.EsperaHoras!.Value).ToList();
        var total = esperas.Count;
        return Faixas.Select(f =>
        {
            var n = esperas.Count(e => (f.Min == 0 ? e >= 0 : e > f.Min) && (f.Max is not { } max || e <= max));
            return new FaixaEspera(f.Rotulo, n, total == 0 ? 0 : Math.Round(n * 100.0 / total, 1));
        }).ToList();
    }

    /// <summary>Entradas por dia e terminal, com a referência diária de cada terminal.</summary>
    public static List<DiaTerminal> DiasPorTerminal(IEnumerable<RegistroAnalise> entradas, Func<string, (int Referencia, bool Informada)> referencia) =>
        entradas.GroupBy(r => (Data: DateOnly.FromDateTime(r.DataMarcacao), r.TerminalId))
            .Select(g =>
            {
                var (refe, inf) = referencia(g.Key.TerminalId);
                return new DiaTerminal(g.Key.Data, g.Key.TerminalId, g.First().Terminal, g.Count(), refe, inf);
            })
            .OrderByDescending(d => d.Entradas).ThenBy(d => d.Data)
            .ToList();

    /// <summary>Entradas por dia (todos os terminais do recorte somados), maiores primeiro.</summary>
    public static List<(DateOnly Data, int Entradas)> DiasTotais(IEnumerable<RegistroAnalise> entradas) =>
        entradas.GroupBy(r => DateOnly.FromDateTime(r.DataMarcacao))
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(d => d.Item2).ThenBy(d => d.Key)
            .ToList();

    /// <summary>Carretas já liberadas com as maiores esperas.</summary>
    public static List<RegistroAnalise> MaioresEsperas(IEnumerable<RegistroAnalise> entradas, int limite) =>
        entradas.Where(r => r.EsperaHoras is not null)
            .OrderByDescending(r => r.EsperaHoras).ThenBy(r => r.DataMarcacao)
            .Take(limite).ToList();

    /// <summary>
    /// Estimativa simples e transparente do próximo mês: média dos três meses completos anteriores.
    /// Antes de mostrar, o método é testado em cada mês completo que tem três meses completos antes
    /// dele; se o erro médio passar de <see cref="ErroMaximoProjecaoPct"/>%, não há estimativa.
    /// </summary>
    public static ResultadoProjecao Projecao(IReadOnlyList<MesHistorico> historico)
    {
        const string metodo = "Média das entradas dos três meses completos anteriores";
        var completos = historico.Where(m => !m.Parcial).ToList();
        var testes = new List<(string, int, int)>();
        for (var i = 3; i < completos.Count; i++)
        {
            var anteriores = completos.Skip(i - 3).Take(3).ToList();
            if (!Consecutivos(anteriores.Append(completos[i]).ToList())) continue;
            testes.Add((completos[i].Rotulo, (int)Math.Round(anteriores.Average(m => m.Entradas)), completos[i].Entradas));
        }
        if (testes.Count < 3)
            return new ResultadoProjecao(false, metodo, null, testes, null, null, null, null,
                $"Há {completos.Count} {(completos.Count == 1 ? "mês completo" : "meses completos")} no histórico — poucos para testar uma estimativa (são necessários ao menos seis seguidos).");
        var erro = Math.Round(testes.Average(t => Math.Abs(t.Item2 - t.Item3) * 100.0 / Math.Max(1, t.Item3)), 1);
        if (erro > ErroMaximoProjecaoPct)
            return new ResultadoProjecao(false, metodo, erro, testes, null, null, null, null,
                $"No teste com o histórico, essa estimativa errou em média {erro.ToString("0.0", Br)}% — acima do limite de {ErroMaximoProjecaoPct.ToString("0", Br)}% aceito para publicar uma projeção.");
        var ult = completos.TakeLast(3).ToList();
        var est = (int)Math.Round(ult.Average(m => m.Entradas));
        var prox = DateOnly.ParseExact(completos[^1].Chave + "-01", "yyyy-MM-dd", Br).AddMonths(1).ToString("yyyy-MM", Br);
        return new ResultadoProjecao(true, metodo, erro, testes, RotuloMes(prox), est,
            (int)Math.Round(est * (1 - erro / 100)), (int)Math.Round(est * (1 + erro / 100)),
            $"Erro médio de {erro.ToString("0.0", Br)}% no teste com o histórico.");
    }

    private static bool Consecutivos(IReadOnlyList<MesHistorico> meses)
    {
        for (var i = 1; i < meses.Count; i++)
        {
            var a = DateOnly.ParseExact(meses[i - 1].Chave + "-01", "yyyy-MM-dd", Br);
            if (a.AddMonths(1).ToString("yyyy-MM", Br) != meses[i].Chave) return false;
        }
        return true;
    }

    private static double? Arred(double? v) => v is null ? null : Math.Round(v.Value, 2);
}
