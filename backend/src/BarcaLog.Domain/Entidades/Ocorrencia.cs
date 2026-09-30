using BarcaLog.Domain.Enums;
using BarcaLog.Domain.Excecoes;

namespace BarcaLog.Domain.Entidades;

public class Ocorrencia
{
    public int Id { get; set; }
    public NivelOcorrencia Nivel { get; set; }
    public string Placa { get; set; } = null!;
    public int TransportadoraId { get; set; }
    public Transportadora? Transportadora { get; set; }

    /// <summary>
    /// Condutor identificado na ocorrência (opcional). Substitui o antigo
    /// cpfMotorista do frontend: quando presente, o N3 bloqueia também o condutor.
    /// </summary>
    public int? CondutorId { get; set; }
    public Condutor? Condutor { get; set; }

    public string Descricao { get; set; } = null!;
    public string? Local { get; set; }
    public string? Responsavel { get; set; }
    public StatusOcorrencia Status { get; set; } = StatusOcorrencia.Ativa;
    public DateTime CriadoEm { get; set; }

    /// <summary>Regra oficial: só N3 bloqueia automaticamente.</summary>
    public bool BloqueiaAutomaticamente => Nivel == NivelOcorrencia.N3;

    public void MarcarContestada()
    {
        if (Status == StatusOcorrencia.Resolvida)
            throw new RegraNegocioException("Ocorrência já resolvida não pode ser contestada.");
        Status = StatusOcorrencia.Contestada;
    }

    public void Resolver() => Status = StatusOcorrencia.Resolvida;

    public void Reativar() => Status = StatusOcorrencia.Ativa;
}
