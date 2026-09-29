using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Domain.Entidades;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BarcaLog.Api.Seguranca;

public class GeradorTokenJwt(IOptions<JwtOptions> opcoes, TimeProvider tempo) : IGeradorToken
{
    public TokenGerado Gerar(Usuario usuario)
    {
        var o = opcoes.Value;
        var agora = tempo.GetUtcNow().UtcDateTime;
        var expira = agora.AddMinutes(o.ExpiracaoMinutos);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Email),
            new Claim(JwtRegisteredClaimNames.Name, usuario.Nome),
            new Claim(ClaimTypes.Role, usuario.Papel.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        var credenciais = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Chave)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(o.Emissor, o.Audiencia, claims, agora, expira, credenciais);
        return new TokenGerado(new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
