using BarcaLog.Domain.Enums;

namespace BarcaLog.Domain.Entidades;

/// <summary>Carreta. É o VEÍCULO (placa) que é negativado, não a transportadora.</summary>
public class Veiculo
{
    public int Id { get; set; }
    public string Placa { get; set; } = null!;
    public int TransportadoraId { get; set; }
    public Transportadora? Transportadora { get; set; }
    public string? Modelo { get; set; }
    public StatusPortaria StatusPortaria { get; set; } = StatusPortaria.Aguardando;
    public DateTime StatusPortariaDesde { get; set; }
    public StatusNegativacao StatusNegativacao { get; set; } = StatusNegativacao.Regular;
    public string TerminalId { get; set; } = null!;
    public Terminal? Terminal { get; set; }

    public bool EstaNegativado => StatusNegativacao == StatusNegativacao.Negativada;

    public void Negativar() => StatusNegativacao = StatusNegativacao.Negativada;

    public void Regularizar() => StatusNegativacao = StatusNegativacao.Regular;

    public void AtualizarStatusPortaria(StatusPortaria novoStatus, DateTime agoraUtc)
    {
        StatusPortaria = novoStatus;
        StatusPortariaDesde = agoraUtc;
    }

    /// <summary>Normaliza a placa pro formato usado nas buscas (maiúsculas, sem espaços).</summary>
    public static string NormalizarPlaca(string? placa) => (placa ?? string.Empty).Trim().ToUpperInvariant();
}
