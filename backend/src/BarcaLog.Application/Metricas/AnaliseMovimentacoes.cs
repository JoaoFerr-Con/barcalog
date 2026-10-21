using System.Globalization;
using BarcaLog.Domain.Enums;

namespace BarcaLog.Application.Metricas;

/// <summary>Movimento como a análise 360° precisa (inclui os sem liberação).</summary>
public sealed record RegistroAnalise(
    string MovimentoId, string Senha, string TerminalId, string Terminal, string Convenio, string? CodConvenio,
    string Operador, string Carga, DateTime DataMarcacao, DateTime? DataLiberacao, double? EsperaHoras, OrigemMarcacao Origem);

/// <summary>Números de um período (todos observados nos registros; nada estimado).</summary>
/// <param name="Entradas">Marcações (chegadas registradas) com data no período.</param>
/// <param name="Saidas">Liberações com data no período (inclui carretas marcadas antes dele).</param>
/// <param name="Concluidas">Marcadas no período e já liberadas.</param>
/// <param name="Pendentes">Marcadas no período e ainda sem liberação.</param>
/// <param name="EntradasPorDia">Entradas ÷ dias corridos do período (compara períodos de durações diferentes).</param>
/// <param name="EsperaMedia">Média de (liberação − marcação) das concluídas, em horas.</param>
/// <param name="EsperaMediana">Mediana das mesmas esperas.</param>
/// <param name="EsperaP90">90º percentil das mesmas esperas.</param>
/// <param name="LiberadasAte24hPct">Concluídas com espera ≤ 24 h ÷ concluídas, em %.</param>
/// <param name="EsperasLongas">Concluídas com espera acima do limite de espera longa.</param>
/// <param name="ComProblemaDeQualidade">Entradas sem código de convênio ou com possível remarcação.</param>
public sealed record ResumoPeriodo(
    int Entradas, int Saidas, int Concluidas, int Pendentes, double EntradasPorDia,
    double? EsperaMedia, double? EsperaMediana, double? EsperaP90, double? LiberadasAte24hPct, int EsperasLongas, int ComProblemaDeQualidade);

/// <summary>Um ponto da série temporal (dia, semana ou mês).</summary>
/// <param name="Chave">yyyy-MM-dd (dia ou segunda-feira da semana) ou yyyy-MM.</param>
/// <param name="De">Primeiro dia do ponto dentro do período.</param>
/// <param name="Ate">Último dia do ponto dentro do período.</param>
/// <param name="Entradas">Marcações.</param>
/// <param name="Saidas">Liberações.</param>
/// <param name="EsperaMediana">Mediana das esperas das marcadas neste ponto e já liberadas.</param>
/// <param name="Parcial">O ponto não cobre a semana/mês inteiro (corte do período).</param>
public sealed record PontoSerie(string Chave, DateOnly De, DateOnly Ate, int Entradas, int Saidas, double? EsperaMediana, bool Parcial);

/// <summary>Faixa horária: médias por dia no período.</summary>
public sealed record PontoHora(int Hora, double EntradasPorDia, double SaidasPorDia);

/// <summary>Dia da semana (0 = domingo): média de entradas por ocorrência daquele dia no período.</summary>
public sealed record PontoDiaSemana(int Dia, double EntradasPorDia);

/// <summary>Um grupo de uma dimensão (terminal, operador, carga, convênio, origem).</summary>
public sealed record GrupoDimensao(
    string Chave, string Rotulo, int Entradas, double PctEntradas, int Concluidas, int Pendentes,
    double? EsperaMediana, double? EsperaP90, double? LiberadasAte24hPct, int EsperasLongas);

/// <summary>Uma dimensão com seus grupos (os maiores primeiro).</summary>
/// <param name="Dimensao">terminal | operador | carga | convenio | origem.</param>
/// <param name="Grupos">Grupos, por entradas decrescentes.</param>
/// <param name="Outros">Grupos além dos exibidos, somados (nulo se todos aparecem).</param>
public sealed record AnaliseDimensao(string Dimensao, IReadOnlyList<GrupoDimensao> Grupos, GrupoDimensao? Outros);

