using BarcaLog.Domain.Regras;

namespace BarcaLog.Application.Metricas;

/// <summary>
/// Porte fiel de src/data/metricsEngine.js. Mesmas fórmulas, mesmos
/// limiares, mesmos textos — a ideia é o frontend parar de calcular e só
/// exibir o que a API devolve. Horas/dias da semana saem do horário local do
/// porto gravado em DataMarcacao/DataLiberacao (igual ao getHours() do JS
/// sobre as strings sem fuso dos datasets).
/// </summary>
public static class MotorMetricas
{
    public const int CapacidadeDiaria = Relatorio.CapacidadeDiariaCarretas; // carretas/dia por terminal
    public const double CustoDemurrageHora = 12.50;                        // R$/hora por carreta retida além do SLA
    public const double SlaLimiteHoras = JanelaPermanencia.SlaLimiteHoras;  // 6 dias = estouro crítico

    // Business case (referências ANTT / mercado)
    public const double DiariaCaminhao = 600;       // R$/dia de caminhão parado
    public const double SlaAlvoHoras = 12;          // meta operacional ideal
    public const double ConsumoDieselHora = 2.5;    // litros/hora em marcha lenta
    public const double PctMotorLigadoFila = 0.30;  // 30% do tempo com motor ligado
    public const double Co2PorLitroDiesel = 2.68;   // kg CO₂ por litro

    // Limiares do índice de risco por hora (ρ = λ/μ) — validados pelo usuário.
    public const double RhoCritico = 1.4;
    public const double RhoAlto = 1.2;
    public const double RhoAtencao = 1.0;

    private static readonly (string Rotulo, int Min, int Max)[] TurnosDoDia =
    [
        ("Madrugada (00–06h)", 0, 6),
        ("Manhã (06–12h)", 6, 12),
        ("Tarde (12–18h)", 12, 18),
        ("Noite (18–00h)", 18, 24)
    ];

    // ---------- A. CICLO DE PERMANÊNCIA E JANELAS ----------
    public static FaixaPermanencia ClassificarJanela(double horasEspera) => JanelaPermanencia.Classificar(horasEspera);

    public static List<JanelaDistribuicao> DistribuicaoJanelas(IReadOnlyCollection<RegistroMarcacao> registros)
    {
        var contagem = JanelaPermanencia.Todas.ToDictionary(j => j.Chave, _ => 0);
        foreach (var m in registros) contagem[ClassificarJanela(m.EsperaHoras).Chave]++;
        var total = registros.Count == 0 ? 1 : registros.Count;
        return JanelaPermanencia.Todas
            .Select(j => new JanelaDistribuicao(j.Chave, j.Rotulo, j.Min, JsMath.MaxOuNulo(j.Max), j.Cor, contagem[j.Chave], (double)contagem[j.Chave] / total * 100))
            .ToList();
    }

    public static List<RegistroMarcacao> OutliersEstouroCritico(IEnumerable<RegistroMarcacao> registros, double limite = SlaLimiteHoras) =>
        registros.Where(m => m.EsperaHoras > limite).OrderByDescending(m => m.EsperaHoras).Take(15).ToList();

    // ---------- B. MÉDIA PONDERADA DE PERMANÊNCIA ----------
    /// <summary>Tp_ponderado = Σ(Permanência_i × Volume_i) / Σ(Volume_i), Volume = carretas do terminal no dia.</summary>
    public static double MediaPonderadaPermanencia(IEnumerable<RegistroMarcacao> registros)
    {
        double numerador = 0, denominador = 0;
        foreach (var g in registros.GroupBy(m => (Dia: Relatorio.ChaveDia(m.MarcadoEm), m.TerminalNome)))
        {
            var n = g.Count();
            var mediaLocal = g.Sum(m => m.EsperaHoras) / n;
            numerador += mediaLocal * n;
            denominador += n;
        }
        return denominador > 0 ? numerador / denominador : 0;
    }

