using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BarcaLog.Api.Infra;

/// <summary>
/// Endpoint mantido só por compatibilidade: usa constantes não validadas com a
/// operação (custo R$ 600/dia, capacidade presumida de 1.000 carretas/dia, CO₂…)
/// ou foi substituído por cálculo documentado. Responde normalmente, mas com
/// os cabeçalhos <c>Deprecation: true</c> e <c>X-BarcaLog-Aviso</c>, e aparece
/// como obsoleto no Swagger. A interface não usa estes endpoints.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DescontinuadoAttribute(string motivo, string? substituto = null) : ActionFilterAttribute
{
    public const string CabecalhoAviso = "X-BarcaLog-Aviso";
    public string Motivo { get; } = motivo;
    public string? Substituto { get; } = substituto;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var h = context.HttpContext.Response.Headers;
        h["Deprecation"] = "true";
        // Cabeçalho HTTP só aceita ASCII: o texto completo fica no Swagger.
        h[CabecalhoAviso] = Substituto is null ? "descontinuado" : $"descontinuado; use {Substituto}";
    }
}

/// <summary>Marca no Swagger os endpoints com <see cref="DescontinuadoAttribute"/>.</summary>
public sealed class FiltroDescontinuadoSwagger : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.GetCustomAttributes(typeof(DescontinuadoAttribute), false).FirstOrDefault() is not DescontinuadoAttribute d) return;
        operation.Deprecated = true;
        operation.Description = $"DESCONTINUADO: {d.Motivo}" + (d.Substituto is null ? "" : $" Use {d.Substituto}.") + (operation.Description is null ? "" : $"\n\n{operation.Description}");
    }
}
