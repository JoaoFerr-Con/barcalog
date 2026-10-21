using System.Globalization;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using BarcaLog.Domain.Entidades;

namespace BarcaLog.Application.Servicos;

/// <summary>
/// Indicadores do painel. Regras (documentadas em docs/INDICADORES.md):
/// - período = datas de MARCAÇÃO, horário local do porto, inclusive nas pontas;
/// - espera = liberação − marcação (só movimentos liberados);
/// - mediana e P90 por interpolação linear;
/// - média diária = movimentações ÷ dias corridos do período;
/// - ocupação só existe com capacidade informada pelo porto.
/// </summary>
public class IndicadoresServico(
    MetricasServico metricas,
    IMarcacaoRepositorio marcacoes,
    ITerminalRepositorio terminais,
    IImportacaoRepositorio importacoes,
    RelogioOperacional relogio,
    IEscopoUsuario escopo)
{
    private sealed record Recorte(PeriodoDto? Periodo, IReadOnlyList<RegistroMarcacao> Registros, IReadOnlyDictionary<string, int> SemLiberacao);

    private async Task<Recorte> RecortarAsync(IReadOnlyList<RegistroMarcacao> todos, string? terminalId, DateOnly? de, DateOnly? ate, CancellationToken ct)
    {
        IEnumerable<RegistroMarcacao> q = todos;
        if (!string.IsNullOrWhiteSpace(terminalId)) q = q.Where(r => r.TerminalId == terminalId);
        DateTime? inicio = de?.ToDateTime(TimeOnly.MinValue), fim = ate?.AddDays(1).ToDateTime(TimeOnly.MinValue);
        if (inicio is { } i) q = q.Where(r => r.MarcadoEm >= i);
        if (fim is { } f) q = q.Where(r => r.MarcadoEm < f);
        var lista = q.ToList();
        var abertas = await marcacoes.ContarSemLiberacaoAsync(inicio, fim, ct);
        if (!string.IsNullOrWhiteSpace(terminalId)) abertas = abertas.Where(a => a.Key == terminalId).ToDictionary(a => a.Key, a => a.Value);

        PeriodoDto? periodo = null;
        if (de is { } d && ate is { } a) periodo = new PeriodoDto(d, a, Indicadores.DiasCorridos(d, a));
        else if (lista.Count > 0)
        {
            var d0 = de ?? DateOnly.FromDateTime(lista.Min(r => r.MarcadoEm));
            var a0 = ate ?? DateOnly.FromDateTime(lista.Max(r => r.MarcadoEm));
            periodo = new PeriodoDto(d0, a0, Indicadores.DiasCorridos(d0, a0));
        }
        return new Recorte(periodo, lista, abertas);
    }

    private static EstatisticasEspera Estatisticas(Recorte r, double limiteLonga, string? terminalId = null)
    {
        var registros = terminalId is null ? r.Registros : r.Registros.Where(x => x.TerminalId == terminalId).ToList();
        var abertas = terminalId is null ? r.SemLiberacao.Values.Sum() : r.SemLiberacao.GetValueOrDefault(terminalId);
        return Indicadores.Calcular(registros, abertas, r.Periodo?.Dias ?? 0, limiteLonga);
    }

    public async Task<ResumoIndicadoresDto> ResumoAsync(FiltroIndicadores f, CancellationToken ct = default)
    {
        var parametros = await terminais.ObterParametrosAsync(false, ct);
        var todos = await metricas.TodosLiberadosAsync(ct);
        var atual = await RecortarAsync(todos, f.TerminalId, f.De, f.Ate, ct);
        Recorte? anterior = f.CompararDe is { } cd && f.CompararAte is { } ca ? await RecortarAsync(todos, f.TerminalId, cd, ca, ct) : null;
        return new ResumoIndicadoresDto(atual.Periodo, anterior?.Periodo,
            Estatisticas(atual, parametros.LimiteEsperaLongaHoras),
            anterior is null ? null : Estatisticas(anterior, parametros.LimiteEsperaLongaHoras),
            parametros.LimiteEsperaLongaHoras);
    }

    public async Task<List<IndicadorTerminalDto>> PorTerminalAsync(FiltroIndicadores f, CancellationToken ct = default)
    {
        var parametros = await terminais.ObterParametrosAsync(false, ct);
        var todos = await metricas.TodosLiberadosAsync(ct);
        var atual = await RecortarAsync(todos, f.TerminalId, f.De, f.Ate, ct);
        Recorte? anterior = f.CompararDe is { } cd && f.CompararAte is { } ca ? await RecortarAsync(todos, f.TerminalId, cd, ca, ct) : null;
        var ultimos = await marcacoes.UltimosEventosPorTerminalAsync(ct);
        var lista = new List<IndicadorTerminalDto>();
        foreach (var t in await terminais.ListarAsync(ct))
        {
            if (!string.IsNullOrWhiteSpace(f.TerminalId) && t.Id != f.TerminalId) continue;
            var e = Estatisticas(atual, parametros.LimiteEsperaLongaHoras, t.Id);
            var pico = atual.Registros.Where(r => r.TerminalId == t.Id).GroupBy(r => DateOnly.FromDateTime(r.MarcadoEm))
                .Select(g => (Dia: g.Key, Total: g.Count())).OrderByDescending(x => x.Total).ThenBy(x => x.Dia).FirstOrDefault();
            double? ocupacao = t.CapacidadeDiariaCarretas is { } cap && e.MediaDiaria is { } md ? 100 * md / cap : null;
            lista.Add(new IndicadorTerminalDto(t.Id, t.Nome, e,
                anterior is null ? null : Estatisticas(anterior, parametros.LimiteEsperaLongaHoras, t.Id),
                pico.Total == 0 ? null : pico.Total, pico.Total == 0 ? null : pico.Dia,
                t.CapacidadeDiariaCarretas, ocupacao,
                ultimos.TryGetValue(t.Id, out var u) ? u.UltimaMarcacao : null));
        }
        return lista;
    }

    public async Task<List<VolumeMensalDto>> VolumeMensalAsync(FiltroIndicadores f, CancellationToken ct = default)
    {
        var todos = await metricas.TodosLiberadosAsync(ct);
        var atual = await RecortarAsync(todos, f.TerminalId, f.De, f.Ate, ct);
        var nomes = (await terminais.ListarAsync(ct)).ToDictionary(t => t.Id, t => t.Nome);
        // Cobertura de cada terminal = primeira e última marcação já recebidas (qualquer período).
        var cobertura = todos.GroupBy(r => r.TerminalId).ToDictionary(g => g.Key, g => (Ini: g.Min(r => r.MarcadoEm), Fim: g.Max(r => r.MarcadoEm)));
        var ultimos = await marcacoes.UltimosEventosPorTerminalAsync(ct);
        return atual.Registros
            .GroupBy(r => (Mes: r.MarcadoEm.ToString("yyyy-MM", CultureInfo.InvariantCulture), r.TerminalId))
            .OrderBy(g => g.Key.Mes).ThenBy(g => nomes.GetValueOrDefault(g.Key.TerminalId))
            .Select(g =>
            {
                var esperas = g.Select(r => r.EsperaHoras).OrderBy(x => x).ToList();
                var inicioMes = DateTime.ParseExact(g.Key.Mes + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var fimMes = inicioMes.AddMonths(1);
                var (ini, fim) = cobertura[g.Key.TerminalId];
                if (ultimos.TryGetValue(g.Key.TerminalId, out var u) && u.UltimaMarcacao > fim) fim = u.UltimaMarcacao.Value;
                // Parcial: o terminal começou depois do 1º dia ou parou antes do último dia do mês.
                var parcial = ini.Date > inicioMes || fim.Date < fimMes.AddDays(-1);
                return new VolumeMensalDto(g.Key.Mes, g.Key.TerminalId, nomes.GetValueOrDefault(g.Key.TerminalId, g.Key.TerminalId),
                    g.Count(), Indicadores.Percentil(esperas, 0.5), parcial);
            })
            .ToList();
    }

    public async Task<List<FluxoHorarioDto>> FluxoHorarioAsync(FiltroIndicadores f, CancellationToken ct = default)
    {
        var todos = await metricas.TodosLiberadosAsync(ct);
        IEnumerable<RegistroMarcacao> q = todos;
        if (!string.IsNullOrWhiteSpace(f.TerminalId)) q = q.Where(r => r.TerminalId == f.TerminalId);
        var lista = q.ToList();
        if (lista.Count == 0) return [];
        var de = f.De ?? DateOnly.FromDateTime(lista.Min(r => r.MarcadoEm));
        var ate = f.Ate ?? DateOnly.FromDateTime(lista.Max(r => r.MarcadoEm));
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var dias = (double)Indicadores.DiasCorridos(de, ate);
        // Chegadas: marcações do período. Liberações: liberações que ACONTECERAM no período.
        var chegadas = lista.Where(r => r.MarcadoEm >= inicio && r.MarcadoEm < fim).GroupBy(r => r.MarcadoEm.Hour).ToDictionary(g => g.Key, g => g.Count());
        var liberacoes = lista.Where(r => r.LiberadoEm >= inicio && r.LiberadoEm < fim).GroupBy(r => r.LiberadoEm.Hour).ToDictionary(g => g.Key, g => g.Count());
        return Enumerable.Range(0, 24).Select(h =>
        {
            var c = chegadas.GetValueOrDefault(h) / dias;
            var l = liberacoes.GetValueOrDefault(h) / dias;
            return new FluxoHorarioDto(h, Math.Round(c, 2), Math.Round(l, 2), l > 0 ? Math.Round(c / l, 2) : null);
        }).ToList();
    }

    public async Task<List<FrescorTerminalDto>> FrescorAsync(CancellationToken ct = default)
    {
        var parametros = await terminais.ObterParametrosAsync(false, ct);
        var ultimos = await marcacoes.UltimosEventosPorTerminalAsync(ct);
        var importacoesPorTerminal = await importacoes.UltimaConfirmadaPorTerminalAsync(ct);
        var hoje = relogio.HojeLocalPorto;
        return (await terminais.ListarAsync(ct)).Where(t => escopo.PodeVerTerminal(t.Id)).Select(t =>
        {
            var u = ultimos.TryGetValue(t.Id, out var x) ? x : (null, null);
            int? dias = u.UltimaMarcacao is { } um ? hoje.DayNumber - DateOnly.FromDateTime(um).DayNumber : null;
            var imp = importacoesPorTerminal.TryGetValue(t.Id, out var i) ? i : ((DateTime, string)?)null;
            return new FrescorTerminalDto(t.Id, t.Nome, u.UltimaMarcacao, u.UltimaLiberacao, imp?.Item1, imp?.Item2, dias,
                dias is null || dias > parametros.DiasSemDadosAlerta);
        }).ToList();
    }
}

/// <summary>Capacidades dos terminais e limites dos alertas. Alteração: só Gestor, auditada.</summary>
public class ConfiguracaoServico(ITerminalRepositorio terminais, IUnitOfWork uow, IContextoAuditoria auditoria, IUsuarioAtual usuario, RelogioOperacional relogio)
{
    public async Task<ConfiguracaoOperacionalDto> ObterAsync(CancellationToken ct = default) =>
        new((await terminais.ListarAsync(ct)).Select(t => t.ParaDto()).ToList(), ParaDto(await terminais.ObterParametrosAsync(false, ct)));

    public async Task<TerminalDto> AtualizarCapacidadeAsync(string terminalId, AtualizarCapacidadeTerminalRequest req, CancellationToken ct = default)
    {
        var t = await terminais.ObterParaAlterarAsync(terminalId, ct) ?? throw new Domain.Excecoes.NaoEncontradoException($"Terminal '{terminalId}' não existe.");
        t.CapacidadeDiariaCarretas = req.CapacidadeDiariaCarretas;
        t.CapacidadeHoraria = req.CapacidadeHoraria;
        auditoria.DefinirAcao("Capacidade do terminal alterada",
            $"{t.Nome}: {Texto(req.CapacidadeDiariaCarretas)} carretas/dia, {Texto(req.CapacidadeHoraria)} por hora");
        await uow.SalvarAsync(ct);
        return t.ParaDto();
    }

    public async Task<ParametrosOperacionaisDto> AtualizarParametrosAsync(AtualizarParametrosRequest req, CancellationToken ct = default)
    {
        var p = await terminais.ObterParametrosAsync(true, ct);
        p.LimiteEsperaMedianaHoras = req.LimiteEsperaMedianaHoras;
        p.MetaLiberadas24hPct = req.MetaLiberadas24hPct;
        p.LimiteEsperaLongaHoras = req.LimiteEsperaLongaHoras;
        p.DiasSemDadosAlerta = req.DiasSemDadosAlerta;
        p.LimiteRazaoChegadaLiberacao = req.LimiteRazaoChegadaLiberacao;
        p.AtualizadoEm = relogio.AgoraUtc;
        p.AtualizadoPor = usuario.Autor;
        auditoria.DefinirAcao("Parâmetros operacionais alterados");
        await uow.SalvarAsync(ct);
        return ParaDto(p);
    }

    private static string Texto(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "não informada";

    public static ParametrosOperacionaisDto ParaDto(ParametrosOperacionais p) => new(
        p.LimiteEsperaMedianaHoras, p.MetaLiberadas24hPct, p.LimiteEsperaLongaHoras, p.DiasSemDadosAlerta, p.LimiteRazaoChegadaLiberacao, p.AtualizadoEm, p.AtualizadoPor);
}
