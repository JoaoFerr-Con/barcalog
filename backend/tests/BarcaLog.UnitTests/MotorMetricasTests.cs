using BarcaLog.Application.Metricas;

namespace BarcaLog.UnitTests;

public class MotorMetricasTests
{
    private static int _seq;

    private static RegistroMarcacao R(string marcado, string liberado, string terminal = "tgpm", string nome = "TGPM", string convenio = "TGPM - SOJA", string operador = "TGPM")
    {
        var m = DateTime.Parse(marcado);
        var l = DateTime.Parse(liberado);
        return new RegistroMarcacao($"{++_seq:D9}", "1", convenio, operador, "Soja", 1, m, l,
            BarcaLog.Domain.Regras.JanelaPermanencia.EsperaEmHoras(m, l), terminal, nome);
    }

    [Theory]
    [InlineData(0.5, "normal")]
    [InlineData(0.99, "normal")]
    [InlineData(1.0, "atencao")]
    [InlineData(1.19, "atencao")]
    [InlineData(1.2, "alto")]
    [InlineData(1.39, "alto")]
    [InlineData(1.4, "critico")]
    [InlineData(3, "critico")]
    public void Nivel_do_indice_de_risco_segue_os_limiares_de_rho(double rho, string nivel)
    {
        Assert.Equal(nivel, MotorMetricas.ClassificarRho(rho));
    }

    [Fact]
    public void Rho_sem_liberacao_na_hora_vale_2_se_houve_chegada_e_0_se_nao()
    {
        Assert.Equal(2, MotorMetricas.CalcularRho(lambda: 3, mu: 0));
        Assert.Equal(0, MotorMetricas.CalcularRho(lambda: 0, mu: 0));
        Assert.Equal(1.5, MotorMetricas.CalcularRho(lambda: 3, mu: 2));
    }

    [Fact]
    public void Indice_de_risco_por_hora_e_lambda_sobre_mu_normalizado_por_dias()
    {
        // 2 dias. Às 8h chegam 3 carretas (λ = 1.5/dia); às 8h saem 2 (μ = 1.0/dia) → ρ = 1.5 crítico.
        var registros = new List<RegistroMarcacao>
        {
            R("2026-05-01T08:10:00", "2026-05-01T08:50:00"),
            R("2026-05-01T08:20:00", "2026-05-01T10:00:00"),
            R("2026-05-02T08:05:00", "2026-05-02T08:30:00"),
        };

        var indice = MotorMetricas.IndiceRiscoGargalo(registros);

        Assert.Equal(24, indice.Count);
        var h8 = indice[8];
        Assert.Equal(1.5, h8.Lambda, 6);
        Assert.Equal(1.0, h8.Mu, 6);
        Assert.Equal(1.5, h8.Rho, 6);
        Assert.Equal(150, h8.Indice);
        Assert.Equal("critico", h8.Nivel);
        Assert.Equal("Historicamente, às 8h chegam em média 1.5 carretas/h contra uma liberação de 1.0/h.", h8.Explicacao);

        var h10 = indice[10];
        Assert.Equal(0, h10.Lambda);
        Assert.Equal("normal", h10.Nivel);
    }

    [Fact]
    public void Distribuicao_de_janelas_soma_100_por_cento()
    {
        var registros = new List<RegistroMarcacao>
        {
            R("2026-05-01T00:00:00", "2026-05-01T10:00:00"), // D0
            R("2026-05-01T00:00:00", "2026-05-02T01:00:00"), // D1
            R("2026-05-01T00:00:00", "2026-05-04T01:00:00"), // D3
            R("2026-05-01T00:00:00", "2026-05-08T01:00:00"), // EC
        };

        var dist = MotorMetricas.DistribuicaoJanelas(registros);

        Assert.Equal(new[] { 1, 1, 0, 1, 0, 1 }, dist.Select(d => d.Total));
        Assert.Equal(100, dist.Sum(d => d.Pct), 6);
        Assert.Null(dist[^1].Max); // Infinity vira null no JSON
    }

