using BarcaLog.Application.Metricas;

namespace BarcaLog.Application.Relatorios;

/// <summary>Tudo o que o montador precisa, já recortado pelo escopo do usuário e pelos filtros.</summary>
/// <remarks>
/// <para>Analise: Análise 360° do período — a mesma da tela Movimentações → Análise.</para>
/// <para>Entradas: Movimentos marcados no período (com os filtros), inclusive sem liberação.</para>
/// <para>Historico: Entradas por mês até o fim do período.</para>
/// <para>Cobertura: Primeira e última marcação de cada terminal do recorte.</para>
/// <para>Referencia: Referência diária por terminal e se é capacidade informada (true) ou referência provisória (false).</para>
/// </remarks>
public sealed record EntradaMontagem(
    FiltroRelatorio Filtro,
    string Abrangencia,
    IReadOnlyList<ItemRotulado> Filtros,
    ResultadoAnalise Analise,
    IReadOnlyList<RegistroAnalise> Entradas,
    IReadOnlyList<MesHistorico> Historico,
    IReadOnlyList<CoberturaTerminal> Cobertura,
    IReadOnlyList<MesTerminal> HistoricoTerminais,
    Func<string, (int Referencia, bool Informada)> Referencia,
    int ReferenciaPadrao,
    double LimiteEsperaMedianaHoras,
    double MetaLiberadas24hPct,
    IReadOnlyList<FonteContexto> Contexto,
    DateTime GeradoEm,
    string? Organizacao);

/// <summary>
/// Compõe o documento de cada tipo de relatório a partir dos números já calculados. Não calcula
/// indicador novo além dos de <see cref="CalculosRelatorio"/>; os textos só afirmam o que os dados
/// mostram e separam dado observado, contexto externo, interpretação e limitação.
/// </summary>
public static class MontadorRelatorio
{
    public const string FonteDados = "Planilhas de marcação e liberação dos terminais, importadas no BarcaLog";

    public static readonly IReadOnlyDictionary<string, string> Titulos = new Dictionary<string, string>
    {
        ["gerencial"] = "Relatório gerencial",
        ["operacional"] = "Relatório operacional",
        ["analise360"] = "Relatório de análise 360°"
    };

    public static readonly IReadOnlyDictionary<string, string> Descricoes = new Dictionary<string, string>
    {
        ["gerencial"] = "Resumo executivo do mês: volume, espera, comparação com o período anterior, terminais, cargas, gargalos e conclusões.",
        ["operacional"] = "O dia a dia da operação: movimento por dia e horário, permanência, maiores esperas, distribuição e dias de pico.",
        ["analise360"] = "A Análise 360° completa em documento: histórico, fluxo, terminais, permanência, dimensões, picos, safra e qualidade dos dados."
    };

    public static DocumentoRelatorio Montar(EntradaMontagem e)
    {
        var tipo = e.Filtro.Tipo;
        var a = e.Analise;
        var semDados = a.Atual.Entradas == 0;
        var secoes = new List<SecaoRelatorio>();
        if (semDados)
        {
            secoes.Add(new SecaoRelatorio("sem-dados", "Sem movimentações no período", [
                new BlocoParagrafo($"Não há entradas de carretas registradas {Fmt.PeriodoNaFrase(a.De, a.Ate)} para {e.Abrangencia.ToLower(Fmt.Br)}. " +
                    "Por isso este relatório não traz indicadores nem conclusões."),
                FonteDadosQuadro(e)
            ]));
        }
        else
        {
            var completo = e.Filtro.Detalhamento == "completo";
            switch (tipo)
            {
                case "operacional":
                    secoes.Add(Resumo(e));
                    secoes.Add(MovimentoPorDia(e, completo));
                    secoes.Add(Horarios(e));
                    secoes.Add(Permanencia(e, completo ? 20 : 10));
                    secoes.Add(Distribuicao(e, completo));
                    secoes.Add(DiasDePico(e, completo ? 15 : 10));
                    secoes.Add(Ocorrencias());
                    secoes.Add(Metodologia(e, false));
                    break;
                case "analise360":
                    secoes.Add(Resumo(e));
                    secoes.Add(Historico(e, true));
                    secoes.Add(Fluxo(e));
                    secoes.Add(Terminais(e));
                    secoes.Add(Permanencia(e, completo ? 20 : 10));
                    secoes.Add(Dimensoes(e, completo));
                    secoes.Add(PicosESafra(e, completo ? 15 : 10));
                    secoes.Add(Qualidade(e));
                    secoes.Add(Ocorrencias());
                    secoes.Add(Conclusoes(e));
                    secoes.Add(Metodologia(e, true));
                    break;
                default:
                    secoes.Add(Resumo(e));
                    secoes.Add(Historico(e, completo));
                    secoes.Add(Terminais(e));
                    secoes.Add(CargasESafra(e));
                    secoes.Add(Gargalos(e));
                    secoes.Add(Ocorrencias());
                    secoes.Add(Conclusoes(e));
                    secoes.Add(Metodologia(e, true));
                    break;
            }
        }
        if (!semDados && secoes.Count > 0)
            secoes[0] = secoes[0] with { Blocos = [.. secoes[0].Blocos, PerguntasRespondidas(secoes)] };
        return new DocumentoRelatorio(tipo, Titulos[tipo], "Movimentação de carretas nos terminais de Barcarena", a.De, a.Ate,
            Fmt.Periodo(a.De, a.Ate), e.Abrangencia, e.Filtros, e.GeradoEm, string.IsNullOrWhiteSpace(e.Organizacao) ? null : e.Organizacao.Trim(),
            FonteDados, semDados, secoes);
    }

    // ------------------------------------------------------------------ seções

    private static SecaoRelatorio Resumo(EntradaMontagem e)
    {
        var a = e.Analise;
        var (at, an) = (a.Atual, a.Anterior);
        var blocos = new List<BlocoRelatorio> { new BlocoParagrafo(TextoResumo(e), "destaque") };

        var itens = new List<Indicador>
        {
            new("Entradas de carretas", Fmt.Num(at.Entradas), an is null ? null : $"{Fmt.VarPct(at.EntradasPorDia, an.EntradasPorDia) ?? "0,0%"} na média diária", "neutro"),
            new("Média por dia", Fmt.Num(at.EntradasPorDia), an is null ? null : $"antes: {Fmt.Num(an.EntradasPorDia)}", null),
            new("Liberações (saídas)", Fmt.Num(at.Saidas), "liberadas no período", null),
            new("Espera mediana", Fmt.Dur(at.EsperaMediana), an is null ? "metade esperou até esse tempo" : $"{Fmt.VarDur(at.EsperaMediana, an.EsperaMediana)} (antes: {Fmt.Dur(an.EsperaMediana)})",
                Sentido(at.EsperaMediana, an?.EsperaMediana, menorEhMelhor: true)),
            new("Espera média", Fmt.Dur(at.EsperaMedia), an is null ? null : $"antes: {Fmt.Dur(an.EsperaMedia)}", Sentido(at.EsperaMedia, an?.EsperaMedia, menorEhMelhor: true)),
            new("Liberadas em até 24 h", Fmt.Pct(at.LiberadasAte24hPct), an is null ? $"meta configurada: {Fmt.Pct(e.MetaLiberadas24hPct, 0)}" : $"{Fmt.VarPp(at.LiberadasAte24hPct, an.LiberadasAte24hPct)} (meta: {Fmt.Pct(e.MetaLiberadas24hPct, 0)})",
                Sentido(at.LiberadasAte24hPct, an?.LiberadasAte24hPct, menorEhMelhor: false)),
            new($"Esperas acima de {Fmt.Num(a.LimiteEsperaLongaHoras, 0)} h", Fmt.Num(at.EsperasLongas), an is null ? "carretas" : $"antes: {Fmt.Num(an.EsperasLongas)}",
                Sentido(at.EsperasLongas, an?.EsperasLongas, menorEhMelhor: true))
        };
        if (at.Pendentes > 0) itens.Add(new("Ainda sem liberação", Fmt.Num(at.Pendentes), "não entram nas esperas", null));
        blocos.Add(new BlocoIndicadores(itens));
        blocos.Add(new BlocoParagrafo(an is null
            ? $"Não há registros no período anterior equivalente ({Fmt.Data(a.AnteriorDe)} a {Fmt.Data(a.AnteriorAte)}); por isso não há comparação."
            : $"Comparação com {Fmt.Data(a.AnteriorDe)} a {Fmt.Data(a.AnteriorAte)}, período de mesma duração imediatamente anterior. Volumes comparados pela média por dia.", "nota"));
        return new SecaoRelatorio("resumo", "Resumo executivo", blocos);
    }

