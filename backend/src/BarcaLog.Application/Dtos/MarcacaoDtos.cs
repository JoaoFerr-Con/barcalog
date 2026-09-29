using System.ComponentModel.DataAnnotations;

namespace BarcaLog.Application.Dtos;

public class FiltroMarcacoes : FiltroPaginado
{
    /// <summary>unitapajos | tgpm | hidrovias</summary>
    public string? TerminalId { get; set; }
    /// <summary>Data de marcação inicial (inclusive, horário local do porto).</summary>
    public DateOnly? De { get; set; }
    /// <summary>Data de marcação final (inclusive).</summary>
    public DateOnly? Ate { get; set; }
    /// <summary>Filtro por convênio (contém, sem diferenciar maiúsculas).</summary>
    public string? Convenio { get; set; }
    public string? Operador { get; set; }
    public string? Carga { get; set; }
    public string? Senha { get; set; }
}

/// <summary>Recorte usado nos endpoints agregados (métricas).</summary>
public class FiltroMetricas
{
    public string? TerminalId { get; set; }
    public DateOnly? De { get; set; }
    public DateOnly? Ate { get; set; }
    public string? Convenio { get; set; }
    /// <summary>Mês no formato yyyy-MM (usado no gráfico de picos por mês).</summary>
    public string? Mes { get; set; }
}

public sealed record MarcacaoDto(
    string MovimentoId,
    string Senha,
    string Convenio,
    string? CodConvenio,
    string Operador,
    string Carga,
    int Ciclo,
    DateTime DataMarcacao,
    DateTime? DataLiberacao,
    double? EsperaHoras,
    string? Janela,
    string TerminalId,
    string? Terminal);

public enum TipoEventoIntegracao
{
    /// <summary>Carreta marcada (chegada). Cria ou atualiza o movimento.</summary>
    Marcacao,
    /// <summary>Carreta liberada. Exige movimento já existente (ou dataMarcacao informada).</summary>
    Liberacao
}

/// <summary>Evento empurrado por sistema externo (terminal, balança, gate).</summary>
public class EventoIntegracaoDto
{
    [Required] public TipoEventoIntegracao Tipo { get; set; }
    [Required, StringLength(32)] public string MovimentoId { get; set; } = null!;
    [StringLength(32)] public string? TerminalId { get; set; }
    [StringLength(32)] public string? Senha { get; set; }
    [StringLength(100)] public string? Convenio { get; set; }
    [StringLength(32)] public string? CodConvenio { get; set; }
    [StringLength(50)] public string? Operador { get; set; }
    [StringLength(50)] public string? Carga { get; set; }
    public int? Ciclo { get; set; }
    /// <summary>Horário local do porto, sem fuso (ex.: 2026-07-31T15:03:09).</summary>
    public DateTime? DataMarcacao { get; set; }
    public DateTime? DataLiberacao { get; set; }
}

public sealed record ErroEventoDto(int Indice, string MovimentoId, string Erro);

public sealed record ResultadoIntegracaoDto(int Recebidos, int Criados, int Atualizados, IReadOnlyList<ErroEventoDto> Erros);

public sealed record ResultadoImportacaoDto(string TerminalId, string Arquivo, int Lidos, int Inseridos, int IgnoradosJaExistentes);
