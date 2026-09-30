using System.ComponentModel.DataAnnotations;

namespace BarcaLog.Application.Dtos;

public class FiltroMarcacoes : FiltroPaginado, IValidatableObject
{
    /// <summary>unitapajos | tgpm | hidrovias</summary>
    public string? TerminalId { get; set; }
    /// <summary>Data de marcação inicial (inclusive, horário local do porto).</summary>
    public DateOnly? De { get; set; }
    /// <summary>Data de marcação final (inclusive).</summary>
    public DateOnly? Ate { get; set; }
    /// <summary>Filtro por convênio (contém, sem diferenciar maiúsculas).</summary>
    [StringLength(100)] public string? Convenio { get; set; }
    [StringLength(50)] public string? Operador { get; set; }
    [StringLength(50)] public string? Carga { get; set; }
    [StringLength(32)] public string? Senha { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => ValidacaoPeriodo.Validar(De, Ate);
}

/// <summary>Recorte usado nos endpoints agregados (métricas).</summary>
public class FiltroMetricas : IValidatableObject
{
    [StringLength(32)] public string? TerminalId { get; set; }
    public DateOnly? De { get; set; }
    public DateOnly? Ate { get; set; }
    [StringLength(100)] public string? Convenio { get; set; }
    /// <summary>Mês no formato yyyy-MM (usado no gráfico de picos por mês).</summary>
    [RegularExpression(@"^(\d{4}-(0[1-9]|1[0-2])|todos)$", ErrorMessage = "Use o formato yyyy-MM.")]
    public string? Mes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) => ValidacaoPeriodo.Validar(De, Ate);
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
    [Required, RegularExpression("^[A-Za-z0-9-]{1,32}$", ErrorMessage = "movimentoId: 1 a 32 caracteres (letras, números e hífen).")]
    public string MovimentoId { get; set; } = null!;
    [StringLength(32)] public string? TerminalId { get; set; }
    [StringLength(32), TextoSimples] public string? Senha { get; set; }
    [StringLength(100), TextoSimples] public string? Convenio { get; set; }
    [StringLength(32), TextoSimples] public string? CodConvenio { get; set; }
    [StringLength(50), TextoSimples] public string? Operador { get; set; }
    [StringLength(50), TextoSimples] public string? Carga { get; set; }
    [Range(0, 100_000)] public int? Ciclo { get; set; }
    /// <summary>Horário local do porto, sem fuso (ex.: 2026-07-31T15:03:09).</summary>
    public DateTime? DataMarcacao { get; set; }
    public DateTime? DataLiberacao { get; set; }
}

public sealed record ErroEventoDto(int Indice, string MovimentoId, string Erro);

public sealed record ResultadoIntegracaoDto(int Recebidos, int Criados, int Atualizados, IReadOnlyList<ErroEventoDto> Erros);

/// <summary>Registros inválidos não são gravados; aparecem em Rejeitados (primeiros 100 listados em Erros).</summary>
public sealed record ResultadoImportacaoDto(string TerminalId, string Arquivo, int Lidos, int Inseridos, int IgnoradosJaExistentes, int Rejeitados, IReadOnlyList<string> Erros);
