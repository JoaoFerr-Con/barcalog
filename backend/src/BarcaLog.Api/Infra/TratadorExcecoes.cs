using System.Text.Json;
using BarcaLog.Domain.Excecoes;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BarcaLog.Api.Infra;

/// <summary>
/// Converte exceções em ProblemDetails (RFC 7807). Regra: mensagem de
/// negócio pode ir pro usuário; qualquer coisa interna (SQL, stack trace, tipo
/// .NET) fica só no log, e o usuário recebe uma mensagem genérica + traceId
/// pra correlacionar com o log.
/// </summary>
public class TratadorExcecoes(IProblemDetailsService problemDetails, ILogger<TratadorExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excecao, CancellationToken ct)
    {
        if (excecao is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
        {
            // Cliente desistiu (fechou aba, caiu a rede): não é erro do servidor.
            http.Response.StatusCode = 499;
            return true;
        }

        var (status, titulo, detalhe) = excecao switch
        {
            NaoEncontradoException e => (StatusCodes.Status404NotFound, "Recurso não encontrado", e.Message),
            RegraNegocioException e => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada", e.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito de edição",
                "O registro foi alterado por outra operação ao mesmo tempo. Recarregue e tente novamente."),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (StatusCodes.Status409Conflict, "Registro duplicado",
                "Já existe um registro com esses dados."),
            DbUpdateException { InnerException: SqlException { Number: 547 } } => (StatusCodes.Status409Conflict, "Registro possui vínculos",
                "A operação viola uma restrição de integridade (registro em uso ou valor inválido)."),
            BadHttpRequestException e => (e.StatusCode, e.StatusCode == 413 ? "Requisição grande demais" : "Requisição inválida",
                e.StatusCode == 413 ? "O corpo da requisição excede o limite permitido." : "Não foi possível ler a requisição."),
            JsonException => (StatusCodes.Status400BadRequest, "JSON inválido", "O corpo da requisição não é um JSON válido."),
            OperationCanceledException => (StatusCodes.Status504GatewayTimeout, "Tempo esgotado", "A operação demorou mais que o permitido."),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Algo deu errado. Informe o traceId ao suporte.")
        };

        if (status >= 500) logger.LogError(excecao, "Erro não tratado ({Status})", status);
        else if (status == StatusCodes.Status409Conflict) logger.LogWarning("Conflito: {Tipo}", excecao.GetType().Name);

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails { Status = status, Title = titulo, Detail = detalhe }
        });
    }
}
