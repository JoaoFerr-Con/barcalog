using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BarcaLog.Api.Seguranca;
using BarcaLog.Infrastructure.Idempotencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace BarcaLog.Api.Infra;

/// <summary>
/// Suporte ao cabeçalho <c>Idempotency-Key</c> em operações que criam/alteram
/// estado. Com a chave:
///   * 1ª requisição executa e guarda a resposta (24h);
///   * repetição com mesmo corpo → devolve a MESMA resposta, sem executar de
///     novo (cabeçalho Idempotent-Replayed: true);
///   * repetição com corpo diferente → 422;
///   * repetição enquanto a 1ª ainda roda → 409.
/// Sem o cabeçalho, a operação roda normalmente (o frontend deve sempre mandar).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotenteAttribute : Attribute, IFilterFactory
{
    public const string Cabecalho = "Idempotency-Key";
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider sp) =>
        new FiltroIdempotencia(sp.GetRequiredService<ServicoIdempotencia>(), sp.GetRequiredService<IOptions<JsonOptions>>());
}

internal sealed class FiltroIdempotencia(ServicoIdempotencia servico, IOptions<JsonOptions> mvcJson) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate proximo)
    {
        var http = ctx.HttpContext;
        if (!http.Request.Headers.TryGetValue(IdempotenteAttribute.Cabecalho, out var valor) || string.IsNullOrWhiteSpace(valor.ToString()))
        {
            await proximo();
            return;
        }
        var chave = valor.ToString();
        if (chave.Length > 100 || chave.Any(char.IsControl))
        {
            ctx.Result = Problema(400, "Idempotency-Key inválida", "Use até 100 caracteres (ex.: um UUID).");
            return;
        }

        var ct = http.RequestAborted;
        var autor = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? http.User.FindFirst(ApiKeyAuthenticationHandler.ClaimSistema)?.Value ?? "anonimo";
        var escopo = $"{autor}|{http.Request.Method}|{http.Request.Path}";
        var opcoesJson = mvcJson.Value.JsonSerializerOptions;
        var argumentos = ctx.ActionArguments.Where(a => a.Value is not CancellationToken).OrderBy(a => a.Key)
            .ToDictionary(a => a.Key, a => a.Value);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(argumentos, opcoesJson))));

        var reserva = await servico.ReservarAsync(escopo, chave, hash, ct);
        switch (reserva.Situacao)
        {
            case SituacaoReserva.CorpoDiferente:
                ctx.Result = Problema(422, "Idempotency-Key reutilizada", "Essa chave já foi usada com outro conteúdo. Gere uma chave nova para cada operação.");
                return;
            case SituacaoReserva.EmAndamento:
                ctx.Result = Problema(409, "Requisição em processamento", "Uma requisição com essa Idempotency-Key ainda está em andamento.");
                return;
            case SituacaoReserva.JaConcluida:
                var e = reserva.Existente!;
                http.Response.Headers["Idempotent-Replayed"] = "true";
                if (e.Location is not null) http.Response.Headers.Location = e.Location;
                ctx.Result = new ContentResult { StatusCode = e.StatusHttp, Content = e.CorpoResposta, ContentType = "application/json; charset=utf-8" };
                return;
        }

        ActionExecutedContext? executado = null;
        try
        {
            executado = await proximo();
        }
        finally
        {
            if (executado is { Exception: null } && executado.Result is ObjectResult { StatusCode: >= 200 and < 300 or null } ok)
            {
                var status = ok.StatusCode ?? 200;
                var corpo = ok.Value is null ? null : JsonSerializer.Serialize(ok.Value, ok.DeclaredType ?? ok.Value.GetType(), opcoesJson);
                var location = ok switch
                {
                    CreatedAtActionResult c => ctx.HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Mvc.Routing.IUrlHelperFactory>()
                        .GetUrlHelper(ctx).Action(c.ActionName, c.ControllerName, c.RouteValues),
                    CreatedResult c => c.Location,
                    _ => null
                };
                await servico.ConcluirAsync(escopo, chave, status, corpo, location, CancellationToken.None);
            }
            else if (executado is { Exception: null } && executado.Result is StatusCodeResult { StatusCode: >= 200 and < 300 } semCorpo)
            {
                await servico.ConcluirAsync(escopo, chave, semCorpo.StatusCode, null, null, CancellationToken.None);
            }
            else
            {
                // Falhou (erro, 4xx/5xx, exceção): libera a chave pra permitir nova tentativa.
                await servico.LiberarAsync(escopo, chave, CancellationToken.None);
            }
        }
    }

    private static ObjectResult Problema(int status, string titulo, string detalhe) =>
        new(new ProblemDetails { Status = status, Title = titulo, Detail = detalhe }) { StatusCode = status };
}
