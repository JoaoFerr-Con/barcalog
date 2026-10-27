namespace BarcaLog.Domain.Entidades;

/// <summary>
/// Registro de um relatório emitido pela central de relatórios. O arquivo não é
/// guardado: guarda-se o que é preciso para emitir de novo com os mesmos filtros
/// (os números podem mudar se os dados do período forem corrigidos depois) e o
/// resumo SHA-256 do arquivo entregue, para conferir uma cópia que circular.
/// </summary>
public class RelatorioGerado
{
    public static class Tamanho
    {
        public const int Codigo = 32, Tipo = 30, Titulo = 200, Abrangencia = 200, Filtros = 2000, Formato = 10, Autor = 200, Hash = 64;
    }

    public int Id { get; set; }
    /// <summary>Código impresso no rodapé do documento (ex.: RL-20261027-0007).</summary>
    public string Codigo { get; set; } = null!;
    /// <summary>gerencial | operacional | analise360.</summary>
    public string Tipo { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public DateOnly De { get; set; }
    public DateOnly Ate { get; set; }
    public string? TerminalId { get; set; }
    /// <summary>Terminal ou "Todos os terminais", com operador/carga quando filtrados.</summary>
    public string Abrangencia { get; set; } = null!;
    /// <summary>Filtros usados, em JSON (para emitir de novo).</summary>
    public string Filtros { get; set; } = null!;
    /// <summary>pdf | xlsx.</summary>
    public string Formato { get; set; } = null!;
    public string GeradoPor { get; set; } = null!;
    public DateTime GeradoEm { get; set; }
    public long TamanhoBytes { get; set; }
    public string HashSha256 { get; set; } = null!;
}
