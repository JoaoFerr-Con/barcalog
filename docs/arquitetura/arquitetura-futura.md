# Arquitetura futura — aplicativo móvel, integrações e infraestrutura

Este documento registra **preparação arquitetural**, não implementação. Nada aqui foi
instalado, configurado ou escolhido em definitivo. O que já existe está em `estado-atual.md`;
decisões e pendências em `decisoes-e-pendencias.md`.

Princípio que guia tudo abaixo: **as regras de negócio ficam no backend**. Portal, aplicativo e
sistemas externos são clientes da mesma API versionada (`/api/v1`). Nenhuma interface fala
diretamente com outra; todas falam com a API, que é a única dona do dado.

---

## 1. Aplicativo móvel

### 1.1 O que se sabe e o que não se sabe

| Item | Situação |
|---|---|
| Existe um app no repositório? | **Não.** |
| Existe um app de fiscalização fora daqui? | A proposta comercial (jul/2026) cita um app existente com OCR, SQLite, PostgreSQL + PostGIS e backend Java/Python. **Não há código, acesso nem contrato de API desse app neste projeto.** A informação é histórica e não foi verificada. |
| Stack do novo app | **Não definida.** Não se assume a stack do portal (React) nem a do app antigo. |
| Usuários do app | Fiscais em campo e operadores de pátio (documento original). A confirmar. |
| Funções previstas (documento original) | Leitura de placa (OCR), consulta de bloqueio N3, exibição da janela autorizada, registro de ocorrência N1/N2/N3 com foto, check-in/check-out de pátio (checkpoints B e C). |

### 1.2 Divisão de responsabilidades proposta

| Fica no backend (centralizado, decide) | Pode ser registrado no app sem conexão (fato observado, decide depois) |
|---|---|
| Efeito de uma ocorrência (N3 negativa veículo e condutor) | Registro da ocorrência: placa, nível escolhido, descrição, foto, horário do aparelho, local (se autorizado) |
| Situação oficial de negativação de veículo, condutor e transportadora | Leitura de placa (OCR) com a imagem de origem |
| Validação de janela/agendamento, conflitos de horário | Check-in/check-out de pátio (evento de checkpoint) |
| Contestações, reabilitação, aprovação | — |
| Usuários, papéis, vínculo por terminal, parâmetros | — |
| Indicadores e alertas | — |

