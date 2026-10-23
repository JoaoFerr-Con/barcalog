# Indicadores do BarcaLog — definições e regras de cálculo

Todos os números do painel vêm da API (`/api/v1/indicadores/*`), calculados a
partir dos movimentos gravados no banco. Nenhum valor é fixo no frontend.

## O que a base registra (e o que não registra)

Cada movimento tem **marcação** (a carreta é registrada no sistema do terminal)
e **liberação** (é chamada/liberada). Não há registro de entrada no terminal,
pesagem, descarga ou saída do porto. Por isso:

- o tempo medido é a **espera entre marcação e liberação**, não a permanência no terminal;
- "chegadas" = marcações; "liberações" = liberações. Nada além disso é inferido;
- 97–98% das liberações acontecem em lote (várias carretas no mesmo segundo):
  liberação reflete a chamada de um grupo, não um atendimento individual.

## Convenções gerais

| Item | Regra |
|---|---|
| Período | Datas de **marcação**, horário local do porto (America/Belem), de/até **inclusive** |
| Espera | `liberação − marcação`, em horas com 2 casas (coluna computada no SQL Server, segundos inteiros) |
| Movimentos sem liberação | Contam em "Movimentações", mas não entram em nenhuma estatística de espera |
| Comparação | Mesmo cálculo no período de comparação escolhido (padrão: mês anterior). Sem período de comparação, não há variação |
| Percentis | Interpolação linear entre vizinhos — mesmo método do pandas/NumPy e do `PERCENTIL.INC` do Excel |

## Indicadores da Visão Geral

| Indicador | Unidade | Regra | Observação |
|---|---|---|---|
| Movimentações | carretas | marcações no período (liberadas + sem liberação) | |
| Média por dia | carretas/dia | movimentações ÷ dias corridos do período | Período que passa da última data com dados dilui a média — ver "Dados disponíveis até" |
| Espera mediana | h | mediana das esperas das liberadas | Indicador principal: metade esperou menos que isso |
| Espera P90 | h | 90º percentil das esperas | 10% esperaram mais que isso |
| Espera média | h | média aritmética | Exibida só como apoio: é puxada por poucos casos extremos |
| Liberadas em até 24 h | % | liberadas com espera ≤ 24 h ÷ liberadas | Meta configurável (padrão 85%) |
| Esperas longas | carretas | liberadas com espera > limite (padrão 72 h) | Casos para investigar um a um |
| Pico diário | carretas | maior número de marcações num dia do período | Empate: o dia mais antigo |
| Ocupação média | % | média por dia ÷ capacidade diária do terminal | **Só aparece com capacidade informada** em Configurações |

## Análise de Volume — Histórico

Movimentações por mês (data de marcação) e terminal, com a espera mediana do
mês. Um mês é marcado como **parcial** quando o terminal começou depois do
primeiro dia ou não tem marcações até o último dia do mês (ex.: setembro de
2026 só tem dados completos da TGPM). É um histórico: não há projeção.

## Chegadas × liberações por hora

Para cada hora do dia: marcações do período naquela hora ÷ dias corridos, e
liberações que **aconteceram** no período naquela hora ÷ dias corridos. A
razão chegadas ÷ liberações acima de 1 indica que, historicamente, entra mais
gente do que sai naquela faixa.

## Estimativa das próximas horas (Gargalos e alertas)

Código: `Application/Metricas/EstimativaJanelas.cs` (testes em `EstimativaJanelasTests`).
É uma **média do que já aconteceu**, não um modelo preditivo: feriados, safra e
navios atracados não entram. A tela diz isso e mostra a base usada.

**Por terminal, para cada uma das próximas 6 janelas de uma hora:**

| Termo na tela | Como é obtido |
|---|---|
| Demanda prevista | marcações naquela hora nos últimos 30 dias de dados do terminal ÷ dias com dados |
| Atendimento habitual | liberações naquela hora no mesmo período ÷ dias com dados |
| Capacidade de atendimento | capacidade por hora cadastrada em Configurações; sem ela, o atendimento habitual (a tela diz qual) |
| Saldo de capacidade | capacidade − demanda (folga ou carretas acima). Estimativa de atendimento, não vagas físicas |

A base termina no último dia com dados **de cada terminal** e divide pelos
dias **com** registro: um dia sem nenhuma marcação no terminal é falta de dado,
não dia parado. (Antes, a soma de todos os terminais terminava no último dia de
qualquer terminal e dividia por 30 dias corridos — com um terminal atrasado na
importação, a demanda dele aparecia subestimada.)

**Situação da janela:**

