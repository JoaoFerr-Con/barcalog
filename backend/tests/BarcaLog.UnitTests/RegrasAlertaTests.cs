using BarcaLog.Application.Metricas;
using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;

namespace BarcaLog.UnitTests;

public class RegrasAlertaTests
{
    private static RegistroMarcacao R(string terminal, DateTime m, double espera, string convenio = "HIDROVIAS - SOJA") =>
        new($"M{m.Ticks}{espera}", "s", convenio, "OP", "Soja", 1, m, m.AddHours(espera), espera, terminal, terminal);

    /// <summary>Um movimento por dia, do dia 1 ao último dia do mês, com a espera informada.</summary>
    private static IEnumerable<RegistroMarcacao> MesCheio(string terminal, int ano, int mes, double espera)
    {
        var d = new DateTime(ano, mes, 1, 10, 0, 0);
        for (; d.Month == mes; d = d.AddDays(1)) yield return R(terminal, d, espera);
    }

    private static EntradaAlertas Entrada(IEnumerable<RegistroMarcacao> registros, DateTime agora, DateTime? ultimaMarcacao = null,
        IReadOnlyList<(string, string, DateTime)>? abertas = null, IReadOnlyList<(string, string, TimeOnly)>? atrasados = null)
    {
        var lista = registros.ToList();
        return new EntradaAlertas(lista, [("hidrovias", "Hidrovias")], new ParametrosOperacionais(),
            new Dictionary<string, DateTime?> { ["hidrovias"] = ultimaMarcacao ?? (lista.Count == 0 ? null : lista.Max(r => r.MarcadoEm)) },
            abertas ?? [], atrasados ?? [], agora);
    }

    [Fact]
    public void Ultimo_mes_completo_ignora_mes_em_andamento()
    {
        var registros = MesCheio("hidrovias", 2026, 8, 5).Append(R("hidrovias", new DateTime(2026, 9, 1, 19, 0, 0), 1)).ToList();
        Assert.Equal(new DateTime(2026, 8, 1), RegrasAlerta.UltimoMesCompleto(registros));
        Assert.Equal(new DateTime(2026, 8, 1), RegrasAlerta.UltimoMesCompleto(MesCheio("hidrovias", 2026, 8, 5).ToList()));
        Assert.Null(RegrasAlerta.UltimoMesCompleto([R("hidrovias", new DateTime(2026, 8, 10), 1)]));
    }

    [Fact]
    public void Espera_mediana_acima_do_limite_gera_alerta_historico_com_comparacao()
    {
        var registros = MesCheio("hidrovias", 2026, 7, 7.5).Concat(MesCheio("hidrovias", 2026, 8, 13.93));
        var alertas = RegrasAlerta.Avaliar(Entrada(registros, new DateTime(2026, 9, 2, 9, 0, 0)));

        var a = Assert.Single(alertas, x => x.Regra == "espera-mediana");
        Assert.Equal("espera-mediana:hidrovias:2026-08", a.Chave);
        Assert.Equal(TipoAlerta.Historico, a.Tipo);
        Assert.Equal(PrioridadeAlerta.Alta, a.Prioridade); // subiu mais de 50% sobre julho
        Assert.Equal("13h56", a.ValorObservado);
        Assert.Equal("12h00", a.Limite);
        Assert.Equal("agosto de 2026", a.Periodo);
        Assert.Contains("contra 7h30 em julho de 2026", a.Evidencia);
    }

    [Fact]
    public void Espera_mediana_dentro_do_limite_nao_gera_alerta()
    {
        var alertas = RegrasAlerta.Avaliar(Entrada(MesCheio("hidrovias", 2026, 8, 11.99), new DateTime(2026, 9, 1, 9, 0, 0)));
        Assert.DoesNotContain(alertas, x => x.Regra == "espera-mediana");
    }

    [Fact]
    public void Esperas_longas_descrevem_o_maior_grupo_liberado_junto()
    {
        var lote = Enumerable.Range(0, 12).Select(i => R("hidrovias", new DateTime(2026, 8, 12, 11, 55, 0).AddMinutes(i), 101, "UNITAPAJOS AMAGGI MILHO"));
        var registros = MesCheio("hidrovias", 2026, 8, 5).Concat(lote);
        var a = Assert.Single(RegrasAlerta.Avaliar(Entrada(registros, new DateTime(2026, 9, 1, 9, 0, 0))), x => x.Regra == "esperas-longas");
        Assert.Equal(PrioridadeAlerta.Alta, a.Prioridade);
        Assert.Equal("12", a.ValorObservado);
        Assert.Contains("Maior grupo: 12 carretas de UNITAPAJOS AMAGGI MILHO", a.Evidencia);
        Assert.Contains("Maior espera: 101h00", a.Evidencia);
    }

    [Fact]
    public void Dados_desatualizados_depois_do_limite_de_dias()
    {
        var registros = MesCheio("hidrovias", 2026, 8, 5).ToList();
        var ultima = new DateTime(2026, 8, 31, 10, 0, 0);
        Assert.DoesNotContain(RegrasAlerta.Avaliar(Entrada(registros, new DateTime(2026, 9, 2, 8, 0, 0), ultima)), x => x.Tipo == TipoAlerta.Dados);
        var a = Assert.Single(RegrasAlerta.Avaliar(Entrada(registros, new DateTime(2026, 10, 20, 8, 0, 0), ultima)), x => x.Tipo == TipoAlerta.Dados);
        Assert.Equal("dados-desatualizados:hidrovias:20260831", a.Chave);
        Assert.Equal("50", a.ValorObservado);
        Assert.Equal(PrioridadeAlerta.Alta, a.Prioridade); // mais de 30 dias
    }

