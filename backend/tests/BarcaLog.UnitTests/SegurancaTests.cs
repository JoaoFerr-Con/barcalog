using System.Text;
using BarcaLog.Application.Seguranca;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Regras;

namespace BarcaLog.UnitTests;

public class TotpTests
{
    // Vetores de teste da RFC 6238 (apêndice B), SHA-1, segredo "12345678901234567890".
    // A RFC mostra 8 dígitos; com 6 dígitos o valor é o módulo 10^6 (os 6 últimos).
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void Calcula_igual_aos_vetores_da_RFC_6238(long unix, string esperado)
    {
        var segredo = Encoding.ASCII.GetBytes("12345678901234567890");
        Assert.Equal(esperado, Totp.Calcular(segredo, unix / Totp.PassoSegundos));
    }

    [Fact]
    public void Aceita_um_passo_de_folga_e_recusa_replay()
    {
        var segredo = Totp.GerarSegredo();
        var agora = new DateTimeOffset(2026, 9, 29, 12, 0, 10, TimeSpan.Zero);
        var passo = Totp.PassoAtual(agora);
        var bytes = Base32.Decodificar(segredo);

        Assert.Equal(passo, Totp.Verificar(segredo, Totp.Calcular(bytes, passo), agora, null));
        Assert.Equal(passo - 1, Totp.Verificar(segredo, Totp.Calcular(bytes, passo - 1), agora, null)); // relógio 30s atrasado
        Assert.Null(Totp.Verificar(segredo, Totp.Calcular(bytes, passo - 2), agora, null));              // velho demais
        Assert.Null(Totp.Verificar(segredo, Totp.Calcular(bytes, passo), agora, ultimoPassoUsado: passo)); // replay
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    public void Codigo_malformado_e_recusado(string? codigo)
    {
        Assert.Null(Totp.Verificar(Totp.GerarSegredo(), codigo, DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public void Base32_ida_e_volta()
    {
        var dados = Enumerable.Range(0, 20).Select(i => (byte)(i * 13)).ToArray();
        Assert.Equal(dados, Base32.Decodificar(Base32.Codificar(dados)));
        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Base32.Codificar(Encoding.ASCII.GetBytes("12345678901234567890")));
    }
}

public class DocumentosTests
{
    [Theory]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11222333000181", true)]
    [InlineData("12.ABC.345/01DE-35", true)]   // exemplo oficial da Receita (CNPJ alfanumérico)
    [InlineData("12abc34501de35", true)]
    [InlineData("11.222.333/0001-82", false)]  // DV errado
    [InlineData("11.111.111/1111-11", false)]  // repetido
    [InlineData("00000000000000", false)]
    [InlineData("1122233300018", false)]       // curto
    [InlineData("12.ABC.345/01DE-3A", false)]  // DV precisa ser numérico
    [InlineData("12.AB$.345/01DE-35", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Valida_CNPJ_numerico_e_alfanumerico(string? cnpj, bool valido)
    {
        Assert.Equal(valido, Cnpj.EhValido(cnpj));
    }

    [Fact]
    public void Normaliza_CNPJ_para_o_formato_com_pontuacao()
    {
        Assert.Equal("12.ABC.345/01DE-35", Cnpj.Normalizar("12abc34501de35"));
        Assert.Equal("35", Cnpj.CalcularDigitos("12ABC34501DE"));
    }

    [Theory]
    [InlineData("ENM-1001", "ENM-1001")]
    [InlineData("enm1001", "ENM-1001")]
    [InlineData(" abc 1234 ", "ABC-1234")]
    [InlineData("ABC1D23", "ABC1D23")]
    [InlineData("abc-1d23", "ABC1D23")]
    public void Normaliza_placas_validas(string entrada, string esperado)
    {
        Assert.True(Placa.EhValida(entrada));
        Assert.Equal(esperado, Placa.Normalizar(entrada));
    }

    [Theory]
    [InlineData("AB-1234")]
    [InlineData("ABCD123")]
    [InlineData("ABC12345")]
    [InlineData("'; DROP TABLE--")]
    [InlineData("<script>")]
    [InlineData("")]
    public void Recusa_placas_invalidas(string placa)
    {
        Assert.False(Placa.EhValida(placa));
    }
}

public class PoliticaSenhaTests
{
    [Theory]
    [InlineData("Porto-Barcarena#2026x")]
    [InlineData("frase longa com espacos e numeros 42")]
    public void Aceita_senhas_fortes(string senha)
    {
        Assert.Empty(PoliticaSenha.Validar(senha, "maria.silva@porto.com.br", "Maria Silva"));
    }

    [Theory]
    [InlineData("curta")]                       // < 12
    [InlineData("123456789012")]                // comum
    [InlineData("aaaaaaaaaaaaaaaa")]            // previsível
    [InlineData("maria.silva-2026!")]           // contém o e-mail
    [InlineData("Barcarena Silva 2026")]        // contém o sobrenome
    public void Recusa_senhas_fracas(string senha)
    {
        Assert.NotEmpty(PoliticaSenha.Validar(senha, "maria.silva@porto.com.br", "Maria Silva"));
    }

    [Fact]
    public void Recusa_senha_gigante_pra_nao_custar_CPU_no_hash()
    {
        Assert.NotEmpty(PoliticaSenha.Validar(new string('x', 5000) + "Ab1!"));
    }
}

public class UsuarioTests
{
    [Fact]
    public void Redefinir_senha_revoga_tokens_e_desbloqueia()
    {
        var u = new Usuario { VersaoToken = 3, FalhasLoginConsecutivas = 4, BloqueadoAte = DateTime.UtcNow.AddMinutes(5) };

        u.DefinirSenha("novo-hash", provisoria: true);

        Assert.Equal(4, u.VersaoToken);
        Assert.True(u.DeveTrocarSenha);
        Assert.Equal(0, u.FalhasLoginConsecutivas);
        Assert.False(u.EstaBloqueado(DateTime.UtcNow));
    }

    [Fact]
    public void Remover_MFA_limpa_segredo_e_revoga_tokens()
    {
        var u = new Usuario { VersaoToken = 1, MfaAtivo = true, MfaSegredoCifrado = "v1:x", MfaUltimoPassoUsado = 10 };

        u.RemoverMfa();

        Assert.False(u.MfaAtivo);
        Assert.Null(u.MfaSegredoCifrado);
        Assert.Null(u.MfaUltimoPassoUsado);
        Assert.Equal(2, u.VersaoToken);
    }

    [Fact]
    public void Bloqueio_expira_sozinho()
    {
        var agora = DateTime.UtcNow;
        var u = new Usuario { BloqueadoAte = agora.AddMinutes(1) };
        Assert.True(u.EstaBloqueado(agora));
        Assert.False(u.EstaBloqueado(agora.AddMinutes(2)));
    }
}
