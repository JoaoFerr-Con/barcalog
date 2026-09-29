using BarcaLog.Application.Abstracoes;
using BarcaLog.Domain.Entidades;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BarcaLog.Infrastructure.Seguranca;

/// <summary>
/// PBKDF2-HMAC-SHA512 com salt aleatório de 128 bits e 210.000 iterações
/// (mínimo recomendado pela OWASP para SHA-512), via PasswordHasher do ASP.NET
/// Core Identity — biblioteca da Microsoft, já no framework, FIPS-compatível.
/// Hashes com parâmetros antigos são regravados no próximo login.
/// Ver SECURITY.md para a comparação com Argon2id.
/// </summary>
public class HashSenha : IHashSenha
{
    public const int Iteracoes = 210_000;

    private static readonly PasswordHasher<Usuario> Hasher = new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = Iteracoes
    }));

    private static readonly Usuario Anonimo = new();

    public string Gerar(string senha) => Hasher.HashPassword(Anonimo, senha);

    public ResultadoVerificacaoSenha Verificar(string hash, string senha)
    {
        try
        {
            return Hasher.VerifyHashedPassword(Anonimo, hash, senha) switch
            {
                PasswordVerificationResult.Success => ResultadoVerificacaoSenha.Sucesso,
                PasswordVerificationResult.SuccessRehashNeeded => ResultadoVerificacaoSenha.SucessoPrecisaRehash,
                _ => ResultadoVerificacaoSenha.Falha
            };
        }
        catch (FormatException)
        {
            return ResultadoVerificacaoSenha.Falha; // hash corrompido nunca autentica
        }
    }
}
