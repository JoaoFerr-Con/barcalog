# Registro de decisões do BarcaLog

Registro central de decisões, hipóteses, alternativas e pendências. Regras de uso (adendo de
governança de 23/10/2026):

- **Confirmado**: verificado no código e preservado; só muda com evidência de inconsistência ou autorização explícita.
- **Hipótese**: não é requisito. **Em avaliação**: não é decisão. **Pendente**: decisão de negócio que o agente de programação não toma.
- **Aprovado**: decisão nova aprovada pelo responsável; **Descartado**: alternativa rejeitada, com motivo.
- Antes de implementar algo ligado a uma pendência: conferir aqui, documentar requisitos e alternativas,
  implementar só o que não antecipa a decisão e não publicar indicador, notificação ou processo baseado em suposição.

Fontes, em ordem: (1) código e banco; (2) documentação atual do repositório; (3) documentos
históricos — proposta comercial (30/07/2026), especificação de levantamento de dados (03/08/2026)
e apresentação; (4) diretrizes novas, sem presumir que estejam implementadas.

## Índice

| Id | Tema | Estado | Aprovação |
|---|---|---|---|
| DEC-001 | Stack .NET 8 + SQL Server + EF Core; React/Vite | Confirmado | Responsável pelo produto |
| DEC-002 | Arquitetura técnica histórica não é obrigatória | Confirmado | Responsável pelo produto |
| DEC-003 | Regras de negócio e cálculos oficiais no backend | Confirmado | Responsável pelo produto |
| DEC-004 | API versionada `/api/v1`, idempotência, ProblemDetails, auditoria automática | Confirmado | Responsável pelo produto |
| DEC-005 | N3 bloqueia veículo e condutor identificado; N1/N2 não bloqueiam; transportadora calculada | Confirmado | Responsável pelo produto |
| DEC-006 | CPF do condutor fora do produto | Confirmado | Responsável pelo produto |
| DEC-007 | Datas operacionais em horário local do porto; timestamps técnicos em UTC | Confirmado | Responsável pelo produto |
| DEC-008 | Vínculo a terminal é permissão no servidor; preferência não amplia acesso | Confirmado | Responsável pelo produto |
| DEC-009 | Dados fictícios só para demonstração e identificados na interface | Confirmado (com ressalva) | Responsável pelo produto |
| DEC-010 | Dicionário de dados derivado do código e verificado no CI | Confirmado | Responsável pelo produto |
| DEC-011 | Significado oficial de D0–D3 | Pendente | Negócio (porto) |
| DEC-012 | Checkpoints A–D | Pendente | Negócio (porto/terminais) |
| DEC-013 | Notificações de N2/N3 | Pendente | Negócio |
| DEC-014 | Geolocalização | Pendente (requisito futuro) | Negócio + LGPD |
| DEC-015 | Integração B2B com transportadoras e Protheus | Pendente | Negócio + TI do porto |
| DEC-016 | Reclassificação de movimentos ao corrigir convênio | Pendente | Negócio |
| DEC-017 | Atualização de movimentos pela integração (API Key) | Pendente — **risco sinalizado** | Negócio + responsável pelo produto |
| DEC-018 | Posto Mirian e o banco externo | Hipótese / pendente | Cliente |
| DEC-019 | Banco local do aplicativo: SQLite × WatermelonDB | Em avaliação | Responsável pelo produto |
| DEC-020 | Hospedagem da API | Em avaliação | Responsável pelo produto + cliente |
| DEC-021 | Notificação assíncrona: outbox no SQL Server × mensageria | Em avaliação | Responsável pelo produto |
| DEC-022 | Sessão: token atual × refresh token com rotação | Em avaliação | Responsável pelo produto |
| DEC-023 | RabbitMQ, Docker (produção) e Kubernetes | Fora da implementação atual | Responsável pelo produto |
| DEC-024 | Escopo do produto: núcleo do ciclo do veículo + operações especializadas | Em avaliação (diretriz de visão) | Responsável pelo produto |
| DEC-025 | Público do produto: gestão municipal, empresas ou ambos | Pendente | Responsável pelo produto + cliente |
| DEC-026 | Transições de situação de agendamento e de portaria | Pendente | Negócio |
| DEC-027 | Desnegativação manual sem motivo; N3 em placa não cadastrada | Pendente | Negócio |
| DEC-028 | Retenção de auditoria, avaliações e dados pessoais (LGPD) | Pendente | Cliente (controlador) |
| DEC-029 | Capacidade dos terminais | Pendente | Porto |
| DEC-030 | Endpoints analíticos legados não usados pelo portal | Em avaliação (parte reaproveitada nos relatórios, DEC-031) | Responsável pelo produto |
| DEC-031 | Central de relatórios: PDF e Excel gerados no servidor, sem impressão do navegador | Decidido (27/10/2026) | Responsável pelo produto |
| DEC-032 | Referência diária provisória de 1.000 carretas por terminal | Decidido como provisório (27/10/2026) | Responsável pelo produto; valor oficial com o porto (DEC-029) |
| DEC-033 | Sem "análise preditiva": só histórico e projeção testada | Decidido (27/10/2026) | Responsável pelo produto |
| DEC-034 | Mapa de ocorrências só com coordenadas reais | Decidido (27/10/2026); depende de DEC-014 | Responsável pelo produto |

