using System.ComponentModel.DataAnnotations;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Regras;

namespace BarcaLog.Application.Dtos;

public class LoginRequest
{
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = null!;
    [Required, StringLength(PoliticaSenha.TamanhoMaximo)] public string Senha { get; set; } = null!;
    /// <summary>Código de 6 dígitos do app autenticador (obrigatório se o MFA estiver ativo).</summary>
    [StringLength(10)] public string? CodigoMfa { get; set; }
}

/// <summary>TransportadoraId só vem preenchido para usuários do Portal (papel Transportadora).</summary>
public sealed record UsuarioDto(int Id, string Nome, string Email, PapelUsuario Papel, bool Ativo, bool MfaAtivo, bool DeveTrocarSenha, bool Bloqueado, DateTime? UltimoLoginEm, int? TransportadoraId);

/// <summary>
/// Restricao: null = acesso normal; "trocar-senha" = senha provisória, só dá
/// pra trocar a senha; "configurar-mfa" = Gestor sem MFA, só dá pra configurar.
/// </summary>
public sealed record LoginRespostaDto(string Token, DateTime ExpiraEm, string? Restricao, UsuarioDto Usuario);

public enum TipoResultadoLogin { Sucesso, CredenciaisInvalidas, MfaRequerido }

public sealed record ResultadoLogin(TipoResultadoLogin Tipo, LoginRespostaDto? Resposta = null);

public class CriarUsuarioRequest
{
    [Required, StringLength(150, MinimumLength = 2)] public string Nome { get; set; } = null!;
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = null!;
    /// <summary>Senha inicial (provisória: o usuário troca no primeiro acesso).</summary>
    [Required, StringLength(PoliticaSenha.TamanhoMaximo)] public string Senha { get; set; } = null!;
    [Required] public PapelUsuario Papel { get; set; }
    /// <summary>Obrigatório quando Papel = Transportadora (usuário do Portal); proibido nos demais.</summary>
    public int? TransportadoraId { get; set; }
}

public class AlterarPapelRequest
{
    [Required] public PapelUsuario Papel { get; set; }
}

public class TrocarSenhaRequest
{
    [Required, StringLength(PoliticaSenha.TamanhoMaximo)] public string SenhaAtual { get; set; } = null!;
    [Required, StringLength(PoliticaSenha.TamanhoMaximo)] public string NovaSenha { get; set; } = null!;
}

public class ConfirmarSenhaRequest
{
    [Required, StringLength(PoliticaSenha.TamanhoMaximo)] public string SenhaAtual { get; set; } = null!;
}

public class CodigoMfaRequest
{
    [Required, StringLength(10)] public string Codigo { get; set; } = null!;
}

public class DesativarMfaRequest
{
    [Required, StringLength(PoliticaSenha.TamanhoMaximo)] public string SenhaAtual { get; set; } = null!;
    [Required, StringLength(10)] public string Codigo { get; set; } = null!;
}

/// <summary>Mostrado UMA vez: o segredo não é devolvido de novo por nenhum endpoint.</summary>
public sealed record ConfiguracaoMfaDto(string Segredo, string UriOtpauth);

/// <summary>Senha provisória gerada pelo Gestor — mostrada uma única vez.</summary>
public sealed record SenhaProvisoriaDto(string SenhaProvisoria);

public class FiltroUsuarios : FiltroPaginado
{
    public PapelUsuario? Papel { get; set; }
    public bool? Ativo { get; set; }
    [StringLength(100)] public string? Busca { get; set; }
}
