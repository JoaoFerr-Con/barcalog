using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Controllers;

/// <summary>Terminais (Unitapajós, TGPM, Hidrovias) e capacidade nominal.</summary>
[ApiController]
[Route("api/terminais")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class TerminaisController(ITerminalRepositorio terminais) : ControllerBase
{
    /// <summary>Lista os terminais cadastrados.</summary>
    [HttpGet]
    public async Task<IEnumerable<TerminalDto>> Listar(CancellationToken ct) =>
        (await terminais.ListarAsync(ct)).Select(t => new TerminalDto(t.Id, t.Nome, t.CapacidadeDiariaCarretas));
}
