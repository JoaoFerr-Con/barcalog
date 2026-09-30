using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Text.RegularExpressions;
using BarcaLog.Api.Seguranca;

namespace BarcaLog.Api.Infra;

/// <summary>
/// Uma linha de log estruturado por requisição (request_id, usuário, rota,
/// status, duração) e o cabeçalho X-Request-Id na resposta.
///
/// Privacidade: registra o TEMPLATE da rota (/api/v1/veiculos/placa/{placa}),
/// nunca a URL real nem a query string (que podem ter placa, texto de busca),
/// nem headers (Authorization/X-Api-Key) nem corpo.
/// </summary>
public partial class LogRequisicoes(RequestDelegate proximo, ILogger<LogRequisicoes> logger)
{
    public const string Cabecalho = "X-Request-Id";
    public const string ChaveRota = "barcalog:rota";

    /// <summary>Registrar logo após o UseRouting: guarda o template antes que o tratador de erro limpe o endpoint.</summary>
    public static Task GuardarRota(HttpContext http, Func<Task> proximo)
    {
        if (http.GetEndpoint() is RouteEndpoint e) http.Items[ChaveRota] = e.RoutePattern.RawText;
        return proximo();
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex IdValido();

    public async Task InvokeAsync(HttpContext http)
    {
        // Aceita o id do proxy/cliente só se for "limpo" (evita log injection).
        if (http.Request.Headers.TryGetValue(Cabecalho, out var recebido) && IdValido().IsMatch(recebido.ToString()))
            http.TraceIdentifier = recebido.ToString();
        http.Response.OnStarting(() =>
        {
            http.Response.Headers[Cabecalho] = http.TraceIdentifier;
            return Task.CompletedTask;
        });

        var inicio = Stopwatch.GetTimestamp();
        using var escopo = logger.BeginScope(new Dictionary<string, object> { ["request_id"] = http.TraceIdentifier });
        try
        {
            await proximo(http);
        }
        finally
        {
            var rota = http.Items[ChaveRota] as string ?? (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(sem rota)";
            var usuario = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? http.User.FindFirst(ApiKeyAuthenticationHandler.ClaimSistema)?.Value
                ?? "anonimo";
            var nivel = http.Response.StatusCode >= 500 ? LogLevel.Error : LogLevel.Information;
            logger.Log(nivel, "HTTP {Metodo} {Rota} {Status} {DuracaoMs} ms usuario={Usuario}",
                http.Request.Method, rota, http.Response.StatusCode,
                Math.Round(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds, 1), usuario);
        }
    }
}
