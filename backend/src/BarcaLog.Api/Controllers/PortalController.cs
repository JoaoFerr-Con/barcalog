using BarcaLog.Api.Infra;
using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Controllers;

/// <summary>
/// Portal da Transportadora (usuários do papel Transportadora). Tudo aqui é
/// filtrado pela transportadora do TOKEN — filtros ou ids enviados pelo
/// cliente não ampliam o acesso. Registro de outra transportadora = 404.
/// </summary>
[ApiController]
[Route("api/v1/portal")]
[Authorize(Policy = Politicas.Portal)]
[Produces("application/json")]
public class PortalController(PortalServico portal) : ControllerBase
{
    private int TransportadoraId => int.Parse(User.FindFirst(ClaimsBarcaLog.Transportadora)!.Value);

    /// <summary>Dados e status calculado da própria transportadora (apta/negativada, carretas negativadas, reincidência N2).</summary>
    [HttpGet("resumo")]
    public Task<TransportadoraDto> Resumo(CancellationToken ct) => portal.ResumoAsync(TransportadoraId, ct);

    /// <summary>Ocorrências da própria transportadora.</summary>
    [HttpGet("ocorrencias")]
    public Task<Pagina<OcorrenciaDto>> Ocorrencias([FromQuery] FiltroOcorrencias filtro, CancellationToken ct) =>
        portal.OcorrenciasAsync(TransportadoraId, filtro, ct);

    /// <summary>Contestações (chamados) da própria transportadora.</summary>
    [HttpGet("contestacoes")]
    public Task<Pagina<ContestacaoDto>> Contestacoes([FromQuery] FiltroContestacoes filtro, CancellationToken ct) =>
        portal.ContestacoesAsync(TransportadoraId, filtro, ct);

    /// <summary>Abre contestação de uma ocorrência da própria transportadora.</summary>
    [HttpPost("contestacoes")]
    [Idempotente]
    [ProducesResponseType<ContestacaoDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ContestacaoDto>> AbrirContestacao(AbrirContestacaoPortalRequest req, CancellationToken ct)
    {
        var c = await portal.AbrirContestacaoAsync(TransportadoraId, req, ct);
        return StatusCode(StatusCodes.Status201Created, c);
    }

    /// <summary>Veículos da própria transportadora.</summary>
    [HttpGet("veiculos")]
    public Task<Pagina<VeiculoDto>> Veiculos([FromQuery] FiltroVeiculos filtro, CancellationToken ct) =>
        portal.VeiculosAsync(TransportadoraId, filtro, ct);

    /// <summary>Condutores da própria transportadora.</summary>
    [HttpGet("condutores")]
    public Task<Pagina<CondutorDto>> Condutores([FromQuery] FiltroCondutores filtro, CancellationToken ct) =>
        portal.CondutoresAsync(TransportadoraId, filtro, ct);
}
