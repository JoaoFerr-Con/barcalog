namespace BarcaLog.Api.Seguranca;

public class JwtOptions
{
    public const string Secao = "Jwt";

    public string Emissor { get; set; } = "BarcaLog";
    public string Audiencia { get; set; } = "BarcaLog";

    /// <summary>Chave HMAC-SHA256 (mínimo 32 caracteres). Em produção: variável de ambiente Jwt__Chave.</summary>
    public string Chave { get; set; } = string.Empty;

    public int ExpiracaoMinutos { get; set; } = 480;
}
