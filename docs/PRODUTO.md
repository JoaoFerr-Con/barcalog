# Documento Mestre do Produto — BarcaLog

Referência central para decisões de produto. Antes de pedir uma funcionalidade nova (a pessoas
ou a agentes de programação), confira aqui e no [registro de decisões](arquitetura/registro-de-decisoes.md).

| Nível de informação | Onde está |
|---|---|
| O que o código faz hoje | [arquitetura/estado-atual.md](arquitetura/estado-atual.md), [dicionario-de-dados/](dicionario-de-dados/README.md) |
| O que foi decidido, o que é hipótese e o que está pendente | [arquitetura/registro-de-decisoes.md](arquitetura/registro-de-decisoes.md) |
| Para onde o produto quer ir (proposta, não implementado) | Este documento, §2–§5 e §11, e [arquitetura/arquitetura-futura.md](arquitetura/arquitetura-futura.md) |

Ordem das fontes: código e banco → documentação atual → documentos históricos (proposta de
30/07/2026, especificação de dados de 03/08/2026, apresentação) → diretrizes novas. Nada das
diretrizes novas está implementado só por estar escrito aqui.

---

## 1. Visão do produto e problemas que resolve

**Problema (documentos históricos):** o fluxo de carretas para os portos de Barcarena gera filas
nas vias urbanas, espera especulativa e pouca previsibilidade para porto, transportadoras e
município. Faltam dados confiáveis sobre chegada, espera, gargalos e disciplina das carretas.

**Visão (diretriz de 23/10/2026, em avaliação — DEC-024):** uma plataforma de gestão operacional
de movimentação de veículos em Barcarena, com:

- maturidade de ERP na forma de administrar dados (cadastros únicos, permissões, histórico,
  regras configuráveis, auditoria, relatórios confiáveis, integração entre módulos);
- profundidade operacional de sistemas de pátio e agendamento (previsto × realizado, entrada,
  saída, situação, ocupação);
- capacidade de atender **diferentes operações** — portuária/grãos hoje; distribuição,
  industrial/comercial, abastecimento e turismo/passageiros no futuro.

**Princípio central:** modelar em torno do **ciclo de vida da movimentação de um veículo** dentro
de uma operação, não de uma mercadoria, empresa ou terminal.

**Fora do escopo:** o BarcaLog não será um ERP completo. Financeiro, contabilidade, folha de
pagamento e faturamento não fazem parte do produto; quando necessários, vêm por integração.

**Situação real hoje:** o código é especializado no fluxo **portuário de grãos** (três terminais,
convênios com operador e carga, planilhas de marcação e liberação). A generalização é proposta,
não implementação.

## 2. Perfis, organizações e responsabilidades

### Hoje (código)

| Perfil | Pode | Escopo de dados |
|---|---|---|
| Gestor | Tudo, inclusive importação, usuários, parâmetros e exclusões | Todos os terminais, ou o terminal vinculado |
| Operador | Consultar e operar (alertas, agenda, portaria, ocorrências, cadastros) | Idem |
| Auditor | Somente leitura, inclusive auditoria | Idem |
| Transportadora | Portal: veículos, condutores, ocorrências e contestações | Só a própria transportadora |
| Sistema integrado | Enviar eventos de marcação/liberação | Todos os terminais (risco DEC-017) |

Não existe a entidade **organização**: o escopo é por terminal (internos) ou por transportadora (Portal).

### Proposto (não implementado — DEC-025)

Três dimensões separadas: **permissão** (o que pode fazer), **contexto operacional** (quais
organizações, locais e operações) e **preferência** (como organiza a própria tela — já existe e
não amplia acesso).

| Perfil proposto | Responsabilidade | Equivalente hoje |
|---|---|---|
| Administração municipal | Visão consolidada conforme atribuição legal; parâmetros públicos | Gestor sem vínculo |
| Gestor de operação/terminal | Demanda, capacidade, permanência, alertas da sua operação | Gestor/Operador vinculado ao terminal |
| Portaria | Fila, identificação, situação, movimentações que exigem ação | Operador (tela Portaria) |
| Fiscalização (campo) | Ocorrências, leituras de placa, evidências — via app | Não existe |
| Empresa/transportadora | Seus veículos, agendamentos, ocorrências e contestações | Portal do Transportador |
| Analista/auditor | Histórico, comparações, relatórios autorizados | Auditor |
| Administrador do sistema | Usuários, integrações, parâmetros técnicos | Gestor |

Pergunta aberta: o BarcaLog será operado pela gestão municipal, por empresas, ou por ambos com
ambientes separados? Recomendação técnica registrada: ambos, com organização como escopo.

## 3. Módulos atuais e futuros

Maturidade detalhada por módulo: [estado-atual.md §2](arquitetura/estado-atual.md).

