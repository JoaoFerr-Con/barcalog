using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BarcaLog.Api.Seguranca;

/// <summary>Chaves de integração sistema-a-sistema (config Integracao:ApiKeys).</summary>
public class IntegracaoOptions
{
    public const string Secao = "Integracao";
    public List<ChaveIntegracao> ApiKeys { get; set; } = [];
}

public class ChaveIntegracao
{
    /// <summary>Nome do sistema cliente — vai para o autor do log de auditoria.</summary>
    public string Sistema { get; set; } = null!;
    public string Chave { get; set; } = null!;
}

/// <summary>
/// Autenticação por cabeçalho X-Api-Key para /api/integracao (outros sistemas,
/// não usuários). Comparação em tempo constante.
/// </summary>
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<IntegracaoOptions> integracao)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "ApiKey";
    public const string Cabecalho = "X-Api-Key";
    public const string ClaimSistema = "barcalog:sistema";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Cabecalho, out var valores) || string.IsNullOrWhiteSpace(valores.ToString()))
            return Task.FromResult(AuthenticateResult.NoResult());

        var recebida = Encoding.UTF8.GetBytes(valores.ToString());
        var cliente = integracao.CurrentValue.ApiKeys.FirstOrDefault(k =>
            !string.IsNullOrEmpty(k.Chave) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(k.Chave), recebida));
        if (cliente is null) return Task.FromResult(AuthenticateResult.Fail("API Key inválida."));

        var identidade = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, cliente.Sistema), new Claim(ClaimSistema, cliente.Sistema)], Esquema);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidade), Esquema)));
    }
}
