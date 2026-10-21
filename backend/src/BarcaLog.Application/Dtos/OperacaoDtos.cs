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

/// <summary>Agendamentos na faixa de uma hora (Hora = início da faixa) e a capacidade por hora cadastrada (nula = não configurada).</summary>
public sealed record AgendamentosPorHoraDto(TimeOnly Hora, int Total, string Nivel, int? Capacidade);

public sealed record ResumoAgendamentosDto(DateOnly? Data, int Total, int Confirmados, int EmOperacao, int Atrasados, IReadOnlyList<AgendamentosPorHoraDto> PorHora);

/// <summary>Carreta na fila virtual (situação No pátio ou Aguardando).</summary>
/// <param name="Posicao">Ordem pela hora em que entrou na situação atual.</param>
/// <param name="VeiculoId">Id do veículo.</param>
/// <param name="Placa">Placa.</param>
/// <param name="Transportadora">Transportadora.</param>
/// <param name="StatusPortaria">Situação atual.</param>
/// <param name="TerminalId">Terminal de destino.</param>
/// <param name="Terminal">Nome do terminal.</param>
/// <param name="Desde">Quando entrou na situação atual (UTC).</param>
/// <param name="MinutosDecorridos">Minutos desde então.</param>
/// <param name="TempoTipicoTerminalHoras">Mediana da espera no terminal (marcação → liberação), últimos 30 dias com dados.</param>
/// <param name="MinutosRestantesEstimados">Tempo típico − decorrido (mínimo 0); nulo sem histórico.</param>
/// <param name="AlemDoTempoTipico">Já passou do tempo típico do terminal.</param>
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
    double? TempoTipicoTerminalHoras,
    int? MinutosRestantesEstimados,
    bool AlemDoTempoTipico);

/// <summary>Tempo típico de espera de um terminal (mediana), com a base usada.</summary>
/// <param name="TerminalId">Id do terminal.</param>
/// <param name="Terminal">Nome.</param>
/// <param name="MedianaHoras">Mediana de (liberação − marcação); nula sem liberações na base.</param>
/// <param name="BaseDe">Primeiro dia da base.</param>
/// <param name="BaseAte">Último dia da base (último dia com dados do terminal).</param>
/// <param name="Liberadas">Carretas liberadas na base.</param>
public sealed record TempoTipicoTerminalDto(string TerminalId, string Terminal, double? MedianaHoras, DateOnly? BaseDe, DateOnly? BaseAte, int Liberadas);

/// <summary>Fila virtual da portaria.</summary>
/// <param name="NoPatio">Carretas com situação No pátio.</param>
/// <param name="LimiarCongestionamento">Limite configurado (Operacao:LimiarCongestionamento).</param>
/// <param name="Congestionado">NoPatio ≥ limite.</param>
/// <param name="Fila">Carretas No pátio ou Aguardando, em ordem.</param>
/// <param name="TempoTipico">Tempo típico de espera por terminal usado nas estimativas.</param>
public sealed record FilaVirtualDto(
    int NoPatio,
    int LimiarCongestionamento,
    bool Congestionado,
    IReadOnlyList<ItemFilaVirtualDto> Fila,
    IReadOnlyList<TempoTipicoTerminalDto> TempoTipico);
