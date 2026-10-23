# Enums e domínios

> Arquivo **gerado** por `dotnet run --project backend/src/BarcaLog.Api -- dicionario` a partir do código
> (modelo do EF Core, enums, ApiExplorer e comentários XML) e de `docs/dicionario-de-dados/curadoria.json`.
> Não edite à mão: o CI recusa o arquivo se ele divergir do código.

No banco, enums são gravados **como texto** (nome do valor), com `CHECK` gerando a lista permitida — exceto `[Flags]`, gravados como número. Transições permitidas e efeitos nos indicadores estão em `../regras-de-negocio.md` (não são deduzidas do nome).

## AvisoImportacao (flags)

Sinais de qualidade: a linha entra, mas fica marcada para revisão.

Usado em: `LinhasImportacao.Avisos`

| Valor | Número | Significado (código) |
|---|---|---|
| `Nenhum` | 0 | Sem aviso. |
| `EsperaAcima72h` | 1 | Espera acima de 72 h. |
| `SemLiberacao` | 2 | Linha sem data de liberação. |
| `PossivelRemarcacao` | 4 | Mesma senha, convênio e liberação, marcada de novo em até 10 minutos. |
| `ConvenioNovo` | 8 | Convênio ainda não cadastrado (operador e carga deduzidos do nome). |

## ClassificacaoLinhaImportacao

Usado em: `LinhasImportacao.Classificacao`

| Valor | Número | Significado (código) |
|---|---|---|
| `Novo` | 0 | Movimento que não existe — será inserido. |
| `Complementar` | 1 | Existe e só falta informação (código do convênio, liberação) — será completado. |
| `Conflito` | 2 | Existe com valor diferente — exige decisão. |
| `Rejeitado` | 3 | Linha inválida — não entra; motivo registrado. |

## DecisaoConflitoImportacao

Usado em: `LinhasImportacao.Decisao`

| Valor | Número | Significado (código) |
|---|---|---|
| `NaoSeAplica` | 0 | A linha não é conflito. |
| `Pendente` | 1 | Conflito ainda sem decisão: impede confirmar o lote. |
| `Aplicar` | 2 | Gravar os valores da planilha. |
| `Manter` | 3 | Manter o que já está no BarcaLog. |

## EstadoAlerta

Estado do tratamento do alerta, alterado por pessoas (a reavaliação automática não desfaz resolução/descarte).

Usado em: `Alertas.Estado`

| Valor | Número | Significado (código) |
|---|---|---|
| `Novo` | 0 | Gerado (ou reaberto) e ainda sem responsável. |
| `EmAnalise` | 1 | Assumido por um usuário (campo Responsavel). |
| `Resolvido` | 2 | Encerrado com o registro do que foi feito (texto obrigatório). |
| `Descartado` | 3 | Encerrado sem ação, com justificativa obrigatória. |

## FormatoArquivoImportacao

Usado em: `LotesImportacao.Formato`

| Valor | Número | Significado (código) |
|---|---|---|
| `Xlsx` | 0 | Planilha Excel (primeira aba). |
| `Csv` | 1 | Texto separado por ";", "," ou tabulação. |

## JanelaConformidade

Compromisso de SLA assumido no agendamento (D0 até 24h, D1 até 48h, ...).

Usado em: `Agendamentos.JanelaConformidade`

| Valor | Número | Significado (código) |
|---|---|---|
| `D0` | 0 | a validar |
| `D1` | 1 | a validar |
| `D2` | 2 | a validar |
| `D3` | 3 | a validar |

## NivelOcorrencia

N1 (leve) → registro histórico/advertência, não bloqueia. N2 (moderada) → reincidência/monitoramento, não bloqueia. N3 (grave) → bloqueio automático imediato do veículo.

Usado em: `Ocorrencias.Nivel`

| Valor | Número | Significado (código) |
|---|---|---|
| `N1` | 0 | a validar |
| `N2` | 1 | a validar |
| `N3` | 2 | a validar |

## OrigemAvaliacao

O que disparou a avaliação das regras de alerta.

Usado em: `AvaliacoesAlertas.Origem`

| Valor | Número | Significado (código) |
|---|---|---|
| `Automatica` | 0 | Execução periódica (padrão: a cada 10 minutos). |
| `Manual` | 1 | Botão "Reavaliar agora". |
| `Importacao` | 2 | Após confirmar uma importação de planilha. |

## OrigemMarcacao

De onde veio um movimento — exibido na tela para o usuário saber o que é manual, importado ou integrado.

Usado em: `Marcacoes.Origem`

| Valor | Número | Significado (código) |
|---|---|---|
| `CargaInicial` | 0 | Datasets JSON originais (carga inicial do sistema). |
| `Planilha` | 1 | Planilha XLSX/CSV confirmada por um Gestor (ver LoteImportacaoId). |
| `Integracao` | 2 | Evento recebido de outro sistema via API Key. |

## PapelUsuario

Usado em: `Usuarios.Papel`