    private static string TextoResumo(EntradaMontagem e)
    {
        var a = e.Analise;
        var (at, an) = (a.Atual, a.Anterior);
        var sujeito = string.IsNullOrEmpty(e.Filtro.TerminalId) ? "os terminais" : $"o terminal {a.Dimensoes.First(d => d.Dimensao == "terminal").Grupos.FirstOrDefault()?.Rotulo ?? e.Abrangencia}";
        var txt = $"{Primeira(Fmt.PeriodoNaFrase(a.De, a.Ate))}, {sujeito} registr{(sujeito.StartsWith("os") ? "aram" : "ou")} {Fmt.Num(at.Entradas)} entradas de carretas, média de {Fmt.Num(at.EntradasPorDia)} por dia";
        if (an is not null && an.EntradasPorDia > 0)
        {
            var v = (at.EntradasPorDia - an.EntradasPorDia) / an.EntradasPorDia * 100;
            txt += Math.Abs(v) < 0.5 ? ", praticamente o mesmo ritmo do período anterior" : $", {Fmt.Num(Math.Abs(v))}% {(v > 0 ? "acima" : "abaixo")} do período anterior";
        }
        txt += ".";
        if (at.EsperaMediana is { } med)
        {
            txt += $" Metade das carretas liberadas esperou até {Fmt.Dur(med)} entre a marcação e a liberação";
            if (an?.EsperaMediana is { } medAnt) txt += $" (antes: {Fmt.Dur(medAnt)})";
            txt += $", e {Fmt.Pct(at.LiberadasAte24hPct)} foram liberadas em até 24 horas.";
        }
        if (at.EsperasLongas > 0) txt += $" {Fmt.Plural(at.EsperasLongas, "carreta esperou", "carretas esperaram")} mais de {Fmt.Num(a.LimiteEsperaLongaHoras, 0)} horas.";
        var pico = CalculosRelatorio.DiasTotais(e.Entradas).FirstOrDefault();
        if (pico.Entradas > 0) txt += $" O dia mais movimentado foi {Fmt.Data(pico.Data)} ({Fmt.DiasSemana[(int)pico.Data.DayOfWeek]}), com {Fmt.Num(pico.Entradas)} entradas.";
        return txt;
    }

    private static SecaoRelatorio Historico(EntradaMontagem e, bool comTabela)
    {
        var blocos = new List<BlocoRelatorio>();
        var h = e.Historico;
        var serie = e.Analise.Serie;
        blocos.Add(new BlocoGrafico($"Entradas e liberações por {RotuloAgrupamento(e.Analise.Agrupamento)} no período", "barras",
            serie.Select(p => RotuloPonto(p, e.Analise.Agrupamento)).ToList(),
            [
                new SerieGrafico("Entradas", serie.Select(p => (double?)p.Entradas).ToList(), false, "marca"),
                new SerieGrafico("Liberações", serie.Select(p => (double?)p.Saidas).ToList(), false, "ambar")
            ], "carretas", serie.Any(p => p.Parcial) ? "Pontos marcados com * não cobrem a semana ou o mês inteiro (corte do período)." : null, []));
        if (GraficoTerminaisNoPeriodo(e) is { } porTerminal) blocos.Add(porTerminal);
        blocos.Add(GraficoEsperaSerie(e));

        if (h.Count > 0)
        {
            blocos.Add(new BlocoGrafico("Histórico mensal de entradas, por tipo de carga", "barrasEmpilhadas",
                h.Select(m => m.Rotulo + (m.Parcial ? "*" : "")).ToList(),
                [
                    new SerieGrafico("Soja", h.Select(m => (double?)m.Soja).ToList(), false, "marca"),
                    new SerieGrafico("Milho", h.Select(m => (double?)m.Milho).ToList(), false, "ambar"),
                    new SerieGrafico("Outras cargas", h.Select(m => (double?)m.Outras).ToList(), false, "neutra")
                ], "carretas por mês",
                h.Any(m => m.Parcial) ? "* Mês parcial: nem todos os terminais têm dados no mês inteiro — não compare diretamente com os demais." : null,
                IndicesPico(h)));
            if (GraficoMesesPorTerminal(e) is { } meses) blocos.Add(meses);
            blocos.Add(new BlocoGrafico("Espera mediana por mês", "linhas", h.Select(m => m.Rotulo + (m.Parcial ? "*" : "")).ToList(),
                [new SerieGrafico("Espera mediana", h.Select(m => m.EsperaMediana).ToList(), false, "marca")], "horas",
                "Metade das carretas do mês já liberadas esperou até esse tempo entre a marcação e a liberação.", IndicesPico(h)));
            if (comTabela)
            {
                var linhas = h.Select(m => (IReadOnlyList<string>)[
                    CalculosRelatorio.RotuloMes(m.Chave), Fmt.Num(m.Entradas), Fmt.Num(m.Soja), Fmt.Num(m.Milho), Fmt.Num(m.Outras), Fmt.Dur(m.EsperaMediana), m.Parcial ? "Parcial" : "Completo"
                ]).ToList();
                var valores = h.Select(m => (IReadOnlyList<double?>)[null, m.Entradas, m.Soja, m.Milho, m.Outras, m.EsperaMediana is { } x ? x / 24 : null, null]).ToList();
                blocos.Add(new BlocoTabela("Histórico mensal",
                    [new("Mês", Largura: 1.6), new("Entradas", "direita", 1, "#,##0"), new("Soja", "direita", 1, "#,##0"), new("Milho", "direita", 1, "#,##0"),
                     new("Outras", "direita", 0.9, "#,##0"), new("Espera mediana", "direita", 1.1, "[h]:mm"), new("Dados do mês", Largura: 1)],
                    linhas, valores, "Entradas pela data de marcação. Espera mediana das carretas do mês já liberadas.", IndicesPico(h)));
            }
            blocos.Add(MesmoMesAnoAnterior(e));
            var tendencia = TendenciaRecente(h);
            if (tendencia is not null) blocos.Add(new BlocoParagrafo(tendencia));
            blocos.Add(ProjecaoBloco(CalculosRelatorio.Projecao(h)));
        }
        return new SecaoRelatorio("historico", "Volume e histórico", blocos);
    }

