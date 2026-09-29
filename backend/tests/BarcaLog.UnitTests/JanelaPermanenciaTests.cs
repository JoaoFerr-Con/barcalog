using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Regras;

namespace BarcaLog.UnitTests;

public class JanelaPermanenciaTests
{
    [Theory]
    [InlineData(0, "D0")]
    [InlineData(0.45, "D0")]
    [InlineData(23.99, "D0")]
    [InlineData(24, "D1")]
    [InlineData(47.99, "D1")]
    [InlineData(48, "D2")]
    [InlineData(71.99, "D2")]
    [InlineData(72, "D3")]
    [InlineData(95.99, "D3")]
    [InlineData(96, "AL")]
    [InlineData(143.99, "AL")]
    [InlineData(144, "EC")]
    [InlineData(1216.8, "EC")]
    public void Classifica_nas_janelas_D0_ate_Estouro_Critico(double horas, string esperado)
    {
        Assert.Equal(esperado, JanelaPermanencia.Classificar(horas).Chave);
    }

    [Fact]
    public void Rotulos_iguais_ao_frontend()
    {
        Assert.Equal(
            ["D0 (0–24h)", "D1 (24–48h)", "D2 (48–72h)", "D3 (72–96h)", "Alerta (96–144h)", "Estouro Crítico (>144h)"],
            JanelaPermanencia.Todas.Select(j => j.Rotulo));
    }

    [Theory]
    // Registros reais dos datasets: esperaHoras deles tem que bater com o cálculo.
    [InlineData("2026-01-01T15:35:46", "2026-01-01T16:02:49", 0.45)]
    [InlineData("2026-01-02T06:07:58", "2026-01-02T09:35:21", 3.46)]
    [InlineData("2026-01-03T06:07:54", "2026-01-03T21:50:52", 15.72)]
    [InlineData("2025-12-31T03:42:56", "2026-01-02T07:04:02", 51.35)]
    [InlineData("2026-07-30T16:27:12", "2026-07-31T22:32:52", 30.09)]
    public void Espera_e_liberacao_menos_marcacao_em_horas_com_2_casas(string marcacao, string liberacao, double esperado)
    {
        Assert.Equal(esperado, JanelaPermanencia.EsperaEmHoras(DateTime.Parse(marcacao), DateTime.Parse(liberacao)));
    }

    [Fact]
    public void Classifica_a_partir_das_datas_do_movimento()
    {
        var marcacao = new Marcacao { DataMarcacao = new DateTime(2026, 3, 1, 8, 0, 0), DataLiberacao = new DateTime(2026, 3, 3, 9, 0, 0) };

        Assert.Equal(49, marcacao.CalcularEsperaHoras());
        Assert.Equal("D2", JanelaPermanencia.Classificar(marcacao.DataMarcacao, marcacao.DataLiberacao!.Value).Chave);
    }

    [Fact]
    public void Marcacao_sem_liberacao_nao_tem_espera()
    {
        Assert.Null(new Marcacao { DataMarcacao = DateTime.Now }.CalcularEsperaHoras());
    }
}