| Núcleo comum (proposto) | Hoje |
|---|---|
| Cadastro de veículos (placa, categoria, características, situação, histórico) | `Veiculos` sem categoria nem histórico; dados de demonstração |
| Empresas e locais (transportadoras, operadores, estabelecimentos, terminais, pontos de controle) | `Transportadoras`, `Terminais`, `Convenios` (operador como texto); sem pontos de controle |
| Agendamentos (janelas, autorizações, capacidade, confirmação) | `Agendamentos` (demonstração; D0–D3 pendente — DEC-011) |
| Eventos e movimentações (chegada, entrada, passagem, atendimento, saída) | `Marcacoes` (marcação → liberação, real); `StatusPortaria` manual |
| Regras e conformidade (restrições, evidências, recursos, auditoria) | N1/N2/N3, contestação, auditoria |
| Indicadores e relatórios | Visão Geral, Análise 360°, alertas, relatório imprimível |

| Operação especializada (proposto) | Hoje |
|---|---|
| Portuária e grãos | Única implementada |
| Distribuição de bebidas, industrial/comercial, turismo/passageiros, outras | Não existem |

Regra de produto proposta: cada operação ativa só os controles que fazem sentido para ela (não
exigir janela de descarga, convênio ou terminal de um ônibus de turismo), sem virar um sistema
genérico sem controles.

## 4. Jornada de uma movimentação

| Etapa (genérica, proposta) | O que comprova a etapa hoje | Nível de evidência hoje |
|---|---|---|
| Identificação do veículo | Placa no cadastro (demonstração) | Registrada |
| Autorização / restrição | Situação de negativação (N3) | Registrada (regra no servidor) |
| Agendamento (quando aplicável) | `Agendamentos` (demonstração) | Registrada, sem conferência com a chegada |
| Chegada | **Marcação** nas planilhas do terminal | Observada na fonte do terminal |
| Passagens por pontos de controle | Não há (checkpoints A–D pendentes — DEC-012) | — |
| Situação no pátio/porto | `StatusPortaria` informado manualmente | Registrada, não confirmada |
| Atendimento / descarga | Não há evento próprio | — |
| Saída / liberação | **Liberação** nas planilhas do terminal | Observada na fonte do terminal |
| Ocorrências | `Ocorrencias` (demonstração) | Registrada |

Ligação entre etapas: as marcações reais **não se ligam** ao cadastro de veículos (as planilhas
não trazem placa). Enquanto isso não mudar, não é possível dizer "onde está este caminhão" com
dado real — só "quantas carretas foram marcadas e liberadas".

A tela deve sempre distinguir: **registrado** (alguém informou), **confirmado por evento**,
**estimado** e **desconhecido/desatualizado**.

## 5. Modelo de dados

**Atual:** [dicionário de dados](dicionario-de-dados/README.md) gerado do código (18 tabelas),
com [modelo relacional](dicionario-de-dados/modelo-relacional.md) e associações sem chave estrangeira.

**Futuro proposto (conceitual, não implementado — aprovar antes de migrar):**

| Conceito | Finalidade | Hoje | Lacuna |
|---|---|---|---|
| Organização | Escopo de dados e responsabilidade (município, empresa, operador, terminal) | Não existe | Base do acesso multiempresa |
| Local operacional | Terminal, pátio, posto, ponto de controle, estabelecimento | `Terminais` | Tipos de local e pontos de controle |
| Tipo de operação | Conjunto de regras ativas (portuária, distribuição…) | Implícito (só portuária) | Configuração por operação |
| Veículo | Identidade única com categoria e histórico | `Veiculos` (sem categoria/histórico) | Categoria, histórico de situação |
| Motorista | Pessoa que conduz (sem CPF — DEC-006) | `Condutores` | Vínculo por evento, não só por placa |
| Movimentação | Uma visita do veículo a uma operação, do previsto ao realizado | `Marcacoes` (sem veículo) | Ligar ao veículo e ao agendamento |
| Evento | Fato com hora, local, fonte e autor (chegada, passagem, saída, leitura de placa) | Não existe (estado atual sobrescrito) | Tabela de eventos imutável |
| Agendamento | Previsto: dia/hora, local, capacidade, autorização | `Agendamentos` | Ligação com a movimentação realizada |
| Ocorrência | Irregularidade com nível, evidência e recurso | `Ocorrencias`, `Contestacoes` | Evidências (fotos), geolocalização (DEC-014) |
| Restrição | Bloqueio ou condição de acesso com origem e validade | `StatusNegativacao` | Motivo, validade, reabilitação com comprovação (DEC-027) |
| Permissão | Papel × organização × operação | Papel + terminal + transportadora | Organização como escopo |

## 6. Regras de negócio

Comportamento implementado, com evidência: [regras-de-negocio.md](dicionario-de-dados/regras-de-negocio.md).

