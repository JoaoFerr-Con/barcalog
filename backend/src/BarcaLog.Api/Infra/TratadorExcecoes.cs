using BarcaLog.Domain.Excecoes;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BarcaLog.Api.Infra;

/// <summary>Converte exceções de domínio em ProblemDetails (RFC 7807).</summary>
public class TratadorExcecoes(IProblemDetailsService problemDetails, ILogger<TratadorExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excecao, CancellationToken ct)
    {
        var (status, titulo) = excecao switch
        {
            NaoEncontradoException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            RegraNegocioException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada"),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (StatusCodes.Status409Conflict, "Registro duplicado"),
            DbUpdateException { InnerException: SqlException { Number: 547 } } => (StatusCodes.Status409Conflict, "Registro possui vínculos"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno")
        };
        if (status == StatusCodes.Status500InternalServerError) logger.LogError(excecao, "Erro não tratado");

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = excecao,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = status == StatusCodes.Status500InternalServerError ? "Ocorreu um erro inesperado." : excecao.Message
            }
        });
    }
}