| Valor | Número | Significado (código) |
|---|---|---|
| `Operador` | 0 | Consulta e opera (política Escrita): alertas, agendamentos, portaria, ocorrências, cadastros. |
| `Gestor` | 1 | Tudo o que o Operador faz, mais gestão: importação, usuários, parâmetros, exclusões. MFA exigido quando Seguranca:ExigirMfaParaGestor (padrão: sim). |
| `Auditor` | 2 | Somente leitura dos dados internos e da auditoria. |
| `Transportadora` | 3 | Usuário externo do Portal: enxerga e contesta SÓ os dados da própria transportadora. |

## PrioridadeAlerta

Definida por cada regra de alerta (critérios em docs/INDICADORES.md); a lista ordena Alta primeiro.

Usado em: `Alertas.Prioridade`

| Valor | Número | Significado (código) |
|---|---|---|
| `Baixa` | 0 | a validar |
| `Media` | 1 | a validar |
| `Alta` | 2 | a validar |

## StatusAgendamento

Situação do agendamento, alterada manualmente (o código não restringe transições). No resumo do dia, Confirmado, ACaminho, AguardandoEntrada, EmOperacao e Finalizado contam como confirmados; Cancelado não entra na verificação de conflito de horário da placa.

Usado em: `Agendamentos.Status`

| Valor | Número | Significado (código) |
|---|---|---|
| `Agendado` | 0 | a validar |
| `Confirmado` | 1 | a validar |
| `ACaminho` | 2 | a validar |
| `AguardandoEntrada` | 3 | a validar |
| `EmOperacao` | 4 | a validar |
| `Finalizado` | 5 | a validar |
| `Atrasado` | 6 | a validar |
| `Cancelado` | 7 | a validar |

## StatusContestacao

Usado em: `Contestacoes.Status`

| Valor | Número | Significado (código) |
|---|---|---|
| `Pendente` | 0 | Aguardando resposta do porto (no máximo uma pendente por ocorrência). |
| `Aprovada` | 1 | Aceita: ocorrência resolvida e veículo/condutor dela regularizados. |
| `Rejeitada` | 2 | Recusada: a ocorrência volta a ativa. |

## StatusLoteImportacao

Usado em: `LotesImportacao.Status`

| Valor | Número | Significado (código) |
|---|---|---|
| `Analisado` | 0 | Analisado, aguardando revisão/confirmação. Nada gravado em Marcacoes. |
| `Confirmado` | 1 | Aplicado em Marcacoes numa transação, após decisão de todos os conflitos. |
| `Cancelado` | 2 | Descartado sem gravar nada em Marcacoes. |

## StatusNegativacao

Decide se a carreta/condutor pode rodar. Transportadora NÃO tem esse campo gravado.

Usado em: `Condutores.StatusNegativacao`, `Veiculos.StatusNegativacao`

| Valor | Número | Significado (código) |
|---|---|---|
| `Regular` | 0 | Pode operar. |
| `Negativada` | 1 | Bloqueada para novos carregamentos (ocorrência N3 ou negativação manual) até ser regularizada. |

## StatusOcorrencia

Usado em: `Ocorrencias.Status`

| Valor | Número | Significado (código) |
|---|---|---|
| `Ativa` | 0 | Em vigor (também após contestação rejeitada). |
| `Contestada` | 1 | Há contestação pendente da transportadora. |
| `Resolvida` | 2 | Contestação aprovada: o veículo/condutor desta ocorrência foi regularizado. |

## StatusPortaria

Situação da carreta na portaria. ForaDoPorto = sem presença registrada (situação inicial de toda carreta cadastrada e situação após a saída); só NoPatio e Aguardando entram na fila virtual. Informada manualmente na Portaria — não é confirmada por leitura de placa nem por checkpoint.

Usado em: `Veiculos.StatusPortaria`

| Valor | Número | Significado (código) |
|---|---|---|
| `NoPatio` | 0 | a validar |
| `Aguardando` | 1 | a validar |
| `NoPorto` | 2 | a validar |
| `DescargaFinalizada` | 3 | a validar |
| `ForaDoPorto` | 4 | a validar |

## TipoAlerta

Histórico: padrão observado no passado. Operacional: situação atual registrada. Previsão: estimativa a partir de padrão histórico. Dados: qualidade/atualização da base.

Usado em: `Alertas.Tipo`

| Valor | Número | Significado (código) |
|---|---|---|
| `Historico` | 0 | Padrão observado no passado (ex.: mediana do último mês acima do limite). |
| `Operacional` | 1 | Situação atual registrada. |
| `Previsao` | 2 | Estimativa a partir do padrão histórico (não é fato observado). |
| `Dados` | 3 | Qualidade ou atualização da base (ex.: terminal sem dados recentes). |

## TipoEventoIntegracao

Usado em: contratos da API (sem coluna própria)

| Valor | Número | Significado (código) |
|---|---|---|
| `Marcacao` | 0 | Carreta marcada (chegada). Cria ou atualiza o movimento. |
| `Liberacao` | 1 | Carreta liberada. Exige movimento já existente (ou dataMarcacao informada). |
