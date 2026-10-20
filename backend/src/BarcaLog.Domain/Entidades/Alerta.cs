using BarcaLog.Domain.Enums;

namespace BarcaLog.Domain.Entidades;

/// <summary>
/// Alerta gerado por regra a partir dos dados. A <see cref="Chave"/> identifica
/// "o mesmo problema" entre uma avaliação e outra (regra + terminal + período),
/// para que estado e histórico de tratamento não se percam quando as regras
/// rodam de novo.
/// </summary>
public class Alerta
{
    public int Id { get; set; }
    public string Chave { get; set; } = null!;
    public TipoAlerta Tipo { get; set; }
    /// <summary>Código da regra que gerou (ex.: "espera-mediana").</summary>
    public string Regra { get; set; } = null!;
    public PrioridadeAlerta Prioridade { get; set; }
    public string? TerminalId { get; set; }
    /// <summary>Período ou janela de referência, por extenso ("agosto de 2026", "hoje, 16h–17h").</summary>
    public string Periodo { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public string Indicador { get; set; } = null!;
    public string ValorObservado { get; set; } = null!;
    public string Limite { get; set; } = null!;
    /// <summary>Texto com os números que sustentam o alerta.</summary>
    public string Evidencia { get; set; } = null!;
    /// <summary>De onde vieram os dados (planilhas importadas, integração, registro manual, padrão histórico…).</summary>
    public string Origem { get; set; } = null!;
    public string? AcaoRecomendada { get; set; }

    public EstadoAlerta Estado { get; set; } = EstadoAlerta.Novo;
    /// <summary>false quando a condição deixou de ocorrer na última avaliação.</summary>
    public bool Ativo { get; set; } = true;
    public DateTime IdentificadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
    public string? Responsavel { get; set; }

    public List<EventoAlerta> Eventos { get; set; } = [];
}

/// <summary>Uma entrada no histórico de tratamento do alerta.</summary>
public class EventoAlerta
{
    public long Id { get; set; }
    public int AlertaId { get; set; }
    public DateTime Quando { get; set; }
    public string Autor { get; set; } = null!;
    public string Acao { get; set; } = null!;
    public string? Texto { get; set; }
}
