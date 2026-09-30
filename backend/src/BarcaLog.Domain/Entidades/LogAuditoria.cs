namespace BarcaLog.Domain.Entidades;

/// <summary>"Quem fez o quê e quando". Populado automaticamente pelo interceptor do EF Core.</summary>
public class LogAuditoria
{
    public long Id { get; set; }
    public string Autor { get; set; } = null!;
    public string Acao { get; set; } = null!;
    public string Detalhes { get; set; } = string.Empty;
    public DateTime Quando { get; set; }
}
