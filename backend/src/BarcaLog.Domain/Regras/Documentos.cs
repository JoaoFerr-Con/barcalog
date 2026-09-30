using System.Text.RegularExpressions;

namespace BarcaLog.Domain.Regras;

/// <summary>
/// Placas brasileiras: padrão antigo (ABC-1234) e Mercosul (ABC1D23).
/// Forma canônica: antigo com hífen "ABC-1234", Mercosul sem hífen "ABC1D23".
/// </summary>
public static partial class Placa
{
    [GeneratedRegex("^[A-Z]{3}-?[0-9]{4}$")]
    private static partial Regex Antiga();

    [GeneratedRegex("^[A-Z]{3}-?[0-9][A-Z][0-9]{2}$")]
    private static partial Regex Mercosul();

    public static bool EhValida(string? placa)
    {
        var p = Limpar(placa);
        return Antiga().IsMatch(p) || Mercosul().IsMatch(p);
    }

    /// <summary>Maiúsculas, sem espaços, formato canônico quando válida (senão devolve só limpa).</summary>
    public static string Normalizar(string? placa)
    {
        var p = Limpar(placa);
        if (Antiga().IsMatch(p)) return $"{p[..3]}-{p[^4..]}";
        if (Mercosul().IsMatch(p)) return p.Replace("-", "");
        return p;
    }

    private static string Limpar(string? placa) =>
        (placa ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "");
}

/// <summary>
/// CNPJ com dígitos verificadores, incluindo o CNPJ ALFANUMÉRICO (IN RFB
/// 2.229/2024, em vigor desde jul/2026): as 12 primeiras posições aceitam
/// 0–9 e A–Z (valor = código ASCII − 48); os 2 DVs continuam numéricos.
/// </summary>
public static class Cnpj
{
    private static readonly int[] Pesos1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] Pesos2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public static bool EhValido(string? cnpj)
    {
        var c = Limpar(cnpj);
        if (c.Length != 14) return false;
        if (!c[..12].All(ch => char.IsAsciiDigit(ch) || ch is >= 'A' and <= 'Z')) return false;
        if (!char.IsAsciiDigit(c[12]) || !char.IsAsciiDigit(c[13])) return false;
        if (c.All(ch => ch == c[0])) return false; // 00000000000000, 11111111111111…
        return c[12] - '0' == Digito(c[..12], Pesos1) && c[13] - '0' == Digito(c[..13], Pesos2);
    }

    /// <summary>Formato "AA.AAA.AAA/AAAA-DD".</summary>
    public static string Normalizar(string? cnpj)
    {
        var c = Limpar(cnpj);
        return c.Length != 14 ? c : $"{c[..2]}.{c[2..5]}.{c[5..8]}/{c[8..12]}-{c[12..]}";
    }

    /// <summary>Calcula os 2 dígitos verificadores de uma base de 12 posições.</summary>
    public static string CalcularDigitos(string base12)
    {
        var b = Limpar(base12);
        var d1 = Digito(b, Pesos1);
        var d2 = Digito(b + d1, Pesos2);
        return $"{d1}{d2}";
    }

    private static int Digito(string valores, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++) soma += (valores[i] - '0') * pesos[i];
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    private static string Limpar(string? cnpj) =>
        new((cnpj ?? string.Empty).Trim().ToUpperInvariant().Where(ch => ch is not ('.' or '/' or '-' or ' ')).ToArray());
}

/// <summary>
/// Política de senha (NIST SP 800-63B): comprimento manda mais que
/// composição. Mínimo 12, máximo 128 (limita custo do hash), fora da lista de
/// senhas comuns e sem conter e-mail/nome do usuário.
/// </summary>
public static class PoliticaSenha
{
    public const int TamanhoMinimo = 12;
    public const int TamanhoMaximo = 128;

    private static readonly HashSet<string> Comuns = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012", "1234567890123", "senhasenha12", "senha1234567", "senha@123456", "password1234",
        "qwertyuiop12", "abcdefghijkl", "aaaaaaaaaaaa", "000000000000", "111111111111", "barcalog1234",
        "barcalog@123", "barcalog2026", "admin1234567", "administrador", "trocar123456", "mudar@123456"
    };

    public static IReadOnlyList<string> Validar(string? senha, string? email = null, string? nome = null)
    {
        var erros = new List<string>();
        if (string.IsNullOrEmpty(senha))
        {
            erros.Add("Senha obrigatória.");
            return erros;
        }
        if (senha.Length < TamanhoMinimo) erros.Add($"A senha precisa ter pelo menos {TamanhoMinimo} caracteres.");
        if (senha.Length > TamanhoMaximo) erros.Add($"A senha pode ter no máximo {TamanhoMaximo} caracteres.");
        if (Comuns.Contains(senha) || senha.Distinct().Count() <= 2) erros.Add("Senha muito comum ou previsível.");

        var minuscula = senha.ToLowerInvariant();
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(email)) partes.Add(email.Split('@')[0]);
        if (!string.IsNullOrWhiteSpace(nome)) partes.AddRange(nome.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (partes.Any(p => p.Length >= 4 && minuscula.Contains(p.ToLowerInvariant())))
            erros.Add("A senha não pode conter seu nome ou e-mail.");
        return erros;
    }
}
