using BarcaLog.Application.Metricas;
using BarcaLog.Application.Seguranca;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;
using BarcaLog.Domain.Regras;

namespace BarcaLog.UnitTests;

public class AnaliseMovimentacoesTests
{
    private static int _seq;

    private static RegistroAnalise R(string marc, string? lib, string terminal = "tgpm", string convenio = "TGPM - SOJA", string? cod = null,
        string senha = "S1", OrigemMarcacao origem = OrigemMarcacao.Planilha)
    {
        var m = DateTime.Parse(marc);
        DateTime? l = lib is null ? null : DateTime.Parse(lib);
        return new RegistroAnalise($"M{Interlocked.Increment(ref _seq)}", senha, terminal, terminal.ToUpperInvariant(), convenio, cod,
            convenio.Split(" - ")[0], convenio.Split(" - ")[^1], m, l, l is { } x ? (x - m).TotalHours : null, origem);
    }

    [Fact]
    public void Periodo_anterior_tem_a_mesma_duracao_e_termina_na_vespera()
    {
        var (de, ate) = AnaliseMovimentacoes.PeriodoAnterior(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        Assert.Equal(new DateOnly(2026, 1, 29), de);
        Assert.Equal(new DateOnly(2026, 2, 28), ate);
    }

    [Theory]
    [InlineData(1, "dia")]
    [InlineData(62, "dia")]
    [InlineData(63, "semana")]
    [InlineData(182, "semana")]
    [InlineData(183, "mes")]
    public void Agrupamento_automatico_pela_duracao(int dias, string esperado) =>
        Assert.Equal(esperado, AnaliseMovimentacoes.AgrupamentoPadrao(dias));

    [Fact]
    public void Resumo_conta_entradas_pela_marcacao_e_saidas_pela_liberacao()
    {
        var regs = new[]
        {
            R("2026-03-01 08:00", "2026-03-01 10:00"),            // 2 h
            R("2026-03-02 08:00", "2026-03-03 12:00", cod: "C1"), // 28 h
            R("2026-03-03 09:00", null),                          // pendente
            R("2026-02-28 22:00", "2026-03-01 02:00"),            // entrou antes, saiu no período
        };
        var a = AnaliseMovimentacoes.Calcular(regs, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 3), null, 24, 10);
        Assert.Equal(3, a.Atual.Entradas);
        Assert.Equal(3, a.Atual.Saidas);
        Assert.Equal(2, a.Atual.Concluidas);
        Assert.Equal(1, a.Atual.Pendentes);
        Assert.Equal(15, a.Atual.EsperaMediana);
        Assert.Equal(1, a.Atual.EsperasLongas);
        Assert.Equal(50, a.Atual.LiberadasAte24hPct);
        Assert.Equal(1.0, a.Atual.EntradasPorDia);
        Assert.Equal(1, a.Anterior!.Entradas);
        Assert.Equal(2, a.Qualidade.SemCodigoConvenio);
        Assert.Equal(1, a.Qualidade.SemLiberacao);
    }

