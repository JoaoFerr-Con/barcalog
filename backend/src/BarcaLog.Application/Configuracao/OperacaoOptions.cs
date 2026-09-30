namespace BarcaLog.Application.Configuracao;

public class OperacaoOptions
{
    public const string Secao = "Operacao";

    /// <summary>Fuso do porto — os horários dos datasets são locais (Barcarena/PA).</summary>
    public string FusoHorario { get; set; } = "America/Belem";

    /// <summary>Carretas "No Pátio" simultâneas a partir das quais a Portaria sinaliza congestionamento.</summary>
    public int LimiarCongestionamento { get; set; } = 3;

    /// <summary>Tempo que o resultado agregado das marcações fica em cache (minutos).</summary>
    public int CacheMetricasMinutos { get; set; } = 30;

    /// <summary>Intervalo mínimo entre recargas do cache depois de importação/integração (segundos).</summary>
    public int IntervaloMinimoRecargaSegundos { get; set; } = 60;
}
