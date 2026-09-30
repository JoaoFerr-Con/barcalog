using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BarcaLog.Application.Abstracoes;

namespace BarcaLog.Api.Seguranca;

/// <summary>Autor das ações = usuário do JWT, sistema da API Key, ou "Sistema" (seed, CLI).</summary>
public class UsuarioAtualHttp(IHttpContextAccessor acessor) : IUsuarioAtual
{
    private ClaimsPrincipal? Principal => acessor.HttpContext?.User;

    public int? UsuarioId =>
        Principal?.Identity?.IsAuthenticated == true && int.TryParse(Principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public string? Nome =>
        Principal?.Identity?.IsAuthenticated == true
            ? Principal.FindFirstValue(JwtRegisteredClaimNames.Name) ?? Principal.FindFirstValue(ClaimTypes.Name)
            : null;

    public string Autor
    {
        get
        {
            var p = Principal;
            if (p?.Identity?.IsAuthenticated != true) return "Sistema";
            if (p.FindFirstValue(ApiKeyAuthenticationHandler.ClaimSistema) is { } sistema) return $"Integração: {sistema}";
            var email = p.FindFirstValue(JwtRegisteredClaimNames.Email) ?? p.FindFirstValue(ClaimTypes.Email);
            return email is null ? Nome ?? "Usuário" : $"{Nome} <{email}>";
        }
    }
}
