using System.ComponentModel.DataAnnotations;
using BarcaLog.Domain.Enums;

namespace BarcaLog.Application.Dtos;

public class FiltroAgendamentos : FiltroPaginado
{
    /// <summary>Data do agendamento (padrão: todas).</summary>
    public DateOnly? Data { get; set; }
    public string? TerminalId { get; set; }
    public StatusAgendamento? Status { get; set; }
    public int? TransportadoraId { get; set; }
    [StringLength(10)] public string? Placa { get; set; }
}

public sealed record AgendamentoDto(
    int Id,
    string Codigo,
    DateOnly Data,
    TimeOnly Hora,
    string Placa,
    int TransportadoraId,
    string? Transportadora,
    string TerminalId,
    string? Terminal,
    string Carga,
    string? Motorista,
    JanelaConformidade JanelaConformidade,
    StatusAgendamento Status);

public class CriarAgendamentoRequest
{
    /// <summary>Padrão: hoje (horário do porto).</summary>
    public DateOnly? Data { get; set; }
    [Required] public TimeOnly Hora { get; set; }
    [Required, StringLength(10, MinimumLength = 7), PlacaValida] public string Placa { get; set; } = null!;
    [Required] public int TransportadoraId { get; set; }
    [Required, StringLength(32)] public string TerminalId { get; set; } = null!;
    [Required, StringLength(50), TextoSimples] public string Carga { get; set; } = "Soja";
    [StringLength(150), TextoSimples] public string? Motorista { get; set; }
    public JanelaConformidade JanelaConformidade { get; set; } = JanelaConformidade.D0;
}

public class AtualizarAgendamentoRequest : CriarAgendamentoRequest
{
    [Required] public StatusAgendamento Status { get; set; }
}

public class AtualizarStatusAgendamentoRequest
{
    [Required] public StatusAgendamento Status { get; set; }
}

public sealed record AgendamentosPorHoraDto(TimeOnly Hora, int Total, string Nivel);

public sealed record ResumoAgendamentosDto(DateOnly? Data, int Total, int Confirmados, int EmOperacao, int Atrasados, IReadOnlyList<AgendamentosPorHoraDto> PorHora);

public sealed record ItemFilaVirtualDto(
    int Posicao,
    int VeiculoId,
    string Placa,
    string? Transportadora,
    StatusPortaria StatusPortaria,
    string TerminalId,
    string? Terminal,
    DateTime Desde,
    int MinutosDecorridos,
    double? EsperaMediaTerminalHoras,
    int? MinutosRestantesEstimados,
    bool JaAlemDaMedia);

public sealed record FilaVirtualDto(
    int NoPatio,
    int LimiarCongestionamento,
    bool Congestionado,
    IReadOnlyList<ItemFilaVirtualDto> Fila,
    IReadOnlyList<Metricas.AlertaGargaloPortaria> PrevisaoGargalo);