/// <summary>Qualidade e integridade dos registros do período (entradas).</summary>
/// <param name="Registros">Entradas analisadas.</param>
/// <param name="SemLiberacao">Sem data de liberação (não entram nas esperas).</param>
/// <param name="SemCodigoConvenio">Sem COD_CONVENIO (típico da carga inicial).</param>
/// <param name="PossiveisRemarcacoes">Mesma senha, convênio e liberação, marcada de novo em até 10 min (contagem de registros repetidos).</param>
/// <param name="LiberadosEmLote">Liberados no mesmo instante que ao menos outro movimento do terminal.</param>
/// <param name="DuplicidadesMovimento">Mesmo número de movimento repetido: sempre 0, garantido pela chave do banco.</param>
/// <param name="UltimaMarcacao">Última marcação entre os registros do recorte (qualquer data).</param>
public sealed record QualidadeDados(int Registros, int SemLiberacao, int SemCodigoConvenio, int PossiveisRemarcacoes, int LiberadosEmLote,
    int DuplicidadesMovimento, DateTime? UltimaMarcacao);

/// <summary>Resultado completo da análise 360°.</summary>
public sealed record ResultadoAnalise(
    DateOnly De, DateOnly Ate, int Dias, DateOnly AnteriorDe, DateOnly AnteriorAte,
    ResumoPeriodo Atual, ResumoPeriodo? Anterior, string Agrupamento, IReadOnlyList<PontoSerie> Serie,
    IReadOnlyList<PontoHora> PorHora, IReadOnlyList<PontoDiaSemana> PorDiaSemana, IReadOnlyList<AnaliseDimensao> Dimensoes,
    QualidadeDados Qualidade, double LimiteEsperaLongaHoras);

/// <summary>
/// Análise 360° das movimentações: tudo calculado dos registros (marcação e liberação) do recorte.
/// Período = datas de MARCAÇÃO (horário do porto); "saídas" usam a data de LIBERAÇÃO. O período
/// anterior tem a mesma duração e termina na véspera do início; comparações usam média por dia.
/// Regras em docs/INDICADORES.md ("Análise 360°").
/// </summary>
public static class AnaliseMovimentacoes
{
    public const int GruposPorDimensao = 12;
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static (DateOnly De, DateOnly Ate) PeriodoAnterior(DateOnly de, DateOnly ate)
    {
        var dias = ate.DayNumber - de.DayNumber + 1;
        return (de.AddDays(-dias), de.AddDays(-1));
    }

    /// <summary>Agrupamento automático: dia até 62 dias, semana até 182, senão mês.</summary>
    public static string AgrupamentoPadrao(int dias) => dias <= 62 ? "dia" : dias <= 182 ? "semana" : "mes";

