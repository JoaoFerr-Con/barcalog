using BarcaLog.Application.Servicos;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Regras;

namespace BarcaLog.UnitTests;

public class RegrasImportacaoTests
{
    private static readonly DateTime Marc = new(2026, 7, 4, 8, 25, 35);
    private static readonly DateTime Lib = new(2026, 7, 4, 17, 0, 0);

    private static DadosMovimento Base(string? cod = "000013", DateTime? lib = null, string convenio = "UNITAPAJOS BUNGE SOJA SEGREGADO") =>
        new("unitapajos", "000008880", convenio, cod, 13, Marc, lib ?? Lib);

    [Fact]
    public void Igual_nao_gera_linha()
    {
        Assert.Null(RegrasImportacao.Comparar(Base(), Base()));
    }

    [Fact]
    public void Planilha_sem_campo_opcional_nao_apaga_o_que_existe()
    {
        // Banco tem o código; a planilha não informou: continua igual, nada a mudar.
        Assert.Null(RegrasImportacao.Comparar(Base(cod: "000013"), Base(cod: null)));
    }

    [Fact]
    public void Preencher_codigo_do_convenio_vazio_e_complemento()
    {
        var r = RegrasImportacao.Comparar(Base(cod: null), Base(cod: "000013"));
        Assert.NotNull(r);
        Assert.Equal(ClassificacaoLinhaImportacao.Complementar, r!.Classificacao);
        Assert.Equal("Código do convênio: (vazio) → 000013", r.Descrever());
    }

    [Fact]
    public void Fechar_movimento_aberto_e_complemento()
    {
        var aberto = Base() with { DataLiberacao = null };
        var r = RegrasImportacao.Comparar(aberto, Base());
        Assert.Equal(ClassificacaoLinhaImportacao.Complementar, r!.Classificacao);
    }

    [Fact]
    public void Convenio_renomeado_e_conflito_com_descricao()
    {
        var r = RegrasImportacao.Comparar(Base(cod: null), Base(convenio: "UNITAPAJOS BUNGE SORGO"));
        Assert.Equal(ClassificacaoLinhaImportacao.Conflito, r!.Classificacao);
        Assert.Equal("Convênio: UNITAPAJOS BUNGE SOJA SEGREGADO → UNITAPAJOS BUNGE SORGO; Código do convênio: (vazio) → 000013", r.Descrever());
    }

    [Fact]
    public void Trocar_liberacao_existente_e_conflito_nao_complemento()
    {
        var r = RegrasImportacao.Comparar(Base(), Base(lib: Lib.AddHours(1)));
        Assert.Equal(ClassificacaoLinhaImportacao.Conflito, r!.Classificacao);
    }

    /// <summary>Os 12 nomes de convênio dos datasets reais e o operador/carga que já estavam gravados neles.</summary>
    [Theory]
    [InlineData("UNITAPAJOS BUNGE SOJA", "BUNGE", "Soja")]
    [InlineData("UNITAPAJOS BUNGE SOJA SEGREGADO", "BUNGE", "Soja Segregado")]
    [InlineData("UNITAPAJOS BUNGE MILHO", "BUNGE", "Milho")]
    [InlineData("UNITAPAJOS AMAGGI SOJA", "AMAGGI", "Soja")]
    [InlineData("UNITAPAJOS AMAGGI MILHO", "AMAGGI", "Milho")]
    [InlineData("UNITAPAJO CACAMBA AMAGGI", "AMAGGI", "Caçamba")]
    [InlineData("TGPM - BUNGE", "BUNGE", "Outro")]
    [InlineData("TGPM - MILHO", "TGPM", "Milho")]
    [InlineData("TGPM - SOJA", "TGPM", "Soja")]
    [InlineData("HIDROVIAS", "HIDROVIAS", "Outro")]
    [InlineData("HIDROVIAS - MILHO", "HIDROVIAS", "Milho")]
    [InlineData("HIDROVIAS - SOJA", "HIDROVIAS", "Soja")]
    [InlineData("UNITAPAJOS BUNGE SORGO", "BUNGE", "Sorgo")]
    public void Deducao_reproduz_operador_e_carga_dos_dados_reais(string convenio, string operador, string carga)
    {
        Assert.Equal((operador, carga), RegrasImportacao.DeduzirOperadorCarga(convenio));
    }

    [Theory]
    [InlineData("20260101", "15:35:46", "2026-01-01T15:35:46")]
    [InlineData("01/01/2026", "15:35", "2026-01-01T15:35:00")]
    [InlineData("2026-01-31", "07:04:02", "2026-01-31T07:04:02")]
    [InlineData(" 20260731 ", " 23:59:59 ", "2026-07-31T23:59:59")]
    public void Data_e_hora_em_texto(string data, string hora, string esperado)
    {
        Assert.Equal(DateTime.Parse(esperado), ImportacaoPlanilhaServico.DataHora(data, hora, out _));
    }

    [Fact]
    public void Data_e_hora_como_celulas_do_excel()
    {
        // Data como data do Excel e hora como fração do dia (0,5 = 12:00).
        Assert.Equal(new DateTime(2026, 3, 2, 12, 0, 0), ImportacaoPlanilhaServico.DataHora(new DateTime(2026, 3, 2), 0.5, out _));
        // Número de série do Excel (46083 = 2026-03-02).
        Assert.Equal(new DateTime(2026, 3, 2, 6, 0, 0), ImportacaoPlanilhaServico.DataHora(46083d, "06:00:00", out _));
    }

    [Theory]
    [InlineData("2026-13-01", "10:00:00", "data \"2026-13-01\" não reconhecida")]
    [InlineData("20260101", "25:00:00", "hora \"25:00:00\" não reconhecida")]
    [InlineData(null, "10:00:00", "data ausente")]
    [InlineData("20260101", null, "hora ausente")]
    public void Data_ou_hora_invalida_explica_o_motivo(string? data, string? hora, string trecho)
    {
        Assert.Null(ImportacaoPlanilhaServico.DataHora(data, hora, out var erro));
        Assert.Contains(trecho, erro);
    }
}
