using BarcaLog.Api.Infra;
using BarcaLog.Api.Seguranca;
using Microsoft.AspNetCore.RateLimiting;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Controllers;

/// <summary>Transportadoras. O status de negativação é sempre calculado a partir da frota.</summary>
[ApiController]
[Route("api/v1/transportadoras")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class TransportadorasController(TransportadoraServico servico) : ControllerBase
{
    /// <summary>Lista transportadoras com status calculado (negativada se ≥1 carreta negativada), contagem de carretas e reincidências N2 (30 dias).</summary>
    [HttpGet]
    public Task<List<TransportadoraDto>> Listar(CancellationToken ct) => servico.ListarAsync(ct);

    /// <summary>Uma transportadora.</summary>
    [HttpGet("{id:int}")]
    public Task<TransportadoraDto> Obter(int id, CancellationToken ct) => servico.ObterAsync(id, ct);

    /// <summary>Cadastra transportadora (CNPJ numérico ou alfanumérico, com DV).</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.Escrita)]
    [Idempotente]
    [ProducesResponseType<TransportadoraDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TransportadoraDto>> Criar(SalvarTransportadoraRequest req, CancellationToken ct)
    {
        var t = await servico.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = t.Id }, t);
    }

    /// <summary>Atualiza nome/CNPJ.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<TransportadoraDto> Atualizar(int id, SalvarTransportadoraRequest req, CancellationToken ct) => servico.AtualizarAsync(id, req, ct);

    /// <summary>Remove (somente sem veículos/condutores/ocorrências/agendamentos vinculados). Somente Gestor.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Politicas.Gestao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        await servico.RemoverAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Frota (carretas). É o veículo que é negativado.</summary>
[ApiController]
[Route("api/v1/veiculos")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class VeiculosController(VeiculoServico servico) : ControllerBase
{
    /// <summary>Lista veículos (filtros: transportadora, terminal, status de portaria/negativação, placa).</summary>
    [HttpGet]
    public Task<Pagina<VeiculoDto>> Listar([FromQuery] FiltroVeiculos filtro, CancellationToken ct) => servico.ListarAsync(filtro, ct);

    /// <summary>Um veículo.</summary>
    [HttpGet("{id:int}")]
    public Task<VeiculoDto> Obter(int id, CancellationToken ct) => servico.ObterAsync(id, ct);

    /// <summary>Busca exata por placa (consulta da Portaria / leitura do QR).</summary>
    [HttpGet("placa/{placa}")]
    [ProducesResponseType<VeiculoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorPlaca([PlacaValida] string placa, CancellationToken ct) =>
        await servico.BuscarPorPlacaAsync(placa, ct) is { } v ? Ok(v) : NotFound();

    /// <summary>Cadastra veículo (entra como Aguardando e Regular). Placa ABC-1234 ou Mercosul ABC1D23.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.Escrita)]
    [Idempotente]
    [ProducesResponseType<VeiculoDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<VeiculoDto>> Criar(CriarVeiculoRequest req, CancellationToken ct)
    {
        var v = await servico.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = v.Id }, v);
    }

    /// <summary>Atualiza placa, transportadora, modelo e terminal.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<VeiculoDto> Atualizar(int id, AtualizarVeiculoRequest req, CancellationToken ct) => servico.AtualizarAsync(id, req, ct);

    /// <summary>Remove veículo. Somente Gestor.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Politicas.Gestao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        await servico.RemoverAsync(id, ct);
        return NoContent();
    }

    /// <summary>Muda a posição física (NoPatio, Aguardando, NoPorto, DescargaFinalizada) e reinicia o "desde".</summary>
    [HttpPatch("{id:int}/status-portaria")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<VeiculoDto> AtualizarStatusPortaria(int id, AtualizarStatusPortariaRequest req, CancellationToken ct) =>
        servico.AtualizarStatusPortariaAsync(id, req.StatusPortaria, ct);

    /// <summary>Negativação manual: registra ocorrência N3 administrativa e bloqueia a carreta.</summary>
    [HttpPost("{id:int}/negativar")]
    [Authorize(Policy = Politicas.Escrita)]
    [Idempotente]
    public Task<VeiculoDto> Negativar(int id, NegativarVeiculoRequest req, CancellationToken ct) => servico.NegativarAsync(id, req.Motivo, ct);

    /// <summary>Desnegativação manual: regulariza a carreta e resolve as ocorrências em aberto dessa placa.</summary>
    [HttpPost("{id:int}/desnegativar")]
    [Authorize(Policy = Politicas.Escrita)]
    [Idempotente]
    public Task<VeiculoDto> Desnegativar(int id, CancellationToken ct) => servico.DesnegativarAsync(id, ct);
}

/// <summary>Condutores (motoristas). Sem CPF.</summary>
[ApiController]
[Route("api/v1/condutores")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class CondutoresController(CondutorServico servico) : ControllerBase
{
    /// <summary>Lista condutores (filtro opcional por transportadora).</summary>
    [HttpGet]
    public Task<Pagina<CondutorDto>> Listar([FromQuery] FiltroCondutores filtro, CancellationToken ct) => servico.ListarAsync(filtro, ct);

    /// <summary>Um condutor.</summary>
    [HttpGet("{id:int}")]
    public Task<CondutorDto> Obter(int id, CancellationToken ct) => servico.ObterAsync(id, ct);

    /// <summary>Cadastra condutor.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.Escrita)]
    [Idempotente]
    [ProducesResponseType<CondutorDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CondutorDto>> Criar(SalvarCondutorRequest req, CancellationToken ct)
    {
        var c = await servico.CriarAsync(req, ct);
        return CreatedAtAction(nameof(Obter), new { id = c.Id }, c);
    }

    /// <summary>Atualiza condutor.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.Escrita)]
    public Task<CondutorDto> Atualizar(int id, SalvarCondutorRequest req, CancellationToken ct) => servico.AtualizarAsync(id, req, ct);

    /// <summary>Remove condutor. Somente Gestor.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Politicas.Gestao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        await servico.RemoverAsync(id, ct);
        return NoContent();
    }

    /// <summary>LGPD — relatório de todos os dados pessoais do condutor (cadastro, ocorrências, agendamentos). Somente Gestor.</summary>
    [HttpGet("{id:int}/dados-pessoais")]
    [Authorize(Policy = Politicas.Gestao)]
    public async Task<DadosPessoaisCondutorDto> DadosPessoais(int id, [FromServices] ILogger<CondutoresController> logger, CancellationToken ct)
    {
        // Acesso a dados pessoais fica registrado (quem, quando), sem copiar os dados pro log.
        logger.LogInformation("LGPD: exportação de dados pessoais do condutor {CondutorId} por {Usuario}", id, User.FindFirst("sub")?.Value);
        return await servico.ExportarDadosPessoaisAsync(id, ct);
    }

    /// <summary>LGPD — anonimiza o condutor (nome vira pseudônimo, placa desvinculada), preservando o histórico operacional. Irreversível. Somente Gestor.</summary>
    [HttpPost("{id:int}/anonimizar")]
    [Authorize(Policy = Politicas.Gestao)]
    [Idempotente]
    public Task<CondutorDto> Anonimizar(int id, CancellationToken ct) => servico.AnonimizarAsync(id, ct);
}
