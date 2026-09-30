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

    /// <summary>
    /// SHA-256 (hex) da chave. A chave em si NUNCA fica na configuração: quem
    /// lê o appsettings/variáveis não consegue se passar pelo sistema. Gere com
    /// <c>dotnet run -- gerar-api-key &lt;sistema&gt;</c>.
    /// </summary>
    public string ChaveSha256 { get; set; } = null!;
}

/// <summary>
/// Autenticação por cabeçalho X-Api-Key para /api/v1/integracao (sistemas, não
/// usuários). Compara o hash da chave recebida em tempo constante.
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

    public static string Hash(string chave) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chave))).ToLowerInvariant();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Cabecalho, out var valores) || string.IsNullOrWhiteSpace(valores.ToString()))
            return Task.FromResult(AuthenticateResult.NoResult());
        var recebida = valores.ToString();
        if (recebida.Length > 200) return Task.FromResult(AuthenticateResult.Fail("API Key inválida."));

        var hashRecebido = Encoding.ASCII.GetBytes(Hash(recebida));
        ChaveIntegracao? cliente = null;
        foreach (var k in integracao.CurrentValue.ApiKeys)
        {
            if (string.IsNullOrEmpty(k.ChaveSha256)) continue;
            // Sem "break": o tempo não depende de qual chave casou.
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(k.ChaveSha256.ToLowerInvariant()), hashRecebido)) cliente = k;
        }
        if (cliente is null) return Task.FromResult(AuthenticateResult.Fail("API Key inválida."));

        var identidade = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, cliente.Sistema), new Claim(ClaimSistema, cliente.Sistema)], Esquema);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidade), Esquema)));
    }
}
