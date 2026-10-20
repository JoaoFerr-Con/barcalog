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
| Razão chegadas ÷ liberações | 1,4 | risco por faixa horária |
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
