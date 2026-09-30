using System.IdentityModel.Tokens.Jwt;
using BarcaLog.Api.Infra;
using BarcaLog.Api.Seguranca;
using BarcaLog.Application.Dtos;
using BarcaLog.Application.Servicos;
using BarcaLog.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BarcaLog.Api.Controllers;

/// <summary>Autenticação, sessão, senha e MFA do próprio usuário.</summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController(AutenticacaoServico auth) : ControllerBase
{
    /// <summary>
    /// Troca e-mail/senha (+ código MFA, se ativo) por um JWT. Resposta 401 é
    /// sempre a mesma para e-mail inexistente, senha errada, conta inativa ou
    /// bloqueada (não revela quais contas existem). Se a senha estiver certa e
    /// o MFA ativo sem código, o 401 traz "codigo": "mfa_requerido".
    /// Limite por IP + bloqueio da conta após falhas seguidas.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitesRequisicao.Login)]
    [ProducesResponseType<LoginRespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var r = await auth.LoginAsync(req, ct);
        return r.Tipo switch
        {
            TipoResultadoLogin.Sucesso => Ok(r.Resposta),
            TipoResultadoLogin.MfaRequerido => Problema401("Código MFA necessário", "Informe o código de 6 dígitos do app autenticador.", "mfa_requerido"),
            _ => Problema401("Credenciais inválidas", "E-mail, senha ou código incorretos, ou conta temporariamente bloqueada.", "credenciais_invalidas")
        };
    }

    /// <summary>Dados do usuário autenticado (funciona também com token restrito).</summary>
    [HttpGet("me")]
    [Authorize(Policy = Politicas.Autenticado)]
    public Task<UsuarioDto> Me(CancellationToken ct) => auth.ObterPerfilAsync(UsuarioId, ct);

    /// <summary>Encerra TODAS as sessões do usuário (todos os tokens emitidos deixam de valer).</summary>
    [HttpPost("logout")]
    [Authorize(Policy = Politicas.Autenticado)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(UsuarioId, ct);
        return NoContent();
    }

    /// <summary>Troca a própria senha (obrigatório quando a senha é provisória). Encerra as sessões; faça login de novo.</summary>
    [HttpPost("trocar-senha")]
    [Authorize(Policy = Politicas.Autenticado)]
    [EnableRateLimiting(LimitesRequisicao.Login)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> TrocarSenha(TrocarSenhaRequest req, CancellationToken ct)
    {
        await auth.TrocarSenhaAsync(UsuarioId, req, ct);
        return NoContent();
    }

    /// <summary>Inicia o cadastro do MFA (TOTP). Devolve o segredo e a URI otpauth:// (para QR code) UMA única vez.</summary>
    [HttpPost("mfa/configurar")]
    [Authorize(Policy = Politicas.Autenticado)]
    [EnableRateLimiting(LimitesRequisicao.Login)]
    public Task<ConfiguracaoMfaDto> ConfigurarMfa(ConfirmarSenhaRequest req, CancellationToken ct) =>
        auth.ConfigurarMfaAsync(UsuarioId, req.SenhaAtual, ct);

    /// <summary>Confirma o MFA com um código do app. Encerra as sessões; faça login de novo com o código.</summary>
    [HttpPost("mfa/ativar")]
    [Authorize(Policy = Politicas.Autenticado)]
    [EnableRateLimiting(LimitesRequisicao.Login)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AtivarMfa(CodigoMfaRequest req, CancellationToken ct)
    {
        await auth.AtivarMfaAsync(UsuarioId, req.Codigo, ct);
        return NoContent();
    }

    /// <summary>Desativa o próprio MFA (não permitido para Gestor quando o MFA é obrigatório).</summary>
    [HttpPost("mfa/desativar")]
    [Authorize(Policy = Politicas.Autenticado)]
    [EnableRateLimiting(LimitesRequisicao.Login)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DesativarMfa(DesativarMfaRequest req, CancellationToken ct)
    {
        await auth.DesativarMfaAsync(UsuarioId, req, ct);
        return NoContent();
    }

    private int UsuarioId => int.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private ObjectResult Problema401(string titulo, string detalhe, string codigo)
    {
        var p = new ProblemDetails { Status = 401, Title = titulo, Detail = detalhe };
        p.Extensions["codigo"] = codigo;
        return new ObjectResult(p) { StatusCode = 401 };
    }
}

/// <summary>Administração de usuários (somente Gestor).</summary>
[ApiController]
[Route("api/v1/usuarios")]
[Authorize(Policy = Politicas.Gestao)]
[Produces("application/json")]
public class UsuariosController(UsuarioServico usuarios) : ControllerBase
{
    /// <summary>Lista usuários (sem hash de senha nem segredo MFA).</summary>
    [HttpGet]
    public Task<Pagina<UsuarioDto>> Listar([FromQuery] FiltroUsuarios filtro, CancellationToken ct) => usuarios.ListarAsync(filtro, ct);

    /// <summary>Um usuário.</summary>
    [HttpGet("{id:int}")]
    public Task<UsuarioDto> Obter(int id, CancellationToken ct) => usuarios.ObterAsync(id, ct);

    /// <summary>Cria usuário com senha PROVISÓRIA (troca obrigatória no primeiro acesso). Senha: mín. 12 caracteres, fora da lista de senhas comuns.</summary>
    [HttpPost]
    [Idempotente]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UsuarioDto>> Criar(CriarUsuarioRequest req, CancellationToken ct)
    {
        var usuario = await usuarios.CriarAsync(req, ct: ct);
        return CreatedAtAction(nameof(Obter), new { id = usuario.Id }, usuario);
    }

    /// <summary>Altera o papel (encerra as sessões do usuário). Não permite rebaixar o último Gestor nem a si mesmo.</summary>
    [HttpPut("{id:int}/papel")]
    public Task<UsuarioDto> AlterarPapel(int id, AlterarPapelRequest req, CancellationToken ct) => usuarios.AlterarPapelAsync(id, req.Papel, ct);

    /// <summary>Desativa (bloqueia acesso e encerra sessões). Usuários não são apagados, para preservar a auditoria.</summary>
    [HttpPost("{id:int}/desativar")]
    public Task<UsuarioDto> Desativar(int id, CancellationToken ct) => usuarios.DesativarAsync(id, ct);

    /// <summary>Reativa e remove bloqueio por tentativas.</summary>
    [HttpPost("{id:int}/reativar")]
    public Task<UsuarioDto> Reativar(int id, CancellationToken ct) => usuarios.ReativarAsync(id, ct);

    /// <summary>Recuperação de senha: gera senha provisória aleatória (mostrada uma vez) e encerra as sessões do usuário.</summary>
    [HttpPost("{id:int}/redefinir-senha")]
    public Task<SenhaProvisoriaDto> RedefinirSenha(int id, CancellationToken ct) => usuarios.RedefinirSenhaAsync(id, ct);

    /// <summary>Remove o MFA de quem perdeu o dispositivo (ele cadastra outro no próximo login).</summary>
    [HttpPost("{id:int}/redefinir-mfa")]
    public Task<UsuarioDto> RedefinirMfa(int id, CancellationToken ct) => usuarios.RedefinirMfaAsync(id, ct);
}