---

## Decisões confirmadas — verificação no código (23/10/2026)

| Id | Decisão | Evidência no código | Resultado da verificação |
|---|---|---|---|
| DEC-001 | Stack | `backend/src/*.csproj` (net8.0, EF Core 8.0.11, SqlServer); `package.json` (React 18, Vite 7) | Conforme |
| DEC-002 | Arquitetura histórica não obrigatória | Nada de Java, PostgreSQL, PostGIS, RabbitMQ, Redis ou Kubernetes no repositório | Conforme |
| DEC-003 | Regras no backend | Regras em `Domain/Regras` e `Application/Metricas`; o frontend lê valores calculados (ex.: `negativacaoStore.reincidenciasN2` só lê o campo da API). `src/data/metricsEngine.js` e `relatorio.js` não são importados pelas telas — só pela ferramenta de fidelidade `backend/tools/verificar-fidelidade.mjs` | Conforme |
| DEC-004 | `/api/v1`, idempotência, ProblemDetails, auditoria | Todas as rotas em `/api/v1` (`gerado/api.md`); `[Idempotente]` em 15 escritas; `TratadorExcecoes` (RFC 7807 com `traceId`); `AuditoriaInterceptor` em toda gravação via EF | Conforme. Ressalva: gravações em lote via SQL (`ExecuteUpdate`/importação set-based) registram um evento de auditoria por operação, não por linha |
| DEC-005 | N3 / N1–N2 / transportadora | `RegrasNegativacao.AplicarOcorrencia`, `CalcularStatusTransportadora`; testes `RegrasNegativacaoTests` | Conforme. Riscos em DEC-027 |
| DEC-006 | Sem CPF | Nenhuma coluna, DTO, tela ou migration com CPF; menções só em comentários que registram a remoção | Conforme |
| DEC-007 | Fuso | `Marcacoes.DataMarcacao/DataLiberacao` sem conversor (horário local); `EmUtc()` nos timestamps de sistema | Conforme |
| DEC-008 | Vínculo × preferência | `EscopoTerminalFiltro`, `EscopoTerminal`, `AtualizarPreferenciasAsync`; teste `Usuario_vinculado_so_ve_o_proprio_terminal_e_preferencia_nao_amplia_acesso` | Conforme. Lacunas documentadas: auditoria e histórico de avaliações não filtrados por terminal |
| DEC-009 | Dados fictícios identificados | `AvisoDemonstracao` em Portaria, Agendamentos, Negativação, Frota, Cadastros, Visão Geral e Portal, exibido quando o banco tem o cadastro de exemplo | **Ressalva**: o aviso depende de o banco conter o cadastro de exemplo (`ExistemDadosExemploAsync`). Se alguém cadastrar dados reais misturados aos fictícios, o aviso continua global, sem distinguir registro a registro |
| DEC-010 | Dicionário verificado no CI | `DicionarioDados.cs`; passo "Dicionário de dados em dia com o código" em `.github/workflows/backend.yml` | Conforme |

Data de confirmação: 23/10/2026 (adendo de governança). Aprovação: responsável pelo produto.

---

## Pendências e divergências

### DEC-011 — Significado oficial de D0–D3

