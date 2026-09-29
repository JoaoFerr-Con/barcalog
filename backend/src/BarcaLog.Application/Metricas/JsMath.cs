using System.Globalization;
using System.Numerics;

namespace BarcaLog.Application.Metricas;

/// <summary>Equivalentes das primitivas JS usadas nas fórmulas, pra não mudar resultado no porte.</summary>
internal static class JsMath
{
    /// <summary>Math.round do JS: arredonda .5 pra cima (em direção a +∞), diferente do banker's rounding do .NET.</summary>
    public static int Round(double valor) => (int)Math.Floor(valor + 0.5);

    public static long RoundLong(double valor) => (long)Math.Floor(valor + 0.5);

    /// <summary>
    /// Number.prototype.toFixed: arredonda pelo valor EXATO do double e, em
    /// empate exato, pra cima (6.25 → "6.3"). O "F1" do .NET daria "6.2".
    /// </summary>
    public static string ToFixed(double valor, int casas)
    {
        if (double.IsNaN(valor) || double.IsInfinity(valor)) return valor.ToString(CultureInfo.InvariantCulture);
        // "F80" devolve a expansão decimal exata do double (IEEE-correto desde o .NET Core 3.0).
        var exato = Math.Abs(valor).ToString("F80", CultureInfo.InvariantCulture);
        var ponto = exato.IndexOf('.');
        var digitos = exato[..ponto] + exato.Substring(ponto + 1, casas);
        var n = BigInteger.Parse(digitos, CultureInfo.InvariantCulture);
        if (exato[ponto + 1 + casas] >= '5') n += 1;
        var texto = n.ToString(CultureInfo.InvariantCulture).PadLeft(casas + 1, '0');
        if (casas > 0) texto = texto[..^casas] + "." + texto[^casas..];
        return (valor < 0 ? "-" : "") + texto;
    }

    public static double? MaxOuNulo(double valor) => double.IsPositiveInfinity(valor) ? null : valor;
}
