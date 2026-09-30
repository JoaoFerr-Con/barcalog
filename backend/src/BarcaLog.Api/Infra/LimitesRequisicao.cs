using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using BarcaLog.Api.Seguranca;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BarcaLog.Api.Infra;

public class LimitesOptions
{
    public const string Secao = "LimitesRequisicao";

    /// <summary>Tentativas de login/troca de senha/MFA por IP por minuto (a conta ainda bloqueia após 5 falhas).</summary>
    public int LoginPorMinutoPorIp { get; set; } = 10;

    /// <summary>Requisições por minuto por usuário (ou por IP, se anônimo).</summary>
    public int GeralPorMinuto { get; set; } = 300;

    /// <summary>Chamadas por minuto por sistema integrado.</summary>
    public int IntegracaoPorMinuto { get; set; } = 120;

    /// <summary>Importações por hora (operação pesada).</summary>
    public int ImportacaoPorHora { get; set; } = 6;
}

/// <summary>Rate limiting nativo do ASP.NET Core (System.Threading.RateLimiting), sem dependência externa.</summary>
public static class LimitesRequisicao
{
    public const string Login = "login";
    public const string Integracao = "integracao";
    public const string Importacao = "importacao";
    public const string TimeoutImportacao = "importacao";
    public const string Exportacao = "exportacao";

    public static IServiceCollection AddLimitesRequisicao(this IServiceCollection services, IConfiguration config)
    {
        var o = config.GetSection(LimitesOptions.Secao).Get<LimitesOptions>() ?? new LimitesOptions();
        services.AddRateLimiter(r =>
        {
            r.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            r.OnRejected = async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                await ctx.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = 429,
                    Title = "Muitas requisições",
                    Detail = "Limite de requisições atingido. Aguarde e tente novamente."
                }, ct);
            };

            // Limite geral: por usuário autenticado; anônimo, por IP. Health checks ficam de fora.
            r.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                http.Request.Path.StartsWithSegments("/health")
                    ? RateLimitPartition.GetNoLimiter("health")
                    : RateLimitPartition.GetFixedWindowLimiter(Chave(http), _ => Janela(o.GeralPorMinuto, TimeSpan.FromMinutes(1))));

            r.AddPolicy(Login, http => RateLimitPartition.GetFixedWindowLimiter(
                "ip:" + Ip(http), _ => Janela(o.LoginPorMinutoPorIp, TimeSpan.FromMinutes(1))));
            r.AddPolicy(Integracao, http => RateLimitPartition.GetFixedWindowLimiter(
                Chave(http), _ => Janela(o.IntegracaoPorMinuto, TimeSpan.FromMinutes(1))));
            r.AddPolicy(Importacao, http => RateLimitPartition.GetFixedWindowLimiter(
                "importacao", _ => Janela(o.ImportacaoPorHora, TimeSpan.FromHours(1))));
            // CSV com 100k+ linhas: operação cara, 10 por minuto por usuário.
            r.AddPolicy(Exportacao, http => RateLimitPartition.GetFixedWindowLimiter(
                Chave(http), _ => Janela(10, TimeSpan.FromMinutes(1))));
        });
        return services;
    }

    private static FixedWindowRateLimiterOptions Janela(int limite, TimeSpan janela) =>
        new() { PermitLimit = Math.Max(1, limite), Window = janela, QueueLimit = 0 };

    private static string Chave(HttpContext http) =>
        http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value is { } sub ? "usuario:" + sub
        : http.User.FindFirst(ApiKeyAuthenticationHandler.ClaimSistema)?.Value is { } sistema ? "sistema:" + sistema
        : "ip:" + Ip(http);

    // Atrás de proxy, só é o IP real se Proxy:Habilitado estiver configurado (ForwardedHeaders).
    private static string Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
}
