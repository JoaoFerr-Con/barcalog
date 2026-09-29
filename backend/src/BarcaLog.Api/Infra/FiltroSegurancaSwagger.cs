using BarcaLog.Api.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BarcaLog.Api.Infra;

/// <summary>Marca no Swagger qual credencial cada endpoint exige (Bearer, X-Api-Key ou nenhuma).</summary>
public class FiltroSegurancaSwagger : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadados = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadados.OfType<IAllowAnonymous>().Any()) return;
        var autorizacoes = metadados.OfType<AuthorizeAttribute>().ToList();
        if (autorizacoes.Count == 0) return;

        var esquema = autorizacoes.Any(a => a.Policy == Politicas.Integracao) ? ApiKeyAuthenticationHandler.Esquema : "Bearer";
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = esquema } }] = []
        });
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Não autenticado." });
        if (esquema == "Bearer") operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Papel sem permissão." });

        if (context.MethodInfo.GetCustomAttributes(typeof(IdempotenteAttribute), false).Length > 0)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = IdempotenteAttribute.Cabecalho,
                In = ParameterLocation.Header,
                Required = false,
                Description = "Recomendado: UUID novo por operação. Reenvio com a mesma chave devolve a resposta original sem executar de novo.",
                Schema = new OpenApiSchema { Type = "string", MaxLength = 100 }
            });
        }

        var papeis = autorizacoes.Select(a => a.Policy).Where(p => p is not null).LastOrDefault();
        var descricaoPapel = papeis switch
        {
            Politicas.Gestao => "Requer papel Gestor.",
            Politicas.Escrita => "Requer papel Operador ou Gestor.",
            Politicas.Leitura => "Qualquer usuário autenticado (Operador, Gestor, Auditor).",
            Politicas.Autenticado => "Qualquer usuário autenticado, inclusive com token restrito (senha provisória / MFA pendente).",
            Politicas.Integracao => "Requer cabeçalho X-Api-Key.",
            _ => null
        };
        if (descricaoPapel is not null)
            operation.Description = string.IsNullOrEmpty(operation.Description) ? descricaoPapel : $"{operation.Description}\n\n{descricaoPapel}";
    }
}