| Campo | Conteúdo |
|---|---|
| Pergunta | O que significa D0–D3 para o porto? |
| Estado | Pendente |
| Evidências | Histórico: dia autorizado da carga (D0 = hoje, D1 = amanhã; carreta fora do dia está em violação). Código: (a) `JanelaPermanencia` — faixas de **duração da espera**: D0 0–24 h, D1 24–48 h, D2 48–72 h, D3 72–96 h, Alerta 96–144 h, Estouro > 144 h (`Domain/Regras/JanelaPermanencia.cs`); (b) `JanelaConformidade` — valor D0–D3 escolhido no agendamento, comentado como "compromisso de SLA (D0 até 24 h…)", não verificado contra nada |
| Consumidores atuais | `JanelaPermanencia`: campo `janela` de `MarcacaoDto` (lista e detalhe de `/api/v1/marcacoes`), `/marcacoes/janelas-permanencia`, `/marcacoes/estouro-critico` (descontinuado) e motor legado (`MotorMetricas`: "taxa D0" = espera ≤ 24 h em `/marcacoes/indicadores-performance`). `JanelaConformidade`: coluna `Agendamentos.JanelaConformidade`, contratos de agendamento e tela Agendamentos. Ainda um quarto uso da palavra: "janelas" da estimativa das próximas horas (`EstimativaJanelas`) são **faixas horárias**, sem relação com D0–D3 |
| Impacto funcional | Sem definição não existe indicador de conformidade de janelas |
| Impacto técnico | Mesmo rótulo para três conceitos (dia autorizado, faixa de espera, compromisso do agendamento) |
| Riscos | Interpretar faixa de espera como janela autorizada e publicar conformidade errada |
| Alternativas | Ao aprovar: nomes explícitos — `FaixaEspera` (hoje `JanelaPermanencia`), `PrazoCompromissoAgendamento` ou `DiaAutorizado` (conforme a definição). Renomear só em `/api/v2` ou com campo novo em paralelo, sem quebrar `/api/v1` |
| Decisão | Nenhuma. Até lá: não criar indicador oficial de conformidade; não converter faixa de espera em janela; não alterar valores |
| Módulos afetados | Agendamentos, indicadores legados, dicionário (`gerado/enums.md`) |

### DEC-012 — Checkpoints A–D

| Campo | Conteúdo |
|---|---|
| Pergunta | Quais pontos de controle existem em cada terminal e qual evento comprova cada passagem? |
| Estado | Pendente |
| Evidências | Histórico: A origem, B entrada triagem, C saída para o porto, D descarga. Código: não há checkpoints; `Veiculos.StatusPortaria` é informado manualmente, sem histórico; planilhas trazem só marcação e liberação |
| Perguntas a responder | Pontos físicos por terminal; evento que comprova cada um; quem registra e com qual dispositivo/sistema; manual, automático ou integrado; correção, duplicidade e evento fora de ordem |
| Regra até a decisão | Não inferir horários intermediários de marcação e liberação; não apresentar checkpoints como implementados |
| Proposta técnica (não implementada) | Tabela de eventos de passagem imutável (veículo/movimento, ponto, horário, fonte, autor, UUID), com estados derivados dos eventos — permite tempo por etapa e prova de passagem |
| Módulos afetados | Portaria, Agendamentos, indicadores futuros, app móvel |

### DEC-013 — Notificações de N2 e N3

| Campo | Conteúdo |
|---|---|
| Estado | Pendente. **Não existe sistema de notificações no código** |
| Condição que gera | Implementada: N3 negativa (registro da ocorrência); N2 conta reincidência em 30 dias |
| Evento que dispara a notificação | A definir (registro de N3? reincidência N2 a partir de quantas?) |
| Destinatários autorizados | A definir (transportadora, porto, fiscalização) |
| Canal | A definir (e-mail, SMS, WhatsApp, notificação no Portal, push no app) |
| Prazo de entrega | A definir (o histórico cita "menos de 2 s / 10 s" para o bloqueio, não para a notificação) |
| Tentativas e falhas | Proposta: outbox com reenvio e limite de tentativas (ver DEC-021) |
| Registro | Proposta: envio, entrega, falha e reprocessamento por notificação |
| Duplicidade | Proposta: chave única por (evento, destinatário, canal) |
| Regra | Nenhum disparo real antes da definição de destinatários, permissões e comportamento |

### DEC-014 — Geolocalização

| Campo | Conteúdo |
|---|---|
| Estado | Requisito futuro a avaliar, para o app móvel |
| Evidência | Não há captura de GPS; `Ocorrencias.Local` é texto livre e não comprova coordenadas |
| A definir | Finalidade (mapa de irregularidades?), precisão necessária, permissão do usuário no aparelho, base legal (LGPD), retenção, quem vê |
| Regra | Não apresentar mapa de calor nem localização precisa como dado existente |

