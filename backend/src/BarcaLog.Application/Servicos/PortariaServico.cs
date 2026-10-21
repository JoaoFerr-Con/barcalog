using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Configuracao;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using BarcaLog.Domain.Enums;
using Microsoft.Extensions.Options;

namespace BarcaLog.Application.Servicos;

public class PortariaServico(
    IVeiculoRepositorio veiculos,
    ITerminalRepositorio terminais,
    MetricasServico metricas,
    RelogioOperacional relogio,
    IOptions<OperacaoOptions> opcoes)
{
    /// <summary>Base do tempo típico: últimos N dias com dados de cada terminal (mesma base da estimativa das próximas horas).</summary>
    public const int DiasBaseTempoTipico = EstimativaJanelas.DiasBase;

    /// <summary>
    /// Fila virtual: carretas No pátio/Aguardando em ordem de entrada na situação.
    /// A estimativa usa o tempo TÍPICO (mediana) de espera do terminal — marcação →
    /// liberação nos últimos 30 dias com dados —, não a média de todo o histórico,
    /// que é puxada por esperas muito longas. É uma referência do terminal, não uma
    /// medição da portaria: a espera na portaria não é registrada separadamente.
    /// </summary>
    public async Task<FilaVirtualDto> FilaVirtualAsync(CancellationToken ct = default)
    {
        var fila = await veiculos.ListarFilaAsync(ct);
        var porStatus = await veiculos.ContarPorStatusPortariaAsync(ct);
        var tipicos = TemposTipicos(await metricas.TodosLiberadosAsync(ct), await terminais.ListarAsync(ct));
        var porId = tipicos.ToDictionary(t => t.TerminalId);

        var agora = relogio.AgoraUtc;
        var itens = fila.Select((v, i) =>
        {
            var decorrido = Math.Max(0, (int)Math.Round((agora - v.StatusPortariaDesde).TotalMinutes, MidpointRounding.AwayFromZero));
            var tipico = porId.TryGetValue(v.TerminalId, out var t) ? t.MedianaHoras : null;
            int? restante = tipico is null ? null : Math.Max(0, (int)Math.Round(tipico.Value * 60 - decorrido, MidpointRounding.AwayFromZero));
            return new ItemFilaVirtualDto(
                i + 1, v.Id, v.Placa, v.Transportadora?.Nome, v.StatusPortaria, v.TerminalId, v.Terminal?.Nome,
                v.StatusPortariaDesde, decorrido, tipico is null ? null : Math.Round(tipico.Value, 2), restante, restante == 0);
        }).ToList();

        var noPatio = porStatus.GetValueOrDefault(StatusPortaria.NoPatio);
        var limiar = opcoes.Value.LimiarCongestionamento;
        return new FilaVirtualDto(noPatio, limiar, noPatio >= limiar, itens, tipicos);
    }

    /// <summary>Mediana da espera por terminal nos últimos <see cref="DiasBaseTempoTipico"/> dias com dados.</summary>
    public static List<TempoTipicoTerminalDto> TemposTipicos(IReadOnlyList<RegistroMarcacao> liberados, IEnumerable<Domain.Entidades.Terminal> terminais)
    {
        var porId = liberados.GroupBy(r => r.TerminalId).ToDictionary(g => g.Key, g => g.ToList());
        return terminais.Select(t =>
        {
            if (!porId.TryGetValue(t.Id, out var regs) || regs.Count == 0)
                return new TempoTipicoTerminalDto(t.Id, t.Nome, null, null, null, 0);
            var fim = regs.Max(r => r.MarcadoEm).Date.AddDays(1);
            var inicio = fim.AddDays(-DiasBaseTempoTipico);
            var esperas = regs.Where(r => r.MarcadoEm >= inicio && r.MarcadoEm < fim).Select(r => r.EsperaHoras).OrderBy(x => x).ToList();
            return new TempoTipicoTerminalDto(t.Id, t.Nome, Indicadores.Percentil(esperas, 0.5), DateOnly.FromDateTime(inicio), DateOnly.FromDateTime(fim.AddDays(-1)), esperas.Count);
        }).ToList();
    }

    /// <summary>
    /// Painel "Situação agora" — contagem da frota cadastrada por situação de portaria.
    /// O campo EmOperacao conta a situação "No porto" (dentro do terminal); ForaDoPorto não entra no total.
    /// </summary>
    public async Task<OperacaoAgoraDto> OperacaoAgoraAsync(CancellationToken ct = default)
    {
        var c = await veiculos.ContarPorStatusPortariaAsync(ct);
        return new OperacaoAgoraDto(
            c.GetValueOrDefault(StatusPortaria.NoPatio),
            c.GetValueOrDefault(StatusPortaria.NoPorto),
            c.GetValueOrDefault(StatusPortaria.Aguardando),
            c.GetValueOrDefault(StatusPortaria.DescargaFinalizada),
            c.Where(x => x.Key != StatusPortaria.ForaDoPorto).Sum(x => x.Value));
    }
}
