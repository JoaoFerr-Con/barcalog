using System.IdentityModel.Tokens.Jwt;
using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Servicos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarcaLog.Api.Controllers;

/// <summary>Autenticação de usuários internos (Operador, Gestor, Auditor).</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(UsuarioServico usuarios) : ControllerBase
{
    /// <summary>Troca e-mail/senha por um JWT (Bearer).</summary>
    /// <response code="200">Token emitido.</response>
    /// <response code="401">E-mail ou senha inválidos.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginRespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var resposta = await usuarios.LoginAsync(req, ct);
        return resposta is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Credenciais inválidas", detail: "E-mail ou senha incorretos.")
            : Ok(resposta);
    }

    /// <summary>Dados do usuário autenticado.</summary>
    [HttpGet("me")]
    [Authorize(Policy = Politicas.Leitura)]
    public async Task<ActionResult<UsuarioDto>> Me(CancellationToken ct)
    {
        var id = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(id, out var usuarioId) ? await usuarios.ObterAsync(usuarioId, ct) : Unauthorized();
    }
}

/// <summary>Gestão de usuários (somente Gestor).</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = Politicas.Gestao)]
[Produces("application/json")]
public class UsuariosController(UsuarioServico usuarios) : ControllerBase
{
    /// <summary>Cria um usuário interno.</summary>
    [HttpPost]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UsuarioDto>> Criar(CriarUsuarioRequest req, CancellationToken ct)
    {
        var usuario = await usuarios.CriarAsync(req, ct);
        return Created($"/api/usuarios/{usuario.Id}", usuario);
    }
}