    private static SecaoRelatorio MovimentoPorDia(EntradaMontagem e, bool completo)
    {
        var a = e.Analise;
        var blocos = new List<BlocoRelatorio>
        {
            new BlocoGrafico($"Entradas e liberações por {RotuloAgrupamento(a.Agrupamento)}", "barras",
                a.Serie.Select(p => RotuloPonto(p, a.Agrupamento)).ToList(),
                [
                    new SerieGrafico("Entradas", a.Serie.Select(p => (double?)p.Entradas).ToList(), false, "marca"),
                    new SerieGrafico("Liberações", a.Serie.Select(p => (double?)p.Saidas).ToList(), false, "ambar")
                ], "carretas", null, [])
        };
        if (GraficoTerminaisNoPeriodo(e) is { } porTerminal) blocos.Add(porTerminal);
        blocos.Add(GraficoEsperaSerie(e));
        if (completo)
        {
            var linhas = a.Serie.Select(p => (IReadOnlyList<string>)[
                a.Agrupamento == "dia" ? $"{Fmt.Data(p.De)} ({Fmt.DiasSemanaCurto[(int)p.De.DayOfWeek]})" : $"{Fmt.Data(p.De)} a {Fmt.Data(p.Ate)}",
                Fmt.Num(p.Entradas), Fmt.Num(p.Saidas), Fmt.Dur(p.EsperaMediana)
            ]).ToList();
            var valores = a.Serie.Select(p => (IReadOnlyList<double?>)[null, p.Entradas, p.Saidas, p.EsperaMediana is { } x ? x / 24 : null]).ToList();
            var maior = a.Serie.Select((p, i) => (p.Entradas, i)).OrderByDescending(x => x.Entradas).Take(1).Select(x => x.i).ToList();
            blocos.Add(new BlocoTabela(a.Agrupamento == "dia" ? "Movimento por dia" : "Movimento por período",
                [new(a.Agrupamento == "dia" ? "Dia" : "Período", Largura: 2), new("Entradas", "direita", 1, "#,##0"), new("Liberações", "direita", 1, "#,##0"), new("Espera mediana", "direita", 1.2, "[h]:mm")],
                linhas, valores, "Liberações contam a data da liberação (incluem carretas marcadas antes do dia). Em destaque, o maior volume de entradas.", maior));
        }
        return new SecaoRelatorio("movimento", "Movimento no período", blocos);
    }

    private static SecaoRelatorio Fluxo(EntradaMontagem e)
    {
        var blocos = new List<BlocoRelatorio> { new BlocoParagrafo(TextoHorarios(e)) };
        blocos.Add(GraficoHoras(e));
        blocos.Add(GraficoDiaSemana(e));
        return new SecaoRelatorio("fluxo", "Distribuição do fluxo no tempo", blocos);
    }

    private static SecaoRelatorio Horarios(EntradaMontagem e) =>
        new("horarios", "Horários e dias da semana", [new BlocoParagrafo(TextoHorarios(e)), GraficoHoras(e), GraficoDiaSemana(e)]);

    private static string TextoHorarios(EntradaMontagem e)
    {
        var a = e.Analise;
        var pico = a.PorHora.OrderByDescending(h => h.EntradasPorDia).First();
        var vale = a.PorHora.OrderBy(h => h.EntradasPorDia).First();
        var picoSaida = a.PorHora.OrderByDescending(h => h.SaidasPorDia).First();
        var dia = a.PorDiaSemana.OrderByDescending(d => d.EntradasPorDia).First();
        var diaMenor = a.PorDiaSemana.OrderBy(d => d.EntradasPorDia).First();
        return $"As entradas se concentram às {pico.Hora:00}h (média de {Fmt.Num(pico.EntradasPorDia)} carretas por dia nessa hora) e são menores às {vale.Hora:00}h ({Fmt.Num(vale.EntradasPorDia)}). " +
               $"As liberações têm pico às {picoSaida.Hora:00}h ({Fmt.Num(picoSaida.SaidasPorDia)} por dia). " +
               $"Entre os dias da semana, {Fmt.DiasSemana[dia.Dia]} teve mais entradas em média ({Fmt.Num(dia.EntradasPorDia)}) e {Fmt.DiasSemana[diaMenor.Dia]} menos ({Fmt.Num(diaMenor.EntradasPorDia)}).";
    }

    private static BlocoGrafico GraficoHoras(EntradaMontagem e)
    {
        var p = e.Analise.PorHora;
        return new BlocoGrafico("Média de entradas e liberações por hora do dia", "barras", p.Select(h => $"{h.Hora:00}h").ToList(),
            [
                new SerieGrafico("Entradas por dia", p.Select(h => (double?)h.EntradasPorDia).ToList(), false, "marca"),
                new SerieGrafico("Liberações por dia", p.Select(h => (double?)h.SaidasPorDia).ToList(), false, "ambar")
            ], "carretas por dia, em cada hora", "Média por dia do período: total da hora dividido pelo número de dias.",
            [p.Select((h, i) => (h.EntradasPorDia, i)).OrderByDescending(x => x.EntradasPorDia).First().i]);
    }

    private static BlocoGrafico GraficoDiaSemana(EntradaMontagem e)
    {
        var d = e.Analise.PorDiaSemana;
        return new BlocoGrafico("Média de entradas por dia da semana", "barras", d.Select(x => Fmt.DiasSemanaCurto[x.Dia]).ToList(),
            [new SerieGrafico("Entradas por dia", d.Select(x => (double?)x.EntradasPorDia).ToList(), false, "marca")],
            "carretas por dia", "Média: total de entradas naquele dia da semana dividido pelo número de vezes que ele ocorreu no período.",
            [d.Select((x, i) => (x.EntradasPorDia, i)).OrderByDescending(x => x.EntradasPorDia).First().i]);
    }

    private static SecaoRelatorio Terminais(EntradaMontagem e)
    {
        var dim = e.Analise.Dimensoes.First(d => d.Dimensao == "terminal");
        var blocos = new List<BlocoRelatorio> { TabelaDimensao("Comparação entre terminais", "Terminal", dim) };
        if (dim.Grupos.Count > 1)
        {
            blocos.Add(GraficoDimensao("Entradas por terminal", dim, "carretas no período", "terminal"));
            blocos.Add(new BlocoGrafico("Espera por terminal", "barras", dim.Grupos.Select(g => g.Rotulo).ToList(),
                [
                    new SerieGrafico("Espera mediana", dim.Grupos.Select(g => g.EsperaMediana).ToList(), false, "marca"),
                    new SerieGrafico("90% liberadas em até", dim.Grupos.Select(g => g.EsperaP90).ToList(), false, "ambar")
                ], "horas", "Espera mediana: metade das carretas esperou até esse tempo. 90% liberadas em até: 9 de cada 10 saíram dentro desse tempo.", []));
        }
        if (dim.Grupos.Count > 1)
        {
            var maiorVolume = dim.Grupos.OrderByDescending(g => g.Entradas).First();
            var maiorEspera = dim.Grupos.Where(g => g.EsperaMediana is not null).OrderByDescending(g => g.EsperaMediana).FirstOrDefault();
            var menorEspera = dim.Grupos.Where(g => g.EsperaMediana is not null).OrderBy(g => g.EsperaMediana).FirstOrDefault();
            var txt = $"{maiorVolume.Rotulo} concentrou a maior parte das entradas ({Fmt.Pct(maiorVolume.PctEntradas)}).";
            if (maiorEspera is not null && menorEspera is not null && maiorEspera != menorEspera)
                txt += $" A maior espera mediana foi em {maiorEspera.Rotulo} ({Fmt.Dur(maiorEspera.EsperaMediana)}) e a menor em {menorEspera.Rotulo} ({Fmt.Dur(menorEspera.EsperaMediana)}).";
            var acima = dim.Grupos.Where(g => g.EsperaMediana > e.LimiteEsperaMedianaHoras).Select(g => g.Rotulo).ToList();
            if (acima.Count > 0)
                txt += $" {Lista(acima)} {(acima.Count == 1 ? "ficou" : "ficaram")} acima da referência de espera mediana de {Fmt.Num(e.LimiteEsperaMedianaHoras, 0)} h configurada no sistema.";
            blocos.Insert(0, new BlocoParagrafo(txt));
        }
        return new SecaoRelatorio("terminais", "Comparação entre terminais", blocos);
    }

