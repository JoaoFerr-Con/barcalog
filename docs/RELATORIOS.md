# Central de relatórios

Menu **Dados › Relatórios**. Fluxo: escolher o relatório → período e filtros → prévia → exportar
(PDF ou Excel). A prévia é o mesmo documento que o servidor transforma em arquivo: nenhum número
muda entre a tela e o PDF/Excel.

## Tipos

| Relatório | Para quem | Conteúdo |
|---|---|---|
| Gerencial (padrão: mensal) | Prefeitura, gestão do porto, Posto Mirian | Resumo executivo, indicadores com comparação, histórico mensal por carga, terminais, cargas e safra, picos, ocorrências, conclusões, metodologia e fontes |
| Operacional | Terminais e operação | Movimento por dia, horários e dias da semana, permanência e maiores esperas, distribuição por terminal/operador/carga/convênio, dias de pico e referência diária |
| Análise 360° | Análise aprofundada | Tudo da tela Análise 360° (resumo, fluxo, dimensões, qualidade dos dados) + histórico, projeção testada, picos e safra, conclusões |

Detalhamento **resumido** tira as tabelas longas (dia a dia, convênios) e reduz os rankings.

## Filtros e escopo

Período (padrão: último mês completo — todos os terminais com dados no mês inteiro), terminal,
operador, carga. Usuário vinculado a um terminal só vê e emite relatórios desse terminal (pedido de
outro terminal = 403). Período sem movimentações: a prévia avisa e a exportação é recusada (422).

## De onde vêm os números

- Indicadores, série, horários, dias da semana, dimensões e qualidade: `AnaliseMovimentacoes.Calcular`
  — o mesmo cálculo da tela Movimentações › Análise 360° (regras em `INDICADORES.md`).
- Histórico mensal por carga, faixas de espera, maiores esperas, dias de pico, dias acima da
  referência e projeção: `Application/Relatorios/CalculosRelatorio.cs`.
- Textos (resumo, conclusões, safra): `MontadorRelatorio.cs`. Só afirmam o que os dados mostram;
  a leitura da safra separa **dado observado**, **contexto externo**, **interpretação** e **limitação**.

Faixas de espera fechadas à direita: "até 6 h" inclui 6 h. Assim, a soma das três primeiras faixas é
igual a "liberadas em até 24 h" e "mais de 72 h" é igual a "esperas longas".

## Referência diária (provisória)

1.000 carretas por dia, **por terminal** (`Relatorios:ReferenciaDiariaCarretas`), enquanto o terminal
não tem capacidade cadastrada em Configurações. É régua de comparação, não capacidade oficial; as
referências não são somadas entre terminais (DEC-032).

## Projeção

Sem "análise preditiva". Estimativa simples (média dos três meses completos anteriores), publicada
só se o teste com o histórico der erro médio de até 15%. Com os dados até setembro de 2026 o erro
foi 28,1% — o relatório mostra o teste e explica por que não projeta (DEC-033).

## Contexto externo (safra)

`backend/src/BarcaLog.Api/Relatorios/contexto-externo.json`: textos com fonte, URL, data de
publicação e de consulta. Editável sem recompilar (recarregado automaticamente). Nada é buscado na
internet na hora de gerar o PDF.

## Mapa de ocorrências

Não há mapa: as ocorrências não têm latitude/longitude e os registros atuais são de demonstração.
O relatório explica a limitação (DEC-034).

## Arquivos e histórico

- **PDF** A4 com papel timbrado (faixa azul-noite com a marca, ficha do documento), cabeçalho nas
  páginas seguintes, rodapé com código, fonte e "Página X de Y". Tabelas quebram entre linhas
  repetindo o cabeçalho. PDFsharp (MIT); fontes Inter e Space Grotesk (OFL) embutidas.
- **Excel**: aba Resumo, uma aba por tabela (valores numéricos com formato), os dados de cada
  gráfico, as movimentações do período e a metodologia. ClosedXML (MIT).
- Cada emissão grava `RelatoriosGerados` (código, filtros, autor, tamanho, SHA-256) e um evento na
  Auditoria. O arquivo não é guardado: "Emitir de novo" gera outro com os mesmos filtros e os dados
  de hoje (código novo).
- `Relatorios:Organizacao` (opcional) aparece como "responsável pelos dados" no cabeçalho. Nenhum
  logotipo de órgão público ou porto é usado; o BarcaLog aparece como sistema que consolida os dados.

## API

`GET /api/v1/relatorios/opcoes` · `GET /api/v1/relatorios/previa` ·
`POST /api/v1/relatorios/exportar?formato=pdf|xlsx` · `GET /api/v1/relatorios/historico` ·
`POST /api/v1/relatorios/historico/{id}/reemitir`. Contratos em `dicionario-de-dados/gerado/api.md`.