    // ---------- C. TEORIA DAS FILAS: RITMO OPERACIONAL λ/μ ----------
    /// <summary>λ = chegadas/hora, μ = liberações/hora, por faixa horária, normalizadas pelo nº de dias do recorte.</summary>
    public static List<FaixaRitmo> RitmoOperacional(IReadOnlyCollection<RegistroMarcacao> registros)
    {
        var chegadas = new int[24];
        var saidas = new int[24];
        foreach (var m in registros)
        {
            chegadas[m.MarcadoEm.Hour]++;
            saidas[m.LiberadoEm.Hour]++;
        }
        var dias = registros.Select(m => Relatorio.ChaveDia(m.MarcadoEm)).Distinct().Count();
        if (dias == 0) dias = 1;
        return Enumerable.Range(0, 24).Select(h =>
        {
            var lambda = (double)chegadas[h] / dias;
            var mu = (double)saidas[h] / dias;
            return new FaixaRitmo(h, chegadas[h], saidas[h], lambda, mu, lambda > mu * 1.15);
        }).ToList();
    }

    /// <summary>Alerta quando λ &gt; μ por 2 horas consecutivas ou mais.</summary>
    public static List<AlertaRitmo> AlertasRitmo(IReadOnlyList<FaixaRitmo> ritmo)
    {
        var alertas = new List<AlertaRitmo>();
        int? inicio = null;
        var sequencia = 0;
        for (var i = 0; i < ritmo.Count; i++)
        {
            if (ritmo[i].Lambda > ritmo[i].Mu)
            {
                inicio ??= i;
                sequencia++;
            }
            else
            {
                if (sequencia >= 2) alertas.Add(new AlertaRitmo(inicio!.Value, i - 1, sequencia));
                inicio = null;
                sequencia = 0;
            }
        }
        if (sequencia >= 2) alertas.Add(new AlertaRitmo(inicio!.Value, 23, sequencia));
        return alertas;
    }

    // ---------- D. CUSTO DE DEMURRAGE ----------
    public static CustoDemurrage CustoTotalDemurrage(IEnumerable<RegistroMarcacao> registros, double limiteHoras = SlaLimiteHoras, double custoHora = CustoDemurrageHora)
    {
        double custoTotal = 0;
        var retidas = 0;
        foreach (var m in registros)
        {
            if (m.EsperaHoras > limiteHoras)
            {
                custoTotal += (m.EsperaHoras - limiteHoras) * custoHora;
                retidas++;
            }
        }
        return new CustoDemurrage(custoTotal, retidas, limiteHoras, custoHora);
    }

    // ---------- E. CONCENTRAÇÃO DE TRÁFEGO POR TURNO ----------
    public static List<TurnoConcentracao> ConcentracaoPorTurno(IEnumerable<RegistroMarcacao> registros)
    {
        var totais = new int[TurnosDoDia.Length];
        var retidos = new int[TurnosDoDia.Length];
        foreach (var m in registros)
        {
            var idx = IndiceTurno(m.MarcadoEm.Hour);
            if (idx < 0) continue;
            totais[idx]++;
            if (m.EsperaHoras > 24) retidos[idx]++;
        }
        return TurnosDoDia.Select((t, i) => new TurnoConcentracao(
            t.Rotulo, t.Min, t.Max, totais[i], retidos[i], totais[i] > 0 ? (double)retidos[i] / totais[i] * 100 : 0)).ToList();
    }

    /// <summary>Top 6 combinações dia-da-semana × turno com mais carretas retidas (≥24h).</summary>
    public static List<HorarioCritico> HorariosCriticos(IEnumerable<RegistroMarcacao> registros) =>
        registros
            .Where(m => m.EsperaHoras >= 24 && IndiceTurno(m.MarcadoEm.Hour) >= 0)
            .GroupBy(m => (Dia: Relatorio.DiasSemana[(int)m.MarcadoEm.DayOfWeek], Turno: TurnosDoDia[IndiceTurno(m.MarcadoEm.Hour)].Rotulo))
            .Select(g => new HorarioCritico(g.Key.Dia, g.Key.Turno, g.Count()))
            .OrderByDescending(h => h.Total)
            .Take(6)
            .ToList();

    private static int IndiceTurno(int hora) => Array.FindIndex(TurnosDoDia, t => hora >= t.Min && hora < t.Max);

    // ---------- F. CAPACIDADE E SATURAÇÃO ----------
    public sealed record MesRotulo(string Chave, string Rotulo);
    public sealed record MatrizVolumeMensal(IReadOnlyList<MesRotulo> Meses, IReadOnlyList<string> Empresas, IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Valores);

