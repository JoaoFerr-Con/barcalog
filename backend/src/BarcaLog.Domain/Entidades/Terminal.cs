namespace BarcaLog.Domain.Entidades;

/// <summary>
/// Terminal portuário. O Id é o mesmo slug usado no frontend
/// (src/data/registry.js): "unitapajos", "tgpm", "hidrovias".
/// </summary>
public class Terminal
{
    public const int CapacidadeNominalPadrao = 1000;

    public string Id { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public int CapacidadeDiariaCarretas { get; set; } = CapacidadeNominalPadrao;
}
