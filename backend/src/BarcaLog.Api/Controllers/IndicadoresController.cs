using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Controllers;

/// <summary>
/// Indicadores do painel (Visão Geral). Período = datas de marcação no horário
/// do porto (de/ate, inclusive); comparação opcional (compararDe/compararAte).
/// Regras de cálculo: docs/INDICADORES.md.
/// </summary>
[ApiController]
[Route("api/v1/indicadores")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class IndicadoresController(IndicadoresServico servico) : ControllerBase
{
    /// <summary>Movimentações, média diária, espera mediana/P90/média, % liberadas em até 24 h e esperas longas — do período e da comparação.</summary>
    [HttpGet("resumo")]
    public Task<ResumoIndicadoresDto> Resumo([FromQuery] FiltroIndicadores f, CancellationToken ct) => servico.ResumoAsync(f, ct);

    /// <summary>Os mesmos indicadores por terminal, com pico diário, capacidade e ocupação (só com capacidade configurada).</summary>
    [HttpGet("terminais")]
    public Task<List<IndicadorTerminalDto>> Terminais([FromQuery] FiltroIndicadores f, CancellationToken ct) => servico.PorTerminalAsync(f, ct);

    /// <summary>Análise de Volume — Histórico: movimentações e espera mediana por mês e terminal (meses parciais marcados).</summary>
    [HttpGet("volume-mensal")]
    public Task<List<VolumeMensalDto>> VolumeMensal([FromQuery] FiltroIndicadores f, CancellationToken ct) => servico.VolumeMensalAsync(f, ct);

    /// <summary>Média por dia de marcações e de liberações em cada hora do dia, no período.</summary>
    [HttpGet("fluxo-horario")]
    public Task<List<FluxoHorarioDto>> FluxoHorario([FromQuery] FiltroIndicadores f, CancellationToken ct) => servico.FluxoHorarioAsync(f, ct);

    /// <summary>Até quando cada terminal tem dados (última marcação/liberação e última importação).</summary>
    [HttpGet("frescor")]
    public Task<List<FrescorTerminalDto>> Frescor(CancellationToken ct) => servico.FrescorAsync(ct);
}

/// <summary>Capacidade dos terminais e limites dos alertas. Consultar: papéis internos. Alterar: Gestor.</summary>
[ApiController]
[Route("api/v1/configuracoes")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class ConfiguracoesController(ConfiguracaoServico servico) : ControllerBase
{
    [HttpGet]
    public Task<ConfiguracaoOperacionalDto> Obter(CancellationToken ct) => servico.ObterAsync(ct);

    /// <summary>Capacidade diária e por hora do terminal. Campos vazios = não informada.</summary>
    [HttpPut("terminais/{terminalId:maxlength(32)}")]
    [Authorize(Policy = Politicas.Gestao)]
    public Task<TerminalDto> AtualizarTerminal(string terminalId, AtualizarCapacidadeTerminalRequest req, CancellationToken ct) =>
        servico.AtualizarCapacidadeAsync(terminalId, req, ct);

    [HttpPut("parametros")]
    [Authorize(Policy = Politicas.Gestao)]
    public Task<ParametrosOperacionaisDto> AtualizarParametros(AtualizarParametrosRequest req, CancellationToken ct) =>
        servico.AtualizarParametrosAsync(req, ct);
}
