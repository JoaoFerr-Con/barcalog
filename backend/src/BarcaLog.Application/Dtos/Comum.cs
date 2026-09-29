namespace BarcaLog.Application.Dtos;

/// <summary>Resultado paginado.</summary>
public sealed record Pagina<T>(IReadOnlyList<T> Itens, int Total, int NumeroPagina, int TamanhoPagina)
{
    public int TotalPaginas => TamanhoPagina <= 0 ? 0 : (int)Math.Ceiling((double)Total / TamanhoPagina);

    public Pagina<TDestino> Mapear<TDestino>(Func<T, TDestino> mapa) =>
        new(Itens.Select(mapa).ToList(), Total, NumeroPagina, TamanhoPagina);
}

public abstract class FiltroPaginado
{
    public const int TamanhoMaximo = 500;

    private int _pagina = 1;
    private int _tamanho = 50;

    /// <summary>Página (a partir de 1).</summary>
    public int Pagina { get => _pagina; set => _pagina = value < 1 ? 1 : value; }

    /// <summary>Itens por página (1–500, padrão 50).</summary>
    public int TamanhoPagina { get => _tamanho; set => _tamanho = Math.Clamp(value, 1, TamanhoMaximo); }
}
