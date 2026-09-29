using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Configuracao;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Metricas;
using BarcaLog.Domain.Enums;
using Microsoft.Extensions.Options;

namespace BarcaLog.Application.Servicos;

public class PortariaServico(
    IVeiculoRepositorio veiculos,
    MetricasServico metricas,
    RelogioOperacional relogio,
    IOptions<OperacaoOptions> opcoes)
{
    /// <summary>
    /// Fila virtual: carretas No Pátio/Aguardando em ordem de chegada no status,
    /// com estimativa baseada na espera média HISTÓRICA REAL do terminal
    /// (nada inventado — mesma lógica da tela de Portaria).
    /// </summary>
    public async Task<FilaVirtualDto> FilaVirtualAsync(CancellationToken ct = default)
    {
        var fila = await veiculos.ListarFilaAsync(ct);
        var porStatus = await veiculos.ContarPorStatusPortariaAsync(ct);
        var contexto = await metricas.CarregarAsync(new FiltroMetricas(), ct);
        var esperaMediaPorTerminal = contexto.Registros
            .GroupBy(r => r.TerminalId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.EsperaHoras) / g.Count());

        var agora = relogio.AgoraUtc;
        var itens = fila.Select((v, i) =>
        {
            var decorrido = Math.Max(0, JsMath.Round((agora - v.StatusPortariaDesde).TotalMinutes));
            double? media = esperaMediaPorTerminal.TryGetValue(v.TerminalId, out var m) ? m : null;
            int? restante = media is null ? null : Math.Max(0, JsMath.Round(media.Value * 60 - decorrido));
            return new ItemFilaVirtualDto(
                i + 1, v.Id, v.Placa, v.Transportadora?.Nome, v.StatusPortaria, v.TerminalId, v.Terminal?.Nome,
                v.StatusPortariaDesde, decorrido, media, restante, restante == 0);
        }).ToList();

        var noPatio = porStatus.GetValueOrDefault(StatusPortaria.NoPatio);
        var limiar = opcoes.Value.LimiarCongestionamento;
        return new FilaVirtualDto(
            noPatio,
            limiar,
            noPatio >= limiar,
            itens,
            MotorMetricas.PrevisaoGargaloPortaria(contexto.Registros, relogio.AgoraLocalPorto.Hour));
    }

    /// <summary>Painel "Operação agora" — contagem da frota cadastrada por status de portaria.</summary>
    public async Task<OperacaoAgoraDto> OperacaoAgoraAsync(CancellationToken ct = default)
    {
        var c = await veiculos.ContarPorStatusPortariaAsync(ct);
        return new OperacaoAgoraDto(
            c.GetValueOrDefault(StatusPortaria.NoPatio),
            c.GetValueOrDefault(StatusPortaria.NoPorto),
            c.GetValueOrDefault(StatusPortaria.Aguardando),
            c.GetValueOrDefault(StatusPortaria.DescargaFinalizada),
            c.Values.Sum());
    }
}
