namespace BarcaLog.Domain.Enums;

/// <summary>Posição física da carreta no fluxo pátio → porto (nada a ver com negativação).</summary>
public enum StatusPortaria
{
    NoPatio,
    Aguardando,
    NoPorto,
    DescargaFinalizada
}

/// <summary>Decide se a carreta/condutor pode rodar. Transportadora NÃO tem esse campo gravado.</summary>
public enum StatusNegativacao
{
    Regular,
    Negativada
}

/// <summary>
/// N1 (leve) → registro histórico/advertência, não bloqueia.
/// N2 (moderada) → reincidência/monitoramento, não bloqueia.
/// N3 (grave) → bloqueio automático imediato do veículo.
/// </summary>
public enum NivelOcorrencia
{
    N1,
    N2,
    N3
}

public enum StatusOcorrencia
{
    Ativa,
    Contestada,
    Resolvida
}

public enum StatusContestacao
{
    Pendente,
    Aprovada,
    Rejeitada
}

/// <summary>Compromisso de SLA assumido no agendamento (D0 até 24h, D1 até 48h, ...).</summary>
public enum JanelaConformidade
{
    D0,
    D1,
    D2,
    D3
}

public enum StatusAgendamento
{
    Agendado,
    Confirmado,
    ACaminho,
    AguardandoEntrada,
    EmOperacao,
    Finalizado,
    Atrasado,
    Cancelado
}

public enum PapelUsuario
{
    Operador,
    Gestor,
    Auditor
}
