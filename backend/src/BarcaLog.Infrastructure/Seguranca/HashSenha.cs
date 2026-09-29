using BarcaLog.Application.Abstracoes;
using BarcaLog.Domain.Entidades;
using Microsoft.AspNetCore.Identity;

namespace BarcaLog.Infrastructure.Seguranca;

/// <summary>PBKDF2 (PasswordHasher do ASP.NET Core Identity) — sem guardar senha em texto.</summary>
public class HashSenha : IHashSenha
{
    private static readonly PasswordHasher<Usuario> Hasher = new();
    private static readonly Usuario Anonimo = new();

    public string Gerar(string senha) => Hasher.HashPassword(Anonimo, senha);

    public bool Verificar(string hash, string senha) =>
        Hasher.VerifyHashedPassword(Anonimo, hash, senha) is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