    private static SecaoRelatorio Permanencia(EntradaMontagem e, int topEsperas)
    {
        var faixas = CalculosRelatorio.DistribuicaoEspera(e.Entradas);
        var concluidas = faixas.Sum(f => f.Quantidade);
        var blocos = new List<BlocoRelatorio>
        {
            new BlocoParagrafo($"Das {Fmt.Num(concluidas)} carretas marcadas no período e já liberadas, {Fmt.Pct(faixas.Take(3).Sum(f => f.Pct))} saíram em até 24 horas " +
                $"e {Fmt.Plural(faixas[^1].Quantidade, "carreta", "carretas")} ({Fmt.Pct(faixas[^1].Pct)}) esperaram mais de 72 horas."),
            new BlocoGrafico("Tempo entre a marcação e a liberação", "barras", faixas.Select(f => f.Rotulo).ToList(),
                [new SerieGrafico("Carretas", faixas.Select(f => (double?)f.Quantidade).ToList(), false, "marca")], "carretas", null, [faixas.Count - 1]),
        };
        var maiores = CalculosRelatorio.MaioresEsperas(e.Entradas, topEsperas);
        if (maiores.Count > 0)
            blocos.Add(new BlocoTabela($"As {maiores.Count} maiores esperas do período",
                [new("Movimento", Largura: 1.1), new("Terminal", Largura: 1.1), new("Convênio", Largura: 2.2), new("Marcação", Largura: 1.4), new("Liberação", Largura: 1.4), new("Espera", "direita", 0.9, "[h]:mm")],
                maiores.Select(r => (IReadOnlyList<string>)[r.MovimentoId, r.Terminal, r.Convenio, Fmt.DataHora(r.DataMarcacao), Fmt.DataHora(r.DataLiberacao!.Value), Fmt.Dur(r.EsperaHoras)]).ToList(),
                maiores.Select(r => (IReadOnlyList<double?>)[null, null, null, null, null, r.EsperaHoras / 24]).ToList(),
                "Esperas muito longas podem indicar carreta que aguardou fora da fila ou registro de liberação tardio; vale conferir com o terminal.", []));
        return new SecaoRelatorio("permanencia", "Permanência e maiores esperas", blocos);
    }

    private static SecaoRelatorio Distribuicao(EntradaMontagem e, bool completo)
    {
        var blocos = new List<BlocoRelatorio>();
        if (string.IsNullOrEmpty(e.Filtro.TerminalId))
            blocos.Add(TabelaDimensao("Por terminal", "Terminal", e.Analise.Dimensoes.First(d => d.Dimensao == "terminal")));
        blocos.AddRange(new List<BlocoRelatorio>
        {
            GraficoDimensao("Entradas por operador", e.Analise.Dimensoes.First(d => d.Dimensao == "operador"), "carretas no período"),
            TabelaDimensao("Por operador", "Operador", e.Analise.Dimensoes.First(d => d.Dimensao == "operador")),
            GraficoDimensao("Entradas por tipo de carga", e.Analise.Dimensoes.First(d => d.Dimensao == "carga"), "carretas no período"),
            TabelaDimensao("Por tipo de carga", "Carga", e.Analise.Dimensoes.First(d => d.Dimensao == "carga"))
        });
        if (completo) blocos.Add(TabelaDimensao("Por convênio", "Convênio", e.Analise.Dimensoes.First(d => d.Dimensao == "convenio")));
        return new SecaoRelatorio("distribuicao", "Distribuição por terminal, operador e carga", blocos);
    }

    private static SecaoRelatorio Dimensoes(EntradaMontagem e, bool completo)
    {
        var blocos = new List<BlocoRelatorio>
        {
            GraficoDimensao("Entradas por operador", e.Analise.Dimensoes.First(d => d.Dimensao == "operador"), "carretas no período"),
            TabelaDimensao("Por operador", "Operador", e.Analise.Dimensoes.First(d => d.Dimensao == "operador")),
            GraficoDimensao("Entradas por tipo de carga", e.Analise.Dimensoes.First(d => d.Dimensao == "carga"), "carretas no período"),
            TabelaDimensao("Por tipo de carga", "Carga", e.Analise.Dimensoes.First(d => d.Dimensao == "carga"))
        };
        if (completo) blocos.Add(TabelaDimensao("Por convênio", "Convênio", e.Analise.Dimensoes.First(d => d.Dimensao == "convenio")));
        blocos.Add(TabelaDimensao("Por origem do registro", "Origem", e.Analise.Dimensoes.First(d => d.Dimensao == "origem")));
        return new SecaoRelatorio("dimensoes", "Operadores, cargas e convênios", blocos);
    }

    private static SecaoRelatorio DiasDePico(EntradaMontagem e, int top) =>
        new("dias-pico", "Dias de maior movimento", BlocosDiasDePico(e, top));