    [Fact]
    public void Previsao_so_para_janelas_de_hoje_em_risco_de_fila()
    {
        // Últimos 30 dias: às 16h chegam 10/dia e saem 4/dia; às 17h chegam 2/dia (volume baixo); às 18h 6 chegam e 6 saem.
        var registros = new List<RegistroMarcacao>();
        var inicio = new DateTime(2026, 9, 1);
        for (var d = 0; d < 30; d++)
        {
            var dia = inicio.AddDays(d);
            for (var i = 0; i < 10; i++) registros.Add(new RegistroMarcacao($"a{d}{i}", "s", "C", "O", "C", 1, dia.AddHours(16), dia.AddHours(i < 4 ? 16.5 : 20), i < 4 ? 0.5 : 4, "hidrovias", "Hidrovias"));
            for (var i = 0; i < 2; i++) registros.Add(new RegistroMarcacao($"b{d}{i}", "s", "C", "O", "C", 1, dia.AddHours(17), dia.AddHours(23), 6, "hidrovias", "Hidrovias"));
            for (var i = 0; i < 6; i++) registros.Add(new RegistroMarcacao($"c{d}{i}", "s", "C", "O", "C", 1, dia.AddHours(18), dia.AddHours(18.5), 0.5, "hidrovias", "Hidrovias"));
        }
        var alertas = RegrasAlerta.Avaliar(Entrada(registros, new DateTime(2026, 10, 20, 15, 30, 0), new DateTime(2026, 10, 19)));
        var a = Assert.Single(alertas, x => x.Tipo == TipoAlerta.Previsao);
        Assert.Equal("risco-horario:hidrovias:2026-10-20:16", a.Chave);
        Assert.Equal("hidrovias", a.TerminalId);
        Assert.Equal("hoje, 16h–17h", a.Periodo);
        Assert.Equal("Risco de fila às 16h", a.Titulo);
        Assert.Equal("10,0 carretas previstas", a.ValorObservado);
        Assert.Equal(PrioridadeAlerta.Alta, a.Prioridade); // 10 previstas ≥ 2 × 4 atendidas
        Assert.Contains("Estimativa pelo padrão histórico, não é ocorrência", a.Evidencia);
        // Cabe nas colunas (um texto longo demais derrubava a avaliação inteira com erro 500).
        Assert.True(a.Limite.Length <= Alerta.Tamanho.Limite, a.Limite);
        Assert.True(a.ValorObservado.Length <= Alerta.Tamanho.ValorObservado, a.ValorObservado);
        Assert.True(a.Indicador.Length <= Alerta.Tamanho.Indicador, a.Indicador);

        // Às 23h o dia acaba: nada de previsão para "amanhã".
        Assert.DoesNotContain(RegrasAlerta.Avaliar(Entrada(registros, new DateTime(2026, 10, 20, 23, 10, 0), new DateTime(2026, 10, 19))), x => x.Tipo == TipoAlerta.Previsao);
    }

    [Fact]
    public void Operacionais_vem_de_registros_reais_abertos_e_da_agenda()
    {
        var agora = new DateTime(2026, 10, 20, 15, 0, 0);
        var alertas = RegrasAlerta.Avaliar(Entrada([], agora, new DateTime(2026, 10, 20),
            abertas: [("M1", "hidrovias", agora.AddHours(-80))],
            atrasados: [("TNC-2290", "hidrovias", new TimeOnly(11, 0))]));
        var aberto = Assert.Single(alertas, x => x.Regra == "movimento-aberto");
        Assert.Equal(TipoAlerta.Operacional, aberto.Tipo);
        Assert.Contains("80h00 atrás", aberto.Evidencia);
        var agenda = Assert.Single(alertas, x => x.Regra == "agendamento-atrasado");
        Assert.Contains("TNC-2290 (11:00)", agenda.Evidencia);
        Assert.Equal("Situação registrada manualmente na Agenda", agenda.Origem);
    }

    [Theory]
    [InlineData(13.9333, "13h56")]
    [InlineData(0.0, "0h00")]
    [InlineData(101.1667, "101h10")]
    [InlineData(23.999, "24h00")]
    public void Duracao_no_formato_do_painel(double horas, string texto)
    {
        Assert.Equal(texto, RegrasAlerta.Duracao(horas));
    }

    [Fact]
    public void Texto_maior_que_a_coluna_e_ajustado_em_vez_de_derrubar_a_avaliacao()
    {
        var c = new AlertaCandidato("k", TipoAlerta.Previsao, "r", PrioridadeAlerta.Media, null, "hoje", "t", "i",
            new string('9', 80), new string('x', 120), "e", "o", null);
        var a = new Alerta();
        BarcaLog.Application.Servicos.AlertasServico.Copiar(c, a);
        Assert.Equal(Alerta.Tamanho.Limite, a.Limite.Length);
        Assert.EndsWith("…", a.Limite);
        Assert.Equal(Alerta.Tamanho.ValorObservado, a.ValorObservado.Length);
    }
}
