using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Controllers;

/// <summary>Agendamentos de descarga (persistidos — antes era simulação em memória).</summary>
[ApiController]
[Route("api/agendamentos")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class AgendamentosController(AgendamentoServico servico) : ControllerBase
{
    /// <summary>Lista agendamentos (filtros: data, terminal, status, transportadora, placa).</summary>
    [HttpGet]
    public Task<List<AgendamentoDto>> Listar([FromQuery] FiltroAgendamentos filtro, CancellationToken ct) => servico.ListarAsync(filtro, ct);

    /// <summary>Resumo do dia: totais, confirmados, em operação, atrasados e concentração por horário.</summary>
    [HttpGet("resumo")]
    public Task<ResumoAgendamentosDto> Resumo([FromQuery] DateOnly? data, [FromQuery] string? terminalId, CancellationToken ct) =>
        servico.ResumoAsync(data, terminalId, ct);

    /// <summary>Um agendamento (o QR codifica o campo "codigo").</summary>
    [HttpGet("{id:int}")]
    public Task<AgendamentoDto> Obter(int id, CancellationToken ct) => servico.ObterAsync(id, ct);

    /// <summary>Cria agendamento. Carreta negativada (N3) é recusada.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.Escrita)]
    [ProducesResponseType<AgendamentoDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AgendamentoDto>> Criar(CriarAgendamentoRequest req, CancellationToken ct)
    {
        var a = await servico.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = a.Id }, a);
    }

    /// <summary>Atualiza o agendamento inteiro (inclusive status).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<AgendamentoDto> Atualizar(int id, AtualizarAgendamentoRequest req, CancellationToken ct) => servico.AtualizarAsync(id, req, ct);

    /// <summary>Atualiza só o status (dropdown da tela).</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<AgendamentoDto> AtualizarStatus(int id, AtualizarStatusAgendamentoRequest req, CancellationToken ct) => servico.AtualizarStatusAsync(id, req.Status, ct);
}

/// <summary>Portaria: fila virtual e estado atual da frota.</summary>
[ApiController]
[Route("api/portaria")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class PortariaController(PortariaServico servico) : ControllerBase
{
    /// <summary>
    /// Fila virtual (No Pátio/Aguardando, ordem de chegada) com estimativa pela
    /// espera média histórica real do terminal, alerta de congestionamento e
    /// previsão de gargalo das próximas 6h.
    /// </summary>
    [HttpGet("fila-virtual")]
    public Task<FilaVirtualDto> FilaVirtual(CancellationToken ct) => servico.FilaVirtualAsync(ct);

    /// <summary>Contagem da frota por status de portaria ("Operação agora").</summary>
    [HttpGet("operacao-agora")]
    public Task<OperacaoAgoraDto> OperacaoAgora(CancellationToken ct) => servico.OperacaoAgoraAsync(ct);
}

/// <summary>Integração sistema-a-sistema (autenticação por cabeçalho X-Api-Key, não JWT).</summary>
[ApiController]
[Route("api/integracao")]
[Authorize(Policy = Politicas.Integracao)]
[Produces("application/json")]
public class IntegracaoController(IntegracaoServico servico) : ControllerBase
{
    /// <summary>
    /// Recebe eventos de marcação/liberação (1 a 1000 por chamada). Eventos
    /// válidos são gravados; inválidos voltam em "erros" com o índice.
    /// "Marcacao" cria/atualiza o movimento; "Liberacao" fecha o movimento
    /// com dataLiberacao. Datas no horário local do porto, sem fuso.
    /// </summary>
    [HttpPost("eventos")]
    [ProducesResponseType<ResultadoIntegracaoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ResultadoIntegracaoDto>> Eventos(List<EventoIntegracaoDto> eventos, CancellationToken ct)
    {
        if (eventos.Count == 0 || eventos.Count > IntegracaoServico.MaximoEventosPorRequisicao)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Lote inválido",
                detail: $"Envie entre 1 e {IntegracaoServico.MaximoEventosPorRequisicao} eventos por requisição.");
        return await servico.ProcessarAsync(eventos, ct);
    }
}
