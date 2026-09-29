using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Domain.Entidades;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BarcaLog.Api.Seguranca;

public static class ClaimsBarcaLog
{
    /// <summary>VersaoToken do usuário no momento da emissão — revogação.</summary>
    public const string Versao = "ver";

    /// <summary>Presente = token restrito ("trocar-senha" ou "configurar-mfa").</summary>
    public const string Restricao = "restricao";

    /// <summary>Id da transportadora do usuário do Portal — escopo de TODOS os dados que ele acessa.</summary>
    public const string Transportadora = "transportadora";
}

public class GeradorTokenJwt(IOptions<JwtOptions> opcoes, TimeProvider tempo) : IGeradorToken
{
    public TokenGerado Gerar(Usuario usuario, string? restricao)
    {
        var o = opcoes.Value;
        var agora = tempo.GetUtcNow().UtcDateTime;
        // Token restrito vive pouco: só serve pra concluir a troca de senha / MFA.
        var expira = agora.AddMinutes(restricao is null ? o.ExpiracaoMinutos : Math.Min(15, o.ExpiracaoMinutos));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(JwtRegisteredClaimNames.Name, usuario.Nome),
            new(ClaimTypes.Role, usuario.Papel.ToString()),
            new(ClaimsBarcaLog.Versao, usuario.VersaoToken.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (restricao is not null) claims.Add(new Claim(ClaimsBarcaLog.Restricao, restricao));
        if (usuario.TransportadoraId is { } tid) claims.Add(new Claim(ClaimsBarcaLog.Transportadora, tid.ToString()));
        var credenciais = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Chave)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(o.Emissor, o.Audiencia, claims, agora, expira, credenciais);
        return new TokenGerado(new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