O app pode **pré-validar** formato (placa, campos obrigatórios) para dar retorno rápido, mas a
decisão final é sempre do servidor ao receber o registro. Consultas feitas offline (ex.: "esta
placa está bloqueada?") usam uma **cópia local com data de atualização visível**, e a tela deve
dizer quando a informação pode estar desatualizada — nunca apresentar a cópia como situação atual.

### 1.3 Operação offline — requisitos

| Requisito | Proposta (a validar na fase do app) |
|---|---|
| Armazenamento local | Banco local no aparelho com: fila de operações pendentes (outbox), cópias de referência (placas bloqueadas, janelas do dia, cadastros mínimos) e arquivos (fotos) |
| Sincronização posterior | Envio da fila quando houver conexão, em lotes, na ordem de criação; recebimento incremental das cópias de referência por cursor ("mudanças desde") |
| Prevenção de duplicidade | Cada operação nasce no aparelho com um identificador único (UUID). Ele vai como `Idempotency-Key` e como chave única no servidor: reenviar nunca duplica. O mecanismo de idempotência **já existe** na API |
| Conflitos | Registros de campo são **eventos imutáveis** (só se acrescenta; não há edição concorrente). Dados que mudam (situação do veículo) seguem **servidor vence** + aviso ao usuário. Edição de cadastro offline: não recomendada na primeira versão |
| Rastreabilidade | Cada evento guarda: UUID, usuário, aparelho, horário do aparelho, horário de recebimento no servidor, versão do app, local (opcional). Divergência grande entre relógios é sinalizada, não corrigida em silêncio |
| Conectividade intermitente | Reenvio com espera crescente; lotes pequenos; cada item do lote responde sucesso/erro individual (o endpoint de integração atual já devolve erros por índice) |
| Segurança dos dados locais | Banco local cifrado; chave no cofre do sistema (Keystore/Keychain); fotos fora da galeria pública; apagar dados locais ao sair ou quando o servidor revogar o acesso |
| Expiração de credenciais | Hoje o token vale 8 h e **não há refresh token**. Operações feitas com o token vencido ficam na fila e só são enviadas após novo login; o app **nunca** estende a validade sozinho. Para uso em campo, avaliar refresh token com rotação e revogação (`VersaoToken` já permite revogar tudo de um usuário) |
| Reenvio seguro | Envio só com token válido; o servidor reavalia permissão e escopo **no momento do recebimento** (um usuário desvinculado enquanto estava offline tem o envio recusado e o app mantém a operação para revisão) |

### 1.4 Camada de dados local: SQLite × WatermelonDB

Avaliação para decisão futura. **Nenhuma das duas foi escolhida**, e não se decidiu se seriam
usadas juntas — a escolha depende da stack do app, que também não foi definida.

| Critério | SQLite (direto, com a biblioteca da stack escolhida) | WatermelonDB |
|---|---|---|
| O que é | Banco relacional embarcado; disponível em praticamente toda plataforma móvel | Camada de dados para **React Native** (e web), que no celular grava em SQLite |
| Dependência de stack | Nenhuma: Kotlin/Swift nativos, Flutter, React Native, .NET MAUI têm acesso a SQLite | Exige React Native (ou JS na web). Escolher WatermelonDB **é** escolher React Native |
| Sincronização | Não traz protocolo pronto: outbox, cursor e conflitos são implementados pela equipe | Traz primitivas de sincronização (pull de mudanças desde um marco + push de mudanças locais); o servidor precisa expor endpoints nesse formato |
| Desempenho com muitos registros | Bom; depende de índices e consultas bem feitas | Projetado para carregar sob demanda e observar mudanças sem travar a interface |
| Criptografia local | SQLCipher ou equivalente, conforme a biblioteca | Depende do adaptador SQLite usado por baixo — **a verificar** antes de decidir |
| Modelo de dados | SQL livre; migrations próprias | Esquema e modelos próprios da biblioteca; migrations próprias |
| Custo de manutenção | Mais código de sincronização na equipe | Menos código de sincronização, mais acoplamento à biblioteca e à sua comunidade |
| Uso conjunto | — | Como o WatermelonDB usa SQLite por baixo, "usar os dois" na prática significa adotar WatermelonDB; acessar o mesmo arquivo SQLite por fora dele não é recomendado |

Critérios para decidir (na fase do app): stack escolhida e experiência da equipe; volume de dados
local esperado (placas bloqueadas, janelas, cadastros); necessidade de criptografia; quanto da
sincronização já vem pronto; prazo; facilidade de testar a sincronização com o backend .NET.

### 1.5 O que a API precisaria ganhar para o app (não implementado)

| Necessidade | Hoje | Proposta |
|---|---|---|
| Receber eventos de campo | Só existe evento de marcação/liberação (`/api/v1/integracao/eventos`), autenticado por API Key de sistema | Endpoint de lote de eventos de campo, autenticado pelo **usuário** (JWT), com UUID por evento e resposta por item |
| Tabela de eventos imutável | Integração atual altera `Marcacoes` direto, sem guardar o evento recebido | Tabela de eventos recebidos (append-only) com UUID único; efeitos aplicados a partir dela — dá trilha completa e permite reprocessar |
| Sincronização incremental | Não há "mudanças desde X"; a maioria das tabelas não tem data de atualização | Coluna de versão/atualização nas tabelas de referência e endpoint de mudanças por cursor (inclui exclusões) |
| Fotos e evidências | Não há armazenamento de arquivos | Armazenamento de objetos + metadados (hash, tamanho, autor) e upload retomável |
| Identificação do aparelho | Não existe | Registro do aparelho por usuário, para auditoria e revogação |
| Renovação de sessão | Token de 8 h sem refresh | Refresh token com rotação, revogável |

---

## 2. Integração entre sistemas

Inventário completo em `../dicionario-de-dados/integracoes.md`. Regras de arquitetura:

1. **Uma porta de entrada por tipo de cliente**, todas na mesma API versionada: usuários (JWT),
   Portal do Transportador (JWT com escopo da transportadora), sistemas (API Key). Mudança
   incompatível vai para `/api/v2`, mantendo `/api/v1` durante a transição.
2. **Idempotência obrigatória** em toda escrita vinda de fora (`Idempotency-Key`).
3. **Erros padronizados** (ProblemDetails, RFC 7807) com `traceId`, e erro por item em lotes.
4. **Validação no servidor**, sempre; o cliente valida só para dar retorno rápido.
5. **Auditoria** de toda gravação, com o autor (usuário ou "Integração: <sistema>").
6. **Escopo de dados no servidor** (terminal, transportadora), nunca só no cliente.
7. **Adaptadores** convertem o formato de cada sistema externo para os contratos do BarcaLog
   (ex.: Protheus → eventos de marcação/liberação), em vez de espalhar formatos externos pelo código.

---

## 3. Infraestrutura futura

Docker, RabbitMQ e Kubernetes são **possibilidades** para etapas futuras. Não fazem parte da
implementação atual e não devem ser introduzidos sem autorização e uma justificativa ligada a
um problema real medido.

| Tecnologia | Situação hoje | Problemas que justificariam adotar | Alternativa mais simples a tentar antes |
|---|---|---|---|
| **Docker** | `Dockerfile` da API e `docker-compose` do SQL Server existem, usados em desenvolvimento e CI. Nenhuma produção usa container | Padronizar implantação em servidor próprio ou nuvem; ambientes idênticos | Publicação direta do .NET em um App Service/VM |
| **RabbitMQ** (ou outra fila) | Não existe. Processamento assíncrono é um serviço em segundo plano dentro da API | Notificar parceiros sem travar a requisição (N2/N3); picos de eventos do app ou de integrações; reprocessamento com novas tentativas; vários consumidores independentes | Tabela "outbox" no SQL Server + serviço em segundo plano que envia e reenvia — resolve notificação confiável sem novo componente |
| **Kubernetes** | Não existe | Vários serviços independentes (ex.: OCR com escala própria), escala automática em safra, alta disponibilidade com várias instâncias | Uma instância da API com plano de escala vertical; serviço gerenciado com 2 instâncias |
| **Cache distribuído (Redis)** | Não existe; cache em memória do processo | Várias instâncias da API precisando do mesmo cache; consultas de bloqueio do app com latência mínima | Cache em memória + invalidação após gravação (já existe) |
| **Banco de leitura separado / particionamento** | Não existe | Relatórios pesados afetando a operação; tabelas com dezenas de milhões de linhas | Índices e agregações em SQL; views indexadas |

### 3.1 Antes de rodar mais de uma instância da API

Itens do código atual que assumem **uma única instância** e precisariam mudar (levantamento, não
correção):

| Ponto | Por que importa | Caminho |
|---|---|---|
| Avaliação de alertas usa trava em memória (`SemaphoreSlim` estático) e roda em toda instância | Duas instâncias avaliariam ao mesmo tempo e registrariam avaliações duplicadas | Trava no banco (ex.: `sp_getapplock`) ou eleição de um único executor |
| Cache das marcações e da versão do token em memória | Cada instância veria dados por até 60 s / 30 s diferentes | Aceitar a defasagem documentada ou cache distribuído |
| Limite de requisições por usuário em memória | O limite valeria por instância, não no total | Limite no proxy/gateway ou armazenamento compartilhado |
| Limpeza de idempotência em toda instância | Inofensivo (idempotente), mas redundante | Executor único |

---

## 4. Ordem sugerida de evolução (orientada por necessidade)

1. Hospedar a API (decisão pendente) — sem isso o portal publicado não funciona.
2. Receber dados reais das fontes que hoje são demonstração (agenda, ocorrências, cadastros) ou
   rotular as telas como demonstração até lá (já feito).
3. Definir com o porto o modelo de checkpoints A–D e de janelas D0–D3 (divergência registrada).
4. Escolher a stack do app com base na seção 1.4 e, em seguida, criar na API a tabela de eventos,
   o endpoint de lote de eventos de campo e a sincronização incremental.
5. Só então avaliar fila, cache distribuído e orquestração, pelos critérios da seção 3.
