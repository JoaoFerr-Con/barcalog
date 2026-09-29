using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Domain.Entidades;

public class Contestacao
{
    public int Id { get; set; }
    public int OcorrenciaId { get; set; }
    public Ocorrencia? Ocorrencia { get; set; }
    public int TransportadoraId { get; set; }
    public Transportadora? Transportadora { get; set; }
    public string Justificativa { get; set; } = null!;
    public StatusContestacao Status { get; set; } = StatusContestacao.Pendente;
    public DateTime CriadoEm { get; set; }
    public DateTime? RespondidoEm { get; set; }
    public string? RespostaOperador { get; set; }

    public void Responder(bool aprovada, string? resposta, DateTime agoraUtc)
    {
        if (Status != StatusContestacao.Pendente)
            throw new RegraNegocioException($"Contestação já foi respondida ({Status}).");
        Status = aprovada ? StatusContestacao.Aprovada : StatusContestacao.Rejeitada;
        RespondidoEm = agoraUtc;
        RespostaOperador = resposta;
    }
}