    [Fact]
    public void Visao_por_terminal_calcula_ocupacao_pela_capacidade_do_terminal_e_semaforo()
    {
        var registros = new List<RegistroMarcacao>();
        for (var i = 0; i < 950; i++) registros.Add(R("2026-05-01T08:00:00", "2026-05-01T09:00:00"));
        for (var i = 0; i < 10; i++) registros.Add(R("2026-05-01T08:00:00", "2026-05-01T09:00:00", "hidrovias", "Hidrovias"));

        var visao = MotorMetricas.VisaoPorTerminal(registros, id => id == "hidrovias" ? 1000 : 1000);

        var tgpm = visao.Single(v => v.TerminalId == "tgpm");
        Assert.Equal(95, tgpm.OcupacaoPct, 6);
        Assert.Equal("critico", tgpm.Status);
        Assert.Equal("vermelho", tgpm.Semaforo);
        var hid = visao.Single(v => v.TerminalId == "hidrovias");
        Assert.Equal("normal", hid.Status);
        Assert.Equal("verde", hid.Semaforo);
    }

    [Fact]
    public void Espera_media_alta_deixa_terminal_em_atencao_mesmo_com_ocupacao_baixa()
    {
        var registros = new List<RegistroMarcacao> { R("2026-05-01T08:00:00", "2026-05-01T23:00:00") }; // 15h

        var v = Assert.Single(MotorMetricas.VisaoPorTerminal(registros));

        Assert.Equal("atencao", v.Status);
        Assert.Equal("amarelo", v.Semaforo);
    }

    [Fact]
    public void Concentracao_por_turno_conta_retencao_acima_de_24h()
    {
        var registros = new List<RegistroMarcacao>
        {
            R("2026-05-01T02:00:00", "2026-05-02T03:00:00"), // madrugada, retido
            R("2026-05-01T07:00:00", "2026-05-01T08:00:00"), // manhã
            R("2026-05-01T13:00:00", "2026-05-01T14:00:00"), // tarde
            R("2026-05-01T19:00:00", "2026-05-02T20:00:00"), // noite, retido
        };

        var turnos = MotorMetricas.ConcentracaoPorTurno(registros);

        Assert.Equal(new[] { 1, 1, 1, 1 }, turnos.Select(t => t.Total));
        Assert.Equal(new[] { 100.0, 0, 0, 100.0 }, turnos.Select(t => t.PctRetencao));
    }

    [Fact]
    public void Kpis_usam_primeiro_maximo_como_no_reduce_do_js()
    {
        var registros = new List<RegistroMarcacao>
        {
            R("2026-04-01T08:00:00", "2026-04-01T10:00:00"),
            R("2026-05-01T08:00:00", "2026-05-01T12:00:00"),
            R("2026-05-02T08:00:00", "2026-05-02T12:00:00"),
        };

        var kpis = Relatorio.ObterKpisGerais(registros)!;

        Assert.Equal(3, kpis.Total);
        Assert.Equal(3, kpis.DiasOperados);
        Assert.Equal(1, kpis.MediaDiaria);
        Assert.Equal("2026-05", kpis.MesMaisMovimentado.Chave);
        Assert.Equal("Maio/26", kpis.MesMaisMovimentado.Rotulo);
        Assert.Equal(registros[1].MovimentoId, kpis.MaiorAtraso.MovimentoId);
        Assert.Equal(10.0 / 3, kpis.TempoMedioEspera, 6);
    }

    [Fact]
    public void Formatar_horas_igual_ao_js()
    {
        Assert.Equal("3h 27min", Relatorio.FormatarHoras(3.45));
        Assert.Equal("5h", Relatorio.FormatarHoras(5));
    }
}

public class JsMathTests
{
    [Theory]
    // Valores conferidos com Number.prototype.toFixed no Node.
    [InlineData(6.25, 1, "6.3")]
    [InlineData(1.005, 2, "1.00")]
    [InlineData(0.05, 1, "0.1")]
    [InlineData(2.5, 0, "3")]
    [InlineData(11.0, 1, "11.0")]
    [InlineData(10.95, 1, "10.9")]
    [InlineData(0, 1, "0.0")]
    [InlineData(123.456, 0, "123")]
    [InlineData(-1.25, 1, "-1.3")]
    public void ToFixed_igual_ao_js(double valor, int casas, string esperado)
    {
        Assert.Equal(esperado, BarcaLog.Application.Metricas.JsMath.ToFixed(valor, casas));
    }
}