    public static MatrizVolumeMensal MatrizVolume(IReadOnlyCollection<RegistroMarcacao> registros)
    {
        var meses = registros.Select(m => Relatorio.ChaveMes(m.MarcadoEm)).Distinct().OrderBy(m => m, StringComparer.Ordinal).ToList();
        var empresas = registros.Select(m => m.TerminalNome).Distinct().OrderBy(e => e, StringComparer.Ordinal).ToList();
        var valores = registros
            .GroupBy(m => Relatorio.ChaveMes(m.MarcadoEm))
            .ToDictionary(g => g.Key, g => (IReadOnlyDictionary<string, int>)g.GroupBy(m => m.TerminalNome).ToDictionary(e => e.Key, e => e.Count()));
        return new MatrizVolumeMensal(
            meses.Select(m => new MesRotulo(m, $"{Relatorio.NomesMes[int.Parse(m.Substring(5, 2)) - 1][..3]}/{m.Substring(2, 2)}")).ToList(),
            empresas,
            valores);
    }

    public static List<CapacidadeDia> CapacidadeDiariaPorDia(IEnumerable<RegistroMarcacao> registros, int capacidade = CapacidadeDiaria) =>
        registros
            .GroupBy(m => Relatorio.ChaveDia(m.MarcadoEm))
            .Select(g => new CapacidadeDia(g.Key, g.Count(), (double)g.Count() / capacidade * 100))
            .OrderByDescending(d => d.Total)
            .ToList();

    // ---------- G. TOP 30 DIAS (COMPACTO) ----------
    public static List<DiaCompacto> Top30DiasCompacto(IEnumerable<RegistroMarcacao> registros, Func<string, int>? capacidadePorEmpresa = null) =>
        Relatorio.AgruparPorDiaEmpresa(registros)
            .Take(30)
            .Select(d =>
            {
                var cap = capacidadePorEmpresa?.Invoke(d.Empresa) ?? CapacidadeDiaria;
                return new DiaCompacto(d.Data, d.Empresa, d.Total, (double)d.Total / cap * 100, d.Total > cap * 0.8);
            })
            .ToList();

