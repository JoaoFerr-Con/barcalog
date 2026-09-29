using BarcaLog.Domain.Enums;

namespace BarcaLog.Domain.Entidades;

public class Agendamento
{
    public int Id { get; set; }
    public DateOnly Data { get; set; }
    public TimeOnly Hora { get; set; }
    public string Placa { get; set; } = null!;
    public int TransportadoraId { get; set; }
    public Transportadora? Transportadora { get; set; }
    public string TerminalId { get; set; } = null!;
    public Terminal? Terminal { get; set; }
    public string Carga { get; set; } = null!;
    public string? Motorista { get; set; }
    public JanelaConformidade JanelaConformidade { get; set; } = JanelaConformidade.D0;
    public StatusAgendamento Status { get; set; } = StatusAgendamento.Agendado;

    /// <summary>Código exibido/codificado no QR (ex.: AGD-004821).</summary>
    public string Codigo => $"AGD-{Id:D6}";
}
