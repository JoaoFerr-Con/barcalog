using BarcaLog.Api.Infra;
using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Servicos;
using BarcaLog.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BarcaLog.Api.Controllers;

/// <summary>
/// Importação de planilhas (XLSX/CSV) em duas etapas: análise (nada gravado em
/// marcações) e confirmação. Consultar: qualquer papel interno. Enviar,
/// decidir conflitos, confirmar e cancelar: só Gestor.
/// </summary>
[ApiController]
[Route("api/v1/importacoes")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class ImportacoesController(ImportacaoPlanilhaServico servico) : ControllerBase
{
    /// <summary>Histórico de importações, mais recentes primeiro.</summary>
    [HttpGet]
    public Task<Pagina<LoteImportacaoDto>> Listar([FromQuery] FiltroLotesImportacao filtro, CancellationToken ct) => servico.ListarAsync(filtro, ct);

    /// <summary>
    /// Envia uma planilha para análise. Formatos: .xlsx (primeira aba) ou .csv
    /// (separador ; , ou tab). Colunas obrigatórias: COD_MOVIMENTO, SENHA,
    /// CONVENIO, DATA_MARCACAO, HORA_MARCACAO; opcionais: COD_CONVENIO, CICLO,
    /// DATA_LIBERACAO, HORA_LIBERACAO. Devolve a classificação das linhas; nada é
    /// gravado em marcações até a confirmação.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Politicas.Gestao)]
    [EnableRateLimiting(LimitesRequisicao.Importacao)]
    [RequestTimeout(LimitesRequisicao.TimeoutImportacao)]
    [RequestSizeLimit(ImportacaoPlanilhaServico.TamanhoMaximoBytes + 1_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportacaoPlanilhaServico.TamanhoMaximoBytes + 1_000_000)]
    [ProducesResponseType<LoteImportacaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Analisar([FromForm] string terminalId, IFormFile arquivo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(terminalId) || terminalId.Length > 32) return ValidationProblem(detail: "Informe o terminal (terminalId).");
        if (arquivo is null || arquivo.Length == 0) return ValidationProblem(detail: "Envie um arquivo.");
        if (arquivo.Length > ImportacaoPlanilhaServico.TamanhoMaximoBytes)
            return ValidationProblem(detail: $"Arquivo maior que {ImportacaoPlanilhaServico.TamanhoMaximoBytes / 1_000_000} MB.");
        var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
        if (extensao is not (".xlsx" or ".csv")) return ValidationProblem(detail: "Envie um arquivo .xlsx ou .csv.");

        await using var stream = arquivo.OpenReadStream();
        // XLSX é um ZIP: confere a assinatura antes de entregar ao leitor.
        if (extensao == ".xlsx")
        {
            var cabecalho = new byte[4];
            var lidos = await stream.ReadAsync(cabecalho, ct);
            if (lidos < 4 || cabecalho[0] != 0x50 || cabecalho[1] != 0x4B) return ValidationProblem(detail: "O arquivo não é um .xlsx válido.");
            stream.Position = 0;
        }
        var lote = await servico.AnalisarAsync(terminalId.Trim(), stream, arquivo.FileName, arquivo.Length, ct);
        return CreatedAtAction(nameof(Obter), new { id = lote.Id }, lote);
    }

    /// <summary>Resumo de uma importação (contagens, avisos, convênios novos, conflitos pendentes).</summary>
    [HttpGet("{id:int}")]
    public Task<LoteImportacaoDto> Obter(int id, CancellationToken ct) => servico.ObterAsync(id, ct);

    /// <summary>Linhas para revisão (filtros: classificacao, aviso, decisao). Linhas iguais ao banco não são guardadas.</summary>
    [HttpGet("{id:int}/linhas")]
    public Task<Pagina<LinhaImportacaoDto>> Linhas(int id, [FromQuery] FiltroLinhasImportacao filtro, CancellationToken ct) => servico.ListarLinhasAsync(id, filtro, ct);

    /// <summary>Decide conflitos: Aplicar (gravar o valor da planilha) ou Manter. Sem "linhas", vale para todos os pendentes.</summary>
    [HttpPost("{id:int}/conflitos")]
    [Authorize(Policy = Politicas.Gestao)]
    public Task<ResultadoDecisaoDto> Decidir(int id, DecidirConflitosRequest req, CancellationToken ct) => servico.DecidirConflitosAsync(id, req, ct);

    /// <summary>Grava a importação (uma transação). Exige todos os conflitos decididos.</summary>
    [HttpPost("{id:int}/confirmar")]
    [Authorize(Policy = Politicas.Gestao)]
    [Idempotente]
    [RequestTimeout(LimitesRequisicao.TimeoutImportacao)]
    public Task<LoteImportacaoDto> Confirmar(int id, CancellationToken ct) => servico.ConfirmarAsync(id, ct);

    /// <summary>Descarta a análise. Nada é gravado em marcações.</summary>
    [HttpPost("{id:int}/cancelar")]
    [Authorize(Policy = Politicas.Gestao)]
    public Task<LoteImportacaoDto> Cancelar(int id, CancellationToken ct) => servico.CancelarAsync(id, ct);

    /// <summary>Relatório CSV com as linhas rejeitadas, conflitantes e com aviso (separador ";", abre no Excel).</summary>
    [HttpGet("{id:int}/relatorio")]
    [EnableRateLimiting(LimitesRequisicao.Exportacao)]
    [Produces("text/csv")]
    public async Task Relatorio(int id, CancellationToken ct)
    {
        var lote = await servico.ObterAsync(id, ct);
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
        { FileNameStar = $"importacao_{id}_{Path.GetFileNameWithoutExtension(lote.Arquivo)}.csv" }.ToString();
        await using var escritor = new StreamWriter(Response.Body, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true), bufferSize: 64 * 1024);
        await escritor.WriteLineAsync("Linha;Situação;Decisão;Motivo / diferenças;Avisos;Movimento;Senha;Convênio;Cód. convênio;Ciclo;Marcação;Liberação");
        var br = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        await foreach (var l in servico.RelatorioAsync(id, ct))
        {
            await escritor.WriteLineAsync(string.Join(';',
                l.NumeroLinha.ToString(br), Csv.Celula(Rotulo(l.Classificacao)),
                Csv.Celula(l.Decisao == DecisaoConflitoImportacao.NaoSeAplica ? "" : l.Decisao.ToString()),
                Csv.Celula(l.Motivo), Csv.Celula(string.Join(", ", l.Avisos)), Csv.Celula(l.MovimentoId), Csv.Celula(l.Senha),
                Csv.Celula(l.Convenio), Csv.Celula(l.CodConvenio), l.Ciclo?.ToString(br) ?? "",
                l.DataMarcacao?.ToString("dd/MM/yyyy HH:mm:ss", br) ?? "", l.DataLiberacao?.ToString("dd/MM/yyyy HH:mm:ss", br) ?? ""));
        }
    }

    private static string Rotulo(ClassificacaoLinhaImportacao c) => c switch
    {
        ClassificacaoLinhaImportacao.Novo => "Novo",
        ClassificacaoLinhaImportacao.Complementar => "Completar",
        ClassificacaoLinhaImportacao.Conflito => "Conflito",
        _ => "Rejeitada"
    };
}

/// <summary>Cadastro de convênios (código do sistema de origem → operador e carga).</summary>
[ApiController]
[Route("api/v1/convenios")]
[Authorize(Policy = Politicas.Leitura)]
[Produces("application/json")]
public class ConveniosController(ConvenioServico servico) : ControllerBase
{
    [HttpGet]
    public Task<List<ConvenioDto>> Listar(CancellationToken ct) => servico.ListarAsync(ct);

    /// <summary>Confirma ou corrige operador e carga deduzidos do nome. Somente Gestor.</summary>
    [HttpPut("{codigo:maxlength(32)}")]
    [Authorize(Policy = Politicas.Gestao)]
    public Task<ConvenioDto> Atualizar(string codigo, AtualizarConvenioRequest req, CancellationToken ct) => servico.AtualizarAsync(codigo, req, ct);
}
