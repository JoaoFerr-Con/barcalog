using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Controllers;

/// <summary>Ocorrências N1/N2/N3. Registrar N3 bloqueia automaticamente a carreta.</summary>
[ApiController]
[Route("api/ocorrencias")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class OcorrenciasController(OcorrenciaServico servico) : ControllerBase
{
    /// <summary>Lista ocorrências (mais recentes primeiro).</summary>
    [HttpGet]
    public Task<List<OcorrenciaDto>> Listar([FromQuery] FiltroOcorrencias filtro, CancellationToken ct) => servico.ListarAsync(filtro, ct);

    /// <summary>Uma ocorrência.</summary>
    [HttpGet("{id:int}")]
    public Task<OcorrenciaDto> Obter(int id, CancellationToken ct) => servico.ObterAsync(id, ct);

    /// <summary>
    /// Registra ocorrência. N3 → veículo da placa (e condutor, se informado)
    /// passa a Negativada na mesma transação. N1/N2 não bloqueiam.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Politicas.Escrita)]
    [ProducesResponseType<OcorrenciaRegistradaDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OcorrenciaRegistradaDto>> Registrar(RegistrarOcorrenciaRequest req, CancellationToken ct)
    {
        var r = await servico.RegistrarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = r.Ocorrencia.Id }, r);
    }
}

/// <summary>Contestações (GED). Aprovar regulariza a carreta daquela ocorrência.</summary>
[ApiController]
[Route("api/contestacoes")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class ContestacoesController(ContestacaoServico servico) : ControllerBase
{
    /// <summary>Lista contestações.</summary>
    [HttpGet]
    public Task<List<ContestacaoDto>> Listar([FromQuery] FiltroContestacoes filtro, CancellationToken ct) => servico.ListarAsync(filtro, ct);

    /// <summary>Uma contestação.</summary>
    [HttpGet("{id:int}")]
    public Task<ContestacaoDto> Obter(int id, CancellationToken ct) => servico.ObterAsync(id, ct);

    /// <summary>Abre contestação para uma ocorrência (ocorrência passa a "Contestada").</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.Escrita)]
    [ProducesResponseType<ContestacaoDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ContestacaoDto>> Abrir(AbrirContestacaoRequest req, CancellationToken ct)
    {
        var c = await servico.AbrirAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = c.Id }, c);
    }

    /// <summary>Aprova: ocorrência resolvida e carreta (e condutor) daquela ocorrência volta a Regular.</summary>
    [HttpPost("{id:int}/aprovar")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<ContestacaoDto> Aprovar(int id, ResponderContestacaoRequest req, CancellationToken ct) => servico.AprovarAsync(id, req.RespostaOperador, ct);

    /// <summary>Rejeita: ocorrência volta a "Ativa", bloqueio mantido.</summary>
    [HttpPost("{id:int}/rejeitar")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<ContestacaoDto> Rejeitar(int id, ResponderContestacaoRequest req, CancellationToken ct) => servico.RejeitarAsync(id, req.RespostaOperador, ct);
}

/// <summary>Log de auditoria (gerado automaticamente em toda mutação).</summary>
[ApiController]
[Route("api/auditoria")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class AuditoriaController(AuditoriaServico servico) : ControllerBase
{
    /// <summary>Busca por autor, ação, texto livre e período; paginado, mais recentes primeiro.</summary>
    [HttpGet]
    public Task<Pagina<LogAuditoriaDto>> Buscar([FromQuery] FiltroAuditoria filtro, CancellationToken ct) => servico.BuscarAsync(filtro, ct);
}
