using BarcaLog.Application.Metricas;

namespace BarcaLog.UnitTests;

public class IndicadoresTests
{
    private static RegistroMarcacao R(DateTime m, double espera, string terminal = "tgpm") =>
        new("id", "s", "TGPM - SOJA", "TGPM", "Soja", 1, m, m.AddHours(espera), espera, terminal, "TGPM");

    [Theory]
    [InlineData(new double[] { 1, 2, 3, 4 }, 0.5, 2.5)]
    [InlineData(new double[] { 1, 2, 3, 4 }, 0.9, 3.7)]
    [InlineData(new double[] { 5 }, 0.9, 5)]
    [InlineData(new double[] { 0.5, 9.7, 9.7, 30 }, 0.5, 9.7)]
    [InlineData(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0.9, 9.1)]
    public void Percentil_linear_igual_ao_pandas_e_ao_excel(double[] valores, double p, double esperado)
    {
        Assert.Equal(esperado, Indicadores.Percentil(valores, p)!.Value, 10);
    }

    [Fact]
    public void Percentil_de_lista_vazia_e_nulo()
    {
        Assert.Null(Indicadores.Percentil([], 0.5));
    }

    [Fact]
    public void Calcula_estatisticas_do_periodo()
    {
        var d = new DateTime(2026, 8, 1, 6, 0, 0);
        var registros = new[] { R(d, 2), R(d, 10), R(d.AddDays(1), 30), R(d.AddDays(2), 80) };
        var e = Indicadores.Calcular(registros, semLiberacao: 1, diasDoPeriodo: 5, limiteEsperaLonga: 72);

        Assert.Equal(5, e.Movimentacoes);   // 4 liberadas + 1 aberta
        Assert.Equal(4, e.Liberadas);
        Assert.Equal(1, e.SemLiberacao);
        Assert.Equal(1.0, e.MediaDiaria);   // 5 ÷ 5 dias corridos
        Assert.Equal(3, e.DiasComOperacao);
        Assert.Equal(20, e.EsperaMediana);  // (10 + 30) / 2
        Assert.Equal(65, e.EsperaP90!.Value, 10); // 30 + (80 − 30) × 0,7
        Assert.Equal(30.5, e.EsperaMedia);
        Assert.Equal(50, e.LiberadasAte24hPct); // 2 de 4
        Assert.Equal(1, e.EsperasLongas);
    }

    [Fact]
    public void Sem_liberadas_nao_inventa_espera()
    {
        var e = Indicadores.Calcular([], semLiberacao: 3, diasDoPeriodo: 1, limiteEsperaLonga: 72);
        Assert.Equal(3, e.Movimentacoes);
        Assert.Null(e.EsperaMediana);
        Assert.Null(e.LiberadasAte24hPct);
    }

    [Fact]
    public void Dias_corridos_inclui_as_pontas()
    {
        Assert.Equal(31, Indicadores.DiasCorridos(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)));
        Assert.Equal(1, Indicadores.DiasCorridos(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 1)));
    }
}