| Situação | Regra |
|---|---|
| Dentro da capacidade | demanda ≤ capacidade + 1 carreta, ou demanda prevista < 5 carretas na hora |
| Atenção | demanda acima da capacidade (por 1 carreta ou mais), abaixo do limite de risco |
| Risco de fila | demanda ≥ capacidade × limite de risco (parâmetro, padrão 1,4), ou capacidade zero com demanda relevante |
| Dados insuficientes | menos de 7 dias com dados na base (não cobre o ciclo semanal) |

- *Excesso menor que 1 carreta*: diferença de médias; carreta é indivisível.
- *Menos de 5 carretas previstas*: mesmo sem nenhuma liberação na hora, o acúmulo
  fica abaixo de 5 e é absorvido nas horas seguintes. Isso é **pouco movimento**
  (com dados suficientes), diferente de **dados insuficientes**.
- Faixa entre a capacidade e o limite de risco = atenção porque as liberações
  acontecem em lote (97–98%) e a média horária oscila; o limite é ajustável.

**Confiabilidade** (independente da situação) — 4 classes, pela pior condição encontrada.
Código: `EstimativaJanelas.Confiabilidade` e `MotivoConfiabilidade`. Cada limitação traz
**motivo, impacto e o que fazer** na tela.

| Classe | Critério objetivo | Impacto | O que fazer |
|---|---|---|---|
| Desatualizada | último registro do terminal há mais dias que "dias sem dados" (padrão 2) | o padrão pode não refletir a operação atual | importar as planilhas mais recentes |
| Insuficiente | menos de 7 dias com dados na base | não há estimativa para o terminal | aguardar/importar mais dias |
| Limitada | base com menos de 30 dias com dados; **ou** erro medido pior que a referência ingênua; **ou** erro não mensurável (nenhum dia com 7 dias anteriores para avaliar); **ou** a hora teve menos de 30 chegadas na base | usar como indicação, não como número firme | conferir com a operação; acompanhar o erro medido |
| Confiável | nenhuma das condições acima | — | — |

*Erro medido*: WAPE dos últimos 30 dias com dados (para cada dia, a estimativa feita só
com os dias anteriores comparada com as chegadas reais, hora a hora), comparado com a
referência ingênua "mesma hora do dia anterior". Se o erro for pior que o da referência,
a confiabilidade não passa de *limitada*. Pouco histórico nunca é tratado como pouca
demanda. A confiabilidade **só muda com dados novos** — reavaliar sem importar nada
devolve a mesma classe (nada é inflado).

### Reavaliar agora

`POST /api/v1/alertas/avaliar` (Operador/Gestor). Uma avaliação por vez: se outra estiver
rodando, a resposta é `em-andamento` e nada é feito. Cada execução (manual, a cada 10 min
ou após importação) fica registrada em `AvaliacoesAlertas` com início, fim, autor, origem,
dados até (última marcação), última importação, registros, **se havia dados novos**
(comparado à avaliação anterior bem-sucedida), alertas criados/alterados/encerrados, ativos,
confiabilidade e a comparação com a anterior (melhorou / igual / piorou / primeira).
Falha também é registrada (com a mensagem) e a tela mostra "Tentar de novo".
Histórico: `GET /api/v1/alertas/avaliacoes`.

**Todos os terminais:** números somados, mas a situação da janela é a do terminal
mais pressionado — as carretas não mudam de terminal. Terminais sem dados
suficientes ficam fora da soma (e a tela avisa).

**Resumo e tendência:** demanda e capacidade somadas no horizonte; o saldo em
destaque é o da janela mais pressionada; a tendência descreve onde está a pior
janela (agora, no meio ou no fim do horizonte) — sem limiar adicional.

**Alerta de previsão** (`risco-horario`): por terminal, janelas de hoje em risco
de fila e com dados suficientes. Prioridade alta só com demanda ≥ 2× a capacidade
e confiabilidade normal.

### Erro medido da estimativa (backtest)

`backend/tools/backtest_estimativa.py` refaz a estimativa como o sistema faria na
véspera de cada dia e compara, hora a hora, com o que aconteceu. Rodado em
21/10/2026 sobre as três planilhas reais (dias com ao menos 30 dias de histórico
antes; chegadas = marcações de movimentos liberados):

| Terminal | Chegadas reais/h (média) | Erro absoluto médio | WAPE base 30 dias | WAPE base 7 dias | WAPE "mesma hora do dia anterior" | Risco de fila: precisão / cobertura (base 30) |
|---|---|---|---|---|---|---|
| Unitapajós | 7,7 | 4,2 | 55% | 47% | 57% | 59% / 55% |
| TGPM | 7,3 | 3,6 | 49% | 43% | 49% | 53% / 47% |
| Hidrovias | 6,8 | 4,5 | 66% | 55% | 57% | 52% / 48% |

