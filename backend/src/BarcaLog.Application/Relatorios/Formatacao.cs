using System.Globalization;

namespace BarcaLog.Application.Relatorios;

/// <summary>Formatação pt-BR dos relatórios — mesmas regras de src/utils/formatar.js (durações "13h44").</summary>
public static class Fmt
{
    public static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");
    public static readonly string[] DiasSemana = ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];
    public static readonly string[] DiasSemanaCurto = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];

    public static string Num(int n) => n.ToString("#,##0", Br);
    public static string Num(double n, int casas = 1) => n.ToString(casas == 0 ? "#,##0" : "#,##0." + new string('0', casas), Br);

    public static string Pct(double? v, int casas = 1) => v is null ? "—" : v.Value.ToString(casas == 0 ? "0" : "0." + new string('0', casas), Br) + "%";

    /// <summary>Horas em "13h44" (arredondado ao minuto), "—" quando não há valor.</summary>
    public static string Dur(double? horas)
    {
        if (horas is null || double.IsNaN(horas.Value)) return "—";
        var totalMin = (int)Math.Round(horas.Value * 60, MidpointRounding.AwayFromZero);
        var sinal = totalMin < 0 ? "−" : "";
        var m = Math.Abs(totalMin);
        return $"{sinal}{m / 60}h{m % 60:00}";
    }

    /// <summary>Variação percentual com sinal ("+12,3%", "−4,0%"); null quando não há base.</summary>
    public static string? VarPct(double? atual, double? anterior)
    {
        if (atual is null || anterior is null || anterior == 0) return null;
        var v = (atual.Value - anterior.Value) / anterior.Value * 100;
        return (v > 0.05 ? "+" : v < -0.05 ? "−" : "") + Math.Abs(v).ToString("0.0", Br) + "%";
    }

    public static string? VarDur(double? atual, double? anterior)
    {
        if (atual is null || anterior is null) return null;
        var d = atual.Value - anterior.Value;
        return (d > 0 ? "+" : d < 0 ? "−" : "") + Dur(Math.Abs(d));
    }

    /// <summary>Diferença em pontos percentuais ("+3,2 p.p.").</summary>
    public static string? VarPp(double? atual, double? anterior)
    {
        if (atual is null || anterior is null) return null;
        var d = atual.Value - anterior.Value;
        return (d > 0.05 ? "+" : d < -0.05 ? "−" : "") + Math.Abs(d).ToString("0.0", Br) + " p.p.";
    }

    public static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", Br);
    public static string DataCurta(DateOnly d) => d.ToString("dd/MM", Br);
    public static string DataHora(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", Br);

    /// <summary>"Abril de 2026" quando o período é um mês inteiro; senão "01/04/2026 a 15/04/2026".</summary>
    public static string Periodo(DateOnly de, DateOnly ate)
    {
        if (de.Day == 1 && ate == de.AddMonths(1).AddDays(-1)) return CalculosRelatorio.RotuloMes(de.ToString("yyyy-MM", Br));
        if (de == ate) return Data(de);
        return $"{Data(de)} a {Data(ate)}";
    }

    /// <summary>Período em minúsculas para o meio de uma frase ("em abril de 2026", "de 01/04 a 15/04/2026").</summary>
    public static string PeriodoNaFrase(DateOnly de, DateOnly ate)
    {
        var p = Periodo(de, ate);
        return char.IsDigit(p[0]) ? (de == ate ? $"em {p}" : $"de {p}") : $"em {p.ToLower(Br)}";
    }

    public static string Plural(int n, string singular, string plural) => n == 1 ? $"{Num(n)} {singular}" : $"{Num(n)} {plural}";
}
