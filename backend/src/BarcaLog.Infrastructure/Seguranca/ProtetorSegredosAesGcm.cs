using System.Security.Cryptography;
using System.Text;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Configuracao;
using Microsoft.Extensions.Options;

namespace BarcaLog.Infrastructure.Seguranca;

/// <summary>
/// AES-256-GCM (autenticado) com nonce aleatório por valor. Formato gravado:
/// "v1:" + Base64(nonce[12] | tag[16] | cifrado). O prefixo de versão permite
/// trocar de chave no futuro sem perder os dados antigos.
/// </summary>
public class ProtetorSegredosAesGcm : IProtetorSegredos
{
    private const string Versao = "v1:";
    private readonly byte[] _chave;

    public ProtetorSegredosAesGcm(IOptions<SegurancaOptions> opcoes)
    {
        var texto = opcoes.Value.ChaveCriptografia;
        byte[] chave;
        try
        {
            chave = Convert.FromBase64String(texto ?? string.Empty);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Seguranca:ChaveCriptografia deve ser Base64 de 32 bytes.");
        }
        if (chave.Length != 32) throw new InvalidOperationException("Seguranca:ChaveCriptografia deve ter exatamente 32 bytes (AES-256).");
        _chave = chave;
    }

    public string Cifrar(string textoClaro)
    {
        var claro = Encoding.UTF8.GetBytes(textoClaro);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var cifrado = new byte[claro.Length];
        using var aes = new AesGcm(_chave, tag.Length);
        aes.Encrypt(nonce, claro, cifrado, tag);
        return Versao + Convert.ToBase64String([.. nonce, .. tag, .. cifrado]);
    }

    public string Decifrar(string textoCifrado)
    {
        if (!textoCifrado.StartsWith(Versao, StringComparison.Ordinal))
            throw new CryptographicException("Formato de segredo desconhecido.");
        var dados = Convert.FromBase64String(textoCifrado[Versao.Length..]);
        var nonce = dados[..12];
        var tag = dados[12..28];
        var cifrado = dados[28..];
        var claro = new byte[cifrado.Length];
        using var aes = new AesGcm(_chave, tag.Length);
        aes.Decrypt(nonce, cifrado, tag, claro); // lança se adulterado ou chave errada
        return Encoding.UTF8.GetString(claro);
    }
}
