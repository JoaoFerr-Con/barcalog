using System.ComponentModel.DataAnnotations;
using BarcaLog.Domain.Enums;

namespace BarcaLog.Application.Dtos;

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = null!;
    [Required] public string Senha { get; set; } = null!;
}

public sealed record UsuarioDto(int Id, string Nome, string Email, PapelUsuario Papel);

public sealed record LoginRespostaDto(string Token, DateTime ExpiraEm, UsuarioDto Usuario);

public class CriarUsuarioRequest
{
    [Required, StringLength(150, MinimumLength = 2)] public string Nome { get; set; } = null!;
    [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = null!;
    [Required, StringLength(128, MinimumLength = 8)] public string Senha { get; set; } = null!;
    [Required] public PapelUsuario Papel { get; set; }
}