### DEC-015 — Integração B2B (transportadoras) e Protheus

| Campo | Conteúdo |
|---|---|
| Estado | Pendente |
| Implementado | Só eventos de marcação/liberação (`POST /api/v1/integracao/eventos`, API Key). **Não é** integração B2B de viagens, cargas, motoristas e janelas |
| Desejado (histórico) | Transportadoras injetando viagens, cargas, janelas e motoristas |
| A definir | Entidades compartilhadas, autenticação por organização, propriedade dos dados, responsabilidades, validações, idempotência, regras de atualização. Protheus: perguntas em `../PROTHEUS.md` |
| Módulos afetados | Integração, Agendamentos, Cadastros, Portal |

### DEC-016 — Reclassificação por convênio

| Campo | Conteúdo |
|---|---|
| Pergunta | Corrigir operador/carga de um convênio deve alterar os movimentos já gravados? |
| Estado | Pendente. **Comportamento atual preservado**: não reclassifica |
| Evidências | `ConvenioServico` (comentário "vale para os próximos movimentos"); importação copia operador/carga para `Marcacoes`; a tela dizia o contrário e foi corrigida em 23/10/2026 |
| Impacto | Indicadores por operador e carga mostram a classificação **da época da importação** |
| Proposta (não implementada) | Distinguir classificação histórica (gravada no movimento), classificação atual (do cadastro) e histórico de alterações do convênio; os indicadores escolhem explicitamente qual usar |
| Regra até a decisão | Não reclassificar retroativamente; documentar a origem temporal (feito em `dicionario-de-dados/indicadores.md`) |

### DEC-017 — Atualização de movimentos pela integração (API Key)

**Risco sinalizado (integridade e autorização).** Detalhes e matriz de campos em
`../dicionario-de-dados/integracoes.md` §2.1.

| Campo | Conteúdo |
|---|---|
| Pergunta | A integração pode substituir qualquer campo de um movimento já existente, ou só completar dados? |
| Estado | Pendente. **Comportamento atual preservado** |
| Evidências | `IntegracaoServico.Atualizar`: com `movimentoId` existente, troca terminal, senha, convênio, código, operador, carga, ciclo e datas sempre que o evento os traz — inclusive em movimentos importados por planilha e confirmados por Gestor. A chave de API não é limitada a terminais |
| Impacto | Um sistema com chave pode mover um movimento de terminal ou alterar datas já validadas; a `Origem` continua "Planilha" (a alteração fica só na auditoria, com valores antes/depois) |
| Exposição atual | Baixa: a API não está hospedada e não há chave de integração emitida. O risco nasce na primeira chave entregue |
| Mitigação proposta (compatível com `/api/v1`, não implementada) | (1) Configurar terminais permitidos por chave (`Integracao:ApiKeys:N:Terminais`) e recusar evento fora deles; (2) modo "só completar" para movimentos de origem Planilha (preenche vazios, não substitui), com conflito devolvido em `erros`; (3) registrar a última origem que alterou o movimento |
| Decisão | Nenhuma. Recomendação técnica: aplicar (1) antes de emitir a primeira chave |

### DEC-018 — Posto Mirian e o banco externo

| Campo | Conteúdo |
|---|---|
| Estado | Hipótese / pendente |
| Evidência | Especificação de 03/08/2026: "processo de cessão de dados do Posto Mirian", com levantamento de veículos, ocorrências, motoristas, checkpoints, janelas, cargas e logs de OCR |
| O que **não** se assume | Que seja terminal, pátio de triagem, posto de combustível ou dono de uma base específica |
| Perguntas | Natureza da operação; organização responsável pelos dados; quem autoriza o compartilhamento; sistemas e registros disponibilizados; formato (dump SQL ou CSV); finalidade autorizada; dados necessários; segurança, retenção e descarte |
| Regra | O dicionário desta fase descreve o BarcaLog. O dicionário do banco externo é entregável separado, condicionado ao escopo confirmado e ao recebimento autorizado |
| Também pendente | A "visão do gestor do Posto Mirian" pedida na fase 8 |

---

## Alternativas em avaliação

### DEC-019 — Banco local do aplicativo: SQLite × WatermelonDB

