using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Enums;
using BarcaLog.Application.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Controllers;


[ApiController]
[Route("api/v1/alertas")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class AlertasController(AlertasServico servico) : ControllerBase
{
    
    [HttpGet]
    public Task<Pagina<AlertaDto>> Listar([FromQuery] FiltroAlertas filtro, CancellationToken ct) => servico.ListarAsync(filtro, ct);

    [HttpGet("resumo")]
    public Task<ResumoAlertasDto> Resumo(CancellationToken ct) => servico.ResumoAsync(ct);

    [HttpGet("proximas-horas")]
    public async Task<ActionResult<ProximasHorasDto>> ProximasHoras([FromQuery] string? terminalId, CancellationToken ct) =>
        await servico.ProximasHorasAsync(terminalId, ct) is { } r ? r : NoContent();

    /// <summary>Um alerta com evidência, limite, origem, ação recomendada e o histórico completo de tratamento.</summary>
    [HttpGet("{id:int}")]
    public Task<AlertaDto> Obter(int id, CancellationToken ct) => servico.ObterAsync(id, ct);

    [HttpPost("avaliar")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<ResultadoAvaliacaoDto> Avaliar(CancellationToken ct) => servico.AvaliarAsync(OrigemAvaliacao.Manual, ct);

    /// <summary>Histórico das avaliações (automáticas, manuais e após importação), mais recentes primeiro.</summary>
    [HttpGet("avaliacoes")]
    public Task<Pagina<ResultadoAvaliacaoDto>> Avaliacoes([FromQuery] FiltroAvaliacoes filtro, CancellationToken ct) => servico.ListarAvaliacoesAsync(filtro, ct);

    /// <summary>Assume o alerta (estado Em análise, responsável = usuário atual). Texto opcional.</summary>
    [HttpPost("{id:int}/assumir")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<AlertaDto> Assumir(int id, TextoAlertaRequest? req, CancellationToken ct) => servico.AssumirAsync(id, req?.Texto, ct);

    /// <summary>Registra uma ação tomada (texto obrigatório).</summary>
    [HttpPost("{id:int}/acoes")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<AlertaDto> Comentar(int id, TextoAlertaRequest req, CancellationToken ct) => servico.ComentarAsync(id, req.Texto, ct);

    /// <summary>Encerra como resolvido (texto obrigatório: o que foi feito).</summary>
    [HttpPost("{id:int}/resolver")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<AlertaDto> Resolver(int id, TextoAlertaRequest req, CancellationToken ct) => servico.ResolverAsync(id, req.Texto, ct);

    /// <summary>Encerra como descartado (justificativa obrigatória).</summary>
    [HttpPost("{id:int}/descartar")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<AlertaDto> Descartar(int id, TextoAlertaRequest req, CancellationToken ct) => servico.DescartarAsync(id, req.Texto, ct);

    /// <summary>Reabre um alerta resolvido ou descartado (volta a Novo, sem responsável).</summary>
    [HttpPost("{id:int}/reabrir")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<AlertaDto> Reabrir(int id, TextoAlertaRequest? req, CancellationToken ct) => servico.ReabrirAsync(id, req?.Texto, ct);
}