| Compartilhadas (todas as operações, proposta) | Específicas da operação portuária (hoje) |
|---|---|
| Identidade única do veículo; restrição impede novo agendamento | Convênio → operador e carga |
| Toda alteração auditada com autor, valores antes/depois e origem | Importação de planilhas de marcação/liberação |
| Situação sempre com nível de evidência | N1/N2/N3 e contestação |
| Idempotência em toda escrita externa | Faixas de espera D0…Estouro (nome a revisar — DEC-011) |

## 7. Arquitetura atual

Confirmada pelo código: [estado-atual.md](arquitetura/estado-atual.md). Resumo: React/Vite na
Vercel; API ASP.NET Core 8 em camadas; SQL Server 2022 via EF Core; JWT + MFA + API Key;
auditoria automática; CI com testes contra SQL Server real e verificação do dicionário.
**A API ainda não está hospedada** (DEC-020).

## 8. Integração e preparação para o aplicativo

[arquitetura-futura.md](arquitetura/arquitetura-futura.md) e
[integracoes.md](dicionario-de-dados/integracoes.md). Princípios: uma API versionada para todos
os clientes, regras no servidor, idempotência, ProblemDetails, escopo no servidor, adaptadores
para sistemas externos, nenhuma interface falando direto com outra.

## 9. Segurança, auditoria, LGPD e rastreabilidade

| Requisito | Situação |
|---|---|
| Autenticação forte | JWT curto com revogação, MFA para Gestor, bloqueio por força bruta, limites por IP/usuário |
| Autorização no servidor | Políticas por papel + escopo por terminal e por transportadora |
| Auditoria | Automática em toda gravação, com valores antes/depois |
| Minimização de dados | Sem CPF; anonimização de condutor; dados pessoais marcados no dicionário |
| Rastreabilidade do dado | Origem por movimento (carga inicial, planilha + linha, integração) |
| Retenção | **Pendente** (DEC-028) |
| Controlador/operador (LGPD) | Proposta comercial: cliente controlador, INOV operadora — a formalizar |

## 10. Indicadores

Catálogo com fórmula, fonte, período, unidade e limitação: [INDICADORES.md](INDICADORES.md) e
[dicionario-de-dados/indicadores.md](dicionario-de-dados/indicadores.md).

| Nível | Pergunta | Existe hoje | Falta |
|---|---|---|---|
| Operacional | O que está acontecendo agora? Quem chegou, aguarda, saiu, está impedido? | Frescor dos dados; fila virtual e situação de portaria (demonstração); estimativa das próximas horas | Eventos reais de chegada/passagem; ligação com veículo |
| Gerencial | Tempos de espera, cumprimento de janelas, ocorrências, capacidade, desempenho por operação | Espera mediana/P90, ≤ 24 h, esperas longas, por terminal/operador/carga, Análise 360° | Capacidades (DEC-029); conformidade de janelas (DEC-011) |
| Estratégico | Sazonalidade, gargalos recorrentes, cenários | Volume mensal, comparação de períodos, backtest da estimativa | Cenários ("e se o volume subir X%") com método documentado |

Regra: histórico, situação atual e estimativa sempre separados; nenhuma estimativa apresentada
como certeza.

## 11. Roadmap por fases

| Fase | Entrega | Situação | Critério de conclusão | Dependências e riscos |
|---|---|---|---|---|
| 1 — Diagnóstico | Inventário, maturidade por módulo, problemas priorizados | **Concluída** (fases 7–8 e 23/10/2026) | Documentos em `docs/arquitetura` e dicionário no CI | — |
| 2 — Modelo de negócio | Modelo conceitual aprovado, catálogo de regras, respostas às pendências | **Em andamento** (este documento) | DEC-011, 012, 016, 017, 024, 025 decididas | Disponibilidade do cliente e do porto |
| 3 — Maturidade do portal | Design system, cadastros padronizados, filtros, formulários, histórico, tratamento de erros, fim de dados fictícios | Não iniciada | Telas sem dado de demonstração; indicadores com origem verificável; testes de interface | Fonte real de cadastros e agenda; hospedagem (DEC-020) |
| 4 — Preparação do ecossistema | Organização como escopo, eventos imutáveis, contratos estáveis, sincronização para o app | Não iniciada | Contratos documentados e testados; isolamento por organização | Fase 2 aprovada |
| 5 — Expansão controlada | App móvel, novas operações, integrações externas, escala | Não iniciada | Critérios de adoção atendidos (DEC-019 a 023) | Fases 2–4 |

Regra: nenhuma migração estrutural antes da aprovação do modelo da fase 2.

## 12. Matriz de decisões

[arquitetura/registro-de-decisoes.md](arquitetura/registro-de-decisoes.md) — estados: confirmado,
hipótese, em avaliação, pendente, aprovado, descartado; com evidência, impacto, riscos,
alternativas, responsável e data.
