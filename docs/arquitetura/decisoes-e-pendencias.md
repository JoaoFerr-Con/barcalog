# Decisões, hipóteses e pendências

Fonte de verdade técnica: o código. Os documentos históricos (proposta comercial de 30/07/2026,
especificação de levantamento de dados de 03/08/2026 e apresentação "Barcarena: Cidade das
Oportunidades") servem para entender a visão, os requisitos funcionais e as regras de negócio.
A arquitetura técnica descrita neles **não é referência obrigatória**.

## 1. Decisões confirmadas (existem no código e/ou foram pedidas explicitamente)

| Decisão | Evidência |
|---|---|
| Backend .NET 8 + SQL Server + EF Core; frontend React/Vite | Código; `ARCHITECTURE.md` |
| Regras de negócio só no backend; o frontend não calcula indicadores | Código; `ARCHITECTURE.md` |
| API versionada `/api/v1`, idempotência, ProblemDetails, auditoria automática | Código |
| N3 bloqueia automaticamente o **veículo** (e o condutor identificado); N1 e N2 não bloqueiam; transportadora negativada é calculada | `RegrasNegativacao`; documentação original |
| CPF do condutor fora do produto | Comentário na entidade `Condutor` ("removido do produto de propósito") |
| Datas de marcação/liberação no horário local do porto, sem fuso; timestamps do sistema em UTC | Código e `docs/INDICADORES.md` |
| Vínculo de usuário a terminal é permissão (servidor); preferências não ampliam acesso | Fase 8; pedido do usuário |
| Dados fictícios só para demonstração, sempre sinalizados na tela | `AvisoDemonstracao`; pedido do usuário |
| Dicionário de dados gerado do código e verificado no CI | Esta fase |

## 2. Divergências entre documentos históricos e código

| Tema | Documento histórico | Código atual | Encaminhamento |
|---|---|---|---|
| Stack | Java/Python, PostgreSQL + PostGIS | .NET 8 + SQL Server | Manter a stack atual; migrar só com justificativa técnica |
| Janelas **D0–D3** | Dia autorizado para a carga (D0 = hoje, D1 = amanhã…); carreta fora da janela está em violação | Dois conceitos diferentes com o mesmo nome: `JanelaPermanencia` (faixas de **espera**: D0 = 0–24 h … Estouro > 144 h) e `JanelaConformidade` do agendamento ("compromisso de SLA: D0 até 24 h…") | **Decisão de negócio pendente**: qual é o significado oficial. Até lá, não criar indicador de "conformidade de janelas" |
| Checkpoints **A–D** (origem, entrada triagem, saída para o porto, descarga) | Registro com data/hora exata em cada ponto | Não há checkpoints. `StatusPortaria` (NoPatio, Aguardando, NoPorto, DescargaFinalizada, ForaDoPorto) é informado manualmente; as planilhas trazem só marcação e liberação | Definir com o porto quais pontos existem em cada terminal e qual evento comprova cada um |
| N2 | "Alerta imediato aos parceiros logísticos" | Sem notificação; só contagem de reincidência em 30 dias | Pendente: canal e destinatários |
| N3 | Bloqueio "em menos de 2 s / 10 s" com notificações automáticas | Bloqueio síncrono na mesma transação do registro; sem notificações | Pendente: notificações (ver infraestrutura futura) |
| Geolocalização (mapa de calor) | GPS na abordagem, PostGIS | Não coletada; campo `Local` é texto livre | Pendente com o app móvel |
| Integração B2B | Transportadoras "injetam viagens, cargas, janelas e motoristas" | Só existe evento de marcação/liberação por API Key | Pendente: contrato com as transportadoras |
| Mensageria, cache Redis, Kubernetes, banco de leitura | Previstos na arquitetura tática | Não existem e não são necessários no volume atual | Condições para adotar em `arquitetura-futura.md` |
| Marcacoes (comentário no código) | — | Comentário diz "os 103k registros importados de JSON"; hoje também entram planilhas e integração | Comentário desatualizado; a curadoria do dicionário traz a origem correta |

## 3. Hipóteses (não confirmadas)

| Hipótese | Base | Como confirmar |
|---|---|---|
| "Posto Mirian" é a operação/estabelecimento do cliente que **cede o próprio banco de dados** (veículos, ocorrências, motoristas, checkpoints, janelas, cargas, leituras de OCR) para o levantamento | Especificação de 03/08/2026 ("processo de cessão de dados do Posto Mirian") | Confirmar com o cliente o que é o Posto Mirian (pátio de triagem? posto de combustível? empresa?) e qual sistema ele usa |
| O dicionário pedido na proposta é o do **banco do Posto Mirian** (engenharia reversa), além do dicionário do BarcaLog | Mesma especificação, itens 2, 6 e 7 | Confirmar escopo. O dicionário entregue nesta fase é o do **BarcaLog** |
| SENHA e CICLO das planilhas têm significado operacional próprio de cada terminal | Nomes das colunas; ausência de definição | Pergunta já registrada em `docs/PROTHEUS.md` |

## 4. Alternativas em avaliação

| Tema | Alternativas | Onde |
|---|---|---|
| Banco local do app | SQLite direto × WatermelonDB | `arquitetura-futura.md` §1.4 |
| Hospedagem da API | Servidor da Prefeitura × Azure App Service + Azure SQL × Render/Railway com o Dockerfile | Conversa da fase 7 |
| Notificações assíncronas | Outbox no SQL Server × fila (RabbitMQ) | `arquitetura-futura.md` §3 |
| Sessão no app | Token atual de 8 h × refresh token com rotação | `arquitetura-futura.md` §1.3 |

## 5. Decisões pendentes (dependem do cliente/negócio)

1. **Posto Mirian**: o que é, quais registros pertencem a ele, quem é o gestor, e quando o banco
   será cedido (formato: dump SQL ou CSV, conforme a especificação).
2. Onde hospedar a API.
3. Significado oficial de D0–D3 e modelo de checkpoints A–D por terminal.
4. Capacidade diária e por hora de cada terminal.
5. Prazo de retenção de auditoria, avaliações de alertas e dados pessoais (LGPD).
6. Canal e destinatários das notificações de N2/N3.
7. Contrato de integração com o Protheus e com as transportadoras.
8. Stack do aplicativo móvel.
9. Transições permitidas de `StatusAgendamento` e de `StatusPortaria` (hoje livres).
10. Corrigir operador/carga de um convênio deve reclassificar os movimentos **já gravados**? Hoje
    não reclassifica (decisão registrada no `ConvenioServico`); a tela dizia o contrário e foi
    corrigida. Até a decisão, os indicadores por operador e carga usam o valor da época da importação.
11. Integração via API Key pode alterar qualquer campo (inclusive o terminal) de um movimento já
    importado por planilha: confirmar se é desejado ou se a integração deve só completar dados.