    private static List<BlocoRelatorio> BlocosDiasDePico(EntradaMontagem e, int top)
    {
        var dias = CalculosRelatorio.DiasPorTerminal(e.Entradas, e.Referencia);
        var blocos = new List<BlocoRelatorio>();
        var topDias = dias.Take(top).ToList();
        blocos.Add(new BlocoTabela($"Os {topDias.Count} dias de maior movimento por terminal",
            [new("Dia", Largura: 1.5), new("Terminal", Largura: 1.2), new("Entradas", "direita", 0.9, "#,##0"), new("Referência diária", "direita", 1.1, "#,##0"), new("% da referência", "direita", 1, "0.0%")],
            topDias.Select(d => (IReadOnlyList<string>)[$"{Fmt.Data(d.Data)} ({Fmt.DiasSemanaCurto[(int)d.Data.DayOfWeek]})", d.Terminal, Fmt.Num(d.Entradas), Fmt.Num(d.Referencia), Fmt.Pct(d.PctReferencia)]).ToList(),
            topDias.Select(d => (IReadOnlyList<double?>)[null, null, d.Entradas, d.Referencia, d.PctReferencia / 100]).ToList(),
            NotaReferencia(e, dias), []));

        var acima = dias.Where(d => d.Entradas > d.Referencia).OrderBy(d => d.Data).ThenBy(d => d.Terminal).ToList();
        if (acima.Count == 0)
        {
            var maior = dias.FirstOrDefault();
            blocos.Add(new BlocoQuadro("Dias acima da referência diária de movimentação", [
                new("Resultado", maior is null ? "Sem entradas no período." :
                    $"Nenhum dia ultrapassou a referência no período. O maior volume diário de um terminal foi {Fmt.Num(maior.Entradas)} entradas ({maior.Terminal}, {Fmt.Data(maior.Data)}), " +
                    $"{Fmt.Pct(maior.PctReferencia)} da referência de {Fmt.Num(maior.Referencia)}.")
            ]));
        }
        else
        {
            blocos.Add(new BlocoTabela("Dias acima da referência diária de movimentação",
                [new("Dia", Largura: 1.5), new("Terminal", Largura: 1.2), new("Entradas", "direita", 1, "#,##0"), new("Referência", "direita", 1, "#,##0"), new("Diferença", "direita", 1, "#,##0"), new("% acima", "direita", 1, "0.0%")],
                acima.Select(d => (IReadOnlyList<string>)[Fmt.Data(d.Data), d.Terminal, Fmt.Num(d.Entradas), Fmt.Num(d.Referencia), "+" + Fmt.Num(d.Diferenca), Fmt.Pct(d.PctReferencia - 100)]).ToList(),
                acima.Select(d => (IReadOnlyList<double?>)[null, null, d.Entradas, d.Referencia, d.Diferenca, (d.PctReferencia - 100) / 100]).ToList(),
                "A referência é uma régua de comparação, não a capacidade oficial do terminal: passar dela não significa que o terminal operou acima da sua capacidade real.", []));
        }

        var totais = CalculosRelatorio.DiasTotais(e.Entradas);
        if (string.IsNullOrEmpty(e.Filtro.TerminalId) && e.Cobertura.Count > 1 && totais.Count > 0)
        {
            var g = totais.Take(10).OrderBy(d => d.Data).ToList();
            blocos.Add(new BlocoGrafico("Os 10 dias de maior movimento, todos os terminais somados", "barras",
                g.Select(d => $"{Fmt.DataCurta(d.Data)} {Fmt.DiasSemanaCurto[(int)d.Data.DayOfWeek]}").ToList(),
                [new SerieGrafico("Entradas", g.Select(d => (double?)d.Entradas).ToList(), false, "marca")], "carretas por dia",
                "Soma dos terminais, sem comparação com a referência: as referências são por terminal e não se somam.",
                [g.Select((d, i) => (d.Entradas, i)).OrderByDescending(x => x.Entradas).First().i]));

        }
        return blocos;
    }

    private static string NotaReferencia(EntradaMontagem e, IReadOnlyList<DiaTerminal> dias)
    {
        var informadas = dias.Where(d => d.ReferenciaInformada).Select(d => d.Terminal).Distinct().ToList();
        var txt = $"Referência diária: {Fmt.Num(e.ReferenciaPadrao)} carretas por dia, por terminal — valor provisório de comparação, não capacidade oficial confirmada.";
        if (informadas.Count > 0) txt += $" Para {Lista(informadas)}, usa-se a capacidade cadastrada em Configurações.";
        return txt;
    }

    private static SecaoRelatorio Gargalos(EntradaMontagem e)
    {
        var a = e.Analise;
        var blocos = new List<BlocoRelatorio> { new BlocoParagrafo(TextoHorarios(e)), GraficoHoras(e) };
        blocos.AddRange(BlocosDiasDePico(e, 5));
        return new SecaoRelatorio("gargalos", "Picos e gargalos", blocos);
    }

    private static SecaoRelatorio CargasESafra(EntradaMontagem e)
    {
        var carga = e.Analise.Dimensoes.First(d => d.Dimensao == "carga");
        var blocos = new List<BlocoRelatorio> { GraficoDimensao("Entradas por tipo de carga no período", carga, "carretas"), TabelaDimensao("Entradas por tipo de carga", "Carga", carga) };
        blocos.AddRange(BlocosSafra(e));
        return new SecaoRelatorio("cargas", "Cargas e safra", blocos);
    }

    private static SecaoRelatorio PicosESafra(EntradaMontagem e, int top)
    {
        var blocos = BlocosDiasDePico(e, top);
        blocos.AddRange(BlocosSafra(e));
        return new SecaoRelatorio("picos", "Picos de movimentação e safra", blocos);
    }

    /// <summary>
    /// Leitura dos picos à luz da safra: separa o que os registros mostram, o que fontes externas
    /// dizem, a interpretação possível e o que não dá para confirmar.
    /// </summary>
    private static List<BlocoRelatorio> BlocosSafra(EntradaMontagem e)
    {
        var h = e.Historico;
        var blocos = new List<BlocoRelatorio>();
        if (h.Count == 0) return blocos;
        var candidatos = h.Where(m => !m.Parcial).ToList();
        if (candidatos.Count == 0) candidatos = h.ToList();
        var pico = candidatos.OrderByDescending(m => m.Entradas).First();
        var totalSoja = h.Sum(m => m.Soja);
        var total = Math.Max(1, h.Sum(m => m.Entradas));
        var mesesMilho = h.Where(m => !m.Parcial && m.Milho > m.Soja).Select(m => CalculosRelatorio.RotuloMes(m.Chave).ToLower(Fmt.Br)).ToList();
        var pctSojaPico = pico.Entradas == 0 ? 0 : pico.Soja * 100.0 / pico.Entradas;

        var observado = $"No histórico disponível ({CalculosRelatorio.RotuloMes(h[0].Chave).ToLower(Fmt.Br)} a {CalculosRelatorio.RotuloMes(h[^1].Chave).ToLower(Fmt.Br)}), " +
                        $"a soja respondeu por {Fmt.Pct(totalSoja * 100.0 / total)} das entradas. O mês completo com mais entradas foi {CalculosRelatorio.RotuloMes(pico.Chave).ToLower(Fmt.Br)} " +
                        $"({Fmt.Num(pico.Entradas)}), com {Fmt.Pct(pctSojaPico)} de soja.";
        if (mesesMilho.Count > 0) observado += $" O milho superou a soja em {Lista(mesesMilho)}.";

        var (contexto, fontes) = FontesExternas(e);
        var externo = contexto.Count == 0 ? "Nenhuma fonte externa cadastrada para este relatório."
            : string.Join(" ", contexto.Select(c => $"{c.Texto} [{fontes.IndexOf(Chave(c)) + 1}]"));

        var mesPico = int.Parse(pico.Chave[5..]);
        string interpretacao;
        if (pctSojaPico >= 50 && mesPico is >= 2 and <= 6)
            interpretacao = "O pico de entradas coincide com o escoamento da safra de soja: a colheita se concentra no início do ano e as carretas chegam aos terminais do Arco Norte nos meses seguintes. " +
                            (mesesMilho.Count > 0 ? "A virada para o milho no segundo semestre é compatível com a colheita do milho de segunda safra. " : "") +
                            "É uma explicação compatível com os dados, não uma causa comprovada.";
        else if (pctSojaPico >= 50)
            interpretacao = "A soja domina o mês de pico, mas o pico não cai no período típico de escoamento da safra; outros fatores (operação dos terminais, navios, clima) podem pesar mais.";
        else
            interpretacao = "O mês de pico não é dominado pela soja; a safra de soja, sozinha, não explica esse pico.";

        var limitacao = $"O histórico começa em {CalculosRelatorio.RotuloMes(h[0].Chave).ToLower(Fmt.Br)}: ainda não há o mesmo mês de anos anteriores para confirmar que o padrão se repete. " +
                        "O tipo de carga vem do nome do convênio na planilha; os registros não informam a origem das carretas nem a data da colheita.";

        blocos.Add(new BlocoQuadro("Por que há picos de movimentação?", [
            new("Dado observado", observado),
            new("Contexto externo", externo),
            new("Interpretação", interpretacao),
            new("Limitação", limitacao)
        ]));
        return blocos;
    }

