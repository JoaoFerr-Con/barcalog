using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Controllers;

/// <summary>Terminais (Unitapajós, TGPM, Hidrovias) e capacidade nominal.</summary>
[ApiController]
[Route("api/v1/terminais")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class TerminaisController(ITerminalRepositorio terminais) : ControllerBase
{
    /// <summary>Lista os terminais cadastrados.</summary>
    [HttpGet]
    public async Task<IEnumerable<TerminalDto>> Listar(CancellationToken ct) =>
        (await terminais.ListarAsync(ct)).Select(t => new TerminalDto(t.Id, t.Nome, t.CapacidadeDiariaCarretas, t.CapacidadeHoraria));
}

/// <summary>Informações do ambiente que a tela precisa exibir.</summary>
/// <param name="DadosExemplo">O banco contém o cadastro fictício de exemplo (placas, transportadoras, condutores, ocorrências, agendamentos).</param>
public sealed record AmbienteDto(bool DadosExemplo);

/// <summary>Ambiente: avisa a interface quando há dados de demonstração.</summary>
[ApiController]
[Route("api/v1/ambiente")]
[Authorize(Policy = Politicas.Autenticado)]
[Produces("application/json")]
public class AmbienteController(ITerminalRepositorio terminais) : ControllerBase
{
    [HttpGet]
    public async Task<AmbienteDto> Obter(CancellationToken ct) => new(await terminais.ExistemDadosExemploAsync(ct));
}
