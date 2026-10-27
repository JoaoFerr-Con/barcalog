using BarcaLog.Application.Metricas;
using BarcaLog.Application.Relatorios;
using BarcaLog.Domain.Enums;

namespace BarcaLog.UnitTests;

public class RelatoriosTests
{
    private static int _seq;

    private static RegistroAnalise R(string marc, double? espera, string terminal = "tgpm", string carga = "Soja")
    {
        var m = DateTime.Parse(marc);
        DateTime? l = espera is { } e ? m.AddHours(e) : null;
        return new RegistroAnalise($"R{Interlocked.Increment(ref _seq)}", "S", terminal, terminal.ToUpperInvariant(), $"{terminal.ToUpperInvariant()} - {carga.ToUpperInvariant()}",
            null, terminal.ToUpperInvariant(), carga, m, l, espera, OrigemMarcacao.Planilha);
    }

    [Fact]
    public void Faixas_de_espera_batem_com_liberadas_ate_24h_e_esperas_longas()
    {
        var regs = new[] { 0, 6, 6.01, 12, 24, 24.01, 48, 72, 72.01, 100 }.Select(e => R("2026-04-01 08:00", e)).Append(R("2026-04-01 09:00", null)).ToList();
        var f = CalculosRelatorio.DistribuicaoEspera(regs);
        Assert.Equal([2, 2, 1, 2, 1, 2], f.Select(x => x.Quantidade));
        // As três primeiras faixas = liberadas em até 24 h (≤ 24); a última = acima de 72 h.
        Assert.Equal(regs.Count(r => r.EsperaHoras <= 24), f.Take(3).Sum(x => x.Quantidade));
        Assert.Equal(regs.Count(r => r.EsperaHoras > 72), f[^1].Quantidade);
    }

    [Fact]
    public void Mes_so_e_completo_quando_todos_os_terminais_cobrem_o_mes_inteiro()
    {
        var cob = new List<CoberturaTerminal>
        {
            new("tgpm", "TGPM", new DateOnly(2026, 1, 1), new DateOnly(2026, 9, 30)),
            new("hidrovias", "Hidrovias", new DateOnly(2025, 12, 29), new DateOnly(2026, 9, 1))
        };
        Assert.True(CalculosRelatorio.MesCompleto("2026-08", cob));
        Assert.False(CalculosRelatorio.MesCompleto("2026-09", cob));   // Hidrovias para em 01/09
        Assert.False(CalculosRelatorio.MesCompleto("2025-12", cob));   // TGPM começa em janeiro
    }

    [Fact]
    public void Historico_separa_soja_milho_e_outras_e_marca_mes_cortado_pelo_periodo()
    {
        var regs = new List<RegistroAnalise> { R("2026-03-02 10:00", 2), R("2026-03-03 10:00", 2, carga: "Milho"), R("2026-03-04 10:00", 2, carga: "Sorgo"), R("2026-04-02 10:00", 3) };
        var cob = new List<CoberturaTerminal> { new("tgpm", "TGPM", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)) };
        var h = CalculosRelatorio.HistoricoMensal(regs, new DateOnly(2026, 4, 15), cob);
        Assert.Equal(2, h.Count);
        Assert.Equal((1, 1, 1), (h[0].Soja, h[0].Milho, h[0].Outras));
        Assert.False(h[0].Parcial);
        Assert.True(h[1].Parcial); // abril cortado no dia 15
    }

    [Fact]
    public void Projecao_so_aparece_quando_o_teste_com_o_historico_passa()
    {
        static MesHistorico M(string k, int n) => new(k, k, n, n, 0, 0, null, false);
        var estavel = new[] { M("2026-01", 1000), M("2026-02", 1010), M("2026-03", 990), M("2026-04", 1005), M("2026-05", 995), M("2026-06", 1000), M("2026-07", 1002) };
        var p = CalculosRelatorio.Projecao(estavel);
        Assert.True(p.Disponivel);
        Assert.Equal("Agosto de 2026", p.MesEstimado);
        Assert.InRange(p.Estimativa!.Value, 990, 1010);

        var safra = new[] { M("2026-01", 3728), M("2026-02", 13021), M("2026-03", 15841), M("2026-04", 20960), M("2026-05", 19576), M("2026-06", 15008), M("2026-07", 15506), M("2026-08", 12607) };
        var q = CalculosRelatorio.Projecao(safra);
        Assert.False(q.Disponivel);
        Assert.True(q.ErroMedioPct > CalculosRelatorio.ErroMaximoProjecaoPct);
        Assert.Null(q.Estimativa);

        Assert.False(CalculosRelatorio.Projecao(safra.Take(4).ToList()).Disponivel); // poucos meses para testar
    }

    [Fact]
    public void Dias_acima_da_referencia_comparam_por_terminal_sem_somar_referencias()
    {
        var regs = Enumerable.Range(0, 5).Select(_ => R("2026-04-10 08:00", 1)).Concat(Enumerable.Range(0, 4).Select(_ => R("2026-04-10 09:00", 1, "hidrovias"))).ToList();
        var dias = CalculosRelatorio.DiasPorTerminal(regs, id => (3, false));
        Assert.All(dias, d => Assert.True(d.Entradas > d.Referencia));
        Assert.Equal(2, dias.Count);
        Assert.Equal(5 * 100.0 / 3, dias[0].PctReferencia, 3);
    }

    [Fact]
    public void Duracao_segue_o_formato_da_tela()
    {
        Assert.Equal("13h44", Fmt.Dur(13 + 44 / 60.0));
        Assert.Equal("0h00", Fmt.Dur(0));
        Assert.Equal("—", Fmt.Dur(null));
        Assert.Equal("Abril de 2026", Fmt.Periodo(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)));
        Assert.Equal("01/04/2026 a 15/04/2026", Fmt.Periodo(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 15)));
    }
}
