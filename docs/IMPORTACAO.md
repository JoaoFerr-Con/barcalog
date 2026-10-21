# Importação de planilhas

Como as planilhas de marcação e liberação dos terminais entram no BarcaLog.
Código: `Application/Servicos/ImportacaoPlanilhaServico.cs`,
`Domain/Regras/RegrasImportacao.cs`, `Infrastructure/Importacao/LeitorPlanilha.cs`,
`Infrastructure/Persistencia/Repositorios/ImportacaoRepositorios.cs`.

## Formato

- `.xlsx` (primeira aba) ou `.csv` (separador `;`, `,` ou tab), até 50 MB, um terminal por arquivo.
- Colunas obrigatórias: `COD_MOVIMENTO`, `SENHA`, `CONVENIO`, `DATA_MARCACAO`, `HORA_MARCACAO`.
- Opcionais: `COD_CONVENIO`, `CICLO`, `DATA_LIBERACAO`, `HORA_LIBERACAO`.
- Identificadores (`COD_MOVIMENTO`, `SENHA`, `COD_CONVENIO`) precisam vir como **texto**: célula
  numérica perde os zeros à esquerda e a linha é rejeitada com o motivo.
- Datas/horas sem fuso = horário local do porto.

## Etapas

1. **Análise** (`POST /api/v1/importacoes`). O arquivo é lido, cada linha vai para staging
   (`LinhasImportacao`) e é comparada com o que já existe em `Marcacoes`. Nada é gravado em
   marcações. Arquivo idêntico (SHA-256) já confirmado para o terminal gera aviso.
2. **Revisão**. Cada linha recebe uma classificação:

   | Classificação | Quando | Na confirmação |
   |---|---|---|
   | Igual | já existe com os mesmos valores | ignorada (só contada) |
   | Novo | movimento não existe | inserido |
   | Completa dados | existe, e a planilha só preenche `COD_CONVENIO` ou liberação vazios | atualiza só os campos vazios |
   | Conflito | existe com valores diferentes | depende da decisão: **Aplicar** (usa a planilha) ou **Manter** |
   | Rejeitada | erro de leitura (campo obrigatório, data inválida, identificador numérico, liberação antes da marcação…) | não entra |

   Avisos (não bloqueiam): espera acima de 72 h, sem liberação, possível remarcação (mesma
   senha, convênio e liberação, marcada de novo em até 10 minutos), convênio novo. Convênios
   novos têm operador e carga **deduzidos do nome** e ficam marcados para revisão em
   Configurações › Convênios.
3. **Confirmação** (`POST /{id}/confirmar`, Gestor). Exige todos os conflitos decididos. Aplica
   tudo numa transação (SQL set-based); recusa a análise se outro lote do mesmo terminal foi
   confirmado depois dela (análise obsoleta — analise de novo). Depois reavalia os alertas.
   **Cancelar** descarta a análise sem gravar nada.

Cada marcação gravada guarda `Origem = Planilha`, o lote e o número da linha; a tela
Movimentações mostra o arquivo e quem importou. O relatório por linha
(`GET /{id}/relatorio`) traz classificação, decisão, motivo e avisos de cada linha.
Toda ação fica na auditoria.

## Validação com os arquivos reais

As três planilhas entregues foram analisadas e as contagens conferidas com pandas
(novos, complementares, conflitos, avisos e remarcações batem). Os arquivos reais **não**
são versionados; os testes usam fixtures sintéticas.
