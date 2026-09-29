namespace BarcaLog.Api.Infra;

/// <summary>Célula de CSV segura: aspas escapadas e neutralização de injeção de fórmula (Excel/LibreOffice).</summary>
public static class Csv
{
    private static readonly char[] InicioPerigoso = ['=', '+', '-', '@', '\t', '\r'];

    public static string Celula(string? valor)
    {
        var texto = valor ?? string.Empty;
        if (texto.Length > 0 && InicioPerigoso.Contains(texto[0])) texto = "'" + texto;
        return "\"" + texto.Replace("\"", "\"\"") + "\"";
    }
}
