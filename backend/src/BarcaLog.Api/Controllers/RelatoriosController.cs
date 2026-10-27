using BarcaLog.Api.Infra;
using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Relatorios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BarcaLog.Api.Controllers;

/// <summary>
/// Central de relatórios: tipos e filtros disponíveis, prévia (o documento em JSON — o mesmo que
/// vira PDF e Excel), emissão do arquivo e histórico. Período = datas de marcação, horário do porto.
/// O terminal vinculado ao usuário restringe tudo (pedido de outro terminal = 403).
/// </summary>
[ApiController]
[Route("api/v1/relatorios")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class RelatoriosController(RelatorioServico servico) : ControllerBase
{
    /// <summary>Tipos de relatório, terminais (com o período coberto), operadores, cargas, meses com dados e o período padrão (último mês completo).</summary>
    [HttpGet("opcoes")]
    public Task<OpcoesRelatorioDto> Opcoes(CancellationToken ct) => servico.OpcoesAsync(ct);

    /// <summary>
    /// Prévia: o documento completo (seções e blocos já com os números formatados), sem código e sem
    /// registro no histórico. Sem datas: o último mês completo com dados.
    /// </summary>
    [HttpGet("previa")]
    public Task<DocumentoRelatorio> Previa([FromQuery] FiltroRelatorio filtro, CancellationToken ct) => servico.MontarAsync(filtro, ct);

    /// <summary>
    /// Emite o relatório em PDF (padrão) ou Excel (formato=xlsx), registra no histórico com código e
    /// resumo SHA-256 e devolve o arquivo. Período sem movimentações = 422.
    /// </summary>
    /// <response code="422">Sem movimentações no período e recorte.</response>
    [HttpPost("exportar")]
    [EnableRateLimiting(LimitesRequisicao.Exportacao)]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/problem+json")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Exportar([FromBody] FiltroRelatorio filtro, [FromQuery] string formato = "pdf", CancellationToken ct = default)
    {
        var a = await servico.EmitirAsync(filtro, formato, ct);
        Response.Headers["X-Relatorio-Codigo"] = a.Codigo;
        return File(a.Conteudo, a.TipoConteudo, a.NomeArquivo);
    }

    /// <summary>Relatórios emitidos, do mais recente para o mais antigo (usuário vinculado vê só os do seu terminal).</summary>
    [HttpGet("historico")]
    public Task<Pagina<RelatorioGeradoDto>> Historico([FromQuery] int pagina = 1, [FromQuery] int tamanho = 20, CancellationToken ct = default) =>
        servico.HistoricoAsync(pagina, tamanho, ct);

    /// <summary>Emite de novo um relatório do histórico com os mesmos filtros (novo código; números refletem os dados atuais).</summary>
    [HttpPost("historico/{id:int}/reemitir")]
    [EnableRateLimiting(LimitesRequisicao.Exportacao)]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/problem+json")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reemitir(int id, [FromQuery] string? formato = null, CancellationToken ct = default)
    {
        var a = await servico.ReemitirAsync(id, formato, ct);
        Response.Headers["X-Relatorio-Codigo"] = a.Codigo;
        return File(a.Conteudo, a.TipoConteudo, a.NomeArquivo);
    }
}
