using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;
using BarcaLog.Domain.Regras;

namespace BarcaLog.UnitTests;

public class RegrasNegativacaoTests
{
    private static Veiculo NovoVeiculo(string placa = "NGL-3021", StatusNegativacao status = StatusNegativacao.Regular) =>
        new() { Id = 1, Placa = placa, TransportadoraId = 1, TerminalId = "unitapajos", StatusNegativacao = status };

    private static Ocorrencia NovaOcorrencia(NivelOcorrencia nivel, string placa = "NGL-3021") =>
        new() { Id = 10, Nivel = nivel, Placa = placa, TransportadoraId = 1, Descricao = "teste", Status = StatusOcorrencia.Ativa };

    [Fact]
    public void N3_bloqueia_automaticamente_o_veiculo()
    {
        var veiculo = NovoVeiculo();

        RegrasNegativacao.AplicarOcorrencia(NovaOcorrencia(NivelOcorrencia.N3), veiculo, condutor: null);

        Assert.Equal(StatusNegativacao.Negativada, veiculo.StatusNegativacao);
    }

    [Fact]
    public void N3_bloqueia_tambem_o_condutor_identificado()
    {
        var veiculo = NovoVeiculo();
        var condutor = new Condutor { Id = 5, Nome = "José Ribeiro", TransportadoraId = 1 };

        RegrasNegativacao.AplicarOcorrencia(NovaOcorrencia(NivelOcorrencia.N3), veiculo, condutor);

        Assert.Equal(StatusNegativacao.Negativada, condutor.StatusNegativacao);
    }

    [Theory]
    [InlineData(NivelOcorrencia.N1)]
    [InlineData(NivelOcorrencia.N2)]
    public void N1_e_N2_nao_bloqueiam(NivelOcorrencia nivel)
    {
        var veiculo = NovoVeiculo();
        var condutor = new Condutor { Id = 5, Nome = "Marcos", TransportadoraId = 1 };

        RegrasNegativacao.AplicarOcorrencia(NovaOcorrencia(nivel), veiculo, condutor);

        Assert.Equal(StatusNegativacao.Regular, veiculo.StatusNegativacao);
        Assert.Equal(StatusNegativacao.Regular, condutor.StatusNegativacao);
    }

    [Fact]
    public void Transportadora_sem_veiculos_negativados_e_regular()
    {
        var frota = new[] { NovoVeiculo("AAA-0001"), NovoVeiculo("AAA-0002") };

        Assert.Equal(StatusNegativacao.Regular, RegrasNegativacao.CalcularStatusTransportadora(frota));
        Assert.True(RegrasNegativacao.EstaAptaParaOperar(frota));
    }

    [Fact]
    public void Transportadora_com_um_veiculo_negativado_e_negativada()
    {
        var frota = new[] { NovoVeiculo("AAA-0001"), NovoVeiculo("AAA-0002", StatusNegativacao.Negativada), NovoVeiculo("AAA-0003") };

        Assert.Equal(StatusNegativacao.Negativada, RegrasNegativacao.CalcularStatusTransportadora(frota));
        Assert.False(RegrasNegativacao.EstaAptaParaOperar(frota));
    }

    [Fact]
    public void Transportadora_sem_frota_e_regular()
    {
        Assert.Equal(StatusNegativacao.Regular, RegrasNegativacao.CalcularStatusTransportadora(Array.Empty<Veiculo>()));
    }

    [Fact]
    public void Status_da_transportadora_acompanha_a_frota_apos_N3_e_aprovacao()
    {
        var alvo = NovoVeiculo("NGL-3021");
        var outro = NovoVeiculo("NGL-9999");
        var frota = new[] { alvo, outro };
        var ocorrencia = NovaOcorrencia(NivelOcorrencia.N3);

        RegrasNegativacao.AplicarOcorrencia(ocorrencia, alvo, null);
        Assert.Equal(StatusNegativacao.Negativada, RegrasNegativacao.CalcularStatusTransportadora(frota));

        var contestacao = new Contestacao { Id = 1, OcorrenciaId = ocorrencia.Id, TransportadoraId = 1, Justificativa = "pane" };
        RegrasNegativacao.AbrirContestacao(contestacao, ocorrencia);
        RegrasNegativacao.ResponderContestacao(contestacao, ocorrencia, aprovada: true, "ok", DateTime.UtcNow, alvo, null);

        Assert.Equal(StatusNegativacao.Regular, RegrasNegativacao.CalcularStatusTransportadora(frota));
    }

