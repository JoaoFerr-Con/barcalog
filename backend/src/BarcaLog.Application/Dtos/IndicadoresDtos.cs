using System.ComponentModel.DataAnnotations;
using BarcaLog.Application.Metricas;

namespace BarcaLog.Application.Dtos;

/// <summary>Período principal (de/ate, datas de marcação no horário do porto) e, opcionalmente, um período de comparação.</summary>
public class FiltroIndicadores : IValidatableObject
{
    [StringLength(32)] public string? TerminalId { get; set; }
    public DateOnly? De { get; set; }
    public DateOnly? Ate { get; set; }
    public DateOnly? CompararDe { get; set; }
    public DateOnly? CompararAte { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var r in ValidacaoPeriodo.Validar(De, Ate)) yield return r;
        foreach (var r in ValidacaoPeriodo.Validar(CompararDe, CompararAte)) yield return r;
        if ((CompararDe is null) != (CompararAte is null))
            yield return new ValidationResult("Informe compararDe e compararAte juntos.", [nameof(CompararDe), nameof(CompararAte)]);
    }
}

public sealed record PeriodoDto(DateOnly De, DateOnly Ate, int Dias);

public sealed record ResumoIndicadoresDto(PeriodoDto? Periodo, PeriodoDto? Comparacao, EstatisticasEspera Atual, EstatisticasEspera? Anterior, double LimiteEsperaLongaHoras);

/// <summary>Indicadores de um terminal no período.</summary>
/// <param name="TerminalId">Id do terminal.</param>
/// <param name="Terminal">Nome.</param>
/// <param name="Atual">Período principal.</param>
/// <param name="Anterior">Período de comparação, se pedido.</param>
/// <param name="PicoDiario">Maior número de marcações num dia do período.</param>
/// <param name="PicoData">Dia do pico.</param>
/// <param name="CapacidadeDiaria">Nula enquanto o porto não informar a capacidade do terminal.</param>
/// <param name="OcupacaoMediaPct">Média diária ÷ capacidade (%). Nula sem capacidade configurada.</param>
/// <param name="UltimaMarcacao">Última marcação recebida do terminal (qualquer período).</param>
public sealed record IndicadorTerminalDto(
    string TerminalId,
    string Terminal,
    EstatisticasEspera Atual,
    EstatisticasEspera? Anterior,
    int? PicoDiario,
    DateOnly? PicoData,
    int? CapacidadeDiaria,
    double? OcupacaoMediaPct,
    DateTime? UltimaMarcacao);

/// <summary>Volume e espera mediana de um terminal num mês. Parcial = o terminal não tem dados no mês inteiro.</summary>
public sealed record VolumeMensalDto(string Mes, string TerminalId, string Terminal, int Movimentacoes, double? EsperaMediana, bool Parcial);

/// <summary>Média de marcações (chegadas) e de liberações por dia, numa hora do dia.</summary>
public sealed record FluxoHorarioDto(int Hora, double ChegadasPorDia, double LiberacoesPorDia, double? Razao);

public sealed record FrescorTerminalDto(
    string TerminalId, string Terminal, DateTime? UltimaMarcacao, DateTime? UltimaLiberacao,
    DateTime? UltimaImportacaoEm, string? UltimaImportacaoArquivo, int? DiasSemDados, bool Desatualizado);

public sealed record ParametrosOperacionaisDto(
    double LimiteEsperaMedianaHoras, double MetaLiberadas24hPct, double LimiteEsperaLongaHoras,
    int DiasSemDadosAlerta, double LimiteRazaoChegadaLiberacao, DateTime? AtualizadoEm, string? AtualizadoPor);

public sealed record ConfiguracaoOperacionalDto(IReadOnlyList<TerminalDto> Terminais, ParametrosOperacionaisDto Parametros);

public class AtualizarCapacidadeTerminalRequest
{
    /// <summary>Carretas/dia. Vazio = não informada (o painel não mostra ocupação).</summary>
    [Range(1, 100_000)] public int? CapacidadeDiariaCarretas { get; set; }
    /// <summary>Carretas por janela de 1 h (agendamentos). Vazio = não configurada.</summary>
    [Range(1, 10_000)] public int? CapacidadeHoraria { get; set; }
}

public class AtualizarParametrosRequest
{
    [Range(0.5, 1000)] public double LimiteEsperaMedianaHoras { get; set; }
    [Range(1, 100)] public double MetaLiberadas24hPct { get; set; }
    [Range(1, 10_000)] public double LimiteEsperaLongaHoras { get; set; }
    [Range(1, 365)] public int DiasSemDadosAlerta { get; set; }
    [Range(1.0, 10)] public double LimiteRazaoChegadaLiberacao { get; set; }
}
