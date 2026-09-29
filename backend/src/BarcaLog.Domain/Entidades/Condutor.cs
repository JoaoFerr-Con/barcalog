using BarcaLog.Domain.Enums;

namespace BarcaLog.Domain.Entidades;

/// <summary>Motorista. Sem CPF — removido do produto de propósito.</summary>
public class Condutor
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public int TransportadoraId { get; set; }
    public Transportadora? Transportadora { get; set; }
    public string? PlacaVinculada { get; set; }
    public StatusNegativacao StatusNegativacao { get; set; } = StatusNegativacao.Regular;

    public void Negativar() => StatusNegativacao = StatusNegativacao.Negativada;

    public void Regularizar() => StatusNegativacao = StatusNegativacao.Regular;
}
