using BarcaLog.Domain.Entidades;
using BarcaLog.Domain.Enums;

namespace BarcaLog.Domain.Regras;

/// <summary>
/// Núcleo das regras do Sistema de Negativação — porte fiel de
/// src/data/negativacaoStore.js (registrarOcorrencia / responderContestacao /
/// listarTransportadoras / reincidenciasN2).
/// </summary>
public static class RegrasNegativacao
{
    /// <summary>Janela usada no sinal de reincidência N2 (monitoramento, não bloqueia).</summary>
    public const int JanelaReincidenciaN2Dias = 30;

    /// <summary>
    /// Aplica os efeitos de uma ocorrência recém-registrada: N3 bloqueia
    /// automaticamente o VEÍCULO (e o condutor, quando identificado). N1 e N2
    /// não bloqueiam nada. Nunca mexe na transportadora como um todo.
    /// </summary>
    public static void AplicarOcorrencia(Ocorrencia ocorrencia, Veiculo? veiculo, Condutor? condutor)
    {
        if (!ocorrencia.BloqueiaAutomaticamente) return;
        veiculo?.Negativar();
        condutor?.Negativar();
    }

    /// <summary>
    /// Status da transportadora é SEMPRE calculado: negativada se tiver pelo
    /// menos um veículo negativado.
    /// </summary>
    public static StatusNegativacao CalcularStatusTransportadora(IEnumerable<Veiculo> frota) =>
        CalcularStatusTransportadora(frota.Select(v => v.StatusNegativacao));

    public static StatusNegativacao CalcularStatusTransportadora(IEnumerable<StatusNegativacao> statusDaFrota) =>
        statusDaFrota.Any(s => s == StatusNegativacao.Negativada)
            ? StatusNegativacao.Negativada
            : StatusNegativacao.Regular;

    /// <summary>Apta a operar = nenhuma carreta negativada.</summary>
    public static bool EstaAptaParaOperar(IEnumerable<Veiculo> frota) =>
        CalcularStatusTransportadora(frota) == StatusNegativacao.Regular;

    /// <summary>Abrir contestação marca a ocorrência como contestada.</summary>
    public static void AbrirContestacao(Contestacao contestacao, Ocorrencia ocorrencia)
    {
        ocorrencia.MarcarContestada();
        contestacao.Status = StatusContestacao.Pendente;
    }

    /// <summary>
    /// Aprovar regulariza o veículo (e condutor) DAQUELA ocorrência e resolve a
    /// ocorrência; rejeitar devolve a ocorrência pra "ativa". Outras carretas da
    /// transportadora não são tocadas.
    /// </summary>
    public static void ResponderContestacao(
        Contestacao contestacao,
        Ocorrencia ocorrencia,
        bool aprovada,
        string? resposta,
        DateTime agoraUtc,
        Veiculo? veiculoDaOcorrencia,
        Condutor? condutorDaOcorrencia)
    {
        contestacao.Responder(aprovada, resposta, agoraUtc);
        if (aprovada)
        {
            ocorrencia.Resolver();
            veiculoDaOcorrencia?.Regularizar();
            condutorDaOcorrencia?.Regularizar();
        }
        else
        {
            ocorrencia.Reativar();
        }
    }

    /// <summary>Quantidade de N2 da transportadora nos últimos <paramref name="janelaDias"/> dias.</summary>
    public static int ContarReincidenciasN2(IEnumerable<Ocorrencia> ocorrenciasDaTransportadora, DateTime agoraUtc, int janelaDias = JanelaReincidenciaN2Dias)
    {
        var limite = agoraUtc.AddDays(-janelaDias);
        return ocorrenciasDaTransportadora.Count(o => o.Nivel == NivelOcorrencia.N2 && o.CriadoEm >= limite);
    }
}
