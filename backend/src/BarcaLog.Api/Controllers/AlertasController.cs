using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Domain.Enums;
using BarcaLog.Application.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Controllers;

/// <summary>
/// Alertas gerados por regras (histórico, operacional, previsão, dados), cada um
/// com evidência, limite, origem e histórico de tratamento. Consultar: papéis
/// internos. Tratar (assumir, registrar ação, resolver, descartar, reabrir): Operador ou Gestor.
/// </summary>
[ApiController]
[Route("api/v1/alertas")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class AlertasController(AlertasServico servico) : ControllerBase
{
    /// <summary>Lista (padrão: só os abertos — ainda ocorrendo e não resolvidos/descartados), prioridade alta primeiro.</summary>
    [HttpGet]
    public Task<Pagina<AlertaDto>> Listar([FromQuery] FiltroAlertas filtro, CancellationToken ct) => servico.ListarAsync(filtro, ct);

    /// <summary>Contagem de alertas abertos por tipo e horário da última avaliação.</summary>
    [HttpGet("resumo")]
    public Task<ResumoAlertasDto> Resumo(CancellationToken ct) => servico.ResumoAsync(ct);

    /// <summary>Próximas 6 horas: razão chegadas ÷ liberações por faixa, no padrão dos últimos 30 dias com dados. Estimativa.</summary>
    [HttpGet("proximas-horas")]
    public async Task<ActionResult<ProximasHorasDto>> ProximasHoras([FromQuery] string? terminalId, CancellationToken ct) =>
        await servico.ProximasHorasAsync(terminalId, ct) is { } r ? r : NoContent();

    /// <summary>Um alerta com evidência, limite, origem, ação recomendada e o histórico completo de tratamento.</summary>
    [HttpGet("{id:int}")]
    public Task<AlertaDto> Obter(int id, CancellationToken ct) => servico.ObterAsync(id, ct);

    /// <summary>
    /// Reavalia as regras agora (também roda sozinho a cada 10 min e após cada importação) e devolve o
    /// resultado registrado: dados novos, alertas criados/alterados/encerrados e confiabilidade antes e depois.
    /// </summary>
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
