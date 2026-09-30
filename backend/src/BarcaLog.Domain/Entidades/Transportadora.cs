namespace BarcaLog.Domain.Entidades;

/// <summary>
/// Transportadora. Não existe coluna de status de negativação aqui de
/// propósito: ela é "negativada" se tiver ≥1 veículo negativado — sempre
/// calculado (ver <see cref="Regras.RegrasNegativacao.CalcularStatusTransportadora"/>).
/// </summary>
public class Transportadora
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Cnpj { get; set; } = null!;

    public List<Veiculo> Veiculos { get; set; } = [];
    public List<Condutor> Condutores { get; set; } = [];
}
