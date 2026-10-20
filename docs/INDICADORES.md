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

**Confiabilidade** (independente da situação): *limitada* quando os dados do
terminal estão desatualizados (último registro há mais dias que "dias sem
dados"), quando a base tem menos de 30 dias com dados, ou quando a hora teve
menos de 30 chegadas registradas na base (amostra pequena). Pouco histórico não
é tratado como pouca demanda.

**Todos os terminais:** números somados, mas a situação da janela é a do terminal
mais pressionado — as carretas não mudam de terminal. Terminais sem dados
suficientes ficam fora da soma (e a tela avisa).

**Resumo e tendência:** demanda e capacidade somadas no horizonte; o saldo em
destaque é o da janela mais pressionada; a tendência descreve onde está a pior
janela (agora, no meio ou no fim do horizonte) — sem limiar adicional.

**Alerta de previsão** (`risco-horario`): por terminal, janelas de hoje em risco
de fila e com dados suficientes. Prioridade alta só com demanda ≥ 2× a capacidade
e confiabilidade normal.

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