    public static ResultadoAnalise Calcular(IReadOnlyList<RegistroAnalise> regs, DateOnly de, DateOnly ate, string? agrupar,
        double limiteLongaHoras, int janelaRemarcacaoMin)
    {
        var dias = ate.DayNumber - de.DayNumber + 1;
        var (ade, aate) = PeriodoAnterior(de, ate);
        var agrupamento = agrupar is "dia" or "semana" or "mes" ? agrupar : AgrupamentoPadrao(dias);

        bool NoPeriodo(DateTime d, DateOnly a, DateOnly b) { var x = DateOnly.FromDateTime(d); return x >= a && x <= b; }
        var entradas = regs.Where(r => NoPeriodo(r.DataMarcacao, de, ate)).ToList();
        var entradasAnt = regs.Where(r => NoPeriodo(r.DataMarcacao, ade, aate)).ToList();
        var remarcacoes = Remarcacoes(entradas, janelaRemarcacaoMin);

        var atual = Resumir(entradas, regs.Count(r => r.DataLiberacao is { } l && NoPeriodo(l, de, ate)), dias, limiteLongaHoras, remarcacoes);
        var anterior = entradasAnt.Count == 0 ? null
            : Resumir(entradasAnt, regs.Count(r => r.DataLiberacao is { } l && NoPeriodo(l, ade, aate)), dias, limiteLongaHoras, Remarcacoes(entradasAnt, janelaRemarcacaoMin));

        var serie = Serie(regs, entradas, de, ate, agrupamento);
        var porHora = Enumerable.Range(0, 24).Select(h => new PontoHora(h,
            Math.Round(entradas.Count(r => r.DataMarcacao.Hour == h) / (double)dias, 2),
            Math.Round(regs.Count(r => r.DataLiberacao is { } l && NoPeriodo(l, de, ate) && l.Hour == h) / (double)dias, 2))).ToList();
        var porDiaSemana = Enumerable.Range(0, 7).Select(d =>
        {
            var ocorrencias = Enumerable.Range(0, dias).Count(i => (int)de.AddDays(i).DayOfWeek == d);
            return new PontoDiaSemana(d, ocorrencias == 0 ? 0 : Math.Round(entradas.Count(r => (int)r.DataMarcacao.DayOfWeek == d) / (double)ocorrencias, 1));
        }).ToList();

        var dimensoes = new List<AnaliseDimensao>
        {
            Dimensao("terminal", entradas, r => r.TerminalId, r => r.Terminal, limiteLongaHoras),
            Dimensao("operador", entradas, r => r.Operador, r => r.Operador, limiteLongaHoras),
            Dimensao("carga", entradas, r => r.Carga, r => r.Carga, limiteLongaHoras),
            Dimensao("convenio", entradas, r => r.Convenio, r => r.Convenio, limiteLongaHoras),
            Dimensao("origem", entradas, r => r.Origem.ToString(), r => r.Origem switch
            {
                OrigemMarcacao.CargaInicial => "Carga inicial", OrigemMarcacao.Planilha => "Planilha", _ => "Integração"
            }, limiteLongaHoras)
        };

        var lotes = entradas.Where(r => r.DataLiberacao is not null)
            .GroupBy(r => (r.TerminalId, r.DataLiberacao)).Where(g => g.Count() > 1).Sum(g => g.Count());
        var qualidade = new QualidadeDados(entradas.Count, entradas.Count(r => r.DataLiberacao is null),
            entradas.Count(r => string.IsNullOrWhiteSpace(r.CodConvenio)), remarcacoes.Count, lotes, 0,
            regs.Count == 0 ? null : regs.Max(r => r.DataMarcacao));

        return new ResultadoAnalise(de, ate, dias, ade, aate, atual, anterior, agrupamento, serie, porHora, porDiaSemana, dimensoes, qualidade, limiteLongaHoras);
    }

    /// <summary>Registros repetidos: mesma senha, convênio, terminal e liberação, marcados de novo em até N minutos.</summary>
    public static HashSet<string> Remarcacoes(IEnumerable<RegistroAnalise> regs, int janelaMin)
    {
        var repetidos = new HashSet<string>();
        foreach (var g in regs.Where(r => r.DataLiberacao is not null).GroupBy(r => (r.TerminalId, r.Senha, r.Convenio, r.DataLiberacao)))
        {
            var ordem = g.OrderBy(r => r.DataMarcacao).ToList();
            for (var i = 1; i < ordem.Count; i++)
                if ((ordem[i].DataMarcacao - ordem[i - 1].DataMarcacao).TotalMinutes <= janelaMin) repetidos.Add(ordem[i].MovimentoId);
        }
        return repetidos;
    }

    private static ResumoPeriodo Resumir(List<RegistroAnalise> entradas, int saidas, int dias, double limite, HashSet<string> remarcacoes)
    {
        var esperas = entradas.Where(r => r.EsperaHoras is not null).Select(r => r.EsperaHoras!.Value).OrderBy(x => x).ToList();
        return new ResumoPeriodo(entradas.Count, saidas, esperas.Count, entradas.Count - esperas.Count, Math.Round(entradas.Count / (double)dias, 1),
            esperas.Count == 0 ? null : Math.Round(esperas.Average(), 2),
            Arred(Indicadores.Percentil(esperas, 0.5)), Arred(Indicadores.Percentil(esperas, 0.9)),
            esperas.Count == 0 ? null : Math.Round(esperas.Count(e => e <= 24) * 100.0 / esperas.Count, 1),
            esperas.Count(e => e > limite),
            entradas.Count(r => string.IsNullOrWhiteSpace(r.CodConvenio) || remarcacoes.Contains(r.MovimentoId)));
    }

