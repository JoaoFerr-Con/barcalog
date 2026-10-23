using BarcaLog.Application.Metricas;

namespace BarcaLog.UnitTests;

public class EstimativaJanelasTests
{
    private const double Limite = 1.4;

    [Theory]
    [InlineData(4.9, 0, "dentro")]      // pouco movimento: abaixo da demanda mínima relevante
    [InlineData(20.5, 20, "dentro")]    // excesso menor que 1 carreta
    [InlineData(23, 20, "atencao")]     // acima, mas abaixo de 1,4×
    [InlineData(28, 20, "risco")]       // 28 ≥ 20 × 1,4
    [InlineData(6, 0, "risco")]         // nada costuma ser liberado nesta hora
    [InlineData(15, 20, "dentro")]
    public void Classificacao_por_demanda_e_capacidade(double demanda, double capacidade, string esperado) =>
        Assert.Equal(esperado, EstimativaJanelas.Classificar(demanda, capacidade, Limite));

    /// <summary>Cria N dias de registros: por dia, chegadas às horaChegada liberadas às horaLiberacao.</summary>
    private static IEnumerable<RegistroMarcacao> Dias(string terminal, DateTime inicio, int dias, int porDia, int horaChegada, int horaLiberacao, int pular = 0) =>
        Enumerable.Range(0, dias).Where(d => pular == 0 || d % pular != 0).SelectMany(d => Enumerable.Range(0, porDia).Select(i =>
        {
            var dia = inicio.AddDays(d);
            var m = dia.AddHours(horaChegada).AddMinutes(i % 60);
            var l = dia.AddHours(horaLiberacao).AddMinutes(30);
            return new RegistroMarcacao($"{terminal}{d}-{horaChegada}-{i}", "s", "C", "O", "C", 1, m, l, (l - m).TotalHours, terminal, terminal);
        }));

    [Fact]
    public void Demanda_e_media_por_dia_com_dados_e_risco_aparece_na_mensagem()
    {
        // 30 dias: às 10h chegam 12 e às 10h saem 6 liberações (de chegadas das 9h).
        var inicio = new DateTime(2026, 9, 1);
        var regs = Dias("tgpm", inicio, 30, 12, 10, 14).Concat(Dias("tgpm", inicio, 30, 6, 9, 10)).ToList();
        var (_, porTerminal) = EstimativaJanelas.Calcular(regs, [new("tgpm", "TGPM", null, null)], new DateTime(2026, 10, 1, 10, 5, 0), Limite, 2);
        var t = Assert.Single(porTerminal);
        Assert.Equal(Confiabilidade.Confiavel, t.Confiabilidade); // padrão constante: erro 0, igual ao ingênuo
        Assert.Equal(0, t.Erro!.ErroPct);
        var j = t.Janelas[0];
        Assert.Equal(10, j.Hora);
        Assert.Equal(12, j.DemandaPrevista);
        Assert.Equal(6, j.CapacidadeAtendimento);
        Assert.Equal("observada", j.FonteCapacidade);
        Assert.Equal(SituacaoJanela.Risco, j.Situacao);
        Assert.Equal(-6, j.Saldo);
        Assert.Contains("cerca de 6 carretas a mais", j.Mensagem);
        Assert.Equal(10, t.Resumo.HoraMaisCritica);
        Assert.Equal("Pressão maior agora (10h–11h), diminuindo nas horas seguintes.", t.Resumo.Tendencia);
    }

    [Fact]
    public void Capacidade_cadastrada_tem_prioridade_sobre_o_atendimento_observado()
    {
        var inicio = new DateTime(2026, 9, 1);
        var regs = Dias("tgpm", inicio, 30, 12, 10, 14).Concat(Dias("tgpm", inicio, 30, 6, 9, 10)).ToList();
        var (_, porTerminal) = EstimativaJanelas.Calcular(regs, [new("tgpm", "TGPM", 15, null)], new DateTime(2026, 10, 1, 10, 5, 0), Limite, 2);
        var j = porTerminal[0].Janelas[0];
        Assert.Equal("cadastrada", j.FonteCapacidade);
        Assert.Equal(15, j.CapacidadeAtendimento);
        Assert.Equal(6, j.AtendimentoObservado);
        Assert.Equal(SituacaoJanela.Dentro, j.Situacao);
        Assert.Contains("acima do que costuma ser liberado", j.Mensagem);
    }

    [Fact]
    public void Dias_sem_dados_nao_diluem_a_media_mas_reduzem_a_confiabilidade()
    {
        // 30 dias de calendário, um em cada 3 sem nenhum registro (20 dias com dados), 12 chegadas/dia.
        var regs = Dias("hidrovias", new DateTime(2026, 9, 1), 30, 12, 10, 11, pular: 3).ToList();
        var (_, porTerminal) = EstimativaJanelas.Calcular(regs, [new("hidrovias", "Hidrovias", null, null)], new DateTime(2026, 10, 1, 10, 0, 0), Limite, 2);
        var t = porTerminal[0];
        Assert.Equal(20, t.DiasComDados);
        Assert.Equal(12, t.Janelas[0].DemandaPrevista); // não 8 (= 240 ÷ 30)
        Assert.Equal(Confiabilidade.Limitada, t.Confiabilidade);
        Assert.Contains(t.Motivos, m => m.Codigo == "base-incompleta" && m.Descricao.StartsWith("20 dias"));
    }

