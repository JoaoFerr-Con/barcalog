# Dicionário dos indicadores

O catálogo completo (nome, classe observado/estimado/meta, fórmula, fonte, período, filtros,
unidade, registros incompletos, limitações) está em **`../INDICADORES.md`**, seção "Catálogo de
indicadores", junto com as regras da Visão Geral, da Análise 360°, da estimativa das próximas
horas, da confiabilidade e dos alertas. Ele não é repetido aqui para não haver duas versões.

Este arquivo liga cada indicador aos **campos** que ele consome.

| Indicador | Tabela.campos | Denominador / ausentes | Onde aparece |
|---|---|---|---|
| Movimentações, média por dia, pico diário | `Marcacoes.DataMarcacao`, `TerminalId` | Média: dias corridos do período. Sem liberação conta | Visão Geral, Relatório |
| Espera mediana, P90, média | `Marcacoes.EsperaHoras` | Só liberadas (`EsperaHoras` não nulo) | Visão Geral, Análise 360°, Relatório |
| Liberadas em até 24 h | `Marcacoes.EsperaHoras`; meta em `ParametrosOperacionais.MetaLiberadas24hPct` | ÷ liberadas | Visão Geral, Análise 360° |
| Esperas longas | `Marcacoes.EsperaHoras`; limite em `ParametrosOperacionais.LimiteEsperaLongaHoras` | Só liberadas | Visão Geral, Análise 360°, alertas |
| Ocupação média | `Marcacoes`; `Terminais.CapacidadeDiariaCarretas` | Só com capacidade informada (hoje nenhuma) | Visão Geral |
| Chegadas × liberações por hora | `Marcacoes.DataMarcacao`, `DataLiberacao` | ÷ dias do período | Visão Geral, Análise 360° |
| Volume mensal | `Marcacoes.DataMarcacao` | Mês parcial marcado | Visão Geral |
| Entradas / saídas / concluídas / pendentes (360°) | `Marcacoes.DataMarcacao`, `DataLiberacao` | Comparação por média diária com período anterior equivalente; sem base → sem comparação | Análise 360° |
| Por operador / carga / convênio / origem | `Marcacoes.Operador`, `Carga`, `Convenio`, `Origem` | Operador e carga **copiados do convênio na importação** | Análise 360° |
| Qualidade (sem liberação, sem código, remarcações, lotes, duplicidade) | `Marcacoes.DataLiberacao`, `CodConvenio`, `Senha`, `Convenio`, `TerminalId` | Sobre as entradas do período | Análise 360°, Importação |
| Estimativa das próximas horas e confiabilidade | `Marcacoes` (últimos 30 dias com dados); `Terminais.CapacidadeHoraria`; `ParametrosOperacionais.LimiteRazaoChegadaLiberacao`, `DiasSemDadosAlerta` | Só movimentos liberados; erro medido por backtest | Gargalos e alertas |
| Frescor dos dados | `Marcacoes` (máximos), `LotesImportacao.ConcluidoEm` | — | Visão Geral, alertas de dados |
| Fila virtual / operação agora | `Veiculos.StatusPortaria` | Situação **informada**, dados de demonstração | Portaria |
| Resumo da agenda | `Agendamentos.Status`, `Data`, `Hora`; `Terminais.CapacidadeHoraria` | Dados de demonstração | Agendamentos |

## Indicadores previstos nos documentos originais e ainda sem dado

| Indicador | Por que não existe | Dado necessário |
|---|---|---|
| Conformidade de janelas D0–D3 (chegada no dia autorizado) | Significado de D0–D3 diverge; não há data autorizada por carga | Janela autorizada por carga + evento de chegada |
| Gargalos por checkpoint A–D, tempo por etapa | Não há eventos de checkpoint | Eventos com horário por ponto |
| Mapa de calor de irregularidades | Ocorrências sem geolocalização; dados fictícios | Ocorrências reais com coordenadas |
| Volume em toneladas por tipo de carga | Planilhas não trazem peso | Peso por movimento |
| Volume de leituras OCR | Não há OCR | Eventos do app |