    [Fact]
    public void Aprovar_contestacao_regulariza_so_o_veiculo_da_ocorrencia()
    {
        var alvo = NovoVeiculo("NGL-3021", StatusNegativacao.Negativada);
        var outroNegativado = NovoVeiculo("NGL-7777", StatusNegativacao.Negativada);
        var ocorrencia = NovaOcorrencia(NivelOcorrencia.N3);
        var contestacao = new Contestacao { Id = 1, OcorrenciaId = ocorrencia.Id, TransportadoraId = 1, Justificativa = "pane" };
        RegrasNegativacao.AbrirContestacao(contestacao, ocorrencia);

        RegrasNegativacao.ResponderContestacao(contestacao, ocorrencia, aprovada: true, "Laudo aceito", DateTime.UtcNow, alvo, null);

        Assert.Equal(StatusNegativacao.Regular, alvo.StatusNegativacao);
        Assert.Equal(StatusNegativacao.Negativada, outroNegativado.StatusNegativacao);
        Assert.Equal(StatusOcorrencia.Resolvida, ocorrencia.Status);
        Assert.Equal(StatusContestacao.Aprovada, contestacao.Status);
        Assert.Equal("Laudo aceito", contestacao.RespostaOperador);
        Assert.NotNull(contestacao.RespondidoEm);
    }

    [Fact]
    public void Rejeitar_contestacao_mantem_bloqueio_e_reativa_ocorrencia()
    {
        var alvo = NovoVeiculo("NGL-3021", StatusNegativacao.Negativada);
        var ocorrencia = NovaOcorrencia(NivelOcorrencia.N3);
        var contestacao = new Contestacao { Id = 1, OcorrenciaId = ocorrencia.Id, TransportadoraId = 1, Justificativa = "pane" };
        RegrasNegativacao.AbrirContestacao(contestacao, ocorrencia);
        Assert.Equal(StatusOcorrencia.Contestada, ocorrencia.Status);

        RegrasNegativacao.ResponderContestacao(contestacao, ocorrencia, aprovada: false, "Sem laudo", DateTime.UtcNow, alvo, null);

        Assert.Equal(StatusNegativacao.Negativada, alvo.StatusNegativacao);
        Assert.Equal(StatusOcorrencia.Ativa, ocorrencia.Status);
        Assert.Equal(StatusContestacao.Rejeitada, contestacao.Status);
    }

    [Fact]
    public void Contestacao_ja_respondida_nao_pode_ser_respondida_de_novo()
    {
        var ocorrencia = NovaOcorrencia(NivelOcorrencia.N3);
        var contestacao = new Contestacao { Id = 1, OcorrenciaId = ocorrencia.Id, TransportadoraId = 1, Justificativa = "pane" };
        RegrasNegativacao.ResponderContestacao(contestacao, ocorrencia, true, null, DateTime.UtcNow, null, null);

        Assert.Throws<RegraNegocioException>(() =>
            RegrasNegativacao.ResponderContestacao(contestacao, ocorrencia, false, null, DateTime.UtcNow, null, null));
    }

    [Fact]
    public void Reincidencia_N2_conta_so_ultimos_30_dias()
    {
        var agora = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
        var ocorrencias = new[]
        {
            new Ocorrencia { Nivel = NivelOcorrencia.N2, CriadoEm = agora.AddDays(-1) },
            new Ocorrencia { Nivel = NivelOcorrencia.N2, CriadoEm = agora.AddDays(-29) },
            new Ocorrencia { Nivel = NivelOcorrencia.N2, CriadoEm = agora.AddDays(-31) },
            new Ocorrencia { Nivel = NivelOcorrencia.N3, CriadoEm = agora.AddDays(-2) },
        };

        Assert.Equal(2, RegrasNegativacao.ContarReincidenciasN2(ocorrencias, agora));
    }
}