Leitura: hora a hora, a média de 30 dias erra cerca de metade do volume e não é
melhor que repetir o dia anterior; com base de 7 dias o erro cai 6 a 11 pontos.
A classificação "risco de fila" acerta pouco mais da metade das vezes — também
porque 97–98% das liberações acontecem em lote, o que torna a hora isolada
ruidosa. Por isso a tela fala em "estimativa pelo histórico" e confiabilidade, e
não em previsão. Próximos passos (roadmap P1): base de 7 dias, exibir o erro
medido na tela e avaliar janelas de várias horas com acúmulo de fila.

## Análise 360° (Movimentações › Análise 360°)

`GET /api/v1/marcacoes/analise` — código: `Application/Metricas/AnaliseMovimentacoes.cs`.
Tudo **observado** nos registros; nada estimado, nada projetado, nenhuma meta.

| Item | Regra |
|---|---|
| Período | datas de marcação (de/até inclusive). Sem datas: 30 dias até a última marcação do recorte. Máximo 2 anos |
| Período anterior | mesma duração, terminando na véspera do início. Sem registros nele = sem comparação (nunca "+100%" sobre base 0) |
| Filtros | terminal, operador, carga, convênio (exato), origem; o terminal é limitado pelo vínculo do usuário |
| Entradas | marcações com data no período; comparação pela **média por dia** |
| Saídas | liberações com data no período, mesmo de carretas marcadas antes |
| Concluídas / pendentes | marcadas no período com / sem liberação |
| Mediana, P90, ≤ 24 h, esperas longas | como na Visão Geral, só das concluídas. ≤ 24 h compara em pontos percentuais; esperas longas em diferença absoluta |
| Série | por dia (até 62 dias), semana seg–dom (até 182) ou mês; pontos cortados pelo período marcados como parciais |
| Por hora | entradas e saídas por hora ÷ dias do período |
| Por dia da semana | entradas daquele dia ÷ ocorrências daquele dia no período |
| Dimensões | terminal, operador, carga, convênio, origem — top 12 + "Outros". Placa, transportadora e motorista **não** existem nas planilhas dos terminais e não aparecem |
| Qualidade | sem liberação, sem código de convênio, possíveis remarcações (mesma senha, convênio, terminal e liberação em até 10 min), liberadas em lote (mesmo instante de outra do terminal), duplicidade de movimento (sempre 0: chave do banco) |

**Detalhamento (drill-down):** cada fatia abre a lista com os mesmos filtros e o período
efetivo da análise, mais a fatia: `de/ate` (ponto da série), `hora`, `diaSemana`,
`terminalId`/`operador`/`carga`/`convenioExato`/`origem` (dimensões), `semLiberacao`,
`semCodigoConvenio`, `esperaAcimaHoras` (qualidade). A contagem da lista é a mesma da
análise (teste de integração `Analise_360_bate_com_o_calculo_manual...`). Remarcações e
liberações em lote não têm filtro de lista (o detalhe do movimento mostra o lote).

## Catálogo de indicadores

Classes: **O** = observado (contado nos registros), **E** = estimativa pelo histórico,
**M** = meta/limite configurado. O sistema não tem previsão (modelo validado) nem custos.
Atualização: os números são recalculados a cada consulta sobre o que está no banco; o
banco muda a cada importação confirmada ou evento da integração. Alertas: a cada 10 min,
após importação ou em "Reavaliar agora".

| Indicador | Classe | Fórmula | Fonte | Período / filtros | Unidade | Registros incompletos | Limitação |
|---|---|---|---|---|---|---|---|
| Movimentações | O | contagem de marcações | Marcacoes | marcação de/até; terminal | carretas | sem liberação contam | não deduplica possíveis remarcações |
| Média por dia | O | movimentações ÷ dias corridos | Marcacoes | idem | carretas/dia | — | dias sem importação diluem a média |
| Espera mediana / P90 / média | O | percentil linear / média de (liberação − marcação) | Marcacoes.EsperaHoras | idem | h | sem liberação ficam fora | liberação em lote pode registrar a hora do lote |
| Liberadas em até 24 h | O (meta M) | ≤ 24 h ÷ liberadas | idem | idem | % | fora | meta configurável |
| Esperas longas | O (limite M) | espera > limite | idem | idem | carretas | fora | limite configurável |
| Pico diário | O | maior contagem diária | Marcacoes | idem | carretas | — | empate: dia mais antigo |
| Ocupação média | O ÷ M | média/dia ÷ capacidade cadastrada | Marcacoes + Terminais | idem | % | — | só com capacidade cadastrada |
| Chegadas × liberações por hora | O | contagem por hora ÷ dias | Marcacoes | idem | carretas/dia | liberação só se houver | — |
| Volume mensal | O | contagem por mês e terminal | Marcacoes | todo o histórico | carretas | — | meses parciais marcados |
| Demanda próximas horas | E | média de chegadas/h nos últimos 30 dias com dados | Marcacoes | terminal | carretas/h | só liberados | erro medido ~50% (ver backtest) |
| Capacidade de atendimento | M ou O | capacidade cadastrada, senão liberações/h observadas | Terminais / Marcacoes | terminal | carretas/h | — | liberações em lote |
| Situação da janela | E | demanda vs. capacidade (limite de risco M) | acima | terminal | classe | — | ver confiabilidade |
| Confiabilidade | O | regras da tabela acima | Marcacoes + backtest | terminal | classe | — | — |
| Análise 360° | O | ver seção acima | Marcacoes | ver seção acima | vários | ver qualidade | — |
| Frescor | O | última marcação/liberação/importação | Marcacoes, LotesImportacao | terminal | data | — | — |