| Critério | SQLite direto | WatermelonDB |
|---|---|---|
| Requisitos atendidos | Armazenamento local, consultas, transações, outbox | Os mesmos + primitivas de sincronização e carga sob demanda |
| Custos e limitações | Sincronização feita pela equipe | Só React Native/JS; esquema próprio da biblioteca |
| Segurança | Cifragem via SQLCipher ou equivalente da stack | Depende do adaptador SQLite usado — **a verificar** |
| Esforço operacional | Baixo em execução; maior em desenvolvimento | Menor em sincronização; exige endpoints no formato da biblioteca |
| Manutenção | Código próprio de sincronização para manter | Dependência de biblioteca de terceiros e sua comunidade |
| Dependências | Biblioteca SQLite da stack | React Native |
| Critério de adoção | Stack não-RN ou necessidade de controle total da sincronização | Stack RN escolhida e volume local grande com UI reativa |
| Migração/reversão | Troca de stack exige reescrever a camada de dados | Sair do WatermelonDB = reescrever modelos e sincronização |

Decisão: nenhuma; depende da escolha da stack do app. Detalhes em `arquitetura-futura.md` §1.4.

### DEC-020 — Hospedagem da API

| Critério | Servidor da Prefeitura | Azure App Service + Azure SQL | Outro PaaS com o Dockerfile (ex.: Render, Railway) |
|---|---|---|---|
| Requisitos atendidos | API + SQL Server sob controle municipal | API + SQL gerenciados, HTTPS, backups | API em container; banco à parte |
| Custos e limitações | Custo de infraestrutura e equipe próprias; disponibilidade depende do datacenter | Mensalidade por plano; dependência de nuvem | Mensalidade; SQL Server gerenciado nem sempre disponível no mesmo provedor |
| Segurança | Rede e patches sob responsabilidade local | Recursos gerenciados (TLS, identidades, firewall) | Varia por provedor |
| Esforço operacional | Alto (SO, IIS/Kestrel, backup, certificados) | Baixo | Baixo/médio |
| Dependências | Acesso de rede externo para Vercel/Portal | Conta Azure | Conta do provedor + banco compatível |
| Critério de adoção | Exigência de soberania/hospedagem pública | Prazo curto, equipe pequena | Teste rápido |
| Migração/reversão | Backup/restore do SQL Server (`backend/database/02-backup-e-restauracao.sql`) entre qualquer opção | Idem | Idem |

Decisão: pendente com o cliente. Sem ela, o portal publicado na Vercel não tem backend.

### DEC-021 — Notificação assíncrona: outbox no SQL Server × mensageria

| Critério | Outbox transacional no SQL Server + serviço em segundo plano | Fila (RabbitMQ ou similar) |
|---|---|---|
| Requisitos atendidos | Envio confiável após gravação, reenvio, histórico | Os mesmos + vários consumidores, alto volume, desacoplamento |
| Custos e limitações | Nenhum componente novo; throughput limitado ao banco | Novo serviço para operar e monitorar |
| Segurança | Mesma superfície atual | Nova credencial e porta de rede |
| Critério de adoção | Primeiras notificações (DEC-013) | Volume ou número de consumidores que o outbox não suporte, medido |
| Migração | Outbox pode publicar na fila depois, sem mudar quem grava | — |

### DEC-022 — Sessão: token atual × refresh token

| Critério | Token atual (8 h, sem refresh, revogável por `VersaoToken`) | Refresh token com rotação |
|---|---|---|
| Atende | Portal web em turno de trabalho | App em campo com conexão intermitente |
| Segurança | Token curto e revogação global por usuário | Exige armazenamento seguro, detecção de reuso e revogação por aparelho |
| Esforço | Nenhum | Tabela de sessões/aparelhos, endpoint de renovação, testes |
| Critério de adoção | — | Início do app móvel |

### DEC-023 — RabbitMQ, Docker em produção e Kubernetes

Fora da implementação atual. Docker existe só para desenvolvimento e CI. Condições de adoção e
itens do código que assumem instância única: `arquitetura-futura.md` §3.

---

## Produto

### DEC-024 — Núcleo do ciclo do veículo + operações especializadas

| Campo | Conteúdo |
|---|---|
| Diretriz | Modelar em torno do ciclo de vida da movimentação de um veículo, não de uma mercadoria, empresa ou terminal; núcleo comum + regras por tipo de operação (portuária/grãos, distribuição, industrial/comercial, turismo, outras) |
| Estado | Em avaliação. O código atual é **específico do fluxo portuário de grãos** (terminais, convênios com operador/carga, planilhas dos terminais) |
| Proposta | Modelo conceitual em `../PRODUTO.md` §5 (atual × futuro) |
| Regra | Não migrar a estrutura antes de aprovar o modelo conceitual |