    private static double? Arred(double? v) => v is null ? null : Math.Round(v.Value, 2);

    private static AnaliseDimensao Dimensao(string nome, List<RegistroAnalise> entradas, Func<RegistroAnalise, string> chave,
        Func<RegistroAnalise, string> rotulo, double limite)
    {
        var total = Math.Max(1, entradas.Count);
        var grupos = entradas.GroupBy(chave).Select(g => Grupo(g.Key, rotulo(g.First()), g.ToList(), total, limite))
            .OrderByDescending(g => g.Entradas).ThenBy(g => g.Rotulo, StringComparer.Create(Br, true)).ToList();
        if (grupos.Count <= GruposPorDimensao + 1) return new AnaliseDimensao(nome, grupos, null);
        var exibidos = grupos.Take(GruposPorDimensao).Select(g => g.Chave).ToHashSet();
        var resto = entradas.Where(r => !exibidos.Contains(chave(r))).ToList();
        return new AnaliseDimensao(nome, grupos.Take(GruposPorDimensao).ToList(),
            Grupo("outros", $"Outros ({grupos.Count - GruposPorDimensao})", resto, total, limite));
    }

    private static GrupoDimensao Grupo(string chave, string rotulo, List<RegistroAnalise> regs, int total, double limite)
    {
        var esperas = regs.Where(r => r.EsperaHoras is not null).Select(r => r.EsperaHoras!.Value).OrderBy(x => x).ToList();
        return new GrupoDimensao(chave, rotulo, regs.Count, Math.Round(regs.Count * 100.0 / total, 1), esperas.Count, regs.Count - esperas.Count,
            Arred(Indicadores.Percentil(esperas, 0.5)), Arred(Indicadores.Percentil(esperas, 0.9)),
            esperas.Count == 0 ? null : Math.Round(esperas.Count(e => e <= 24) * 100.0 / esperas.Count, 1), esperas.Count(e => e > limite));
    }

    private static List<PontoSerie> Serie(IReadOnlyList<RegistroAnalise> regs, List<RegistroAnalise> entradas, DateOnly de, DateOnly ate, string agrupamento)
    {
        DateOnly Inicio(DateOnly d) => agrupamento switch
        {
            "semana" => d.AddDays(-(((int)d.DayOfWeek + 6) % 7)), // segunda-feira
            "mes" => new DateOnly(d.Year, d.Month, 1),
            _ => d
        };
        DateOnly Fim(DateOnly i) => agrupamento switch { "semana" => i.AddDays(6), "mes" => i.AddMonths(1).AddDays(-1), _ => i };
        var pontos = new List<PontoSerie>();
        for (var i = Inicio(de); i <= ate; i = Fim(i).AddDays(1))
        {
            var a = i < de ? de : i;
            var f = Fim(i) > ate ? ate : Fim(i);
            var noPonto = entradas.Where(r => { var x = DateOnly.FromDateTime(r.DataMarcacao); return x >= a && x <= f; }).ToList();
            var saidas = regs.Count(r => r.DataLiberacao is { } l && DateOnly.FromDateTime(l) >= a && DateOnly.FromDateTime(l) <= f);
            var esperas = noPonto.Where(r => r.EsperaHoras is not null).Select(r => r.EsperaHoras!.Value).OrderBy(x => x).ToList();
            var chave = agrupamento == "mes" ? i.ToString("yyyy-MM", Br) : i.ToString("yyyy-MM-dd", Br);
            pontos.Add(new PontoSerie(chave, a, f, noPonto.Count, saidas, Arred(Indicadores.Percentil(esperas, 0.5)), a != i || f != Fim(i)));
        }
        return pontos;
    }
}