## Acesso por terminal e preferências

- **Vínculo** (`Usuario.TerminalId`, definido pelo Gestor em Configurações › Acesso por
  terminal): é permissão. Vai no token (`barcalog:terminal`), alterar encerra as sessões
  do usuário. O servidor aplica em toda ação (`EscopoTerminalFiltro`): pedido sem terminal
  vira o terminal vinculado; pedido de outro terminal = **403**; registro avulso de outro
  terminal (movimento, alerta, importação) = **404**. Alertas gerais (todos os terminais
  somados) ficam fora da visão do vinculado, porque misturam dados de outros terminais.
  Usuário do Portal nunca tem vínculo (constraint `CK_Usuarios_TerminalSoInterno`).
- **Preferências** (`PUT /api/v1/auth/me/preferencias`): tela ao entrar e terminal já
  selecionado. Não mudam papel nem vínculo; terminal preferido fora do vínculo = 403.
  Tela padrão por papel (proposta, ajustável pelo usuário): Gestor → Visão Geral,
  Operador → Gargalos e alertas, Auditor → Movimentações.
- Não coberto ainda (documentado, sem regra inventada): auditoria e o histórico de
  avaliações de alertas não são filtrados por terminal; "Reavaliar agora" reavalia todos
  os terminais; um Gestor vinculado só pode vincular outros ao próprio terminal.
- **"Posto Mirian"**: não existe nas planilhas, no código nem na documentação. A visão
  do gestor do posto aguarda a definição de quais registros pertencem a ele.

## Frescor dos dados

Por terminal: última marcação, última liberação e última importação
confirmada. "Desatualizado" quando a última marcação tem mais dias que o
parâmetro "dias sem dados" (padrão 2).

## Parâmetros configuráveis (Configurações, só Gestor, auditado)

| Parâmetro | Padrão | Usado em |
|---|---|---|
| Limite de espera mediana | 12 h | alertas de terminal |
| Meta de liberadas em até 24 h | 85% | cor do indicador |
| Limite de espera longa | 72 h | "esperas longas", aviso na importação |
| Dias sem dados | 2 | aviso de dados desatualizados |
| Limite de risco de fila (× capacidade de atendimento) | 1,4 | estimativa das próximas horas e alerta de previsão |
| Capacidade diária / por hora (por terminal) | não informada | ocupação, agenda |

Os padrões são pontos de partida, não normas do porto.

## O que foi retirado do painel e por quê

| Antes | Motivo |
|---|---|
| Ocupação sobre 1.000 carretas/dia | Valor padrão, não medido (o pico real é ~330/dia). A migration `CapacidadeOpcionalEParametros` apaga o 1.000 e deixa a capacidade em branco |
| "SLA por etapa" (gate, pátio, pesagem, tombador…) | Percentuais fixos no código; a base não registra essas etapas |
| Business case (demurrage R$ 12,50/h, diária R$ 600, diesel, CO₂, ROI) | Premissas fixas apresentadas como medição |
| Análise preditiva / projeção de volume / saturação em 7 dias | Projeções por tendência; o painel mostra só histórico |

Os endpoints antigos (`/api/v1/marcacoes/*`, porte fiel do `metricsEngine.js`)
continuam disponíveis para compatibilidade e para a ferramenta
`backend/tools/verificar-fidelidade.mjs`; quando a capacidade não está
informada, eles usam o valor antigo de 1.000/dia. O painel novo não os usa.

## Validação

`scripts`/validação desta etapa (fora do repositório, com as planilhas reais):
os indicadores de agosto e julho de 2026, por terminal e no total, o volume
mensal e o fluxo horário foram comparados com cálculo independente em pandas
sobre as 119.245 movimentações — diferença máxima de 0,01 h (arredondamento
da espera em 2 casas).
