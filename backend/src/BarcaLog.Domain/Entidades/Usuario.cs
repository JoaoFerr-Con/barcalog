using BarcaLog.Domain.Enums;

namespace BarcaLog.Domain.Entidades;

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string SenhaHash { get; set; } = null!;
    public PapelUsuario Papel { get; set; }
    public bool Ativo { get; set; } = true;
}