    private static SecaoRelatorio Qualidade(EntradaMontagem e)
    {
        var q = e.Analise.Qualidade;
        var itens = new List<ItemRotulado>
        {
            new("Registros analisados", Fmt.Num(q.Registros)),
            new("Sem data de liberação", $"{Fmt.Num(q.SemLiberacao)} — não entram nas esperas"),
            new("Sem código de convênio", $"{Fmt.Num(q.SemCodigoConvenio)} — comum nos registros da carga inicial; não altera as contagens"),
            new("Possíveis remarcações", $"{Fmt.Num(q.PossiveisRemarcacoes)} — mesma senha, convênio e liberação, marcadas de novo em até 10 minutos; podem contar a mesma carreta duas vezes"),
            new("Liberadas em lote", $"{Fmt.Num(q.LiberadosEmLote)} — liberadas no mesmo instante que outra carreta do terminal (comum quando a liberação é registrada em bloco)"),
            new("Movimentos duplicados", "0 — o número do movimento é único no banco")
        };
        return new SecaoRelatorio("qualidade", "Qualidade dos dados", [
            new BlocoParagrafo("Números que ajudam a saber o quanto confiar nos indicadores acima."),
            new BlocoQuadro("Registros do período", itens)
        ]);
    }

    private static SecaoRelatorio Ocorrencias() =>
        new("ocorrencias", "Ocorrências e mapa", [
            new BlocoQuadro("Mapa de ocorrências indisponível", [
                new("Situação", "As ocorrências registradas no BarcaLog ainda não têm localização geográfica (latitude e longitude) — só um campo de texto com o local — e os registros atuais são de demonstração."),
                new("Consequência", "Este relatório não traz mapa nem mapa de calor: distribuir ocorrências no mapa sem a localização real seria inventar dados."),
                new("Como viabilizar", "Registrar as coordenadas no momento da ocorrência (previsto para o aplicativo de fiscalização). Um ponto de referência, como o Posto Mirian na PA-483, pode orientar a leitura do mapa, mas não substitui a localização de cada ocorrência.")
            ], "limitacao")
        ]);

    private static SecaoRelatorio Conclusoes(EntradaMontagem e)
    {
        var a = e.Analise;
        var (at, an) = (a.Atual, a.Anterior);
        var achados = new List<string>();
        if (an is not null && an.EntradasPorDia > 0)
        {
            var v = (at.EntradasPorDia - an.EntradasPorDia) / an.EntradasPorDia * 100;
            achados.Add(Math.Abs(v) < 0.5 ? "O volume diário de entradas ficou estável em relação ao período anterior."
                : $"O volume diário de entradas {(v > 0 ? "cresceu" : "caiu")} {Fmt.Num(Math.Abs(v))}% em relação ao período anterior ({Fmt.Num(an.EntradasPorDia)} → {Fmt.Num(at.EntradasPorDia)} por dia).");
        }
        if (at.EsperaMediana is { } med && an?.EsperaMediana is { } medAnt)
        {
            var d = med - medAnt;
            achados.Add(Math.Abs(d) < 1.0 / 60 ? "A espera mediana não mudou." : $"A espera mediana {(d > 0 ? "aumentou" : "diminuiu")} {Fmt.Dur(Math.Abs(d))} ({Fmt.Dur(medAnt)} → {Fmt.Dur(med)}).");
        }
        var pico = a.PorHora.OrderByDescending(h => h.EntradasPorDia).First();
        achados.Add($"O horário de maior chegada foi {pico.Hora:00}h, com média de {Fmt.Num(pico.EntradasPorDia)} carretas por dia.");
        var dias = CalculosRelatorio.DiasPorTerminal(e.Entradas, e.Referencia);
        var acima = dias.Count(d => d.Entradas > d.Referencia);
        achados.Add(acima == 0 ? $"Nenhum terminal passou da referência diária de {Fmt.Num(e.ReferenciaPadrao)} carretas no período."
            : $"Houve {Fmt.Plural(acima, "dia", "dias")} com um terminal acima da referência diária.");

        var acompanhar = new List<string>();
        if (at.EsperaMediana > e.LimiteEsperaMedianaHoras)
            acompanhar.Add($"Espera mediana ({Fmt.Dur(at.EsperaMediana)}) acima da referência de {Fmt.Num(e.LimiteEsperaMedianaHoras, 0)} h configurada no sistema.");
        if (at.LiberadasAte24hPct < e.MetaLiberadas24hPct)
            acompanhar.Add($"Liberadas em até 24 h ({Fmt.Pct(at.LiberadasAte24hPct)}) abaixo da meta de {Fmt.Pct(e.MetaLiberadas24hPct, 0)}.");
        var dimTerm = a.Dimensoes.First(d => d.Dimensao == "terminal");
        foreach (var g in dimTerm.Grupos.Where(g => g.EsperaMediana > e.LimiteEsperaMedianaHoras && dimTerm.Grupos.Count > 1))
            acompanhar.Add($"{g.Rotulo}: espera mediana de {Fmt.Dur(g.EsperaMediana)}.");
        if (at.EsperasLongas > 0) acompanhar.Add($"{Fmt.Plural(at.EsperasLongas, "carreta", "carretas")} com espera acima de {Fmt.Num(a.LimiteEsperaLongaHoras, 0)} h — conferir os casos na tabela de maiores esperas.");
        if (at.Pendentes > 0) acompanhar.Add($"{Fmt.Plural(at.Pendentes, "carreta marcada", "carretas marcadas")} no período ainda sem liberação registrada.");

        var blocos = new List<BlocoRelatorio> { new BlocoLista(achados) };
        if (acompanhar.Count > 0)
            blocos.Add(new BlocoQuadro("O que merece acompanhamento", acompanhar.Select((t, i) => new ItemRotulado($"{i + 1}", t)).ToList(), "atencao"));
        blocos.Add(new BlocoParagrafo("As conclusões descrevem o que os registros mostram. Elas não apontam causas nem responsáveis sem verificação com os terminais.", "nota"));
        return new SecaoRelatorio("conclusoes", "Conclusões", blocos);
    }

    private static SecaoRelatorio Metodologia(EntradaMontagem e, bool comFontes)
    {
        var limite = Fmt.Num(e.Analise.LimiteEsperaLongaHoras, 0);
        var blocos = new List<BlocoRelatorio>
        {
            new BlocoLista([
                "Entrada: carreta marcada no terminal (data de marcação dentro do período, horário de Barcarena).",
                "Liberação (saída): carreta liberada pelo terminal; conta na data da liberação, mesmo que tenha sido marcada antes do período.",
                "Espera: tempo entre a marcação e a liberação, só para as carretas já liberadas. É guardada em horas com duas casas decimais (precisão de 36 segundos), como nas planilhas de origem; por isso uma carreta exatamente na fronteira de uma faixa pode cair na faixa vizinha.",
                "Espera mediana: metade das carretas esperou até esse tempo. É menos afetada por casos extremos do que a média.",
                "90% liberadas em até: tempo dentro do qual 9 de cada 10 carretas foram liberadas.",
                $"Esperas longas: carretas com espera acima de {limite} horas (parâmetro do sistema).",
                "Período anterior: mesmo número de dias, imediatamente antes do início do período; volumes comparados pela média por dia.",
                $"Referência diária: {Fmt.Num(e.ReferenciaPadrao)} carretas por dia, por terminal — valor provisório de comparação, não capacidade oficial."
            ]),
            FonteDadosQuadro(e)
        };
        if (comFontes)
        {
            var (contexto, fontes) = FontesExternas(e);
            if (fontes.Count > 0)
                blocos.Add(new BlocoQuadro("Fontes externas consultadas", fontes.Select((chave, i) =>
                {
                    var c = contexto.First(x => Chave(x) == chave);
                    return new ItemRotulado($"[{i + 1}]", $"{c.Fonte}{(c.Publicacao is null ? "" : $", {c.Publicacao}")}. {c.Url}{(c.ConsultadoEm is null ? "" : $" (consultado em {c.ConsultadoEm})")}");
                }).ToList()));
        }
        return new SecaoRelatorio("metodologia", "Metodologia e fontes", blocos);
    }

