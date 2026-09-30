using System.ComponentModel.DataAnnotations;
using BarcaLog.Domain.Enums;

namespace BarcaLog.Application.Dtos;

public class FiltroOcorrencias : FiltroPaginado
{
    public int? TransportadoraId { get; set; }
    public NivelOcorrencia? Nivel { get; set; }
    public StatusOcorrencia? Status { get; set; }
    [StringLength(10)] public string? Placa { get; set; }
}

public sealed record OcorrenciaDto(
    int Id,
    NivelOcorrencia Nivel,
    string Placa,
    int TransportadoraId,
    string? Transportadora,
    int? CondutorId,
    string? Condutor,
    string Descricao,
    string? Local,
    string? Responsavel,
    StatusOcorrencia Status,
    DateTime CriadoEm);

public sealed record OcorrenciaRegistradaDto(OcorrenciaDto Ocorrencia, bool VeiculoBloqueado, bool CondutorBloqueado, bool VeiculoCadastrado);

public class RegistrarOcorrenciaRequest
{
    [Required] public NivelOcorrencia Nivel { get; set; }
    [Required, StringLength(10, MinimumLength = 7), PlacaValida] public string Placa { get; set; } = null!;
    [Required] public int TransportadoraId { get; set; }
    /// <summary>Condutor identificado (opcional). Em N3, também é bloqueado.</summary>
    public int? CondutorId { get; set; }
    [Required, StringLength(2000, MinimumLength = 3), TextoSimples] public string Descricao { get; set; } = null!;
    [StringLength(150), TextoSimples] public string? Local { get; set; }
    /// <summary>Padrão: nome do usuário autenticado.</summary>
    [StringLength(150), TextoSimples] public string? Responsavel { get; set; }
}

public class FiltroContestacoes : FiltroPaginado
{
    public int? TransportadoraId { get; set; }
    public int? OcorrenciaId { get; set; }
    public StatusContestacao? Status { get; set; }
}

public sealed record ContestacaoDto(
    int Id,
    int OcorrenciaId,
    NivelOcorrencia? NivelOcorrencia,
    string? Placa,
    int TransportadoraId,
    string? Transportadora,
    string Justificativa,
    StatusContestacao Status,
    DateTime CriadoEm,
    DateTime? RespondidoEm,
    string? RespostaOperador);

public class AbrirContestacaoRequest
{
    [Required] public int OcorrenciaId { get; set; }
    /// <summary>Opcional — se informado, precisa ser a transportadora da ocorrência.</summary>
    public int? TransportadoraId { get; set; }
    [Required, StringLength(4000, MinimumLength = 10), TextoSimples] public string Justificativa { get; set; } = null!;
}

/// <summary>Contestação aberta pelo Portal: a transportadora vem do token, nunca do corpo.</summary>
public class AbrirContestacaoPortalRequest
{
    [Required] public int OcorrenciaId { get; set; }
    [Required, StringLength(4000, MinimumLength = 10), TextoSimples] public string Justificativa { get; set; } = null!;
}

public class ResponderContestacaoRequest
{
    [StringLength(2000), TextoSimples] public string? RespostaOperador { get; set; }
}

public class FiltroAuditoria : FiltroPaginado, IValidatableObject
{
    [StringLength(100)] public string? Autor { get; set; }
    [StringLength(100)] public string? Acao { get; set; }
    /// <summary>Busca livre em autor, ação e detalhes (3 a 100 caracteres — a busca em detalhes varre a tabela).</summary>
    [StringLength(100, MinimumLength = 3)] public string? Texto { get; set; }
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (De is { } d && Ate is { } a && d > a)
            yield return new ValidationResult("'de' não pode ser maior que 'ate'.", [nameof(De), nameof(Ate)]);
    }
}

public sealed record LogAuditoriaDto(long Id, string Autor, string Acao, string Detalhes, DateTime Quando);
