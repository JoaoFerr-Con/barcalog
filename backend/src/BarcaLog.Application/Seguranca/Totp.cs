using System.Security.Cryptography;
using System.Text;

namespace BarcaLog.Application.Seguranca;

/// <summary>
/// TOTP (RFC 6238, HMAC-SHA1, 6 dígitos, passo de 30 s) — compatível com
/// Google Authenticator, Microsoft Authenticator, Authy etc. Implementado com
/// a BCL pra não trazer dependência de terceiros pra dentro da autenticação.
/// </summary>
public static class Totp
{
    public const int PassoSegundos = 30;
    public const int Digitos = 6;

    /// <summary>20 bytes aleatórios (160 bits, o recomendado pela RFC 4226) em Base32.</summary>
    public static string GerarSegredo() => Base32.Codificar(RandomNumberGenerator.GetBytes(20));

    public static long PassoAtual(DateTimeOffset agora) => agora.ToUnixTimeSeconds() / PassoSegundos;

    public static string Calcular(byte[] segredo, long passo)
    {
        Span<byte> contador = stackalloc byte[8];
        for (var i = 7; i >= 0; i--) { contador[i] = (byte)(passo & 0xFF); passo >>= 8; }
        var hash = HMACSHA1.HashData(segredo, contador);
        var offset = hash[^1] & 0x0F;
        var binario = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binario % 1_000_000).ToString("D6");
    }

    /// <summary>
    /// Confere o código aceitando ±1 passo de relógio. Devolve o passo aceito
    /// (pra gravar e impedir replay) ou null. Códigos de passos ≤ ao último já
    /// usado são recusados.
    /// </summary>
    public static long? Verificar(string segredoBase32, string? codigo, DateTimeOffset agora, long? ultimoPassoUsado)
    {
        if (codigo is null) return null;
        codigo = codigo.Trim().Replace(" ", "");
        if (codigo.Length != Digitos || !codigo.All(char.IsAsciiDigit)) return null;
        var segredo = Base32.Decodificar(segredoBase32);
        var atual = PassoAtual(agora);
        for (var delta = -1; delta <= 1; delta++)
        {
            var passo = atual + delta;
            if (ultimoPassoUsado is { } ultimo && passo <= ultimo) continue;
            var esperado = Encoding.ASCII.GetBytes(Calcular(segredo, passo));
            if (CryptographicOperations.FixedTimeEquals(esperado, Encoding.ASCII.GetBytes(codigo))) return passo;
        }
        return null;
    }

    public static string UriConfiguracao(string emissor, string conta, string segredoBase32) =>
        $"otpauth://totp/{Uri.EscapeDataString(emissor)}:{Uri.EscapeDataString(conta)}" +
        $"?secret={segredoBase32}&issuer={Uri.EscapeDataString(emissor)}&algorithm=SHA1&digits={Digitos}&period={PassoSegundos}";
}

/// <summary>Base32 RFC 4648 sem padding (formato esperado pelos apps autenticadores).</summary>
public static class Base32
{
    private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Codificar(byte[] dados)
    {
        var sb = new StringBuilder((dados.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in dados)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5) { sb.Append(Alfabeto[(buffer >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(Alfabeto[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    public static byte[] Decodificar(string texto)
    {
        var limpo = texto.Trim().TrimEnd('=').ToUpperInvariant();
        var saida = new List<byte>(limpo.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var c in limpo)
        {
            var valor = Alfabeto.IndexOf(c);
            if (valor < 0) throw new FormatException("Base32 inválido.");
            buffer = (buffer << 5) | valor;
            bits += 5;
            if (bits >= 8) { saida.Add((byte)((buffer >> (bits - 8)) & 0xFF)); bits -= 8; }
        }
        return [.. saida];
    }
}