### DEC-025 — Público do produto

| Campo | Conteúdo |
|---|---|
| Pergunta | O BarcaLog será operado pela gestão municipal, usado diretamente por empresas e operadores, ou ambos com ambientes e permissões separados? |
| Estado | Pendente |
| Evidências | Hoje: usuários internos (Operador, Gestor, Auditor), Portal do Transportador e vínculo por terminal; não há entidade "organização" |
| Recomendação técnica | Ambos, com organização como escopo de dados (generalização do vínculo por terminal e da transportadora do Portal) |

### DEC-026 — Transições de agendamento e portaria

Pendente. Hoje qualquer situação vai para qualquer outra, manualmente. Ver `../dicionario-de-dados/regras-de-negocio.md` §4–5.

### DEC-027 — Riscos da negativação

Pendente. (a) N3 em placa não cadastrada não bloqueia nada; (b) desnegativação manual sem motivo
nem comprovação, enquanto o histórico prevê análise/medida corretiva; (c) carreta negativada pode
ter a situação de portaria alterada livremente. Comportamento atual preservado.

### DEC-028 — Retenção (LGPD)

Pendente com o controlador dos dados. Sem expurgo hoje em auditoria, avaliações de alertas e
cadastros; idempotência guarda respostas por 24 h.

### DEC-029 — Capacidade dos terminais

Pendente com o porto. Sem capacidade, ocupação e comparação da agenda com limite não são calculadas.

### DEC-030 — Endpoints analíticos legados

Em avaliação. Cerca de 35 endpoints de `/api/v1/marcacoes/*` não são usados pelo portal atual (15
marcados como descontinuados). Opções: manter para integração ou remover em `/api/v2`.
Em 27/10/2026 a central de relatórios (DEC-031) passou a reaproveitar, revisados, o ranking de maiores
esperas, os totais por operador e carga, os dias de maior movimento, os dias acima da referência e o
histórico mensal. Os endpoints antigos continuam como estavam; os relatórios usam
`Application/Relatorios`, sobre os mesmos registros da Análise 360°.

## Relatórios (27/10/2026)

### DEC-031 — Central de relatórios

Relatórios gerencial, operacional e de análise 360° em `/api/v1/relatorios` (opções, prévia,
exportar, histórico, reemitir). A prévia é o mesmo documento que vira PDF (PDFsharp, MIT) e Excel
(ClosedXML, MIT), então os números não mudam entre tela e arquivo. Os indicadores vêm da Análise 360°
(`AnaliseMovimentacoes.Calcular`). O relatório imprimível da Visão Geral (impressão do navegador) foi
removido; o botão "Relatório" abre a central com o mesmo período e terminal. Cada emissão grava
`RelatoriosGerados` (código, filtros, autor, SHA-256 do arquivo) e um evento de auditoria; o arquivo
não é guardado. Fontes Inter e Space Grotesk (OFL) embutidas no assembly. O contexto externo de safra
fica em `backend/src/BarcaLog.Api/Relatorios/contexto-externo.json`, atualizável sem recompilar.

### DEC-032 — Referência diária de 1.000 carretas

Usada em "dias acima da referência diária", por terminal, enquanto o terminal não tem capacidade
cadastrada (DEC-029). Configurável em `Relatorios:ReferenciaDiariaCarretas`. O relatório diz sempre
que é referência de comparação, não capacidade oficial, e não soma referências entre terminais.
Observação com os dados reais (dez/2025 a set/2026): o maior volume diário de um terminal foi 331
(TGPM, 18/04/2026) e o maior dia somando os três terminais, 836 (17/04/2026). Com 1.000 por terminal,
nenhum dia aparece acima da referência.

### DEC-033 — Projeção de volume

A "análise preditiva" (tendência com faixa de confiança não validada) não é usada. O relatório mostra
histórico, tendência recente e uma estimativa simples (média dos três meses completos anteriores) só
quando o teste com o histórico dá erro médio de até 15%. Com os dados atuais o erro foi de 28,1%,
então não há projeção publicada. A estimativa de 6 horas dos alertas não foi alterada.

### DEC-034 — Mapa de ocorrências

As ocorrências não têm latitude/longitude (só texto do local) e são de demonstração. O relatório
explica a limitação em vez de distribuir pontos no mapa. A localização do Posto Mirian (PA-483) pode
ser referência visual, mas não substitui a coordenada de cada ocorrência. Depende de DEC-014.
