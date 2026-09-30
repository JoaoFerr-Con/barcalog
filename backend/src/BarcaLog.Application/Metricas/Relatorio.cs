namespace BarcaLog.Application.Metricas;

/// <summary>
/// Porte fiel de src/data/relatorio.js — agregações sobre os registros reais
/// de marcação/liberação. Funções puras: recebem os registros já filtrados.
/// </summary>
public static class Relatorio
{
    public static readonly string[] NomesMes =
    [
        "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho",
        "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
    ];

    public static readonly string[] DiasSemana = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];

    /// <summary>Limite de referência informado: 1.000 carretas/dia por terminal.</summary>
    public const int CapacidadeDiariaCarretas = 1000;

    public static string ChaveMes(DateTime data) => data.ToString("yyyy-MM");

    public static string ChaveDia(DateTime data) => data.ToString("yyyy-MM-dd");

    public static string RotuloMes(string chave)
    {
        var ano = chave[..4];
        var mes = int.Parse(chave.Substring(5, 2));
        return $"{NomesMes[mes - 1]}/{ano[2..]}";
    }

    public static KpisGerais? ObterKpisGerais(IReadOnlyList<RegistroMarcacao> registros)
    {
        if (registros.Count == 0) return null;
        var total = registros.Count;
        var dias = registros.Select(m => ChaveDia(m.MarcadoEm)).Distinct().Count();
        var mediaDiaria = (double)total / dias;

        var porMes = AgruparPorMes(registros);
        var mesMaisMovimentado = porMes.Aggregate((a, b) => b.Total > a.Total ? b : a);

        var tempoMedioEspera = registros.Sum(m => m.EsperaHoras) / total;
        var maiorAtraso = registros.Aggregate((a, b) => b.EsperaHoras > a.EsperaHoras ? b : a);

        return new KpisGerais(
            total,
            mediaDiaria,
            dias,
            mesMaisMovimentado,
            tempoMedioEspera,
            MotorMetricas.MediaPonderadaPermanencia(registros),
            maiorAtraso);
    }

    public static List<MesTotal> AgruparPorMes(IEnumerable<RegistroMarcacao> registros) =>
        registros
            .GroupBy(m => ChaveMes(m.MarcadoEm))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new MesTotal(g.Key, RotuloMes(g.Key), g.Count()))
            .ToList();

    public static List<MesDetalhado> AgruparPorMesDetalhado(IReadOnlyList<RegistroMarcacao> registros)
    {
        var porMes = AgruparPorMes(registros);
        var totalGeral = registros.Count;
        var diasPorMes = registros
            .GroupBy(m => ChaveMes(m.MarcadoEm))
            .ToDictionary(g => g.Key, g => g.Select(m => ChaveDia(m.MarcadoEm)).Distinct().Count());
        return porMes.Select(m =>
        {
            var diasOperados = diasPorMes.GetValueOrDefault(m.Chave);
            return new MesDetalhado(
                m.Chave,
                m.Rotulo,
                m.Total,
                totalGeral > 0 ? (double)m.Total / totalGeral * 100 : 0,
                diasOperados,
                diasOperados > 0 ? (double)m.Total / diasOperados : 0);
        }).ToList();
    }

    public static List<RegistroMarcacao> RankingMaioresEsperas(IEnumerable<RegistroMarcacao> registros, int limite = 10) =>
        registros.OrderByDescending(m => m.EsperaHoras).Take(limite).ToList();

    /// <summary>Bunge e Amaggi consolidados numa única categoria (mesma decisão do frontend).</summary>
    public static List<OperadorTotal> TotaisPorOperador(IEnumerable<RegistroMarcacao> registros) =>
        registros
            .GroupBy(m => m.Operador is "BUNGE" or "AMAGGI" ? "BUNGE / AMAGGI" : m.Operador)
            .Select(g => new OperadorTotal(g.Key, g.Count()))
            .OrderByDescending(o => o.Total)
            .ToList();

    public static List<CargaTotal> TotaisPorCarga(IEnumerable<RegistroMarcacao> registros) =>
        registros
            .GroupBy(m => m.Carga)
            .Select(g => new CargaTotal(g.Key, g.Count()))
            .OrderByDescending(c => c.Total)
            .ToList();

    public static List<EmpresaTotal> TotaisPorEmpresa(IEnumerable<RegistroMarcacao> registros) =>
        registros
            .GroupBy(m => m.TerminalNome)
            .Select(g => new EmpresaTotal(g.Key, g.Count(), g.Sum(m => m.EsperaHoras) / g.Count()))
            .OrderByDescending(e => e.Total)
            .ToList();

    /// <summary>Regressão linear simples (mínimos quadrados) sobre o histórico mensal.</summary>
    public static ProjecaoVolume? ProjetarVolume(IReadOnlyList<MesTotal> porMes, int mesesAFrente = 2)
    {
        var n = porMes.Count;
        if (n < 3) return null;
        var mediaX = Enumerable.Range(0, n).Average();
        var mediaY = porMes.Average(m => m.Total);
        double numerador = 0, denominador = 0;
        for (var i = 0; i < n; i++)
        {
            numerador += (i - mediaX) * (porMes[i].Total - mediaY);
            denominador += Math.Pow(i - mediaX, 2);
        }
        var inclinacao = denominador == 0 ? 0 : numerador / denominador;
        var intercepto = mediaY - inclinacao * mediaX;

        var partes = porMes[n - 1].Chave.Split('-');
        int anoUlt = int.Parse(partes[0]), mesUlt = int.Parse(partes[1]);

        var projecao = new List<ProjecaoPonto>();
        for (var p = 1; p <= mesesAFrente; p++)
        {
            var totalPrevisto = Math.Max(0, JsMath.Round(intercepto + inclinacao * (n - 1 + p)));
            int mes = mesUlt + p, ano = anoUlt;
            while (mes > 12) { mes -= 12; ano += 1; }
            projecao.Add(new ProjecaoPonto($"{ano}-{mes:D2}", $"{NomesMes[mes - 1]}/{ano.ToString()[2..]}", totalPrevisto, true));
        }
        return new ProjecaoVolume(inclinacao >= 0 ? "alta" : "queda", projecao);
    }

    public static List<HoraTotal> PorHoraDoDia(IEnumerable<RegistroMarcacao> registros)
    {
        var baldes = new int[24];
        foreach (var m in registros) baldes[m.MarcadoEm.Hour]++;
        return baldes.Select((total, hora) => new HoraTotal(hora, total)).ToList();
    }

    public static List<DiaSemanaTotal> PorDiaDaSemana(IEnumerable<RegistroMarcacao> registros)
    {
        var baldes = new int[7];
        foreach (var m in registros) baldes[(int)m.MarcadoEm.DayOfWeek]++;
        return baldes.Select((total, dia) => new DiaSemanaTotal(DiasSemana[dia], total)).ToList();
    }

    /// <summary>Espera média do último mês vs. média dos 3 meses anteriores.</summary>
    public static TendenciaSla? TendenciaSLA(IReadOnlyList<RegistroMarcacao> registros)
    {
        var porMes = AgruparPorMes(registros);
        if (porMes.Count < 2) return null;
        var mapaEspera = registros
            .GroupBy(m => ChaveMes(m.MarcadoEm))
            .ToDictionary(g => g.Key, g => (Soma: g.Sum(m => m.EsperaHoras), N: g.Count()));
        var chaves = porMes.Select(m => m.Chave).ToList();
        var ultima = chaves[^1];
        var anteriores = chaves.Skip(Math.Max(0, chaves.Count - 4)).Take(chaves.Count - 1 - Math.Max(0, chaves.Count - 4)).ToList();
        if (anteriores.Count == 0) return null;

        var esperaUltimoMes = mapaEspera[ultima].Soma / mapaEspera[ultima].N;
        var somaAnteriores = anteriores.Sum(c => mapaEspera[c].Soma);
        var nAnteriores = anteriores.Sum(c => mapaEspera[c].N);
        var esperaMediaAnterior = somaAnteriores / nAnteriores;
        var variacaoPct = esperaMediaAnterior == 0 ? 0 : (esperaUltimoMes - esperaMediaAnterior) / esperaMediaAnterior * 100;
        return new TendenciaSla(porMes[^1].Rotulo, esperaUltimoMes, esperaMediaAnterior, variacaoPct, variacaoPct > 2, variacaoPct < -2);
    }

    /// <summary>Índice 0–100: volume 40%, espera 40%, consistência (desvio) 20%.</summary>
    public static List<ScoreOperador> ScoreEficienciaPorOperador(IEnumerable<RegistroMarcacao> registros)
    {
        var operadores = registros
            .GroupBy(m => m.Operador)
            .Select(g =>
            {
                var esperas = g.Select(m => m.EsperaHoras).ToList();
                var media = esperas.Average();
                var variancia = esperas.Sum(e => Math.Pow(e - media, 2)) / esperas.Count;
                return (Operador: g.Key, Total: esperas.Count, EsperaMedia: media, Desvio: Math.Sqrt(variancia));
            })
            .ToList();
        if (operadores.Count == 0) return [];

        var maiorVolume = operadores.Max(o => o.Total);
        var maiorEspera = operadores.Max(o => o.EsperaMedia);
        var maiorDesvio = Math.Max(operadores.Max(o => o.Desvio), 1);

        return operadores.Select(o =>
        {
            var notaVolume = maiorVolume == 0 ? 0 : (double)o.Total / maiorVolume * 100;
            var notaEspera = maiorEspera == 0 ? 100 : (1 - o.EsperaMedia / maiorEspera) * 100;
            var notaConsistencia = (1 - o.Desvio / maiorDesvio) * 100;
            var score = notaVolume * 0.4 + notaEspera * 0.4 + notaConsistencia * 0.2;
            return new ScoreOperador(o.Operador, o.Total, o.EsperaMedia, o.Desvio, JsMath.Round(Math.Max(0, Math.Min(100, score))));
        }).OrderByDescending(o => o.Score).ToList();
    }

    /// <summary>Convênios que ultrapassaram o limite pelo menos <paramref name="minOcorrencias"/> vezes.</summary>
    public static List<AtrasoRecorrente> AtrasosRecorrentes(IEnumerable<RegistroMarcacao> registros, double limiteHoras = 24, int minOcorrencias = 3) =>
        registros
            .Where(m => m.EsperaHoras >= limiteHoras)
            .GroupBy(m => m.Convenio)
            .Where(g => g.Count() >= minOcorrencias)
            .Select(g => new AtrasoRecorrente(g.Key, g.First().TerminalNome, g.Count(), g.Sum(m => m.EsperaHoras) / g.Count()))
            .OrderByDescending(c => c.Ocorrencias)
            .ToList();

    public static string FormatarHoras(double horas)
    {
        var h = (int)Math.Floor(horas);
        var min = JsMath.Round((horas - h) * 60);
        return $"{h}h{(min > 0 ? $" {min:D2}min" : "")}";
    }

    /// <summary>Baldes 1, 2, 3, 4, 5+ do campo "ciclo".</summary>
    public static List<CicloBalde> DistribuicaoPorCiclo(IEnumerable<RegistroMarcacao> registros)
    {
        string[] chaves = ["1", "2", "3", "4", "5+"];
        var baldes = chaves.ToDictionary(c => c, _ => new List<double>());
        foreach (var m in registros)
        {
            var chave = m.Ciclo >= 5 ? "5+" : m.Ciclo.ToString();
            if (baldes.TryGetValue(chave, out var lista)) lista.Add(m.EsperaHoras);
        }
        return chaves.Select(c => new CicloBalde(c, baldes[c].Count, baldes[c].Count > 0 ? baldes[c].Average() : 0)).ToList();
    }

    public static List<DiaEmpresaTotal> AgruparPorDiaEmpresa(IEnumerable<RegistroMarcacao> registros) =>
        registros
            .GroupBy(m => (Data: ChaveDia(m.MarcadoEm), Empresa: m.TerminalNome))
            .Select(g => new DiaEmpresaTotal(g.Key.Data, g.Key.Empresa, g.Count()))
            .OrderByDescending(d => d.Total)
            .ToList();

    public static List<DetalhamentoMes> DetalhamentoDiarioPorMes(IEnumerable<RegistroMarcacao> registros) =>
        registros
            .GroupBy(m => ChaveMes(m.MarcadoEm))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var ano = int.Parse(g.Key[..4]);
                var mes = int.Parse(g.Key.Substring(5, 2));
                var porDia = g.GroupBy(m => m.MarcadoEm.Day).ToDictionary(d => d.Key, d => d.Count());
                var dias = Enumerable.Range(1, DateTime.DaysInMonth(ano, mes))
                    .Select(d => new DiaDoMes(d, porDia.GetValueOrDefault(d)))
                    .ToList();
                return new DetalhamentoMes(g.Key, $"{NomesMes[mes - 1]} {ano}", dias.Sum(d => d.Total), dias);
            })
            .ToList();

    public static List<DiaEmpresaTotal> DiasQueExcederamCapacidade(IEnumerable<RegistroMarcacao> registros, Func<string, int>? capacidadePorEmpresa = null) =>
        AgruparPorDiaEmpresa(registros)
            .Where(d => d.Total > (capacidadePorEmpresa?.Invoke(d.Empresa) ?? CapacidadeDiariaCarretas))
            .ToList();
}
