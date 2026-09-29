using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Infra;

/// <summary>
/// Recusa com 413 corpo declarado (Content-Length) acima do limite do
/// endpoint ([RequestSizeLimit]) ou do padrão, ANTES de ler/deserializar.
/// Complementa o limite do Kestrel, que não vale atrás de IIS nem em outros hosts.
/// </summary>
public class LimiteCorpo(RequestDelegate proximo)
{
    public const long LimitePadrao = 1_000_000;

    public async Task InvokeAsync(HttpContext http)
    {
        var limite = http.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize ?? LimitePadrao;
        if (http.Request.ContentLength > limite)
        {
            http.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await http.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 413,
                Title = "Requisição grande demais",
                Detail = "O corpo da requisição excede o limite permitido."
            }, options: null, contentType: "application/problem+json");
            return;
        }
        await proximo(http);
    }
}
