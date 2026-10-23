# Integrações — inventário e contratos

Nenhuma integração com sistema externo está **ativa** hoje: não há sistema conectado ao
endpoint de eventos e não há chave de integração cadastrada em produção (não existe produção da
API). As fontes de dado real hoje são a carga inicial e as planilhas importadas manualmente.

## 1. Inventário

| # | Integração | Direção | Dados compartilhados | Autenticação | Estado | Responsável (lado externo) |
|---|---|---|---|---|---|---|
| 1 | Portal web interno ↔ API | Portal → API | Tudo o que as telas mostram e gravam | JWT | **Implementado** | — |
| 2 | Portal do Transportador ↔ API | Portal → API | Veículos, condutores, ocorrências e contestações da transportadora | JWT (papel Transportadora) | **Implementado** (dados de demonstração) | Transportadoras (a cadastrar) |
| 3 | Planilhas dos terminais | Terminal → BarcaLog (manual) | COD_MOVIMENTO, SENHA, CONVENIO, COD_CONVENIO, CICLO, marcação, liberação | Upload por Gestor | **Implementado** | Unitapajós, TGPM, Hidrovias |
| 4 | Carga inicial (JSON) | Arquivo → BarcaLog | Movimentos históricos sem COD_CONVENIO | Comando do servidor | **Implementado** (uma vez) | — |
| 5 | Eventos de marcação/liberação | Sistema → BarcaLog | Movimento, terminal, senha, convênio, operador, carga, ciclo, datas | `X-Api-Key` | **Implementado, sem consumidor** | A definir |
| 6 | Protheus (TOTVS) | A definir | A definir | A definir | **Sem especificação** — perguntas em `../PROTHEUS.md` | TI do porto |
| 7 | Banco do Posto Mirian | Posto Mirian → BarcaLog (cessão, uma vez) | Veículos, ocorrências, motoristas, checkpoints, janelas, cargas, leituras de OCR (se existirem) | Canal seguro a combinar | **Aguardando os dados** (especificação de 03/08/2026) | Posto Mirian |
| 8 | Aplicativo móvel de fiscalização | App ↔ BarcaLog | Ocorrências, fotos, leituras de placa, checkpoints; cópia local de bloqueios e janelas | JWT do usuário (proposta) | **Planejado** (`../arquitetura/arquitetura-futura.md`) | Equipe BarcaLog |
| 9 | App de fiscalização existente (OCR, PostGIS) citado na proposta | Desconhecida | Desconhecidos | Desconhecida | **Sem acesso nem evidência** | A identificar |
| 10 | Transportadoras (B2B) | Transportadora → BarcaLog | Viagens, cargas, janelas, motoristas | JWT/API Key (proposta) | **Planejado**, sem contrato | Transportadoras |
| 11 | Notificações (N2/N3, alertas) | BarcaLog → parceiros | Placa, nível, motivo | A definir | **Planejado**, canal não definido | Porto / transportadoras |
| 12 | Exportação para análise (BI) | BarcaLog → planilha | Movimentações liberadas do recorte | JWT (download CSV) | **Implementado** (CSV) | — |

## 2. Contrato implementado: eventos de marcação/liberação

`POST /api/v1/integracao/eventos` — detalhes de campos e validações em `gerado/api.md`.

| Aspecto | Regra |
|---|---|
| Autenticação | `X-Api-Key`; a API guarda só o SHA-256 da chave (`dotnet run -- gerar-api-key <sistema>`). O autor nos logs é "Integração: <sistema>" |
| Lote | 1 a 1.000 eventos; corpo até 2 MB |
| Tipos | `Marcacao` cria o movimento ou atualiza campos informados; `Liberacao` fecha o movimento (exige `dataLiberacao`) |
| Identificador externo | `movimentoId` (1–32 letras, números e hífen) = COD_MOVIMENTO; é a chave do movimento no BarcaLog |
| Campos obrigatórios para criar | `terminalId`, `senha`, `convenio`, `operador`, `carga`, `dataMarcacao` (`ciclo` padrão 1) |
| Datas | Horário local do porto, sem fuso; entre 2000 e agora + 2 h; liberação ≥ marcação |
| Idempotência | `Idempotency-Key` recomendado: reenvio do mesmo lote não reaplica |
| Duplicidade | O mesmo `movimentoId` em dois eventos do lote é tratado em sequência (segundo atualiza o primeiro) |
| Erros | Eventos válidos são gravados; inválidos voltam em `erros` com `indice`, `movimentoId` e mensagem (resposta 200 com contagens). Lote fora do tamanho → 400 |
| Reprocessamento | Reenviar os eventos com erro após corrigir; os já gravados são atualizados sem duplicar |
| Auditoria | Um registro por lote com contagens (criados, atualizados, rejeitados) |
| Limite | 120 chamadas/min por sistema |
| Versionamento | Campo novo opcional = compatível em `/api/v1`; mudança de significado ou campo obrigatório novo = `/api/v2` |

**Limitações registradas (decisão pendente):**
- O evento recebido não é guardado como evento: o movimento é alterado direto. Não dá para
  reprocessar a partir do histórico de eventos.
- Um evento de `Marcacao` pode alterar qualquer campo (inclusive o terminal) de um movimento já
  importado por planilha — "o último que escreve vence". Não há ordem por horário do evento.
- A chave de integração não é limitada a um terminal: qualquer sistema com chave grava em qualquer terminal.

## 3. Mapeamento planilha → banco

| Coluna da planilha | Campo | Regra |
|---|---|---|
| `COD_MOVIMENTO` | `Marcacoes.MovimentoId` | Texto obrigatório (número perde zeros à esquerda e a linha é rejeitada) |
| `SENHA` | `Marcacoes.Senha` | Texto obrigatório |
| `CONVENIO` | `Marcacoes.Convenio` | Obrigatório |
| `COD_CONVENIO` | `Marcacoes.CodConvenio`, `Convenios.Codigo` | Opcional; convênio novo é cadastrado com operador/carga deduzidos do nome |
| `CICLO` | `Marcacoes.Ciclo` | Opcional |
| `DATA_MARCACAO` + `HORA_MARCACAO` | `Marcacoes.DataMarcacao` | Obrigatório; horário local do porto |
| `DATA_LIBERACAO` + `HORA_LIBERACAO` | `Marcacoes.DataLiberacao` | Opcional; ≥ marcação |
| (terminal escolhido na tela) | `Marcacoes.TerminalId` | Um terminal por arquivo |
| (convênio) | `Marcacoes.Operador`, `Marcacoes.Carga` | Copiados do cadastro de convênios |

Regras completas da importação: `../IMPORTACAO.md`.

## 4. O que fica centralizado e o que pode ser registrado fora

| Centralizado no backend (decide) | Pode ser registrado pelo cliente e enviado depois |
|---|---|
| Efeitos de N1/N2/N3, situação de negativação, reabilitação | Ocorrência observada em campo |
| Validação de agendamento e janela | Leitura de placa, foto, check-in/check-out |
| Alertas e indicadores | — |
| Permissões e escopo | — |

Nenhuma interface se comunica com outra diretamente: portal, app e sistemas externos só falam
com a API.