    [Fact]
    public void Poucos_dias_nao_estimam_e_dados_antigos_limitam_a_confiabilidade()
    {
        var poucos = Dias("tgpm", new DateTime(2026, 9, 1), 5, 12, 10, 11).ToList();
        var (_, a) = EstimativaJanelas.Calcular(poucos, [new("tgpm", "TGPM", null, null)], new DateTime(2026, 9, 6, 10, 0, 0), Limite, 2);
        Assert.Equal(Confiabilidade.Insuficiente, a[0].Confiabilidade);
        Assert.All(a[0].Janelas, j => Assert.Equal(SituacaoJanela.Insuficiente, j.Situacao));
        Assert.Equal("Sem dados suficientes para estimar esta janela.", a[0].Janelas[0].Mensagem);

        var antigos = Dias("tgpm", new DateTime(2026, 7, 1), 30, 12, 10, 11).ToList();
        var (_, b) = EstimativaJanelas.Calcular(antigos, [new("tgpm", "TGPM", null, null)], new DateTime(2026, 10, 1, 10, 0, 0), Limite, 2);
        Assert.True(b[0].Desatualizado);
        Assert.Equal(Confiabilidade.Desatualizada, b[0].Confiabilidade);
        var m = Assert.Single(b[0].Motivos, x => x.Codigo == "desatualizado");
        Assert.Contains("Importar", m.Acao);
    }

    [Fact]
    public void Todos_os_terminais_somam_numeros_mas_mostram_o_pior_terminal()
    {
        var inicio = new DateTime(2026, 9, 1);
        var regs = Dias("tgpm", inicio, 30, 12, 10, 14).Concat(Dias("tgpm", inicio, 30, 6, 9, 10))     // TGPM: 12 × 6 → risco
            .Concat(Dias("hidrovias", inicio, 30, 6, 10, 10)).Concat(Dias("hidrovias", inicio, 30, 20, 8, 10)) // Hidrovias: 6 × 26 → folga
            .ToList();
        var (geral, _) = EstimativaJanelas.Calcular(regs, [new("tgpm", "TGPM", null, null), new("hidrovias", "Hidrovias", null, null), new("unitapajos", "Unitapajós", null, null)],
            new DateTime(2026, 10, 1, 10, 0, 0), Limite, 2);
        var j = geral.Janelas[0];
        Assert.Equal(18, j.DemandaPrevista);
        Assert.Equal(32, j.CapacidadeAtendimento);
        Assert.Equal(SituacaoJanela.Risco, j.Situacao); // a soma tem folga, mas TGPM não
        Assert.Equal(["TGPM"], j.TerminaisEmRisco);
        Assert.StartsWith("Risco de fila em TGPM.", j.Mensagem);
        Assert.Equal(Confiabilidade.Limitada, geral.Confiabilidade); // Unitapajós sem dados, fora da soma
        Assert.Contains(geral.Motivos, m => m.Terminal == "Unitapajós" && m.Codigo == "sem-base");
    }
}

public class TempoTipicoTests
{
    [Fact]
    public void Tempo_tipico_e_a_mediana_dos_ultimos_30_dias_de_dados_do_terminal()
    {
        static RegistroMarcacao R(DateTime m, double h) => new($"m{m:yyyyMMddHHmm}{h}", "s", "C", "O", "C", 1, m, m.AddHours(h), h, "tgpm", "TGPM");
        var regs = new List<RegistroMarcacao>
        {
            R(new DateTime(2026, 6, 1, 8, 0, 0), 500),  // fora da base (mais de 30 dias antes do último dia)
            R(new DateTime(2026, 9, 10, 8, 0, 0), 2),
            R(new DateTime(2026, 9, 20, 8, 0, 0), 4),
            R(new DateTime(2026, 9, 30, 8, 0, 0), 90),  // espera longa não puxa a mediana como puxaria a média
        };
        var t = Assert.Single(BarcaLog.Application.Servicos.PortariaServico.TemposTipicos(regs, [new BarcaLog.Domain.Entidades.Terminal { Id = "tgpm", Nome = "TGPM" }]));
        Assert.Equal(4, t.MedianaHoras);
        Assert.Equal(3, t.Liberadas);
        Assert.Equal(new DateOnly(2026, 9, 1), t.BaseDe);
        Assert.Equal(new DateOnly(2026, 9, 30), t.BaseAte);
    }
}

public class ErroMedidoTests
{
    [Fact]
    public void Erro_compara_a_media_com_repetir_o_dia_anterior()
    {
        // Dias alternados: 10 e 20 chegadas às 10h. A média (~15) erra ~5 por dia; repetir o dia anterior erra 10.
        var inicio = new DateTime(2026, 8, 1);
        var regs = Enumerable.Range(0, 60).SelectMany(d => Enumerable.Range(0, d % 2 == 0 ? 10 : 20).Select(i =>
        {
            var m = inicio.AddDays(d).AddHours(10).AddMinutes(i);
            return new RegistroMarcacao($"e{d}-{i}", "s", "C", "O", "C", 1, m, m.AddHours(1), 1, "tgpm", "TGPM");
        })).ToList();
        var e = EstimativaJanelas.MedirErro(regs)!;
        Assert.Equal(30, e.DiasAvaliados);
        Assert.InRange(e.ErroPct, 32, 35);
        Assert.InRange(e.ErroIngenuoPct, 66, 67);
        Assert.True(e.NaoPiorQueIngenua);
    }

    [Fact]
    public void Sem_dias_anteriores_suficientes_o_erro_nao_e_medido()
    {
        var regs = Enumerable.Range(0, 5).Select(d => new RegistroMarcacao($"x{d}", "s", "C", "O", "C", 1,
            new DateTime(2026, 8, 1).AddDays(d).AddHours(9), new DateTime(2026, 8, 1).AddDays(d).AddHours(10), 1, "tgpm", "TGPM")).ToList();
        Assert.Null(EstimativaJanelas.MedirErro(regs));
    }
}