    // ------------------------------------------------------------------ gráficos de apoio

    private static BlocoGrafico GraficoDimensao(string titulo, AnaliseDimensao dim, string unidade, string? cores = null)
    {
        var grupos = dim.Grupos.Take(10).ToList();
        if (cores == "terminal")
            return new BlocoGrafico(titulo, "barrasEmpilhadas", grupos.Select(g => g.Rotulo).ToList(),
                grupos.Select((g, i) => new SerieGrafico(g.Rotulo, grupos.Select((x, k) => k == i ? (double?)x.Entradas : null).ToList(), false, CorTerminal(g.Chave))).ToList(),
                unidade, null, []);
        return new BlocoGrafico(titulo, "barras", grupos.Select(g => g.Rotulo).ToList(),
            [new SerieGrafico("Entradas", grupos.Select(g => (double?)g.Entradas).ToList(), false, "marca")], unidade,
            dim.Grupos.Count > 10 ? "Mostra os 10 maiores; os demais estão na tabela." : null, [0]);
    }

    private static BlocoGrafico GraficoEsperaSerie(EntradaMontagem e)
    {
        var a = e.Analise;
        return new BlocoGrafico($"Espera mediana por {RotuloAgrupamento(a.Agrupamento)}", "linhas", a.Serie.Select(p => RotuloPonto(p, a.Agrupamento)).ToList(),
            [new SerieGrafico("Espera mediana", a.Serie.Select(p => p.EsperaMediana).ToList(), false, "marca")], "horas",
            $"Carretas marcadas em cada {RotuloAgrupamento(a.Agrupamento)} e já liberadas. Referência de alerta configurada: {Fmt.Num(e.LimiteEsperaMedianaHoras, 0)} h.", []);
    }

    /// <summary>Entradas do período por dia/semana/mês, empilhadas por terminal (só quando há mais de um).</summary>
    private static BlocoGrafico? GraficoTerminaisNoPeriodo(EntradaMontagem e)
    {
        var a = e.Analise;
        var terminais = e.Entradas.GroupBy(r => r.TerminalId).Select(g => (Id: g.Key, Nome: g.First().Terminal)).OrderBy(t => t.Nome).ToList();
        if (terminais.Count < 2) return null;
        var porPonto = a.Serie.Select(p => e.Entradas.Where(r => { var d = DateOnly.FromDateTime(r.DataMarcacao); return d >= p.De && d <= p.Ate; })
            .GroupBy(r => r.TerminalId).ToDictionary(g => g.Key, g => g.Count())).ToList();
        return new BlocoGrafico($"Entradas por {RotuloAgrupamento(a.Agrupamento)} e terminal", "barrasEmpilhadas", a.Serie.Select(p => RotuloPonto(p, a.Agrupamento)).ToList(),
            terminais.Select(t => new SerieGrafico(t.Nome, porPonto.Select(d => (double?)d.GetValueOrDefault(t.Id)).ToList(), false, CorTerminal(t.Id))).ToList(),
            "carretas", null, []);
    }

    private static BlocoGrafico? GraficoMesesPorTerminal(EntradaMontagem e)
    {
        var meses = e.Historico.Select(m => m.Chave).ToList();
        var terminais = e.HistoricoTerminais.GroupBy(x => x.TerminalId).Select(g => (Id: g.Key, Nome: g.First().Terminal)).OrderBy(t => t.Nome).ToList();
        if (terminais.Count < 2 || meses.Count == 0) return null;
        var mapa = e.HistoricoTerminais.ToDictionary(x => (x.Chave, x.TerminalId), x => x.Entradas);
        return new BlocoGrafico("Histórico mensal de entradas, por terminal", "barrasEmpilhadas",
            e.Historico.Select(m => m.Rotulo + (m.Parcial ? "*" : "")).ToList(),
            terminais.Select(t => new SerieGrafico(t.Nome, meses.Select(k => (double?)mapa.GetValueOrDefault((k, t.Id))).ToList(), false, CorTerminal(t.Id))).ToList(),
            "carretas por mês", "Um terminal que aparece com pouco volume num mês pode não ter enviado os dados do mês inteiro (veja a origem dos dados).", []);
    }

    private static string CorTerminal(string id) => id is "unitapajos" or "tgpm" or "hidrovias" ? id : "neutra";

    /// <summary>Lista as perguntas que o documento responde (e o que ele não responde), com o número da seção.</summary>
    private static BlocoQuadro PerguntasRespondidas(IReadOnlyList<SecaoRelatorio> secoes)
    {
        var perguntas = new Dictionary<string, string>
        {
            ["resumo"] = "Quantas carretas entraram e foram liberadas, quanto esperaram e como isso se compara ao período anterior?",
            ["historico"] = "Como o volume e a espera evoluíram mês a mês? Qual a tendência? Dá para projetar o próximo mês?",
            ["movimento"] = "Como foi o movimento dia a dia, em cada terminal?",
            ["fluxo"] = "Em que horários e dias da semana as chegadas e as liberações se concentram?",
            ["horarios"] = "Em que horários e dias da semana as chegadas e as liberações se concentram?",
            ["terminais"] = "Qual terminal recebe mais carretas e em qual se espera mais?",
            ["permanencia"] = "Quanto tempo as carretas ficam entre a marcação e a liberação? Quais esperaram mais?",
            ["distribuicao"] = "Como o movimento se divide entre terminais, operadores, cargas e convênios?",
            ["dimensoes"] = "Como o movimento se divide entre operadores, cargas, convênios e origem do registro?",
            ["dias-pico"] = "Quais foram os dias de maior movimento? Algum terminal passou da referência diária?",
            ["gargalos"] = "Quando estão os picos do dia e os dias de maior movimento? Algum terminal passou da referência diária?",
            ["cargas"] = "Que cargas passaram e o pico de movimento coincide com a safra?",
            ["picos"] = "Quais foram os dias de pico e o pico de movimento coincide com a safra?",
            ["qualidade"] = "Quanto dá para confiar nos registros (sem liberação, remarcações, liberações em lote)?",
            ["ocorrencias"] = "Onde se concentram as ocorrências? (Ainda sem resposta: faltam coordenadas.)",
            ["conclusoes"] = "O que mudou no período e o que merece acompanhamento?",
            ["metodologia"] = "Como cada número foi calculado e de onde vêm os dados e as fontes externas?"
        };
        var itens = secoes.Select((s, i) => (s, i)).Where(x => perguntas.ContainsKey(x.s.Id))
            .Select(x => new ItemRotulado($"Seção {x.i + 1:00}", perguntas[x.s.Id])).ToList();
        itens.Add(new ItemRotulado("Não responde", "Quantas carretas diferentes passaram (as planilhas não trazem a placa: cada linha é um movimento) " +
            "nem se os registros batem com os de outro sistema, como o do Posto Mirian — isso exige um relatório de conciliação com os dois lados."));
        return new BlocoQuadro("Perguntas que este relatório responde", itens);
    }

