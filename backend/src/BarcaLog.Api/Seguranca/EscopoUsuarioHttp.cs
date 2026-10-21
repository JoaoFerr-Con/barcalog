using System.Collections.Concurrent;
using System.Reflection;
using BarcaLog.Application.Abstracoes;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BarcaLog.Api.Seguranca;

/// <summary>
/// Escopo vindo do token. O vínculo só muda pelo Gestor, e mudar revoga os tokens
/// do usuário (VersaoToken): a claim nunca fica velha por mais de uma requisição.
/// </summary>
public class EscopoUsuarioHttp(IHttpContextAccessor acessor) : IEscopoUsuario
{
    public string? TerminalVinculado =>
        acessor.HttpContext?.User is { Identity.IsAuthenticated: true } u && u.FindFirst(ClaimsBarcaLog.Terminal)?.Value is { Length: > 0 } t ? t : null;
}

/// <summary>
/// Aplica o escopo de terminal em TODA ação, antes do controller: qualquer parâmetro
/// "terminalId" e qualquer filtro com propriedade TerminalId passam por
/// IEscopoUsuario.RestringirTerminal — vazio vira o terminal vinculado, outro terminal = 403.
/// Fica no servidor de propósito: o frontend só reflete, não decide.
/// </summary>
public class EscopoTerminalFiltro(IEscopoUsuario escopo) : IActionFilter
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> Propriedades = new();

    public void OnActionExecuting(ActionExecutingContext ctx)
    {
        if (escopo.TerminalVinculado is null) return;
        foreach (var p in ctx.ActionDescriptor.Parameters)
        {
            if (p.ParameterType == typeof(string) && string.Equals(p.Name, "terminalId", StringComparison.OrdinalIgnoreCase))
            {
                ctx.ActionArguments.TryGetValue(p.Name, out var valor);
                ctx.ActionArguments[p.Name] = escopo.RestringirTerminal(valor as string);
                continue;
            }
            if (!ctx.ActionArguments.TryGetValue(p.Name, out var arg) || arg is null) continue;
            var prop = Propriedades.GetOrAdd(arg.GetType(), t =>
                t.GetProperty("TerminalId", BindingFlags.Public | BindingFlags.Instance) is { CanWrite: true } pi && pi.PropertyType == typeof(string) ? pi : null);
            if (prop is not null) prop.SetValue(arg, escopo.RestringirTerminal((string?)prop.GetValue(arg)));
        }
    }

    public void OnActionExecuted(ActionExecutedContext ctx) { }
}
