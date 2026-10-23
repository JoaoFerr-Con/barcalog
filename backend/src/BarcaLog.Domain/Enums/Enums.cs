namespace BarcaLog.Domain.Enums;

/// <summary>
/// Histórico: padrão observado no passado. Operacional: situação atual registrada.
/// Previsão: estimativa a partir de padrão histórico. Dados: qualidade/atualização da base.
/// </summary>
public enum TipoAlerta
{
    /// <summary>Padrão observado no passado (ex.: mediana do último mês acima do limite).</summary>
    Historico,
    /// <summary>Situação atual registrada.</summary>
    Operacional,
    /// <summary>Estimativa a partir do padrão histórico (não é fato observado).</summary>
    Previsao,
    /// <summary>Qualidade ou atualização da base (ex.: terminal sem dados recentes).</summary>
    Dados
}

/// <summary>Definida por cada regra de alerta (critérios em docs/INDICADORES.md); a lista ordena Alta primeiro.</summary>
public enum PrioridadeAlerta
{
    Baixa,
    Media,
    Alta
}

/// <summary>Estado do tratamento do alerta, alterado por pessoas (a reavaliação automática não desfaz resolução/descarte).</summary>
public enum EstadoAlerta
{
    /// <summary>Gerado (ou reaberto) e ainda sem responsável.</summary>
    Novo,
    /// <summary>Assumido por um usuário (campo Responsavel).</summary>
    EmAnalise,
    /// <summary>Encerrado com o registro do que foi feito (texto obrigatório).</summary>
    Resolvido,
    /// <summary>Encerrado sem ação, com justificativa obrigatória.</summary>
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
    /// <summary>Planilha Excel (primeira aba).</summary>
    Xlsx,
    /// <summary>Texto separado por ";", "," ou tabulação.</summary>
    Csv
}

public enum StatusLoteImportacao
{
    /// <summary>Analisado, aguardando revisão/confirmação. Nada gravado em Marcacoes.</summary>
    Analisado,
    /// <summary>Aplicado em Marcacoes numa transação, após decisão de todos os conflitos.</summary>
    Confirmado,
    /// <summary>Descartado sem gravar nada em Marcacoes.</summary>
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
    /// <summary>A linha não é conflito.</summary>
    NaoSeAplica,
    /// <summary>Conflito ainda sem decisão: impede confirmar o lote.</summary>
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
    /// <summary>Sem aviso.</summary>
    Nenhum = 0,
    /// <summary>Espera acima de 72 h.</summary>
    EsperaAcima72h = 1,
    /// <summary>Linha sem data de liberação.</summary>
    SemLiberacao = 2,
    /// <summary>Mesma senha, convênio e liberação, marcada de novo em até 10 minutos.</summary>
    PossivelRemarcacao = 4,
    /// <summary>Convênio ainda não cadastrado (operador e carga deduzidos do nome).</summary>
    ConvenioNovo = 8
}

/// <summary>
/// Situação da carreta na portaria. ForaDoPorto = sem presença registrada (situação
/// inicial de toda carreta cadastrada e situação após a saída); só NoPatio e
/// Aguardando entram na fila virtual. Informada manualmente na Portaria — não é
/// confirmada por leitura de placa nem por checkpoint.
/// </summary>
public enum StatusPortaria
{
    NoPatio,
    Aguardando,
    NoPorto,
    DescargaFinalizada,
    ForaDoPorto
}

/// <summary>Decide se a carreta/condutor pode rodar. Transportadora NÃO tem esse campo gravado.</summary>
public enum StatusNegativacao
{
    /// <summary>Pode operar.</summary>
    Regular,
    /// <summary>Bloqueada para novos carregamentos (ocorrência N3 ou negativação manual) até ser regularizada.</summary>
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
    /// <summary>Em vigor (também após contestação rejeitada).</summary>
    Ativa,
    /// <summary>Há contestação pendente da transportadora.</summary>
    Contestada,
    /// <summary>Contestação aprovada: o veículo/condutor desta ocorrência foi regularizado.</summary>
    Resolvida
}

public enum StatusContestacao
{
    /// <summary>Aguardando resposta do porto (no máximo uma pendente por ocorrência).</summary>
    Pendente,
    /// <summary>Aceita: ocorrência resolvida e veículo/condutor dela regularizados.</summary>
    Aprovada,
    /// <summary>Recusada: a ocorrência volta a ativa.</summary>
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

/// <summary>
/// Situação do agendamento, alterada manualmente (o código não restringe transições).
/// No resumo do dia, Confirmado, ACaminho, AguardandoEntrada, EmOperacao e Finalizado contam como
/// confirmados; Cancelado não entra na verificação de conflito de horário da placa.
/// </summary>
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
    /// <summary>Consulta e opera (política Escrita): alertas, agendamentos, portaria, ocorrências, cadastros.</summary>
    Operador,
    /// <summary>Tudo o que o Operador faz, mais gestão: importação, usuários, parâmetros, exclusões. MFA exigido quando Seguranca:ExigirMfaParaGestor (padrão: sim).</summary>
    Gestor,
    /// <summary>Somente leitura dos dados internos e da auditoria.</summary>
    Auditor,
    /// <summary>Usuário externo do Portal: enxerga e contesta SÓ os dados da própria transportadora.</summary>
    Transportadora
}

/// <summary>O que disparou a avaliação das regras de alerta.</summary>
public enum OrigemAvaliacao
{
    /// <summary>Execução periódica (padrão: a cada 10 minutos).</summary>
    Automatica,
    /// <summary>Botão "Reavaliar agora".</summary>
    Manual,
    /// <summary>Após confirmar uma importação de planilha.</summary>
    Importacao
}