    // ---------- H. ALERTAS PREDITIVOS (7 DIAS) ----------
    /// <summary>
    /// Se a tendência dos últimos 7 dias (a partir da última marcação) continuar,
    /// em quantos dias satura? Os volumes diários são ordenados por data.
    /// </summary>
    public static List<AlertaSaturacao> AlertasSaturacao(IReadOnlyCollection<RegistroMarcacao> registros, int capacidade = CapacidadeDiaria)
    {
        if (registros.Count == 0) return [];
        var agora = registros.Max(m => m.MarcadoEm);
        var limite7d = agora.AddDays(-7);
        var volumes = registros
            .Where(m => m.MarcadoEm >= limite7d)
            .GroupBy(m => Relatorio.ChaveDia(m.MarcadoEm))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (double)g.Count())
            .ToList();
        if (volumes.Count < 3) return [];
        var mediaRecente = volumes.Average();
        var tendenciaDiaria = volumes.Count > 1 ? (volumes[^1] - volumes[0]) / (volumes.Count - 1) : 0;
        int? diasParaSaturacao = tendenciaDiaria > 0 ? (int)Math.Ceiling((capacidade - mediaRecente) / tendenciaDiaria) : null;
        var critico = mediaRecente > capacidade * 0.8;
        var mensagem = critico
            ? $"Volume médio recente ({JsMath.Round(mediaRecente)}/dia) está acima de 80% da capacidade nominal. Risco de saturação iminente."
            : tendenciaDiaria > 5
                ? $"Tendência de alta: +{JsMath.Round(tendenciaDiaria)} carretas/dia. Saturação projetada em ~{(diasParaSaturacao is > 0 or < 0 ? diasParaSaturacao.ToString() : "?")} dias se mantido o ritmo."
                : $"Operação estável. Volume médio recente: {JsMath.Round(mediaRecente)}/dia ({JsMath.ToFixed(mediaRecente / capacidade * 100, 0)}% da capacidade).";
        return
        [
            new AlertaSaturacao(
                critico ? "critico" : tendenciaDiaria > 0 ? "atencao" : "estavel",
                JsMath.Round(mediaRecente),
                JsMath.Round(tendenciaDiaria),
                diasParaSaturacao,
                mensagem)
        ];
    }

    // ---------- I. RECOMENDAÇÕES PRESCRITIVAS ----------
    public static List<Recomendacao> RecomendacoesPrescritivas(IReadOnlyCollection<RegistroMarcacao> registros)
    {
        var recs = new List<Recomendacao>();
        var dist = DistribuicaoJanelas(registros);
        var pctEstouro = dist.FirstOrDefault(d => d.Chave == "EC")?.Pct ?? 0;
        var pctAlerta = dist.FirstOrDefault(d => d.Chave == "AL")?.Pct ?? 0;
        if (pctEstouro > 0.5)
            recs.Add(new Recomendacao("alta", "Revisar agendamento de descargas",
                $"{JsMath.ToFixed(pctEstouro, 1)}% dos veículos permanecem >144h. Recomenda-se redistribuir agendamentos entre terminais e ampliar janela D0 para reduzir acúmulo."));
        if (pctAlerta > 2)
            recs.Add(new Recomendacao("media", "Monitorar trocas de turno",
                $"{JsMath.ToFixed(pctAlerta, 1)}% dos veículos ficam entre 96–144h. Verificar se gargalos coincidem com trocas de turno ou finais de semana."));
        var horasCongestionadas = RitmoOperacional(registros).Count(r => r.Congestionado);
        if (horasCongestionadas > 4)
            recs.Add(new Recomendacao("media", "Rebalancear horários de entrada",
                $"{horasCongestionadas} faixas horárias com λ > μ. Considerar escalonar chegadas para horários de menor demanda (madrugada/manhã cedo)."));
        if (recs.Count == 0)
            recs.Add(new Recomendacao("baixa", "Operação dentro dos parâmetros", "Nenhum gargalo crítico identificado no período. Manter monitoramento contínuo."));
        return recs;
    }

    // ====================================================================
    // BUSINESS CASE — Impacto Financeiro, ROI e ESG
    // ====================================================================

    /// <summary>1. Custo ampliado do gargalo (perda invisível D1–D3 em diante).</summary>
    public static CustoAmpliadoGargalo CustoAmpliado(IReadOnlyCollection<RegistroMarcacao> registros)
    {
        (string Chave, string Rotulo, double Min, double Max)[] faixas =
        [
            ("D1", "D1 (24–48h)", 24, 48),
            ("D2", "D2 (48–72h)", 48, 72),
            ("D3", "D3 (72–96h)", 72, 96),
            ("AL", "Alerta (96–144h)", 96, 144),
            ("EC", "Estouro (>144h)", 144, double.PositiveInfinity)
        ];
        var resultado = faixas.Select(f =>
        {
            var veiculos = registros.Where(m => m.EsperaHoras >= f.Min && m.EsperaHoras < f.Max).ToList();
            var qtd = veiculos.Count;
            var mediaExcedente = qtd > 0 ? veiculos.Sum(m => Math.Max(0, m.EsperaHoras - SlaAlvoHoras)) / qtd : 0;
            var custoPorVeiculo = mediaExcedente / 24 * DiariaCaminhao;
            return new FaixaCustoGargalo(f.Chave, f.Rotulo, f.Min, JsMath.MaxOuNulo(f.Max), qtd, mediaExcedente, custoPorVeiculo, custoPorVeiculo * qtd);
        }).ToList();
        return new CustoAmpliadoGargalo(resultado, resultado.Sum(r => r.Qtd), resultado.Sum(r => r.ImpactoTotal));
    }

    /// <summary>2. Fator de utilização (ρ = λ/μ) nos horários críticos — top 6.</summary>
    public static List<FatorUtilizacao> FatoresUtilizacaoCriticos(IReadOnlyCollection<RegistroMarcacao> registros) =>
        RitmoOperacional(registros)
            .Where(r => r.Lambda > r.Mu && r.Mu > 0)
            .Select(r =>
            {
                var rho = r.Mu > 0 ? r.Lambda / r.Mu : 0;
                var diagnostico = rho >= 1.4 ? "Fila exponencial" : rho >= 1.0 ? "Acúmulo crescente" : "Sob controle";
                return new FatorUtilizacao(r.Hora, r.Lambda, r.Mu, rho, diagnostico);
            })
            .OrderByDescending(f => f.Rho)
            .Take(6)
            .ToList();

    /// <summary>3. SLA por etapa operacional (decomposição proporcional do Tp).</summary>
    public static List<SlaEtapa> SlaPorEtapa(double tmpPonderado)
    {
        (string Etapa, double Pct, string Sla, string Controle)[] etapas =
        [
            ("Chegada e Triagem (Gate In)", 0.16, "15 min", "Validação automática de janela / Agendamento"),
            ("Permanência em Pátio", 0.605, "2h 00min", "Convocação dinâmica via push / Painel digital"),
            ("Pesagem e Amostragem", 0.125, "30 min", "Integração de dados de laudo em tempo real"),
            ("Tombador / Descarga", 0.071, "35 min", "Monitoramento de sensor de moega"),
            ("Pesagem Final e Gate Out", 0.039, "10 min", "Automação OCR / Liberação digital")
        ];
        return etapas.Select(e => new SlaEtapa(e.Etapa, e.Pct, e.Sla, e.Controle, Relatorio.FormatarHoras(tmpPonderado * e.Pct))).ToList();
    }

    /// <summary>4. Simulação de ROI em 3 cenários.</summary>
    public static List<CenarioRoi> SimulacaoRoi(IReadOnlyCollection<RegistroMarcacao> registros)
    {
        var total = registros.Count;
        var tmpMedio = registros.Sum(m => m.EsperaHoras) / (total == 0 ? 1 : total);
        (string Rotulo, double Reducao)[] cenarios =
        [
            ("Conservador (Apenas Agendamento)", 0.20),
            ("Moderado (+ Monitoramento de Gate)", 0.40),
            ("Otimizado (+ Slot Booking Preditivo)", 0.60)
        ];
        return cenarios.Select(c =>
        {
            var novoTmp = tmpMedio * (1 - c.Reducao);
            var horasEconomizadas = tmpMedio - novoTmp;
            var economiaPorCarreta = horasEconomizadas / 24 * DiariaCaminhao;
            return new CenarioRoi(c.Rotulo, c.Reducao, Relatorio.FormatarHoras(novoTmp), horasEconomizadas, economiaPorCarreta * total);
        }).ToList();
    }

    /// <summary>5. Impacto ESG (diesel desperdiçado + CO₂).</summary>
    public static ImpactoEsg ImpactoESG(IReadOnlyCollection<RegistroMarcacao> registros)
    {
        var totalCarretas = registros.Count;
        var tmpMedio = registros.Sum(m => m.EsperaHoras) / (totalCarretas == 0 ? 1 : totalCarretas);
        var litros = totalCarretas * tmpMedio * ConsumoDieselHora * PctMotorLigadoFila;
        var toneladas = litros * Co2PorLitroDiesel / 1000;
        return new ImpactoEsg(totalCarretas, tmpMedio, JsMath.RoundLong(litros), JsMath.RoundLong(toneladas));
    }

    // ====================================================================
    // INTELIGÊNCIA PREDITIVA E EFICIÊNCIA OPERACIONAL
    // ====================================================================

    /// <summary>
    /// Projeção mensal pela tendência recente (últimos até 6 meses), limitada a
    /// [55%, 160%] da média recente, com faixa de ~80% de confiança.
    /// </summary>
    public static AnalisePreditiva AnalisePreditivaMensal(IEnumerable<RegistroMarcacao> registros, int mesesAFrente = 3)
    {
        var porMes = Relatorio.AgruparPorMes(registros);
        var valores = porMes.Select(m => (double)m.Total).ToList();
        var n = valores.Count;
        if (n < 3) return new AnalisePreditiva([], "insuficiente", 0);

        var janela = Math.Min(6, n);
        var valoresJanela = valores.Skip(n - janela).ToList();
        var mediaXJ = (janela - 1) / 2.0;
        var mediaRecente = valoresJanela.Average();
        double num = 0, den = 0;
        for (var i = 0; i < janela; i++)
        {
            num += (i - mediaXJ) * (valoresJanela[i] - mediaRecente);
            den += Math.Pow(i - mediaXJ, 2);
        }
        var inclinacao = den == 0 ? 0 : num / den;
        var interceptoJ = mediaRecente - inclinacao * mediaXJ;

        var partes = porMes[n - 1].Chave.Split('-');
        int anoUlt = int.Parse(partes[0]), mesUlt = int.Parse(partes[1]);
        var limiteInferior = mediaRecente * 0.55;
        var limiteSuperior = mediaRecente * 1.6;

        double somaResiduosQuad = 0;
        for (var i = 0; i < janela; i++)
        {
            var ajustado = interceptoJ + inclinacao * i;
            somaResiduosQuad += Math.Pow(valoresJanela[i] - ajustado, 2);
        }
        var desvioResidual = Math.Sqrt(somaResiduosQuad / janela);

        var projecoes = new List<ProjecaoMensal>();
        for (var p = 1; p <= mesesAFrente; p++)
        {
            int mes = mesUlt + p, ano = anoUlt;
            while (mes > 12) { mes -= 12; ano += 1; }
            var bruto = interceptoJ + inclinacao * (janela - 1 + p);
            var total = JsMath.Round(Math.Min(limiteSuperior, Math.Max(limiteInferior, bruto)));
            var margem = desvioResidual * 1.28 * Math.Sqrt(p);
            projecoes.Add(new ProjecaoMensal(
                $"{ano}-{mes:D2}",
                $"{Relatorio.NomesMes[mes - 1]}/{ano.ToString()[2..]}",
                total,
                true,
                JsMath.Round(Math.Max(0, total - margem)),
                JsMath.Round(total + margem),
                Math.Max(60, 95 - p * 8)));
        }
        var inclinacaoPct = mediaRecente > 0 ? inclinacao / mediaRecente * 100 : 0;
        return new AnalisePreditiva(
            projecoes,
            inclinacaoPct > 3 ? "alta" : inclinacaoPct < -3 ? "queda" : "estavel",
            JsMath.Round(inclinacao));
    }

    /// <summary>TMA (tempo médio de atendimento/espera) por terminal, geral e último mês.</summary>
    public static List<TmaTerminal> TmaPorTerminal(IEnumerable<RegistroMarcacao> registros) =>
        registros
            .GroupBy(m => m.TerminalNome)
            .Select(g =>
            {
                var porMes = g.GroupBy(m => Relatorio.ChaveMes(m.MarcadoEm)).OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
                var ult = porMes[^1];
                var pen = porMes.Count >= 2 ? porMes[^2] : null;
                var tmaAtual = ult.Average(m => m.EsperaHoras);
                double? tmaAnterior = pen?.Average(m => m.EsperaHoras);
                double? variacao = tmaAnterior is > 0 or < 0 ? (tmaAtual - tmaAnterior.Value) / tmaAnterior.Value * 100 : null;
                return new TmaTerminal(g.Key, g.Average(m => m.EsperaHoras), tmaAtual, variacao, g.Count());
            })
            .OrderBy(t => t.TmaGeral)
            .ToList();

    /// <summary>Previsão de gargalo pra Portaria nas próximas 6 horas a partir de <paramref name="horaAtual"/> (hora local do porto).</summary>
    public static List<AlertaGargaloPortaria> PrevisaoGargaloPortaria(IReadOnlyCollection<RegistroMarcacao> registros, int horaAtual)
    {
        var ritmo = RitmoOperacional(registros);
        var alertas = new List<AlertaGargaloPortaria>();
        for (var i = 1; i <= 6; i++)
        {
            var h = (horaAtual + i) % 24;
            var f = ritmo[h];
            if (f.Lambda > f.Mu * 1.1)
            {
                var divisor = f.Mu == 0 ? 1 : f.Mu;
                alertas.Add(new AlertaGargaloPortaria(
                    h, i, f.Lambda, f.Mu,
                    f.Lambda / divisor >= 1.3 ? "alto" : "moderado",
                    $"Em ~{i}h ({h}h): entrada prevista de {JsMath.ToFixed(f.Lambda, 1)} carretas/h vs liberação de {JsMath.ToFixed(f.Mu, 1)}/h"));
            }
        }
        return alertas;
    }

    // ====================================================================
    // HORÁRIO DE PICO — ENTRADA (marcação) x SAÍDA (liberação)
    // ====================================================================
    public static PicosEntradaSaida PicosEntradaSaida(IEnumerable<RegistroMarcacao> registros)
    {
        var entrada = new int[24];
        var saida = new int[24];
        foreach (var m in registros)
        {
            entrada[m.MarcadoEm.Hour]++;
            saida[m.LiberadoEm.Hour]++;
        }
        var mediaEntrada = entrada.Sum() / 24.0;
        var mediaSaida = saida.Sum() / 24.0;
        var listaEntrada = entrada.Select((t, h) => new HoraPico(h, t, t > mediaEntrada * 1.15)).ToList();
        var listaSaida = saida.Select((t, h) => new HoraPico(h, t, t > mediaSaida * 1.15)).ToList();
        var picoEntrada = listaEntrada.Aggregate((max, e) => e.Total > max.Total ? e : max);
        var picoSaida = listaSaida.Aggregate((max, e) => e.Total > max.Total ? e : max);
        var valeEntrada = listaEntrada.Aggregate((min, e) => e.Total < min.Total ? e : min);
        return new PicosEntradaSaida(listaEntrada, listaSaida, picoEntrada, picoSaida, valeEntrada, mediaEntrada, mediaSaida);
    }

    // ====================================================================
    // INDICADORES DE PERFORMANCE — comparativo por terminal com Δ
    // ====================================================================
    public static List<IndicadorPerformance> IndicadoresPerformance(IEnumerable<RegistroMarcacao> registros) =>
        registros
            .GroupBy(m => m.TerminalNome)
            .Select(doTerminal =>
            {
                var porMes = doTerminal
                    .GroupBy(m => Relatorio.ChaveMes(m.MarcadoEm))
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.ToList());
                var meses = porMes.Keys.ToList();
                var ultimoMes = meses[^1];
                var penultimoMes = meses.Count >= 2 ? meses[^2] : null;

                var atual = Calc(porMes[ultimoMes]);
                var anterior = penultimoMes is null ? ((double Tma, double TaxaD0, int Total)?)null : Calc(porMes[penultimoMes]);
                var geral = Calc(doTerminal.ToList());

                return new IndicadorPerformance(
                    doTerminal.Key,
                    geral.Tma,
                    geral.TaxaD0,
                    geral.Total,
                    atual.Tma,
                    atual.TaxaD0,
                    anterior is { Tma: > 0 } a ? (atual.Tma - a.Tma) / a.Tma * 100 : null,
                    anterior is { } b ? atual.TaxaD0 - b.TaxaD0 : null,
                    meses.Select(m => new SerieMensal(m, Calc(porMes[m]).Tma)).ToList(),
                    meses.Select(m => new SerieMensal(m, Calc(porMes[m]).TaxaD0)).ToList(),
                    ultimoMes);
            })
            .ToList();

    private static (double Tma, double TaxaD0, int Total) Calc(IReadOnlyCollection<RegistroMarcacao> lista)
    {
        if (lista.Count == 0) return (0, 0, 0);
        var tma = lista.Sum(m => m.EsperaHoras) / lista.Count;
        var noD0 = lista.Count(m => m.EsperaHoras <= 24);
        return (tma, (double)noD0 / lista.Count * 100, lista.Count);
    }

    // ====================================================================
    // VISÃO POR TERMINAL (cards) + PREVISÃO DE GARGALOS
    // ====================================================================

    /// <summary>
    /// Volume, espera média, ocupação (média diária ÷ capacidade nominal do
    /// terminal) e status semafórico. Crítico: ocupação ≥90% ou espera ≥20h;
    /// atenção: ≥75% ou ≥14h; senão normal.
    /// </summary>
    public static List<VisaoTerminal> VisaoPorTerminal(IEnumerable<RegistroMarcacao> registros, Func<string, int>? capacidadePorTerminalId = null) =>
        registros
            .GroupBy(m => (m.TerminalId, m.TerminalNome))
            .Select(g =>
            {
                var lista = g.ToList();
                var dias = lista.Select(m => Relatorio.ChaveDia(m.MarcadoEm)).Distinct().Count();
                if (dias == 0) dias = 1;
                var mediaDiaria = (double)lista.Count / dias;
                var esperaMedia = lista.Sum(m => m.EsperaHoras) / lista.Count;
                var capacidade = capacidadePorTerminalId?.Invoke(g.Key.TerminalId) ?? CapacidadeDiaria;
                var ocupacaoPct = mediaDiaria / capacidade * 100;
                var status = "normal";
                if (ocupacaoPct >= 90 || esperaMedia >= 20) status = "critico";
                else if (ocupacaoPct >= 75 || esperaMedia >= 14) status = "atencao";
                return new VisaoTerminal(g.Key.TerminalId, g.Key.TerminalNome, lista.Count, mediaDiaria, capacidade, esperaMedia, ocupacaoPct, status, Semaforo(status));
            })
            .OrderByDescending(t => t.Total)
            .ToList();

    public static string Semaforo(string status) => status switch
    {
        "critico" => "vermelho",
        "atencao" => "amarelo",
        _ => "verde"
    };

    /// <summary>
    /// Índice de risco por hora = ρ = λ/μ (×100 no campo Indice). ρ ≥ 1.4 crítico,
    /// ≥ 1.2 alto, ≥ 1.0 atenção, abaixo normal. Sem liberação na hora (μ=0) e
    /// com chegada, ρ = 2 (mesma convenção do frontend).
    /// </summary>
    public static List<RiscoHora> IndiceRiscoGargalo(IReadOnlyCollection<RegistroMarcacao> registros) =>
        RitmoOperacional(registros).Select(r =>
        {
            var rho = CalcularRho(r.Lambda, r.Mu);
            return new RiscoHora(
                r.Hora, r.Lambda, r.Mu, rho, JsMath.Round(rho * 100), ClassificarRho(rho),
                $"Historicamente, às {r.Hora}h chegam em média {JsMath.ToFixed(r.Lambda, 1)} carretas/h contra uma liberação de {JsMath.ToFixed(r.Mu, 1)}/h.");
        }).ToList();

    public static double CalcularRho(double lambda, double mu) => mu > 0 ? lambda / mu : (lambda > 0 ? 2 : 0);

    public static string ClassificarRho(double rho) =>
        rho >= RhoCritico ? "critico" : rho >= RhoAlto ? "alto" : rho >= RhoAtencao ? "atencao" : "normal";

    /// <summary>Painel "Alertas Operacionais" (hora crítica, atraso crescente, ocupação crítica).</summary>
    public static List<AlertaOperacional> AlertasOperacionais(IReadOnlyCollection<RegistroMarcacao> registros, Func<string, int>? capacidadePorTerminalId = null)
    {
        var alertas = new List<AlertaOperacional>();

        var horaCritica = IndiceRiscoGargalo(registros)
            .Where(h => h.Nivel == "critico")
            .OrderByDescending(h => h.Indice)
            .FirstOrDefault();
        if (horaCritica is not null)
            alertas.Add(new AlertaOperacional("critico", $"Gargalo recorrente às {horaCritica.Hora}h",
                $"{horaCritica.Explicacao} Padrão histórico consistente — considerar reforço de equipe ou redistribuição de horários nessa faixa."));

        foreach (var d in IndicadoresPerformance(registros))
        {
            if (d.DeltaTMA is > 15)
                alertas.Add(new AlertaOperacional("alto", $"Atraso crescente — {d.Terminal}",
                    $"Tempo médio de espera subiu {JsMath.ToFixed(d.DeltaTMA.Value, 1)}% no último mês com dados, em relação ao mês anterior."));
        }

        foreach (var t in VisaoPorTerminal(registros, capacidadePorTerminalId).Where(t => t.Status == "critico"))
        {
            alertas.Add(new AlertaOperacional("critico", $"Ocupação crítica — {t.Terminal}",
                $"Ocupação média de {JsMath.ToFixed(t.OcupacaoPct, 0)}% da capacidade nominal, com espera média de {Relatorio.FormatarHoras(t.EsperaMedia)}."));
        }

        if (alertas.Count == 0)
            alertas.Add(new AlertaOperacional("normal", "Operação dentro do padrão histórico", "Nenhum terminal ou horário apresenta desvio relevante no período analisado."));
        return alertas;
    }
}
