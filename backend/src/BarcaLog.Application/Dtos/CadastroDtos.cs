using System.ComponentModel.DataAnnotations;
using BarcaLog.Domain.Enums;

namespace BarcaLog.Application.Dtos;

public sealed record TerminalDto(string Id, string Nome, int CapacidadeDiariaCarretas);

/// <summary>Status é sempre calculado: negativada se ≥1 carreta negativada.</summary>
public sealed record TransportadoraDto(
    int Id,
    string Nome,
    string Cnpj,
    StatusNegativacao Status,
    int CarretasNegativadas,
    int CarretasTotal,
    int ReincidenciasN2Ultimos30Dias,
    bool AptaParaOperar);

public class SalvarTransportadoraRequest
{
    [Required, StringLength(150, MinimumLength = 2), TextoSimples] public string Nome { get; set; } = null!;
    [Required, StringLength(18, MinimumLength = 14), CnpjValido] public string Cnpj { get; set; } = null!;
}

public class FiltroVeiculos : FiltroPaginado
{
    public int? TransportadoraId { get; set; }
    public string? TerminalId { get; set; }
    public StatusPortaria? StatusPortaria { get; set; }
    public StatusNegativacao? StatusNegativacao { get; set; }
    /// <summary>Placa (contém).</summary>
    [StringLength(10)] public string? Placa { get; set; }
}

public sealed record VeiculoDto(
    int Id,
    string Placa,
    int TransportadoraId,
    string? Transportadora,
    string? Modelo,
    StatusPortaria StatusPortaria,
    DateTime StatusPortariaDesde,
    StatusNegativacao StatusNegativacao,
    string TerminalId,
    string? Terminal);

public class CriarVeiculoRequest
{
    [Required, StringLength(10, MinimumLength = 7), PlacaValida] public string Placa { get; set; } = null!;
    [Required] public int TransportadoraId { get; set; }
    [StringLength(100), TextoSimples] public string? Modelo { get; set; }
    [Required, StringLength(32)] public string TerminalId { get; set; } = "unitapajos";
    /// <summary>Padrão: Aguardando.</summary>
    public StatusPortaria? StatusPortaria { get; set; }
}

public class AtualizarVeiculoRequest
{
    [Required, StringLength(10, MinimumLength = 7), PlacaValida] public string Placa { get; set; } = null!;
    [Required] public int TransportadoraId { get; set; }
    [StringLength(100), TextoSimples] public string? Modelo { get; set; }
    [Required, StringLength(32)] public string TerminalId { get; set; } = null!;
}

public class AtualizarStatusPortariaRequest
{
    [Required] public StatusPortaria StatusPortaria { get; set; }
}

public class NegativarVeiculoRequest
{
    [StringLength(1000), TextoSimples] public string? Motivo { get; set; }
}

public class FiltroCondutores : FiltroPaginado
{
    public int? TransportadoraId { get; set; }
}

public sealed record CondutorDto(int Id, string Nome, int TransportadoraId, string? Transportadora, string? PlacaVinculada, StatusNegativacao StatusNegativacao);

public class SalvarCondutorRequest
{
    [Required, StringLength(150, MinimumLength = 2), TextoSimples] public string Nome { get; set; } = null!;
    [Required] public int TransportadoraId { get; set; }
    [StringLength(10), PlacaValida] public string? PlacaVinculada { get; set; }
}

public sealed record OperacaoAgoraDto(int NoPatio, int EmOperacao, int Aguardando, int Finalizados, int Total);

/// <summary>Relatório LGPD dos dados pessoais de um condutor.</summary>
public sealed record DadosPessoaisCondutorDto(CondutorDto Condutor, IReadOnlyList<OcorrenciaDto> Ocorrencias, IReadOnlyList<AgendamentoDto> Agendamentos);
