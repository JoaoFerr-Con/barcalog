namespace BarcaLog.Api.Infra;

/// <summary>
/// Headers de segurança em toda resposta. A API só devolve JSON, então a CSP
/// é "nada pode ser carregado nem embutido". O Swagger UI (só em dev) precisa
/// de scripts/estilos próprios e recebe uma CSP menos restrita.
/// </summary>
public class CabecalhosSeguranca(RequestDelegate proximo)
{
    public Task InvokeAsync(HttpContext http)
    {
        http.Response.OnStarting(() =>
        {
            var h = http.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            h["Cross-Origin-Opener-Policy"] = "same-origin";
            if (http.Request.Path.StartsWithSegments("/swagger"))
            {
                h["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; frame-ancestors 'none'";
            }
            else
            {
                h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                // Respostas da API têm dados operacionais/pessoais: nada de cache em proxy/navegador.
                h["Cache-Control"] = "no-store";
            }
            return Task.CompletedTask;
        });
        return proximo(http);
    }
}
