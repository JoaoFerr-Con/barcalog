namespace BarcaLog.Domain.Entidades;

/// <summary>
/// Terminal portuário. O Id é o mesmo slug usado no frontend:
/// "unitapajos", "tgpm", "hidrovias".
/// </summary>
public class Terminal
{
    /// <summary>
    /// Valor que as métricas antigas (porte do metricsEngine.js) usam quando o
    /// terminal não tem capacidade cadastrada. Não é um dado real: o painel novo
    /// não mostra ocupação sem capacidade configurada.
    /// </summary>
    public const int CapacidadeNominalPadrao = 1000;

    public string Id { get; set; } = null!;
    public string Nome { get; set; } = null!;

    /// <summary>Carretas atendidas por dia. Nulo = ainda não informada pelo porto.</summary>
    public int? CapacidadeDiariaCarretas { get; set; }

    /// <summary>Carretas por janela de 1 hora (agendamentos). Nulo = não configurada.</summary>
    public int? CapacidadeHoraria { get; set; }
}

/// <summary>
/// Limites usados pelos alertas e indicadores. Uma única linha (Id = 1),
/// editável pelo Gestor em Configurações. Os valores iniciais são pontos de
/// partida, não normas do porto.
/// </summary>
public class ParametrosOperacionais
{
    public const int IdUnico = 1;

    public int Id { get; set; } = IdUnico;
    /// <summary>Espera mediana (h) acima da qual o terminal gera alerta.</summary>
    public double LimiteEsperaMedianaHoras { get; set; } = 12;
    /// <summary>Meta de carretas liberadas em até 24 h (%).</summary>
    public double MetaLiberadas24hPct { get; set; } = 85;
    /// <summary>Espera individual (h) a partir da qual a carreta entra em "esperas longas".</summary>
    public double LimiteEsperaLongaHoras { get; set; } = 72;
    /// <summary>Dias sem marcação nova a partir dos quais o terminal aparece como "dados desatualizados".</summary>
    public int DiasSemDadosAlerta { get; set; } = 2;
    /// <summary>Razão chegadas ÷ liberações numa faixa horária que marca risco de fila.</summary>
    public double LimiteRazaoChegadaLiberacao { get; set; } = 1.4;
    public DateTime? AtualizadoEm { get; set; }
    public string? AtualizadoPor { get; set; }
}