    [Fact]
    public void Sem_registros_no_periodo_anterior_nao_ha_comparacao()
    {
        var a = AnaliseMovimentacoes.Calcular([R("2026-03-01 08:00", "2026-03-01 10:00")], new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1), null, 72, 10);
        Assert.Null(a.Anterior);
    }

    [Fact]
    public void Remarcacao_e_repeticao_em_ate_10_min_com_mesma_senha_convenio_e_liberacao()
    {
        var regs = new[]
        {
            R("2026-03-01 08:00", "2026-03-01 12:00", senha: "X"),
            R("2026-03-01 08:09", "2026-03-01 12:00", senha: "X"),   // repetida
            R("2026-03-01 08:30", "2026-03-01 12:00", senha: "X"),   // 21 min depois: não é remarcação
            R("2026-03-01 08:05", "2026-03-01 12:00", senha: "Y"),   // outra senha
        };
        Assert.Single(AnaliseMovimentacoes.Remarcacoes(regs, 10));
    }

    [Fact]
    public void Serie_semanal_marca_pontos_parciais_nas_pontas()
    {
        // 04/03/2026 é quarta; 20/05/2026 é quarta → primeira e última semanas parciais.
        var a = AnaliseMovimentacoes.Calcular([R("2026-03-04 08:00", "2026-03-04 09:00")], new DateOnly(2026, 3, 4), new DateOnly(2026, 5, 20), null, 72, 10);
        Assert.Equal("semana", a.Agrupamento);
        Assert.True(a.Serie[0].Parcial);
        Assert.Equal(new DateOnly(2026, 3, 4), a.Serie[0].De);
        Assert.False(a.Serie[1].Parcial);
        Assert.True(a.Serie[^1].Parcial);
        Assert.Equal(1, a.Serie.Sum(p => p.Entradas));
    }

    [Fact]
    public void Dimensao_com_muitos_grupos_soma_o_resto_em_outros()
    {
        var regs = Enumerable.Range(0, 15).Select(i => R("2026-03-01 08:00", "2026-03-01 09:00", convenio: $"OP{i:00} - SOJA")).ToList();
        var a = AnaliseMovimentacoes.Calcular(regs, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1), null, 72, 10);
        var conv = a.Dimensoes.Single(d => d.Dimensao == "convenio");
        Assert.Equal(AnaliseMovimentacoes.GruposPorDimensao, conv.Grupos.Count);
        Assert.Equal(3, conv.Outros!.Entradas);
        Assert.Equal(15, conv.Grupos.Sum(g => g.Entradas) + conv.Outros.Entradas);
    }

    [Fact]
    public void Media_por_dia_da_semana_divide_pelas_ocorrencias_do_dia_no_periodo()
    {
        // 01 a 14/03/2026: dois domingos (01 e 08); 3 entradas em domingos → 1,5 por domingo.
        var regs = new[] { R("2026-03-01 08:00", null), R("2026-03-01 09:00", null), R("2026-03-08 08:00", null) };
        var a = AnaliseMovimentacoes.Calcular(regs, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 14), null, 72, 10);
        Assert.Equal(1.5, a.PorDiaSemana[0].EntradasPorDia);
        Assert.Equal(0, a.PorDiaSemana[1].EntradasPorDia);
    }
}

public class EscopoTerminalTests
{
    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, "tgpm", "tgpm")]
    [InlineData("tgpm", null, "tgpm")]
    [InlineData("tgpm", " ", "tgpm")]
    [InlineData("tgpm", "TGPM", "tgpm")]
    [InlineData("tgpm", "todos", "tgpm")]
    [InlineData(null, "todos", "todos")]
    public void Sem_conflito_devolve_o_terminal_efetivo(string? vinculo, string? pedido, string? esperado) =>
        Assert.Equal(esperado, EscopoTerminal.Restringir(vinculo, pedido));

    [Fact]
    public void Pedido_de_outro_terminal_e_negado() =>
        Assert.Throws<AcessoNegadoException>(() => EscopoTerminal.Restringir("tgpm", "hidrovias"));

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, "tgpm", true)]
    [InlineData("tgpm", "tgpm", true)]
    [InlineData("tgpm", "hidrovias", false)]
    [InlineData("tgpm", null, false)] // registro geral soma outros terminais
    public void Visibilidade_de_registro(string? vinculo, string? terminal, bool ve) =>
        Assert.Equal(ve, EscopoTerminal.PodeVer(vinculo, terminal));

    [Fact]
    public void Tela_padrao_por_papel_e_telas_validas()
    {
        Assert.Equal("visao-geral", PreferenciasUsuario.TelaPadrao(PapelUsuario.Gestor));
        Assert.Equal("gargalos", PreferenciasUsuario.TelaPadrao(PapelUsuario.Operador));
        Assert.Equal("movimentacoes", PreferenciasUsuario.TelaPadrao(PapelUsuario.Auditor));
        Assert.All(Enum.GetValues<PapelUsuario>().Where(p => p != PapelUsuario.Transportadora),
            p => Assert.Contains(PreferenciasUsuario.TelaPadrao(p), PreferenciasUsuario.Telas));
    }
}