    // ------------------------------------------------------------------ apoio

    /// <summary>Itens de contexto usados no relatório e a lista de fontes sem repetição (mesma URL = mesma fonte).</summary>
    private static (List<FonteContexto> Contexto, List<string> Fontes) FontesExternas(EntradaMontagem e)
    {
        var contexto = e.Contexto.Where(c => c.Tema is "soja" or "milho" or "arco-norte" or "barcarena").ToList();
        return (contexto, contexto.Select(Chave).Distinct().ToList());
    }

    private static string Chave(FonteContexto c) => string.IsNullOrWhiteSpace(c.Url) ? c.Fonte : c.Url!;

    private static BlocoQuadro FonteDadosQuadro(EntradaMontagem e) =>
        new("Origem dos dados", [
            new("Fonte", FonteDados + "."),
            ..e.Cobertura.Select(c => new ItemRotulado(c.Terminal, $"marcações de {Fmt.Data(c.Primeira)} a {Fmt.Data(c.Ultima)}"))
        ]);

    private static BlocoTabela TabelaDimensao(string titulo, string rotulo, AnaliseDimensao dim)
    {
        var grupos = dim.Grupos.Concat(dim.Outros is null ? [] : [dim.Outros]).ToList();
        return new BlocoTabela(titulo,
            [new(rotulo, Largura: 2), new("Entradas", "direita", 1, "#,##0"), new("% do total", "direita", 0.9, "0.0%"), new("Espera mediana", "direita", 1.1, "[h]:mm"),
             new("90% liberadas em até", "direita", 1.3, "[h]:mm"), new("Liberadas em até 24 h", "direita", 1.2, "0.0%"), new("Esperas longas", "direita", 1, "#,##0")],
            grupos.Select(g => (IReadOnlyList<string>)[g.Rotulo, Fmt.Num(g.Entradas), Fmt.Pct(g.PctEntradas), Fmt.Dur(g.EsperaMediana), Fmt.Dur(g.EsperaP90), Fmt.Pct(g.LiberadasAte24hPct), Fmt.Num(g.EsperasLongas)]).ToList(),
            grupos.Select(g => (IReadOnlyList<double?>)[null, g.Entradas, g.PctEntradas / 100, g.EsperaMediana / 24, g.EsperaP90 / 24, g.LiberadasAte24hPct / 100, g.EsperasLongas]).ToList(),
            null, []);
    }

    private static BlocoRelatorio MesmoMesAnoAnterior(EntradaMontagem e)
    {
        var chave = e.Analise.De.ToString("yyyy-MM", Fmt.Br);
        var anterior = e.Analise.De.AddYears(-1).ToString("yyyy-MM", Fmt.Br);
        var atual = e.Historico.FirstOrDefault(m => m.Chave == chave);
        var mesmo = e.Historico.FirstOrDefault(m => m.Chave == anterior);
        if (atual is null || mesmo is null)
            return new BlocoParagrafo($"Comparação com o mesmo mês do ano anterior ({CalculosRelatorio.RotuloMes(anterior).ToLower(Fmt.Br)}): indisponível — o histórico do BarcaLog começa em " +
                $"{(e.Historico.Count > 0 ? CalculosRelatorio.RotuloMes(e.Historico[0].Chave).ToLower(Fmt.Br) : "data posterior")}.", "nota");
        return new BlocoParagrafo($"Em relação a {CalculosRelatorio.RotuloMes(anterior).ToLower(Fmt.Br)} ({Fmt.Num(mesmo.Entradas)} entradas), {CalculosRelatorio.RotuloMes(chave).ToLower(Fmt.Br)} teve " +
            $"{Fmt.VarPct(atual.Entradas, mesmo.Entradas)} de entradas{(atual.Parcial || mesmo.Parcial ? " (um dos meses é parcial)" : "")}.");
    }

    private static string? TendenciaRecente(IReadOnlyList<MesHistorico> h)
    {
        var completos = h.Where(m => !m.Parcial).ToList();
        if (completos.Count < 2) return null;
        var (u, p) = (completos[^1], completos[^2]);
        return $"Tendência recente: no último mês completo ({CalculosRelatorio.RotuloMes(u.Chave).ToLower(Fmt.Br)}), as entradas somaram {Fmt.Num(u.Entradas)}, " +
               $"{Fmt.VarPct(u.Entradas, p.Entradas)} em relação a {CalculosRelatorio.RotuloMes(p.Chave).ToLower(Fmt.Br)} ({Fmt.Num(p.Entradas)}).";
    }

    private static BlocoRelatorio ProjecaoBloco(ResultadoProjecao p)
    {
        var itens = new List<ItemRotulado> { new("Método", p.Metodo + ", testada contra o que aconteceu em cada mês do histórico.") };
        if (p.MesesTestados.Count > 0)
            itens.Add(new("Teste", string.Join("; ", p.MesesTestados.Select(t => $"{t.Rotulo}: estimado {Fmt.Num(t.Estimado)}, realizado {Fmt.Num(t.Realizado)}")) + "."));
        if (p.Disponivel)
            itens.Add(new("Estimativa indicativa", $"{p.MesEstimado}: cerca de {Fmt.Num(p.Estimativa!.Value)} entradas (faixa de {Fmt.Num(p.Minimo!.Value)} a {Fmt.Num(p.Maximo!.Value)}). {p.Motivo} É uma estimativa, não um dado observado."));
        else
            itens.Add(new("Resultado", $"Sem projeção para o próximo mês. {p.Motivo} Com menos de um ano de dados, o efeito da safra ainda não pode ser separado da tendência."));
        return new BlocoQuadro("Projeção de volume", itens, p.Disponivel ? "informativo" : "limitacao");
    }

    private static List<int> IndicesPico(IReadOnlyList<MesHistorico> h)
    {
        var completos = h.Select((m, i) => (m, i)).Where(x => !x.m.Parcial).ToList();
        if (completos.Count == 0) return [];
        return [completos.OrderByDescending(x => x.m.Entradas).First().i];
    }

    private static string RotuloAgrupamento(string agrupamento) => agrupamento switch { "semana" => "semana", "mes" => "mês", _ => "dia" };

    private static string RotuloPonto(PontoSerie p, string agrupamento) => agrupamento switch
    {
        "mes" => CalculosRelatorio.RotuloMesCurto(p.Chave) + (p.Parcial ? "*" : ""),
        "semana" => Fmt.DataCurta(p.De) + (p.Parcial ? "*" : ""),
        _ => Fmt.DataCurta(p.De)
    };

    private static string? Sentido(double? atual, double? anterior, bool menorEhMelhor)
    {
        if (atual is null || anterior is null || Math.Abs(atual.Value - anterior.Value) < 1e-9) return anterior is null ? null : "neutro";
        var melhorou = menorEhMelhor ? atual < anterior : atual > anterior;
        return melhorou ? "melhor" : "pior";
    }

    private static string Lista(IReadOnlyList<string> itens) => itens.Count switch
    {
        0 => "",
        1 => itens[0],
        _ => string.Join(", ", itens.Take(itens.Count - 1)) + " e " + itens[^1]
    };

    private static string Primeira(string s) => s.Length == 0 ? s : char.ToUpper(s[0], Fmt.Br) + s[1..];
}
