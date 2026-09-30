using System.Security.Cryptography;
using BarcaLog.Application.Abstracoes;
using BarcaLog.Application.Configuracao;
using BarcaLog.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BarcaLog.IntegrationTests;

/// <summary>Componentes criptográficos da Infrastructure (não precisam de banco).</summary>
public class CriptografiaTests
{
    private static ProtetorSegredosAesGcm Protetor(byte[]? chave = null) =>
        new(Options.Create(new SegurancaOptions { ChaveCriptografia = Convert.ToBase64String(chave ?? RandomNumberGenerator.GetBytes(32)) }));

    [Fact]
    public void AES_GCM_ida_e_volta_com_nonce_diferente_a_cada_vez()
    {
        var p = Protetor();
        var a = p.Cifrar("JBSWY3DPEHPK3PXP");
        var b = p.Cifrar("JBSWY3DPEHPK3PXP");

        Assert.NotEqual(a, b); // mesmo texto, cifrados diferentes (nonce aleatório)
        Assert.Equal("JBSWY3DPEHPK3PXP", p.Decifrar(a));
        Assert.DoesNotContain("JBSWY3DP", a);
    }

    [Fact]
    public void AES_GCM_detecta_adulteracao_e_chave_errada()
    {
        var chave = RandomNumberGenerator.GetBytes(32);
        var cifrado = Protetor(chave).Cifrar("segredo");
        var bytes = Convert.FromBase64String(cifrado[3..]);
        bytes[^1] ^= 0xFF;
        var adulterado = "v1:" + Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() => Protetor(chave).Decifrar(adulterado));
        Assert.ThrowsAny<CryptographicException>(() => Protetor().Decifrar(cifrado));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao-e-base64!!")]
    [InlineData("c2hvcnQ=")] // 5 bytes
    public void Chave_de_criptografia_invalida_impede_a_subida(string chave)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ProtetorSegredosAesGcm(Options.Create(new SegurancaOptions { ChaveCriptografia = chave })));
    }

    [Fact]
    public void Hash_de_senha_usa_salt_e_210k_iteracoes_e_pede_rehash_de_hash_antigo()
    {
        var h = new HashSenha();
        var h1 = h.Gerar("Porto-Barcarena#2026x");
        var h2 = h.Gerar("Porto-Barcarena#2026x");

        Assert.NotEqual(h1, h2); // salt aleatório
        Assert.Equal(ResultadoVerificacaoSenha.Sucesso, h.Verificar(h1, "Porto-Barcarena#2026x"));
        Assert.Equal(ResultadoVerificacaoSenha.Falha, h.Verificar(h1, "porto-barcarena#2026x"));
        Assert.Equal(ResultadoVerificacaoSenha.Falha, h.Verificar("lixo-que-nao-e-hash", "x"));

        // Hash gerado com o padrão antigo (100k) → senha certa, mas pede regravação.
        var antigo = new PasswordHasher<BarcaLog.Domain.Entidades.Usuario>().HashPassword(new(), "Porto-Barcarena#2026x");
        Assert.Equal(ResultadoVerificacaoSenha.SucessoPrecisaRehash, h.Verificar(antigo, "Porto-Barcarena#2026x"));
    }
}
