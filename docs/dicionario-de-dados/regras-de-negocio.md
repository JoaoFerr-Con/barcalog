# Regras de negócio — estados, transições e validações

Valores de cada enum: `gerado/enums.md`. Aqui está **o comportamento** que o código implementa,
com a evidência, e o que ainda depende de decisão. Nada foi deduzido só do nome.

## 1. Movimentação (marcação → liberação)

| Regra | Evidência |
|---|---|
| Um movimento é identificado por `MovimentoId` (COD_MOVIMENTO, texto) — único no banco | PK de `Marcacoes` |
| Espera = liberação − marcação, em horas (2 casas), calculada pelo banco; nula sem liberação | Coluna calculada `EsperaHoras` |
| Liberação nunca anterior à marcação | `CK_Marcacoes_Liberacao`; validação na importação e na integração |
| Origem: `CargaInicial`, `Planilha` (com lote e linha) ou `Integracao` | `OrigemMarcacao` |
| Datas fora de 2000…agora + 2 h são recusadas na integração | `IntegracaoServico.ValidarDatas` |

Estados implícitos (não há coluna de estado): **aberto** (sem liberação) e **liberado**.
Não existem estados intermediários (pátio, porto, descarga) nos dados reais.

## 2. Importação de planilha

```
Analisado ──(Gestor decide todos os conflitos e confirma)──▶ Confirmado
    └──────────────(Gestor cancela)─────────────────────────▶ Cancelado
```

| Regra | Evidência |
|---|---|
| Análise não grava em `Marcacoes` | `ImportacaoPlanilhaServico`, `docs/IMPORTACAO.md` |
| Linha: Novo / Complementar / Conflito / Rejeitado; conflito exige decisão Aplicar ou Manter | `ClassificacaoLinhaImportacao`, `DecisaoConflitoImportacao` |
| Confirmação recusada se outro lote do mesmo terminal foi confirmado depois da análise | `ConfirmarAsync` |
| Arquivo idêntico (SHA-256) já confirmado gera aviso | `ConfirmadoComMesmoHashAsync` |
| Avisos não bloqueiam: espera > 72 h, sem liberação, possível remarcação (mesma senha, convênio e liberação, em até 10 min), convênio novo | `AvisoImportacao` |
| Após confirmar: reavaliação dos alertas | `ImportacoesController` |

## 3. Negativação (Matriz de Disciplina)

| Regra | Evidência |
|---|---|
| N3 negativa automaticamente o **veículo** e, se informado, o **condutor**. N1 e N2 não bloqueiam | `RegrasNegativacao.AplicarOcorrencia` |
| Transportadora é negativada se tiver ≥ 1 veículo negativado (calculado, não gravado) | `CalcularStatusTransportadora` |
| Reincidência N2: contagem em 30 dias, só monitoramento | `ContarReincidenciasN2` |
| Negativação manual (Operador/Gestor) cria uma ocorrência N3 "Administrativo" | `VeiculoServico.NegativarAsync` |
| Desnegativação manual (Operador/Gestor) regulariza o veículo e resolve **todas** as ocorrências não resolvidas da placa, **sem justificativa obrigatória** | `VeiculoServico.DesnegativarAsync` |
| Carreta negativada não pode ser agendada | `AgendamentoServico.ValidarAsync` |

Estados da ocorrência:

```
Ativa ──(transportadora contesta)──▶ Contestada ──(porto aprova)──▶ Resolvida
  ▲                                       │
  └──────────(porto rejeita)──────────────┘
Ativa / Contestada ──(desnegativação manual da placa)──▶ Resolvida
```

Contestação: `Pendente → Aprovada | Rejeitada`; no máximo uma pendente por ocorrência (índice
único); aprovar regulariza só o veículo/condutor **daquela** ocorrência.

**Riscos registrados (decisão pendente):**
- N3 numa placa **não cadastrada** não bloqueia nada (não há veículo para negativar).
- Desnegativação manual não exige motivo nem comprovação — o documento original prevê "análise /
  medida corretiva" antes de reabilitar.
- A situação de portaria de uma carreta negativada pode ser alterada livremente (inclusive para
  "No porto").

## 4. Situação na portaria (`StatusPortaria`)

| Regra | Evidência |
|---|---|
| Informada manualmente na tela Portaria; grava a situação e o instante (`StatusPortariaDesde`) | `Veiculo.AtualizarStatusPortaria` |
| Qualquer situação pode ir para qualquer outra (sem máquina de estados) | Mesmo método |
| Só `NoPatio` e `Aguardando` entram na fila virtual; `ForaDoPorto` é a situação inicial e após a saída | `StatusPortaria`, `PortariaServico` |
| Não há histórico: só a situação atual | Modelo de `Veiculos` |

Classificação de precisão (o que a tela pode afirmar): **registrada** (alguém informou), nunca
**confirmada por evento** — não existe leitura de placa, cancela ou checkpoint integrado.

## 5. Agendamento

| Regra | Evidência |
|---|---|
| Transportadora e terminal precisam existir; placa cadastrada precisa ser da mesma transportadora e não negativada | `AgendamentoServico.ValidarAsync` |
| Mesma placa não pode ter dois agendamentos ativos no mesmo dia e hora (também por índice único) | `ValidarConflitoAsync` |
| Situação alterada manualmente, **sem restrição de transição** | `AtualizarStatusAsync` |
| No resumo, contam como confirmados: Confirmado, ACaminho, AguardandoEntrada, EmOperacao, Finalizado | `StatusConfirmados` |
| `JanelaConformidade` (D0–D3) é escolhida no cadastro e não é verificada contra nada | Código |

Pendente: transições permitidas, quem pode marcar "Atrasado" (hoje manual) e o significado de D0–D3.

## 6. Alertas

```
Novo ──assumir──▶ EmAnalise ──resolver (texto)──▶ Resolvido
  │                    └──────descartar (justificativa)──▶ Descartado
  └──resolver/descartar──▶ …
Resolvido / Descartado ──reabrir──▶ Novo
```

| Regra | Evidência |
|---|---|
| Alertas criados/atualizados por regra; `Ativo = false` quando a condição deixa de ocorrer | `AlertasServico.AplicarAsync` |
| A reavaliação não desfaz resolução nem descarte | Teste `Tratamento_do_alerta_fica_registrado…` |
| Cada ação vira `EventoAlerta` com autor e texto | `AgirAsync` |
| Usuário vinculado a terminal só vê e trata alertas do próprio terminal (alertas gerais ficam fora) | `EscopoTerminal.PodeVer` |
| Uma avaliação por vez; cada execução registrada, inclusive falhas | `AvaliarAsync`, `AvaliacoesAlertas` |

Regras de disparo, severidade e confiabilidade: `../INDICADORES.md`.

## 7. Usuários e acesso

| Regra | Evidência |
|---|---|
| Papéis: Operador, Gestor, Auditor (internos) e Transportadora (Portal) | `PapelUsuario` |
| Usuário do Portal sempre tem transportadora; interno nunca tem; terminal só para internos | `CK_Usuarios_EscopoTransportadora`, `CK_Usuarios_TerminalSoInterno` |
| Trocar papel, senha, MFA, vínculo de terminal ou desativar revoga todos os tokens | `RevogarTokens` |
| 5 falhas seguidas bloqueiam a conta; limite de tentativas por IP | `AutenticacaoServico`, `LimitesRequisicao` |
| Não é possível remover o último Gestor | `GarantirQueNaoRemoveUltimoGestorAsync` |
| Preferências (tela inicial, terminal preferido) não alteram permissão; terminal preferido fora do vínculo = 403 | `AtualizarPreferenciasAsync` |
