namespace BarcaLog.Domain.Enums;

/// <summary>
/// Histórico: padrão observado no passado. Operacional: situação atual registrada.
/// Previsão: estimativa a partir de padrão histórico. Dados: qualidade/atualização da base.
/// </summary>
public enum TipoAlerta
{
    Historico,
    Operacional,
    Previsao,
    Dados
}

public enum PrioridadeAlerta
{
    Baixa,
    Media,
    Alta
}

public enum EstadoAlerta
{
    Novo,
    EmAnalise,
    Resolvido,
    Descartado
}

/// <summary>De onde veio um movimento — exibido na tela para o usuário saber o que é manual, importado ou integrado.</summary>
public enum OrigemMarcacao
{
    /// <summary>Datasets JSON originais (carga inicial do sistema).</summary>
    CargaInicial,
    /// <summary>Planilha XLSX/CSV confirmada por um Gestor (ver LoteImportacaoId).</summary>
    Planilha,
    /// <summary>Evento recebido de outro sistema via API Key.</summary>
    Integracao
}

public enum FormatoArquivoImportacao
{
    Xlsx,
    Csv
}

public enum StatusLoteImportacao
{
    /// <summary>Analisado, aguardando revisão/confirmação. Nada gravado em Marcacoes.</summary>
    Analisado,
    Confirmado,
    Cancelado
}

public enum ClassificacaoLinhaImportacao
{
    /// <summary>Movimento que não existe — será inserido.</summary>
    Novo,
    /// <summary>Existe e só falta informação (código do convênio, liberação) — será completado.</summary>
    Complementar,
    /// <summary>Existe com valor diferente — exige decisão.</summary>
    Conflito,
    /// <summary>Linha inválida — não entra; motivo registrado.</summary>
    Rejeitado
}

public enum DecisaoConflitoImportacao
{
    NaoSeAplica,
    Pendente,
    /// <summary>Gravar os valores da planilha.</summary>
    Aplicar,
    /// <summary>Manter o que já está no BarcaLog.</summary>
    Manter
}

/// <summary>Sinais de qualidade: a linha entra, mas fica marcada para revisão.</summary>
[Flags]
public enum AvisoImportacao
{
    Nenhum = 0,
    EsperaAcima72h = 1,
    SemLiberacao = 2,
    PossivelRemarcacao = 4,
    ConvenioNovo = 8
}

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
    Auditor,
    /// <summary>Usuário externo do Portal: enxerga e contesta SÓ os dados da própria transportadora.</summary>
    Transportadora
}
