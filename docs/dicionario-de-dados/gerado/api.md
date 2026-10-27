# API — endpoints e contratos

> Arquivo **gerado** por `dotnet run --project backend/src/BarcaLog.Api -- dicionario` a partir do código
> (modelo do EF Core, enums, ApiExplorer e comentários XML) e de `docs/dicionario-de-dados/curadoria.json`.
> Não edite à mão: o CI recusa o arquivo se ele divergir do código.

133 endpoints · 136 contratos. Erros e políticas de acesso comuns a todos: `../api.md`.

Acesso: `Leitura` = Operador, Gestor ou Auditor · `Escrita` = Operador ou Gestor · `Gestao` = Gestor · `Autenticado` = qualquer token válido (inclusive restrito) · `Portal` = usuário de transportadora · `Integracao` = `X-Api-Key`.


## Agendamentos

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/agendamentos` | Leitura | Lista agendamentos (filtros: data, terminal, status, transportadora, placa). |
| POST | `/api/v1/agendamentos` | Escrita | Cria agendamento. Carreta negativada (N3) é recusada. |
| GET | `/api/v1/agendamentos/resumo` | Leitura | Resumo do dia: totais, confirmados, em operação, atrasados e concentração por horário. |
| GET | `/api/v1/agendamentos/{id}` | Leitura | Um agendamento (o QR codifica o campo "codigo"). |
| PUT | `/api/v1/agendamentos/{id}` | Escrita | Atualiza o agendamento inteiro (inclusive status). |
| PATCH | `/api/v1/agendamentos/{id}/status` | Escrita | Atualiza só o status (dropdown da tela). |

### GET `/api/v1/agendamentos`

Lista agendamentos (filtros: data, terminal, status, transportadora, placa).

- Acesso: **Leitura**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<AgendamentoDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `data` | consulta | data? | não |  | Data do agendamento (padrão: todas). |
| `terminalId` | consulta | texto | não |  |  |
| `status` | consulta | texto | não |  |  |
| `transportadoraId` | consulta | inteiro? | não |  |  |
| `placa` | consulta | texto | não | até 10 caracteres |  |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### POST `/api/v1/agendamentos`

Cria agendamento. Carreta negativada (N3) é recusada.

- Acesso: **Escrita**
- Corpo: `CriarAgendamentoRequest`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 201 `AgendamentoDto`

### GET `/api/v1/agendamentos/resumo`

Resumo do dia: totais, confirmados, em operação, atrasados e concentração por horário.

- Acesso: **Leitura**
- Respostas: 200 `ResumoAgendamentosDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `data` | consulta | data? | não |  |  |
| `terminalId` | consulta | texto | não |  |  |

### GET `/api/v1/agendamentos/{id}`

Um agendamento (o QR codifica o campo "codigo").

- Acesso: **Leitura**
- Respostas: 200 `AgendamentoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### PUT `/api/v1/agendamentos/{id}`

Atualiza o agendamento inteiro (inclusive status).

- Acesso: **Escrita**
- Corpo: `AtualizarAgendamentoRequest`
- Respostas: 200 `AgendamentoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### PATCH `/api/v1/agendamentos/{id}/status`

Atualiza só o status (dropdown da tela).

- Acesso: **Escrita**
- Corpo: `AtualizarStatusAgendamentoRequest`
- Respostas: 200 `AgendamentoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

## Alertas

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/alertas` | Leitura | Lista (padrão: só os abertos — ainda ocorrendo e não resolvidos/descartados), prioridade alta primeiro. |
| GET | `/api/v1/alertas/avaliacoes` | Leitura | Histórico das avaliações (automáticas, manuais e após importação), mais recentes primeiro. |
| POST | `/api/v1/alertas/avaliar` | Escrita | Reavalia as regras agora (também roda sozinho a cada 10 min e após cada importação) e devolve o resultado registrado: dados novos, alertas criados/alterados/encerrados e confiabilidade antes e depois. |
| GET | `/api/v1/alertas/proximas-horas` | Leitura | Próximas 6 horas: razão chegadas ÷ liberações por faixa, no padrão dos últimos 30 dias com dados. Estimativa. |
| GET | `/api/v1/alertas/resumo` | Leitura | Contagem de alertas abertos por tipo e horário da última avaliação. |
| GET | `/api/v1/alertas/{id}` | Leitura | Um alerta com evidência, limite, origem, ação recomendada e o histórico completo de tratamento. |
| POST | `/api/v1/alertas/{id}/acoes` | Escrita | Registra uma ação tomada (texto obrigatório). |
| POST | `/api/v1/alertas/{id}/assumir` | Escrita | Assume o alerta (estado Em análise, responsável = usuário atual). Texto opcional. |
| POST | `/api/v1/alertas/{id}/descartar` | Escrita | Encerra como descartado (justificativa obrigatória). |
| POST | `/api/v1/alertas/{id}/reabrir` | Escrita | Reabre um alerta resolvido ou descartado (volta a Novo, sem responsável). |
| POST | `/api/v1/alertas/{id}/resolver` | Escrita | Encerra como resolvido (texto obrigatório: o que foi feito). |

### GET `/api/v1/alertas`

Lista (padrão: só os abertos — ainda ocorrendo e não resolvidos/descartados), prioridade alta primeiro.

- Acesso: **Leitura**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<AlertaDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `tipo` | consulta | texto | não |  |  |
| `estado` | consulta | texto | não |  |  |
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `somenteAbertos` | consulta | booleano | não |  | Padrão: só os que estão ocorrendo (Ativo) e ainda não foram resolvidos/descartados. |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### GET `/api/v1/alertas/avaliacoes`

Histórico das avaliações (automáticas, manuais e após importação), mais recentes primeiro.

- Acesso: **Leitura**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<ResultadoAvaliacaoDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### POST `/api/v1/alertas/avaliar`

Reavalia as regras agora (também roda sozinho a cada 10 min e após cada importação) e devolve o resultado registrado: dados novos, alertas criados/alterados/encerrados e confiabilidade antes e depois.

- Acesso: **Escrita**
- Respostas: 200 `ResultadoAvaliacaoDto`

### GET `/api/v1/alertas/proximas-horas`

Próximas 6 horas: razão chegadas ÷ liberações por faixa, no padrão dos últimos 30 dias com dados. Estimativa.

- Acesso: **Leitura**
- Respostas: 200 `ProximasHorasDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não |  |  |

### GET `/api/v1/alertas/resumo`

Contagem de alertas abertos por tipo e horário da última avaliação.

- Acesso: **Leitura**
- Respostas: 200 `ResumoAlertasDto`

### GET `/api/v1/alertas/{id}`

Um alerta com evidência, limite, origem, ação recomendada e o histórico completo de tratamento.

- Acesso: **Leitura**
- Respostas: 200 `AlertaDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/alertas/{id}/acoes`

Registra uma ação tomada (texto obrigatório).

- Acesso: **Escrita**
- Corpo: `TextoAlertaRequest`
- Respostas: 200 `AlertaDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/alertas/{id}/assumir`

Assume o alerta (estado Em análise, responsável = usuário atual). Texto opcional.

- Acesso: **Escrita**
- Corpo: `TextoAlertaRequest`
- Respostas: 200 `AlertaDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/alertas/{id}/descartar`

Encerra como descartado (justificativa obrigatória).

- Acesso: **Escrita**
- Corpo: `TextoAlertaRequest`
- Respostas: 200 `AlertaDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/alertas/{id}/reabrir`

Reabre um alerta resolvido ou descartado (volta a Novo, sem responsável).

- Acesso: **Escrita**
- Corpo: `TextoAlertaRequest`
- Respostas: 200 `AlertaDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/alertas/{id}/resolver`

Encerra como resolvido (texto obrigatório: o que foi feito).

- Acesso: **Escrita**
- Corpo: `TextoAlertaRequest`
- Respostas: 200 `AlertaDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

## Ambiente

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/ambiente` | Autenticado | Indica se o banco contém o cadastro fictício de demonstração (placas, transportadoras, agenda, ocorrências). |

### GET `/api/v1/ambiente`

Indica se o banco contém o cadastro fictício de demonstração (placas, transportadoras, agenda, ocorrências).

- Acesso: **Autenticado**
- Respostas: 200 `AmbienteDto`

## Auditoria

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/auditoria` | Leitura | Busca por autor, ação, texto livre e período; paginado, mais recentes primeiro. |

### GET `/api/v1/auditoria`

Busca por autor, ação, texto livre e período; paginado, mais recentes primeiro.

- Acesso: **Leitura**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<LogAuditoriaDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `autor` | consulta | texto | não | até 100 caracteres |  |
| `acao` | consulta | texto | não | até 100 caracteres |  |
| `texto` | consulta | texto | não | 3 a 100 caracteres | Busca livre em autor, ação e detalhes (3 a 100 caracteres — a busca em detalhes varre a tabela). |
| `de` | consulta | data e hora? | não |  |  |
| `ate` | consulta | data e hora? | não |  |  |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

## Auth

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| POST | `/api/v1/auth/login` | público | Troca e-mail/senha (+ código MFA, se ativo) por um JWT. Resposta 401 é sempre a mesma para e-mail inexistente, senha errada, conta inativa ou bloqueada (não revela quais contas existem). Se a senha estiver certa e o MFA ativo sem código, o 401 traz "codigo": "mfa_requerido". Limite por IP + bloqueio da conta após falhas seguidas. |
| POST | `/api/v1/auth/logout` | Autenticado | Encerra TODAS as sessões do usuário (todos os tokens emitidos deixam de valer). |
| GET | `/api/v1/auth/me` | Autenticado | Dados do usuário autenticado (funciona também com token restrito). |
| PUT | `/api/v1/auth/me/preferencias` | Leitura | Preferências pessoais (tela inicial, terminal já selecionado). Não alteram papel nem vínculo de terminal. |
| POST | `/api/v1/auth/mfa/ativar` | Autenticado | Confirma o MFA com um código do app. Encerra as sessões; faça login de novo com o código. |
| POST | `/api/v1/auth/mfa/configurar` | Autenticado | Inicia o cadastro do MFA (TOTP). Devolve o segredo e a URI otpauth:// (para QR code) UMA única vez. |
| POST | `/api/v1/auth/mfa/desativar` | Autenticado | Desativa o próprio MFA (não permitido para Gestor quando o MFA é obrigatório). |
| POST | `/api/v1/auth/trocar-senha` | Autenticado | Troca a própria senha (obrigatório quando a senha é provisória). Encerra as sessões; faça login de novo. |

### POST `/api/v1/auth/login`

Troca e-mail/senha (+ código MFA, se ativo) por um JWT. Resposta 401 é sempre a mesma para e-mail inexistente, senha errada, conta inativa ou bloqueada (não revela quais contas existem). Se a senha estiver certa e o MFA ativo sem código, o 401 traz "codigo": "mfa_requerido". Limite por IP + bloqueio da conta após falhas seguidas.

- Acesso: **público**
- Corpo: `LoginRequest`
- Limite de taxa: `login`
- Respostas: 200 `LoginRespostaDto`, 401 `ProblemDetails`, 429 `ProblemDetails`

### POST `/api/v1/auth/logout`

Encerra TODAS as sessões do usuário (todos os tokens emitidos deixam de valer).

- Acesso: **Autenticado**
- Respostas: 204

### GET `/api/v1/auth/me`

Dados do usuário autenticado (funciona também com token restrito).

- Acesso: **Autenticado**
- Respostas: 200 `UsuarioDto`

### PUT `/api/v1/auth/me/preferencias`

Preferências pessoais (tela inicial, terminal já selecionado). Não alteram papel nem vínculo de terminal.

- Acesso: **Leitura**
- Corpo: `AtualizarPreferenciasRequest`
- Respostas: 200 `UsuarioDto`

### POST `/api/v1/auth/mfa/ativar`

Confirma o MFA com um código do app. Encerra as sessões; faça login de novo com o código.

- Acesso: **Autenticado**
- Corpo: `CodigoMfaRequest`
- Limite de taxa: `login`
- Respostas: 204

### POST `/api/v1/auth/mfa/configurar`

Inicia o cadastro do MFA (TOTP). Devolve o segredo e a URI otpauth:// (para QR code) UMA única vez.

- Acesso: **Autenticado**
- Corpo: `ConfirmarSenhaRequest`
- Limite de taxa: `login`
- Respostas: 200 `ConfiguracaoMfaDto`

### POST `/api/v1/auth/mfa/desativar`

Desativa o próprio MFA (não permitido para Gestor quando o MFA é obrigatório).

- Acesso: **Autenticado**
- Corpo: `DesativarMfaRequest`
- Limite de taxa: `login`
- Respostas: 204

### POST `/api/v1/auth/trocar-senha`

Troca a própria senha (obrigatório quando a senha é provisória). Encerra as sessões; faça login de novo.

- Acesso: **Autenticado**
- Corpo: `TrocarSenhaRequest`
- Limite de taxa: `login`
- Respostas: 204

## Condutores

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/condutores` | Leitura | Lista condutores (filtro opcional por transportadora). |
| POST | `/api/v1/condutores` | Escrita | Cadastra condutor. |
| DELETE | `/api/v1/condutores/{id}` | Gestao | Remove condutor. Somente Gestor. |
| GET | `/api/v1/condutores/{id}` | Leitura | Um condutor. |
| PUT | `/api/v1/condutores/{id}` | Escrita | Atualiza condutor. |
| POST | `/api/v1/condutores/{id}/anonimizar` | Gestao | LGPD — anonimiza o condutor (nome vira pseudônimo, placa desvinculada), preservando o histórico operacional. Irreversível. Somente Gestor. |
| GET | `/api/v1/condutores/{id}/dados-pessoais` | Gestao | LGPD — relatório de todos os dados pessoais do condutor (cadastro, ocorrências, agendamentos). Somente Gestor. |

### GET `/api/v1/condutores`

Lista condutores (filtro opcional por transportadora).

- Acesso: **Leitura**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<CondutorDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `transportadoraId` | consulta | inteiro? | não |  |  |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### POST `/api/v1/condutores`

Cadastra condutor.

- Acesso: **Escrita**
- Corpo: `SalvarCondutorRequest`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 201 `CondutorDto`

### DELETE `/api/v1/condutores/{id}`

Remove condutor. Somente Gestor.

- Acesso: **Gestao**
- Respostas: 204

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### GET `/api/v1/condutores/{id}`

Um condutor.

- Acesso: **Leitura**
- Respostas: 200 `CondutorDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### PUT `/api/v1/condutores/{id}`

Atualiza condutor.

- Acesso: **Escrita**
- Corpo: `SalvarCondutorRequest`
- Respostas: 200 `CondutorDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/condutores/{id}/anonimizar`

LGPD — anonimiza o condutor (nome vira pseudônimo, placa desvinculada), preservando o histórico operacional. Irreversível. Somente Gestor.

- Acesso: **Gestao**
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 200 `CondutorDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### GET `/api/v1/condutores/{id}/dados-pessoais`

LGPD — relatório de todos os dados pessoais do condutor (cadastro, ocorrências, agendamentos). Somente Gestor.

- Acesso: **Gestao**
- Respostas: 200 `DadosPessoaisCondutorDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

## Configuracoes

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/configuracoes` | Leitura | Capacidades dos terminais e parâmetros (limites e metas) usados pelos indicadores e alertas. |
| PUT | `/api/v1/configuracoes/parametros` | Gestao | Altera limites e metas dos indicadores e alertas (Gestor; auditado). |
| PUT | `/api/v1/configuracoes/terminais/{terminalId}` | Gestao | Capacidade diária e por hora do terminal. Campos vazios = não informada. |

### GET `/api/v1/configuracoes`

Capacidades dos terminais e parâmetros (limites e metas) usados pelos indicadores e alertas.

- Acesso: **Leitura**
- Respostas: 200 `ConfiguracaoOperacionalDto`

### PUT `/api/v1/configuracoes/parametros`

Altera limites e metas dos indicadores e alertas (Gestor; auditado).

- Acesso: **Gestao**
- Corpo: `AtualizarParametrosRequest`
- Respostas: 200 `ParametrosOperacionaisDto`

### PUT `/api/v1/configuracoes/terminais/{terminalId}`

Capacidade diária e por hora do terminal. Campos vazios = não informada.

- Acesso: **Gestao**
- Corpo: `AtualizarCapacidadeTerminalRequest`
- Respostas: 200 `TerminalDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | rota | texto | sim | maxlength |  |

## Contestacoes

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/contestacoes` | Leitura | Lista contestações. |
| POST | `/api/v1/contestacoes` | Escrita | Abre contestação para uma ocorrência (ocorrência passa a "Contestada"). |
| GET | `/api/v1/contestacoes/{id}` | Leitura | Uma contestação. |
| POST | `/api/v1/contestacoes/{id}/aprovar` | Escrita | Aprova: ocorrência resolvida e carreta (e condutor) daquela ocorrência volta a Regular. |
| POST | `/api/v1/contestacoes/{id}/rejeitar` | Escrita | Rejeita: ocorrência volta a "Ativa", bloqueio mantido. |

### GET `/api/v1/contestacoes`

Lista contestações.

- Acesso: **Leitura**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<ContestacaoDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `transportadoraId` | consulta | inteiro? | não |  |  |
| `ocorrenciaId` | consulta | inteiro? | não |  |  |
| `status` | consulta | texto | não |  |  |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### POST `/api/v1/contestacoes`

Abre contestação para uma ocorrência (ocorrência passa a "Contestada").

- Acesso: **Escrita**
- Corpo: `AbrirContestacaoRequest`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 201 `ContestacaoDto`

### GET `/api/v1/contestacoes/{id}`

Uma contestação.

- Acesso: **Leitura**
- Respostas: 200 `ContestacaoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/contestacoes/{id}/aprovar`

Aprova: ocorrência resolvida e carreta (e condutor) daquela ocorrência volta a Regular.

- Acesso: **Escrita**
- Corpo: `ResponderContestacaoRequest`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 200 `ContestacaoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/contestacoes/{id}/rejeitar`

Rejeita: ocorrência volta a "Ativa", bloqueio mantido.

- Acesso: **Escrita**
- Corpo: `ResponderContestacaoRequest`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 200 `ContestacaoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

## Convenios

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/convenios` | Leitura | Convênios com operador e carga (deduzidos do nome na importação ou corrigidos pelo Gestor). |
| PUT | `/api/v1/convenios/{codigo}` | Gestao | Confirma ou corrige operador e carga deduzidos do nome. Somente Gestor. |

### GET `/api/v1/convenios`

Convênios com operador e carga (deduzidos do nome na importação ou corrigidos pelo Gestor).

- Acesso: **Leitura**
- Respostas: 200 `lista de ConvenioDto`

### PUT `/api/v1/convenios/{codigo}`

Confirma ou corrige operador e carga deduzidos do nome. Somente Gestor.

- Acesso: **Gestao**
- Corpo: `AtualizarConvenioRequest`
- Respostas: 200 `ConvenioDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `codigo` | rota | texto | sim | maxlength |  |

## Importacoes

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/importacoes` | Leitura | Histórico de importações, mais recentes primeiro. |
| POST | `/api/v1/importacoes` | Gestao | Envia uma planilha para análise. Formatos: .xlsx (primeira aba) ou .csv (separador ; , ou tab). Colunas obrigatórias: COD_MOVIMENTO, SENHA, CONVENIO, DATA_MARCACAO, HORA_MARCACAO; opcionais: COD_CONVENIO, CICLO, DATA_LIBERACAO, HORA_LIBERACAO. Devolve a classificação das linhas; nada é gravado em marcações até a confirmação. |
| GET | `/api/v1/importacoes/{id}` | Leitura | Resumo de uma importação (contagens, avisos, convênios novos, conflitos pendentes). |
| POST | `/api/v1/importacoes/{id}/cancelar` | Gestao | Descarta a análise. Nada é gravado em marcações. |
| POST | `/api/v1/importacoes/{id}/confirmar` | Gestao | Grava a importação (uma transação). Exige todos os conflitos decididos. |
| POST | `/api/v1/importacoes/{id}/conflitos` | Gestao | Decide conflitos: Aplicar (gravar o valor da planilha) ou Manter. Sem "linhas", vale para todos os pendentes. |
| GET | `/api/v1/importacoes/{id}/linhas` | Leitura | Linhas para revisão (filtros: classificacao, aviso, decisao). Linhas iguais ao banco não são guardadas. |
| GET | `/api/v1/importacoes/{id}/relatorio` | Leitura | Relatório CSV com as linhas rejeitadas, conflitantes e com aviso (separador ";", abre no Excel). |

### GET `/api/v1/importacoes`

Histórico de importações, mais recentes primeiro.

- Acesso: **Leitura**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<LoteImportacaoDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `status` | consulta | texto | não |  |  |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### POST `/api/v1/importacoes`

Envia uma planilha para análise. Formatos: .xlsx (primeira aba) ou .csv (separador ; , ou tab). Colunas obrigatórias: COD_MOVIMENTO, SENHA, CONVENIO, DATA_MARCACAO, HORA_MARCACAO; opcionais: COD_CONVENIO, CICLO, DATA_LIBERACAO, HORA_LIBERACAO. Devolve a classificação das linhas; nada é gravado em marcações até a confirmação.

- Acesso: **Gestao**
- Limite de taxa: `importacao`
- Respostas: 201 `LoteImportacaoDto`, 400 `ValidationProblemDetails`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | formulário | texto | não |  |  |
| `arquivo` | formulário | arquivo | não |  |  |

### GET `/api/v1/importacoes/{id}`

Resumo de uma importação (contagens, avisos, convênios novos, conflitos pendentes).

- Acesso: **Leitura**
- Respostas: 200 `LoteImportacaoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/importacoes/{id}/cancelar`

Descarta a análise. Nada é gravado em marcações.

- Acesso: **Gestao**
- Respostas: 200 `LoteImportacaoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/importacoes/{id}/confirmar`

Grava a importação (uma transação). Exige todos os conflitos decididos.

- Acesso: **Gestao**
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 200 `LoteImportacaoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/importacoes/{id}/conflitos`

Decide conflitos: Aplicar (gravar o valor da planilha) ou Manter. Sem "linhas", vale para todos os pendentes.

- Acesso: **Gestao**
- Corpo: `DecidirConflitosRequest`
- Respostas: 200 `ResultadoDecisaoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### GET `/api/v1/importacoes/{id}/linhas`

Linhas para revisão (filtros: classificacao, aviso, decisao). Linhas iguais ao banco não são guardadas.

- Acesso: **Leitura**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<LinhaImportacaoDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |
| `classificacao` | consulta | texto | não |  |  |
| `aviso` | consulta | texto | não |  | Só linhas com este aviso. |
| `decisao` | consulta | texto | não |  |  |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### GET `/api/v1/importacoes/{id}/relatorio`

Relatório CSV com as linhas rejeitadas, conflitantes e com aviso (separador ";", abre no Excel).

- Acesso: **Leitura**
- Limite de taxa: `exportacao`
- Respostas: 200

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

## Indicadores

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/indicadores/fluxo-horario` | Leitura | Média por dia de marcações e de liberações em cada hora do dia, no período. |
| GET | `/api/v1/indicadores/frescor` | Leitura | Até quando cada terminal tem dados (última marcação/liberação e última importação). |
| GET | `/api/v1/indicadores/resumo` | Leitura | Movimentações, média diária, espera mediana/P90/média, % liberadas em até 24 h e esperas longas — do período e da comparação. |
| GET | `/api/v1/indicadores/terminais` | Leitura | Os mesmos indicadores por terminal, com pico diário, capacidade e ocupação (só com capacidade configurada). |
| GET | `/api/v1/indicadores/volume-mensal` | Leitura | Análise de Volume — Histórico: movimentações e espera mediana por mês e terminal (meses parciais marcados). |

### GET `/api/v1/indicadores/fluxo-horario`

Média por dia de marcações e de liberações em cada hora do dia, no período.

- Acesso: **Leitura**
- Respostas: 200 `lista de FluxoHorarioDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `compararDe` | consulta | data? | não |  |  |
| `compararAte` | consulta | data? | não |  |  |

### GET `/api/v1/indicadores/frescor`

Até quando cada terminal tem dados (última marcação/liberação e última importação).

- Acesso: **Leitura**
- Respostas: 200 `lista de FrescorTerminalDto`

### GET `/api/v1/indicadores/resumo`

Movimentações, média diária, espera mediana/P90/média, % liberadas em até 24 h e esperas longas — do período e da comparação.

- Acesso: **Leitura**
- Respostas: 200 `ResumoIndicadoresDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `compararDe` | consulta | data? | não |  |  |
| `compararAte` | consulta | data? | não |  |  |

### GET `/api/v1/indicadores/terminais`

Os mesmos indicadores por terminal, com pico diário, capacidade e ocupação (só com capacidade configurada).

- Acesso: **Leitura**
- Respostas: 200 `lista de IndicadorTerminalDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `compararDe` | consulta | data? | não |  |  |
| `compararAte` | consulta | data? | não |  |  |

### GET `/api/v1/indicadores/volume-mensal`

Análise de Volume — Histórico: movimentações e espera mediana por mês e terminal (meses parciais marcados).

- Acesso: **Leitura**
- Respostas: 200 `lista de VolumeMensalDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `compararDe` | consulta | data? | não |  |  |
| `compararAte` | consulta | data? | não |  |  |

## Integracao

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| POST | `/api/v1/integracao/eventos` | Integracao | Recebe eventos de marcação/liberação (1 a 1000 por chamada). Eventos válidos são gravados; inválidos voltam em "erros" com o índice. "Marcacao" cria/atualiza o movimento; "Liberacao" fecha o movimento com dataLiberacao. Datas no horário local do porto, sem fuso. |

### POST `/api/v1/integracao/eventos`

Recebe eventos de marcação/liberação (1 a 1000 por chamada). Eventos válidos são gravados; inválidos voltam em "erros" com o índice. "Marcacao" cria/atualiza o movimento; "Liberacao" fecha o movimento com dataLiberacao. Datas no horário local do porto, sem fuso.

- Acesso: **Integracao**
- Corpo: `lista de EventoIntegracaoDto`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Limite de taxa: `integracao`
- Respostas: 200 `ResultadoIntegracaoDto`, 400 `ProblemDetails`

## Marcacoes

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/marcacoes` | Leitura | Lista paginada de marcações (filtros: terminal, período, convênio, operador, carga, senha). |
| GET | `/api/v1/marcacoes/alertas-operacionais` | Leitura | Alertas operacionais (hora crítica, atraso crescente, ocupação crítica). **(descontinuado)** |
| GET | `/api/v1/marcacoes/alertas-saturacao` | Leitura | Alerta de saturação com base na tendência dos últimos 7 dias do recorte. **(descontinuado)** |
| GET | `/api/v1/marcacoes/analise` | Leitura | Análise 360° do recorte: resumo com período anterior equivalente, série temporal, faixa horária, dia da semana, dimensões (terminal, operador, carga, convênio, origem) e qualidade dos dados. Sem período: os 30 dias até a última marcação. Regras: docs/INDICADORES.md ("Análise 360°"). |
| GET | `/api/v1/marcacoes/analise-preditiva` | Leitura | Projeção mensal por tendência recente com faixa de confiança (~80%). **(descontinuado)** |
| GET | `/api/v1/marcacoes/atrasos-recorrentes` | Leitura | Convênios com atrasos recorrentes (≥ limiteHoras, pelo menos minOcorrencias vezes). |
| GET | `/api/v1/marcacoes/business-case` | Leitura | Business case: custo de demurrage, custo ampliado do gargalo, fatores de utilização, SLA por etapa, ROI e ESG. **(descontinuado)** |
| GET | `/api/v1/marcacoes/capacidade-diaria` | Leitura | Volume por dia e % da capacidade nominal (dias mais cheios primeiro). **(descontinuado)** |
| GET | `/api/v1/marcacoes/concentracao-turno` | Leitura | Concentração de tráfego e % de retenção (>24h) por turno do dia. |
| GET | `/api/v1/marcacoes/detalhamento-diario` | Leitura | Detalhamento diário (dia 1..31) de cada mês. |
| GET | `/api/v1/marcacoes/dias-acima-capacidade` | Leitura | Dias × terminal em que o volume passou da capacidade nominal. **(descontinuado)** |
| GET | `/api/v1/marcacoes/estouro-critico` | Leitura | Top 15 permanências acima do SLA de 144h. **(descontinuado)** |
| GET | `/api/v1/marcacoes/exportar` | Leitura | Exporta as marcações liberadas do recorte em CSV (separador ";", UTF-8 com BOM, abre direto no Excel). Células que começam com = + - @ são neutralizadas (injeção de fórmula). Limite de 10 exportações/min. |
| GET | `/api/v1/marcacoes/horarios-criticos` | Leitura | Top 6 combinações dia-da-semana × turno com mais retenção. |
| POST | `/api/v1/marcacoes/importar` | Gestao | Importa os JSON reais do diretório configurado (Seed:DiretorioDatasets). Idempotente. Somente Gestor. |
| POST | `/api/v1/marcacoes/importar/{terminalId}` | Gestao | Importa um arquivo JSON (mesmo formato de src/data/datasets/*.json) para o terminal informado. Somente Gestor. |
| GET | `/api/v1/marcacoes/indicadores-performance` | Leitura | Indicadores de performance por terminal com Δ mês a mês (TMA e taxa D0). |
| GET | `/api/v1/marcacoes/indice-risco-hora` | Leitura | Índice de risco por hora do dia: ρ = λ/μ sobre todo o histórico do recorte. ρ ≥ 1.4 crítico, ≥ 1.2 alto, ≥ 1.0 atenção, abaixo normal. **(descontinuado)** |
| GET | `/api/v1/marcacoes/janelas-permanencia` | Leitura | Distribuição nas janelas de permanência D0, D1, D2, D3, Alerta e Estouro Crítico. |
| GET | `/api/v1/marcacoes/kpis` | Leitura | KPIs gerais: total, média diária, dias operados, mês mais movimentado, tempo médio de espera (simples e ponderado) e maior atraso. |
| GET | `/api/v1/marcacoes/matriz-volume-mensal` | Leitura | Matriz volume mês × terminal. |
| GET | `/api/v1/marcacoes/picos-entrada-saida` | Leitura | Horário de pico de entrada (marcação) × saída (liberação). Aceita filtro mes=yyyy-MM. |
| GET | `/api/v1/marcacoes/por-carga` | Leitura | Totais por tipo de carga. |
| GET | `/api/v1/marcacoes/por-ciclo` | Leitura | Distribuição e espera média por ciclo (1, 2, 3, 4, 5+). |
| GET | `/api/v1/marcacoes/por-dia-semana` | Leitura | Volume de marcações por dia da semana. |
| GET | `/api/v1/marcacoes/por-hora` | Leitura | Volume de marcações por hora do dia. |
| GET | `/api/v1/marcacoes/por-mes` | Leitura | Volume por mês com representatividade, dias operados e média diária. |
| GET | `/api/v1/marcacoes/por-operador` | Leitura | Totais por operador (Bunge e Amaggi consolidados). |
| GET | `/api/v1/marcacoes/por-terminal` | Leitura | Totais e espera média por terminal. |
| GET | `/api/v1/marcacoes/projecao-volume` | Leitura | Projeção de volume por regressão linear simples sobre todo o histórico mensal. **(descontinuado)** |
| GET | `/api/v1/marcacoes/ranking-esperas` | Leitura | Ranking das maiores esperas (padrão: top 10, máx. 500). |
| GET | `/api/v1/marcacoes/recomendacoes` | Leitura | Recomendações prescritivas derivadas das janelas e do ritmo λ/μ. **(descontinuado)** |
| GET | `/api/v1/marcacoes/ritmo-operacional` | Leitura | Ritmo operacional λ (chegadas/h) × μ (liberações/h) por hora do dia, com alertas de λ > μ por 2h+ seguidas. **(descontinuado)** |
| GET | `/api/v1/marcacoes/score-operadores` | Leitura | Score de eficiência 0–100 por operador (volume 40%, espera 40%, consistência 20%). **(descontinuado)** |
| GET | `/api/v1/marcacoes/tendencia-sla` | Leitura | Tendência de SLA: espera do último mês vs. média dos 3 anteriores. **(descontinuado)** |
| GET | `/api/v1/marcacoes/tma-terminal` | Leitura | TMA por terminal (geral, último mês e variação). |
| GET | `/api/v1/marcacoes/top-dias` | Leitura | Top 30 dias × terminal com maior volume, marcando gargalo (>80% da capacidade). **(descontinuado)** |
| GET | `/api/v1/marcacoes/visao-terminal` | Leitura | Cards "Visão por Terminal": volume, espera média, % de ocupação vs. capacidade nominal e semáforo (verde/amarelo/vermelho). **(descontinuado)** |
| GET | `/api/v1/marcacoes/{movimentoId}` | Leitura | Um movimento pelo id. |
| GET | `/api/v1/marcacoes/{movimentoId}/detalhe` | Leitura | Movimento com origem (arquivo/linha da importação) e quantas carretas foram liberadas no mesmo instante. |

### GET `/api/v1/marcacoes`

Lista paginada de marcações (filtros: terminal, período, convênio, operador, carga, senha).

- Acesso: **Leitura**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<MarcacaoDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não |  | unitapajos \| tgpm \| hidrovias |
| `de` | consulta | data? | não |  | Data de marcação inicial (inclusive, horário local do porto). |
| `ate` | consulta | data? | não |  | Data de marcação final (inclusive). |
| `convenio` | consulta | texto | não | até 100 caracteres | Filtro por convênio (contém, sem diferenciar maiúsculas). |
| `operador` | consulta | texto | não | até 50 caracteres |  |
| `carga` | consulta | texto | não | até 50 caracteres |  |
| `senha` | consulta | texto | não | até 32 caracteres |  |
| `busca` | consulta | texto | não | até 32 caracteres | Número do movimento ou senha (exato). |
| `codConvenio` | consulta | texto | não | até 32 caracteres |  |
| `esperaAcimaHoras` | consulta | número? | não | de 0 a 100000 | Só esperas acima de X horas (liberados). |
| `semLiberacao` | consulta | booleano? | não |  | true = só marcações ainda sem liberação. |
| `origem` | consulta | texto | não |  |  |
| `convenioExato` | consulta | texto | não | até 100 caracteres | Convênio exato (nome como na planilha) — usado pelo detalhamento da Análise 360°. |
| `semCodigoConvenio` | consulta | booleano? | não |  | true = só registros sem COD_CONVENIO. |
| `hora` | consulta | inteiro? | não | de 0 a 23 | Hora da marcação (0–23, horário do porto). |
| `diaSemana` | consulta | inteiro? | não | de 0 a 6 | Dia da semana da marcação (0 = domingo … 6 = sábado). |
| `ordenacao` | consulta | texto | não | formato `^(marcacao\|espera)_(asc\|desc)$` | marcacao_desc (padrão), marcacao_asc, espera_desc, espera_asc. |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### GET `/api/v1/marcacoes/alertas-operacionais`

Alertas operacionais (hora crítica, atraso crescente, ocupação crítica).

- Acesso: **Leitura**
- **Descontinuado:** O alerta de ocupação usa a capacidade presumida de 1.000 carretas/dia quando o terminal não tem capacidade cadastrada. Use /api/v1/alertas.
- Respostas: 200 `lista de AlertaOperacional`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/alertas-saturacao`

Alerta de saturação com base na tendência dos últimos 7 dias do recorte.

- Acesso: **Leitura**
- **Descontinuado:** Tendência de 7 dias comparada à capacidade presumida de 1.000 carretas/dia quando o terminal não tem capacidade cadastrada. Use /api/v1/alertas.
- Respostas: 200 `lista de AlertaSaturacao`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/analise`

Análise 360° do recorte: resumo com período anterior equivalente, série temporal, faixa horária, dia da semana, dimensões (terminal, operador, carga, convênio, origem) e qualidade dos dados. Sem período: os 30 dias até a última marcação. Regras: docs/INDICADORES.md ("Análise 360°").

- Acesso: **Leitura**
- Respostas: 200 `ResultadoAnalise`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `de` | consulta | data? | não |  | Início (data de marcação). Sem período: os 30 dias até a última marcação do recorte. |
| `ate` | consulta | data? | não |  |  |
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `operador` | consulta | texto | não | até 50 caracteres |  |
| `carga` | consulta | texto | não | até 50 caracteres |  |
| `convenio` | consulta | texto | não | até 100 caracteres | Convênio exato (nome como na planilha). |
| `origem` | consulta | texto | não |  |  |
| `agrupar` | consulta | texto | não | formato `^(dia\|semana\|mes)$` | dia \| semana \| mes (padrão: pela duração do período). |

### GET `/api/v1/marcacoes/analise-preditiva`

Projeção mensal por tendência recente com faixa de confiança (~80%).

- Acesso: **Leitura**
- **Descontinuado:** Projeção por tendência com faixa de confiança não validada contra resultados reais.
- Respostas: 200 `AnalisePreditiva`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |
| `meses` | consulta | inteiro | não |  |  |

### GET `/api/v1/marcacoes/atrasos-recorrentes`

Convênios com atrasos recorrentes (≥ limiteHoras, pelo menos minOcorrencias vezes).

- Acesso: **Leitura**
- Respostas: 200 `lista de AtrasoRecorrente`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |
| `limiteHoras` | consulta | número | não |  |  |
| `minOcorrencias` | consulta | inteiro | não |  |  |

### GET `/api/v1/marcacoes/business-case`

Business case: custo de demurrage, custo ampliado do gargalo, fatores de utilização, SLA por etapa, ROI e ESG.

- Acesso: **Leitura**
- **Descontinuado:** Custo de R$ 600/dia (referência ANTT genérica), diesel/CO₂ e cenários de ROI sem fonte validada para o porto.
- Respostas: 200

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/capacidade-diaria`

Volume por dia e % da capacidade nominal (dias mais cheios primeiro).

- Acesso: **Leitura**
- **Descontinuado:** O percentual usa a capacidade presumida de 1.000 carretas/dia quando o terminal não tem capacidade cadastrada. Use /api/v1/indicadores/terminais.
- Respostas: 200 `lista de CapacidadeDia`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/concentracao-turno`

Concentração de tráfego e % de retenção (>24h) por turno do dia.

- Acesso: **Leitura**
- Respostas: 200 `lista de TurnoConcentracao`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/detalhamento-diario`

Detalhamento diário (dia 1..31) de cada mês.

- Acesso: **Leitura**
- Respostas: 200 `lista de DetalhamentoMes`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/dias-acima-capacidade`

Dias × terminal em que o volume passou da capacidade nominal.

- Acesso: **Leitura**
- **Descontinuado:** A comparação usa a capacidade presumida de 1.000 carretas/dia quando o terminal não tem capacidade cadastrada. Use /api/v1/indicadores/terminais.
- Respostas: 200 `lista de DiaEmpresaTotal`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/estouro-critico`

Top 15 permanências acima do SLA de 144h.

- Acesso: **Leitura**
- **Descontinuado:** O SLA de 144 h não foi definido pela operação. Use /api/v1/marcacoes?esperaAcimaHoras=72&ordenacao=espera_desc.
- Respostas: 200 `lista de RegistroMarcacao`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/exportar`

Exporta as marcações liberadas do recorte em CSV (separador ";", UTF-8 com BOM, abre direto no Excel). Células que começam com = + - @ são neutralizadas (injeção de fórmula). Limite de 10 exportações/min.

- Acesso: **Leitura**
- Limite de taxa: `exportacao`
- Respostas: 200

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/horarios-criticos`

Top 6 combinações dia-da-semana × turno com mais retenção.

- Acesso: **Leitura**
- Respostas: 200 `lista de HorarioCritico`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### POST `/api/v1/marcacoes/importar`

Importa os JSON reais do diretório configurado (Seed:DiretorioDatasets). Idempotente. Somente Gestor.

- Acesso: **Gestao**
- Limite de taxa: `importacao`
- Respostas: 200 `lista de ResultadoImportacaoDto`

### POST `/api/v1/marcacoes/importar/{terminalId}`

Importa um arquivo JSON (mesmo formato de src/data/datasets/*.json) para o terminal informado. Somente Gestor.

- Acesso: **Gestao**
- Limite de taxa: `importacao`
- Respostas: 200 `ResultadoImportacaoDto`, 400 `ValidationProblemDetails`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | rota | texto | sim | maxlength |  |
| `arquivo` | formulário | arquivo | não |  |  |

### GET `/api/v1/marcacoes/indicadores-performance`

Indicadores de performance por terminal com Δ mês a mês (TMA e taxa D0).

- Acesso: **Leitura**
- Respostas: 200 `lista de IndicadorPerformance`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/indice-risco-hora`

Índice de risco por hora do dia: ρ = λ/μ sobre todo o histórico do recorte. ρ ≥ 1.4 crítico, ≥ 1.2 alto, ≥ 1.0 atenção, abaixo normal.

- Acesso: **Leitura**
- **Descontinuado:** Limiares fixos sobre todo o histórico, somando terminais. Use /api/v1/alertas/proximas-horas.
- Respostas: 200 `lista de RiscoHora`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/janelas-permanencia`

Distribuição nas janelas de permanência D0, D1, D2, D3, Alerta e Estouro Crítico.

- Acesso: **Leitura**
- Respostas: 200 `lista de JanelaDistribuicao`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/kpis`

KPIs gerais: total, média diária, dias operados, mês mais movimentado, tempo médio de espera (simples e ponderado) e maior atraso.

- Acesso: **Leitura**
- Respostas: 200 `KpisGerais`, 204

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/matriz-volume-mensal`

Matriz volume mês × terminal.

- Acesso: **Leitura**
- Respostas: 200 `MotorMetricas.MatrizVolumeMensal`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/picos-entrada-saida`

Horário de pico de entrada (marcação) × saída (liberação). Aceita filtro mes=yyyy-MM.

- Acesso: **Leitura**
- Respostas: 200 `PicosEntradaSaida`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/por-carga`

Totais por tipo de carga.

- Acesso: **Leitura**
- Respostas: 200 `lista de CargaTotal`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/por-ciclo`

Distribuição e espera média por ciclo (1, 2, 3, 4, 5+).

- Acesso: **Leitura**
- Respostas: 200 `lista de CicloBalde`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/por-dia-semana`

Volume de marcações por dia da semana.

- Acesso: **Leitura**
- Respostas: 200 `lista de DiaSemanaTotal`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/por-hora`

Volume de marcações por hora do dia.

- Acesso: **Leitura**
- Respostas: 200 `lista de HoraTotal`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/por-mes`

Volume por mês com representatividade, dias operados e média diária.

- Acesso: **Leitura**
- Respostas: 200 `lista de MesDetalhado`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/por-operador`

Totais por operador (Bunge e Amaggi consolidados).

- Acesso: **Leitura**
- Respostas: 200 `lista de OperadorTotal`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/por-terminal`

Totais e espera média por terminal.

- Acesso: **Leitura**
- Respostas: 200 `lista de EmpresaTotal`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/projecao-volume`

Projeção de volume por regressão linear simples sobre todo o histórico mensal.

- Acesso: **Leitura**
- **Descontinuado:** Regressão linear sobre o histórico mensal apresentada como projeção, sem medição de erro.
- Respostas: 200

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |
| `meses` | consulta | inteiro | não |  |  |

### GET `/api/v1/marcacoes/ranking-esperas`

Ranking das maiores esperas (padrão: top 10, máx. 500).

- Acesso: **Leitura**
- Respostas: 200 `lista de RegistroMarcacao`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |
| `limite` | consulta | inteiro | não |  |  |

### GET `/api/v1/marcacoes/recomendacoes`

Recomendações prescritivas derivadas das janelas e do ritmo λ/μ.

- Acesso: **Leitura**
- **Descontinuado:** Textos fixos disparados por limiares não validados. Use /api/v1/alertas.
- Respostas: 200 `lista de Recomendacao`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/ritmo-operacional`

Ritmo operacional λ (chegadas/h) × μ (liberações/h) por hora do dia, com alertas de λ > μ por 2h+ seguidas.

- Acesso: **Leitura**
- **Descontinuado:** Limiares fixos sobre todo o histórico, somando terminais. Use /api/v1/alertas/proximas-horas.
- Respostas: 200 `RitmoOperacional`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/score-operadores`

Score de eficiência 0–100 por operador (volume 40%, espera 40%, consistência 20%).

- Acesso: **Leitura**
- **Descontinuado:** Pesos 40/40/20 escolhidos sem validação com a operação.
- Respostas: 200 `lista de ScoreOperador`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/tendencia-sla`

Tendência de SLA: espera do último mês vs. média dos 3 anteriores.

- Acesso: **Leitura**
- **Descontinuado:** "SLA" sem meta definida pela operação. Use /api/v1/indicadores/resumo.
- Respostas: 200

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/tma-terminal`

TMA por terminal (geral, último mês e variação).

- Acesso: **Leitura**
- Respostas: 200 `lista de TmaTerminal`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/top-dias`

Top 30 dias × terminal com maior volume, marcando gargalo (>80% da capacidade).

- Acesso: **Leitura**
- **Descontinuado:** "Gargalo" = 80% da capacidade presumida de 1.000 carretas/dia quando o terminal não tem capacidade cadastrada. Use /api/v1/indicadores/terminais.
- Respostas: 200 `lista de DiaCompacto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/visao-terminal`

Cards "Visão por Terminal": volume, espera média, % de ocupação vs. capacidade nominal e semáforo (verde/amarelo/vermelho).

- Acesso: **Leitura**
- **Descontinuado:** A ocupação usa a capacidade presumida de 1.000 carretas/dia quando o terminal não tem capacidade cadastrada, e a espera é média (puxada por esperas longas). Use /api/v1/indicadores/terminais.
- Respostas: 200 `lista de VisaoTerminal`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `terminalId` | consulta | texto | não | até 32 caracteres |  |
| `de` | consulta | data? | não |  |  |
| `ate` | consulta | data? | não |  |  |
| `convenio` | consulta | texto | não | até 100 caracteres |  |
| `mes` | consulta | texto | não | formato `^(\d{4}-(0[1-9]\|1[0-2])\|todos)$` | Mês no formato yyyy-MM (usado no gráfico de picos por mês). |

### GET `/api/v1/marcacoes/{movimentoId}`

Um movimento pelo id.

- Acesso: **Leitura**
- Respostas: 200 `MarcacaoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `movimentoId` | rota | texto | sim | maxlength |  |

### GET `/api/v1/marcacoes/{movimentoId}/detalhe`

Movimento com origem (arquivo/linha da importação) e quantas carretas foram liberadas no mesmo instante.

- Acesso: **Leitura**
- Respostas: 200 `MarcacaoDetalheDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `movimentoId` | rota | texto | sim | maxlength |  |

## Ocorrencias

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/ocorrencias` | Leitura | Lista ocorrências (mais recentes primeiro). |
| POST | `/api/v1/ocorrencias` | Escrita | Registra ocorrência. N3 → veículo da placa (e condutor, se informado) passa a Negativada na mesma transação. N1/N2 não bloqueiam. |
| GET | `/api/v1/ocorrencias/{id}` | Leitura | Uma ocorrência. |

### GET `/api/v1/ocorrencias`

Lista ocorrências (mais recentes primeiro).

- Acesso: **Leitura**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<OcorrenciaDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `transportadoraId` | consulta | inteiro? | não |  |  |
| `nivel` | consulta | texto | não |  |  |
| `status` | consulta | texto | não |  |  |
| `placa` | consulta | texto | não | até 10 caracteres |  |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### POST `/api/v1/ocorrencias`

Registra ocorrência. N3 → veículo da placa (e condutor, se informado) passa a Negativada na mesma transação. N1/N2 não bloqueiam.

- Acesso: **Escrita**
- Corpo: `RegistrarOcorrenciaRequest`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 201 `OcorrenciaRegistradaDto`

### GET `/api/v1/ocorrencias/{id}`

Uma ocorrência.

- Acesso: **Leitura**
- Respostas: 200 `OcorrenciaDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

## Portal

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/portal/condutores` | Portal | Condutores da própria transportadora. |
| GET | `/api/v1/portal/contestacoes` | Portal | Contestações (chamados) da própria transportadora. |
| POST | `/api/v1/portal/contestacoes` | Portal | Abre contestação de uma ocorrência da própria transportadora. |
| GET | `/api/v1/portal/ocorrencias` | Portal | Ocorrências da própria transportadora. |
| GET | `/api/v1/portal/resumo` | Portal | Dados e status calculado da própria transportadora (apta/negativada, carretas negativadas, reincidência N2). |
| GET | `/api/v1/portal/veiculos` | Portal | Veículos da própria transportadora. |

### GET `/api/v1/portal/condutores`

Condutores da própria transportadora.

- Acesso: **Portal**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<CondutorDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `transportadoraId` | consulta | inteiro? | não |  |  |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### GET `/api/v1/portal/contestacoes`

Contestações (chamados) da própria transportadora.

- Acesso: **Portal**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<ContestacaoDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `transportadoraId` | consulta | inteiro? | não |  |  |
| `ocorrenciaId` | consulta | inteiro? | não |  |  |
| `status` | consulta | texto | não |  |  |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### POST `/api/v1/portal/contestacoes`

Abre contestação de uma ocorrência da própria transportadora.

- Acesso: **Portal**
- Corpo: `AbrirContestacaoPortalRequest`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 201 `ContestacaoDto`

### GET `/api/v1/portal/ocorrencias`

Ocorrências da própria transportadora.

- Acesso: **Portal**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<OcorrenciaDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `transportadoraId` | consulta | inteiro? | não |  |  |
| `nivel` | consulta | texto | não |  |  |
| `status` | consulta | texto | não |  |  |
| `placa` | consulta | texto | não | até 10 caracteres |  |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### GET `/api/v1/portal/resumo`

Dados e status calculado da própria transportadora (apta/negativada, carretas negativadas, reincidência N2).

- Acesso: **Portal**
- Respostas: 200 `TransportadoraDto`

### GET `/api/v1/portal/veiculos`

Veículos da própria transportadora.

- Acesso: **Portal**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<VeiculoDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `transportadoraId` | consulta | inteiro? | não |  |  |
| `terminalId` | consulta | texto | não |  |  |
| `statusPortaria` | consulta | texto | não |  |  |
| `statusNegativacao` | consulta | texto | não |  |  |
| `placa` | consulta | texto | não | até 10 caracteres | Placa (contém). |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

## Portaria

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/portaria/fila-virtual` | Leitura | Fila virtual (No Pátio/Aguardando, ordem de chegada) com estimativa pela espera média histórica real do terminal, alerta de congestionamento e previsão de gargalo das próximas 6h. |
| GET | `/api/v1/portaria/operacao-agora` | Leitura | Contagem da frota por status de portaria ("Operação agora"). |

### GET `/api/v1/portaria/fila-virtual`

Fila virtual (No Pátio/Aguardando, ordem de chegada) com estimativa pela espera média histórica real do terminal, alerta de congestionamento e previsão de gargalo das próximas 6h.

- Acesso: **Leitura**
- Respostas: 200 `FilaVirtualDto`

### GET `/api/v1/portaria/operacao-agora`

Contagem da frota por status de portaria ("Operação agora").

- Acesso: **Leitura**
- Respostas: 200 `OperacaoAgoraDto`

## Terminais

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/terminais` | Leitura | Lista os terminais cadastrados. |

### GET `/api/v1/terminais`

Lista os terminais cadastrados.

- Acesso: **Leitura**
- Respostas: 200 `lista de TerminalDto`

## Transportadoras

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/transportadoras` | Leitura | Lista transportadoras com status calculado (negativada se ≥1 carreta negativada), contagem de carretas e reincidências N2 (30 dias). |
| POST | `/api/v1/transportadoras` | Escrita | Cadastra transportadora (CNPJ numérico ou alfanumérico, com DV). |
| DELETE | `/api/v1/transportadoras/{id}` | Gestao | Remove (somente sem veículos/condutores/ocorrências/agendamentos vinculados). Somente Gestor. |
| GET | `/api/v1/transportadoras/{id}` | Leitura | Uma transportadora. |
| PUT | `/api/v1/transportadoras/{id}` | Escrita | Atualiza nome/CNPJ. |

### GET `/api/v1/transportadoras`

Lista transportadoras com status calculado (negativada se ≥1 carreta negativada), contagem de carretas e reincidências N2 (30 dias).

- Acesso: **Leitura**
- Respostas: 200 `lista de TransportadoraDto`

### POST `/api/v1/transportadoras`

Cadastra transportadora (CNPJ numérico ou alfanumérico, com DV).

- Acesso: **Escrita**
- Corpo: `SalvarTransportadoraRequest`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 201 `TransportadoraDto`

### DELETE `/api/v1/transportadoras/{id}`

Remove (somente sem veículos/condutores/ocorrências/agendamentos vinculados). Somente Gestor.

- Acesso: **Gestao**
- Respostas: 204

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### GET `/api/v1/transportadoras/{id}`

Uma transportadora.

- Acesso: **Leitura**
- Respostas: 200 `TransportadoraDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### PUT `/api/v1/transportadoras/{id}`

Atualiza nome/CNPJ.

- Acesso: **Escrita**
- Corpo: `SalvarTransportadoraRequest`
- Respostas: 200 `TransportadoraDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

## Usuarios

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/usuarios` | Gestao | Lista usuários (sem hash de senha nem segredo MFA). |
| POST | `/api/v1/usuarios` | Gestao | Cria usuário com senha PROVISÓRIA (troca obrigatória no primeiro acesso). Senha: mín. 12 caracteres, fora da lista de senhas comuns. |
| GET | `/api/v1/usuarios/{id}` | Gestao | Um usuário. |
| POST | `/api/v1/usuarios/{id}/desativar` | Gestao | Desativa (bloqueia acesso e encerra sessões). Usuários não são apagados, para preservar a auditoria. |
| PUT | `/api/v1/usuarios/{id}/papel` | Gestao | Altera o papel (encerra as sessões do usuário). Não permite rebaixar o último Gestor nem a si mesmo. |
| POST | `/api/v1/usuarios/{id}/reativar` | Gestao | Reativa e remove bloqueio por tentativas. |
| POST | `/api/v1/usuarios/{id}/redefinir-mfa` | Gestao | Remove o MFA de quem perdeu o dispositivo (ele cadastra outro no próximo login). |
| POST | `/api/v1/usuarios/{id}/redefinir-senha` | Gestao | Recuperação de senha: gera senha provisória aleatória (mostrada uma vez) e encerra as sessões do usuário. |
| PUT | `/api/v1/usuarios/{id}/terminal` | Gestao | Vincula o usuário interno a um terminal (vazio = todos). Restringe no servidor tudo o que ele consulta; encerra as sessões dele. |

### GET `/api/v1/usuarios`

Lista usuários (sem hash de senha nem segredo MFA).

- Acesso: **Gestao**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<UsuarioDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `papel` | consulta | texto | não |  |  |
| `ativo` | consulta | booleano? | não |  |  |
| `busca` | consulta | texto | não | até 100 caracteres |  |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### POST `/api/v1/usuarios`

Cria usuário com senha PROVISÓRIA (troca obrigatória no primeiro acesso). Senha: mín. 12 caracteres, fora da lista de senhas comuns.

- Acesso: **Gestao**
- Corpo: `CriarUsuarioRequest`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 201 `UsuarioDto`

### GET `/api/v1/usuarios/{id}`

Um usuário.

- Acesso: **Gestao**
- Respostas: 200 `UsuarioDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/usuarios/{id}/desativar`

Desativa (bloqueia acesso e encerra sessões). Usuários não são apagados, para preservar a auditoria.

- Acesso: **Gestao**
- Respostas: 200 `UsuarioDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### PUT `/api/v1/usuarios/{id}/papel`

Altera o papel (encerra as sessões do usuário). Não permite rebaixar o último Gestor nem a si mesmo.

- Acesso: **Gestao**
- Corpo: `AlterarPapelRequest`
- Respostas: 200 `UsuarioDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/usuarios/{id}/reativar`

Reativa e remove bloqueio por tentativas.

- Acesso: **Gestao**
- Respostas: 200 `UsuarioDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/usuarios/{id}/redefinir-mfa`

Remove o MFA de quem perdeu o dispositivo (ele cadastra outro no próximo login).

- Acesso: **Gestao**
- Respostas: 200 `UsuarioDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/usuarios/{id}/redefinir-senha`

Recuperação de senha: gera senha provisória aleatória (mostrada uma vez) e encerra as sessões do usuário.

- Acesso: **Gestao**
- Respostas: 200 `SenhaProvisoriaDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### PUT `/api/v1/usuarios/{id}/terminal`

Vincula o usuário interno a um terminal (vazio = todos). Restringe no servidor tudo o que ele consulta; encerra as sessões dele.

- Acesso: **Gestao**
- Corpo: `DefinirTerminalUsuarioRequest`
- Respostas: 200 `UsuarioDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

## Veiculos

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/v1/veiculos` | Leitura | Lista veículos (filtros: transportadora, terminal, status de portaria/negativação, placa). |
| POST | `/api/v1/veiculos` | Escrita | Cadastra veículo (entra como Aguardando e Regular). Placa ABC-1234 ou Mercosul ABC1D23. |
| GET | `/api/v1/veiculos/placa/{placa}` | Leitura | Busca exata por placa (consulta da Portaria / leitura do QR). |
| DELETE | `/api/v1/veiculos/{id}` | Gestao | Remove veículo. Somente Gestor. |
| GET | `/api/v1/veiculos/{id}` | Leitura | Um veículo. |
| PUT | `/api/v1/veiculos/{id}` | Escrita | Atualiza placa, transportadora, modelo e terminal. |
| POST | `/api/v1/veiculos/{id}/desnegativar` | Escrita | Desnegativação manual: regulariza a carreta e resolve as ocorrências em aberto dessa placa. |
| POST | `/api/v1/veiculos/{id}/negativar` | Escrita | Negativação manual: registra ocorrência N3 administrativa e bloqueia a carreta. |
| PATCH | `/api/v1/veiculos/{id}/status-portaria` | Escrita | Muda a posição física (NoPatio, Aguardando, NoPorto, DescargaFinalizada) e reinicia o "desde". |

### GET `/api/v1/veiculos`

Lista veículos (filtros: transportadora, terminal, status de portaria/negativação, placa).

- Acesso: **Leitura**
- Paginado: `pagina` e `tamanhoPagina` na consulta; resposta `Pagina<…>` com `itens`, `total`, `numeroPagina`, `tamanhoPagina`
- Respostas: 200 `Pagina<VeiculoDto>`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `transportadoraId` | consulta | inteiro? | não |  |  |
| `terminalId` | consulta | texto | não |  |  |
| `statusPortaria` | consulta | texto | não |  |  |
| `statusNegativacao` | consulta | texto | não |  |  |
| `placa` | consulta | texto | não | até 10 caracteres | Placa (contém). |
| `pagina` | consulta | inteiro | não |  | Página (a partir de 1). |
| `tamanhoPagina` | consulta | inteiro | não |  | Itens por página (1–500, padrão 50). |

### POST `/api/v1/veiculos`

Cadastra veículo (entra como Aguardando e Regular). Placa ABC-1234 ou Mercosul ABC1D23.

- Acesso: **Escrita**
- Corpo: `CriarVeiculoRequest`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 201 `VeiculoDto`

### GET `/api/v1/veiculos/placa/{placa}`

Busca exata por placa (consulta da Portaria / leitura do QR).

- Acesso: **Leitura**
- Respostas: 200 `VeiculoDto`, 404 `ProblemDetails`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `placa` | rota | texto | sim | PlacaValida |  |

### DELETE `/api/v1/veiculos/{id}`

Remove veículo. Somente Gestor.

- Acesso: **Gestao**
- Respostas: 204

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### GET `/api/v1/veiculos/{id}`

Um veículo.

- Acesso: **Leitura**
- Respostas: 200 `VeiculoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### PUT `/api/v1/veiculos/{id}`

Atualiza placa, transportadora, modelo e terminal.

- Acesso: **Escrita**
- Corpo: `AtualizarVeiculoRequest`
- Respostas: 200 `VeiculoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/veiculos/{id}/desnegativar`

Desnegativação manual: regulariza a carreta e resolve as ocorrências em aberto dessa placa.

- Acesso: **Escrita**
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 200 `VeiculoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### POST `/api/v1/veiculos/{id}/negativar`

Negativação manual: registra ocorrência N3 administrativa e bloqueia a carreta.

- Acesso: **Escrita**
- Corpo: `NegativarVeiculoRequest`
- Aceita `Idempotency-Key` (repetição devolve a mesma resposta)
- Respostas: 200 `VeiculoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

### PATCH `/api/v1/veiculos/{id}/status-portaria`

Muda a posição física (NoPatio, Aguardando, NoPorto, DescargaFinalizada) e reinicia o "desde".

- Acesso: **Escrita**
- Corpo: `AtualizarStatusPortariaRequest`
- Respostas: 200 `VeiculoDto`

| Parâmetro | Em | Tipo | Obrigatório | Regras | Descrição |
|---|---|---|---|---|---|
| `id` | rota | inteiro | sim | int |  |

## Contratos (DTOs)

Nomes dos campos como trafegam em JSON (camelCase). Enums trafegam pelo nome.

### AbrirContestacaoPortalRequest

Contestação aberta pelo Portal: a transportadora vem do token, nunca do corpo.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `ocorrenciaId` | inteiro | não | obrigatório |  |
| `justificativa` | texto | não | obrigatório; 10 a 4000 caracteres; TextoSimples |  |

### AbrirContestacaoRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `ocorrenciaId` | inteiro | não | obrigatório |  |
| `transportadoraId` | inteiro? | sim |  | Opcional — se informado, precisa ser a transportadora da ocorrência. |
| `justificativa` | texto | não | obrigatório; 10 a 4000 caracteres; TextoSimples |  |

### AgendamentoDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro | não |  |  |
| `codigo` | texto | não |  |  |
| `data` | data | não |  |  |
| `hora` | hora | não |  |  |
| `placa` | texto | não |  |  |
| `transportadoraId` | inteiro | não |  |  |
| `transportadora` | texto | sim |  |  |
| `terminalId` | texto | não |  |  |
| `terminal` | texto | sim |  |  |
| `carga` | texto | não |  |  |
| `motorista` | texto | sim |  |  |
| `janelaConformidade` | JanelaConformidade | não |  |  |
| `status` | StatusAgendamento | não |  |  |

### AgendamentosPorHoraDto

Agendamentos na faixa de uma hora (Hora = início da faixa) e a capacidade por hora cadastrada (nula = não configurada).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `hora` | hora | não |  |  |
| `total` | inteiro | não |  |  |
| `nivel` | texto | não |  |  |
| `capacidade` | inteiro? | sim |  |  |

### AlertaDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro | não |  |  |
| `tipo` | TipoAlerta | não |  |  |
| `regra` | texto | não |  |  |
| `prioridade` | PrioridadeAlerta | não |  |  |
| `terminalId` | texto | sim |  |  |
| `terminal` | texto | sim |  |  |
| `periodo` | texto | não |  |  |
| `titulo` | texto | não |  |  |
| `indicador` | texto | não |  |  |
| `valorObservado` | texto | não |  |  |
| `limite` | texto | não |  |  |
| `evidencia` | texto | não |  |  |
| `origem` | texto | não |  |  |
| `acaoRecomendada` | texto | sim |  |  |
| `estado` | EstadoAlerta | não |  |  |
| `ativo` | booleano | não |  |  |
| `identificadoEm` | data e hora | não |  |  |
| `atualizadoEm` | data e hora | não |  |  |
| `responsavel` | texto | sim |  |  |
| `eventos` | lista de EventoAlertaDto | sim |  |  |

### AlertaOperacional

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `nivel` | texto | não |  |  |
| `titulo` | texto | não |  |  |
| `detalhe` | texto | não |  |  |

### AlertaRitmo

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `de` | inteiro | não |  |  |
| `ate` | inteiro | não |  |  |
| `duracaoHoras` | inteiro | não |  |  |

### AlertaSaturacao

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `tipo` | texto | não |  |  |
| `mediaRecente` | inteiro | não |  |  |
| `tendenciaDiaria` | inteiro | não |  |  |
| `diasParaSaturacao` | inteiro? | sim |  |  |
| `mensagem` | texto | não |  |  |

### AlterarPapelRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `papel` | PapelUsuario | não | obrigatório |  |

### AmbienteDto

Informações do ambiente que a tela precisa exibir.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `dadosExemplo` | booleano | não |  | O banco contém o cadastro fictício de exemplo (placas, transportadoras, condutores, ocorrências, agendamentos). |

### AnaliseDimensao

Uma dimensão com seus grupos (os maiores primeiro).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `dimensao` | texto | não |  | terminal \| operador \| carga \| convenio \| origem. |
| `grupos` | lista de GrupoDimensao | não |  | Grupos, por entradas decrescentes. |
| `outros` | GrupoDimensao | sim |  | Grupos além dos exibidos, somados (nulo se todos aparecem). |

### AnalisePreditiva

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `projecoes` | lista de ProjecaoMensal | não |  |  |
| `tendencia` | texto | não |  |  |
| `inclinacao` | inteiro | não |  |  |

### AtrasoRecorrente

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `convenio` | texto | não |  |  |
| `empresaNome` | texto | não |  |  |
| `ocorrencias` | inteiro | não |  |  |
| `esperaMedia` | número | não |  |  |

### AtualizarAgendamentoRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `status` | StatusAgendamento | não | obrigatório |  |
| `data` | data? | sim |  | Padrão: hoje (horário do porto). |
| `hora` | hora | não | obrigatório |  |
| `placa` | texto | não | obrigatório; 7 a 10 caracteres; PlacaValida |  |
| `transportadoraId` | inteiro | não | obrigatório |  |
| `terminalId` | texto | não | obrigatório; até 32 caracteres |  |
| `carga` | texto | não | obrigatório; até 50 caracteres; TextoSimples |  |
| `motorista` | texto | sim | até 150 caracteres; TextoSimples |  |
| `janelaConformidade` | JanelaConformidade | não |  |  |

### AtualizarCapacidadeTerminalRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `capacidadeDiariaCarretas` | inteiro? | sim | de 1 a 100000 | Carretas/dia. Vazio = não informada (o painel não mostra ocupação). |
| `capacidadeHoraria` | inteiro? | sim | de 1 a 10000 | Carretas por janela de 1 h (agendamentos). Vazio = não configurada. |

### AtualizarConvenioRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `operador` | texto | não | obrigatório; 1 a 50 caracteres; TextoSimples |  |
| `carga` | texto | não | obrigatório; 1 a 50 caracteres; TextoSimples |  |

### AtualizarParametrosRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `limiteEsperaMedianaHoras` | número | não | de 0.5 a 1000 |  |
| `metaLiberadas24hPct` | número | não | de 1 a 100 |  |
| `limiteEsperaLongaHoras` | número | não | de 1 a 10000 |  |
| `diasSemDadosAlerta` | inteiro | não | de 1 a 365 |  |
| `limiteRazaoChegadaLiberacao` | número | não | de 1 a 10 |  |

### AtualizarPreferenciasRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `telaInicial` | texto | sim | até 40 caracteres | Chave de uma tela interna (visao-geral, gargalos, movimentacoes…) ou vazio para o padrão do papel. |
| `terminalPreferido` | texto | sim | até 32 caracteres |  |

### AtualizarStatusAgendamentoRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `status` | StatusAgendamento | não | obrigatório |  |

### AtualizarStatusPortariaRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `statusPortaria` | StatusPortaria | não | obrigatório |  |

### AtualizarVeiculoRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `placa` | texto | não | obrigatório; 7 a 10 caracteres; PlacaValida |  |
| `transportadoraId` | inteiro | não | obrigatório |  |
| `modelo` | texto | sim | até 100 caracteres; TextoSimples |  |
| `terminalId` | texto | não | obrigatório; até 32 caracteres |  |

### AvisosImportacaoDto

Quantas linhas novas/complementares entram com cada sinal de qualidade.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `esperaAcima72h` | inteiro | não |  |  |
| `semLiberacao` | inteiro | não |  |  |
| `possivelRemarcacao` | inteiro | não |  |  |
| `convenioNovo` | inteiro | não |  |  |

### CapacidadeDia

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `data` | texto | não |  |  |
| `total` | inteiro | não |  |  |
| `pctCapacidade` | número | não |  |  |

### CargaTotal

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `carga` | texto | não |  |  |
| `total` | inteiro | não |  |  |

### CicloBalde

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `balde` | texto | não |  |  |
| `total` | inteiro | não |  |  |
| `esperaMedia` | número | não |  |  |

### CodigoMfaRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `codigo` | texto | não | obrigatório; até 10 caracteres |  |

### CondutorDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro | não |  |  |
| `nome` | texto | não |  |  |
| `transportadoraId` | inteiro | não |  |  |
| `transportadora` | texto | sim |  |  |
| `placaVinculada` | texto | sim |  |  |
| `statusNegativacao` | StatusNegativacao | não |  |  |

### ConfiguracaoMfaDto

Mostrado UMA vez: o segredo não é devolvido de novo por nenhum endpoint.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `segredo` | texto | não |  |  |
| `uriOtpauth` | texto | não |  |  |

### ConfiguracaoOperacionalDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `terminais` | lista de TerminalDto | não |  |  |
| `parametros` | ParametrosOperacionaisDto | não |  |  |

### ConfirmarSenhaRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `senhaAtual` | texto | não | obrigatório; até 128 caracteres |  |

### ContestacaoDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro | não |  |  |
| `ocorrenciaId` | inteiro | não |  |  |
| `nivelOcorrencia` | NivelOcorrencia? | sim |  |  |
| `placa` | texto | sim |  |  |
| `transportadoraId` | inteiro | não |  |  |
| `transportadora` | texto | sim |  |  |
| `justificativa` | texto | não |  |  |
| `status` | StatusContestacao | não |  |  |
| `criadoEm` | data e hora | não |  |  |
| `respondidoEm` | data e hora? | sim |  |  |
| `respostaOperador` | texto | sim |  |  |

### ConvenioDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `codigo` | texto | não |  |  |
| `nome` | texto | não |  |  |
| `operador` | texto | não |  |  |
| `carga` | texto | não |  |  |
| `deduzido` | booleano | não |  |  |

### CriarAgendamentoRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `data` | data? | sim |  | Padrão: hoje (horário do porto). |
| `hora` | hora | não | obrigatório |  |
| `placa` | texto | não | obrigatório; 7 a 10 caracteres; PlacaValida |  |
| `transportadoraId` | inteiro | não | obrigatório |  |
| `terminalId` | texto | não | obrigatório; até 32 caracteres |  |
| `carga` | texto | não | obrigatório; até 50 caracteres; TextoSimples |  |
| `motorista` | texto | sim | até 150 caracteres; TextoSimples |  |
| `janelaConformidade` | JanelaConformidade | não |  |  |

### CriarUsuarioRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `nome` | texto | não | obrigatório; 2 a 150 caracteres |  |
| `email` | texto | não | obrigatório; e-mail; até 200 caracteres |  |
| `senha` | texto | não | obrigatório; até 128 caracteres | Senha inicial (provisória: o usuário troca no primeiro acesso). |
| `papel` | PapelUsuario | não | obrigatório |  |
| `transportadoraId` | inteiro? | sim |  | Obrigatório quando Papel = Transportadora (usuário do Portal); proibido nos demais. |

### CriarVeiculoRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `placa` | texto | não | obrigatório; 7 a 10 caracteres; PlacaValida |  |
| `transportadoraId` | inteiro | não | obrigatório |  |
| `modelo` | texto | sim | até 100 caracteres; TextoSimples |  |
| `terminalId` | texto | não | obrigatório; até 32 caracteres |  |
| `statusPortaria` | StatusPortaria? | sim |  | Padrão: Aguardando. |

### DadosPessoaisCondutorDto

Relatório LGPD dos dados pessoais de um condutor.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `condutor` | CondutorDto | não |  |  |
| `ocorrencias` | lista de OcorrenciaDto | não |  |  |
| `agendamentos` | lista de AgendamentoDto | não |  |  |

### DecidirConflitosRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `decisao` | DecisaoConflitoImportacao | não | obrigatório | Aplicar (gravar os valores da planilha) ou Manter (deixar como está). |
| `linhas` | lista de inteiro longo | sim | até 5000 itens/caracteres | Linhas a decidir. Vazio = todos os conflitos ainda pendentes do lote. |

### DefinirTerminalUsuarioRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `terminalId` | texto | sim | até 32 caracteres | Terminal ao qual o usuário fica vinculado; vazio = todos os terminais. |

### DesativarMfaRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `senhaAtual` | texto | não | obrigatório; até 128 caracteres |  |
| `codigo` | texto | não | obrigatório; até 10 caracteres |  |

### DetalhamentoMes

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `chave` | texto | não |  |  |
| `rotulo` | texto | não |  |  |
| `total` | inteiro | não |  |  |
| `dias` | lista de DiaDoMes | não |  |  |

### DiaCompacto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `data` | texto | não |  |  |
| `empresa` | texto | não |  |  |
| `total` | inteiro | não |  |  |
| `pctCapacidade` | número | não |  |  |
| `gargalo` | booleano | não |  |  |

### DiaDoMes

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `dia` | inteiro | não |  |  |
| `total` | inteiro | não |  |  |

### DiaEmpresaTotal

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `data` | texto | não |  |  |
| `empresa` | texto | não |  |  |
| `total` | inteiro | não |  |  |

### DiaSemanaTotal

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `rotulo` | texto | não |  |  |
| `total` | inteiro | não |  |  |

### EmpresaTotal

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `empresa` | texto | não |  |  |
| `total` | inteiro | não |  |  |
| `esperaMedia` | número | não |  |  |

### ErroEventoDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `indice` | inteiro | não |  |  |
| `movimentoId` | texto | não |  |  |
| `erro` | texto | não |  |  |

### ErroMedido

Erro da estimativa medido nos dados do próprio terminal (backtest).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `diasAvaliados` | inteiro | não |  | Dias reestimados como na véspera. |
| `erroPct` | número | não |  | WAPE das chegadas por hora: soma dos erros ÷ soma do real, em %. |
| `erroIngenuoPct` | número | não |  | WAPE de "repetir o mesmo horário do último dia com dados", em %. |
| `naoPiorQueIngenua` | booleano | não |  | Critério objetivo: a estimativa não pode errar mais que a referência ingênua. |

### EstatisticasEspera

Estatísticas de espera (marcação → liberação) de um conjunto de movimentos. Esperas só existem para movimentos liberados; os ainda abertos são contados à parte.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `movimentacoes` | inteiro | não |  | Marcações no período (liberadas + ainda sem liberação). |
| `liberadas` | inteiro | não |  | Movimentos já liberados (base das esperas). |
| `semLiberacao` | inteiro | não |  | Marcados e ainda não liberados. |
| `mediaDiaria` | número? | sim |  | Movimentações ÷ dias corridos do período. |
| `diasComOperacao` | inteiro | não |  | Dias com ao menos uma marcação. |
| `esperaMediana` | número? | sim |  | Mediana da espera (h). |
| `esperaP90` | número? | sim |  | 90º percentil da espera (h). |
| `esperaMedia` | número? | sim |  | Média da espera (h) — puxada por casos extremos. |
| `liberadasAte24hPct` | número? | sim |  | % das liberadas com espera ≤ 24 h. |
| `esperasLongas` | inteiro | não |  | Liberadas com espera acima do limite de espera longa (padrão 72 h). |

### EstimativaTerminal

Estimativa de um terminal (ou de todos somados).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `terminalId` | texto | sim |  | Id do terminal; nulo na visão de todos. |
| `terminal` | texto | não |  | Nome do terminal ou "Todos os terminais". |
| `baseDe` | data? | sim |  | Primeiro dia da base histórica usada. |
| `baseAte` | data? | sim |  | Último dia da base histórica usada. |
| `diasComDados` | inteiro | não |  | Dias da base com ao menos uma marcação. |
| `ultimoRegistro` | data e hora? | sim |  | Última marcação recebida. |
| `desatualizado` | booleano | não |  | Último registro mais antigo que o limite de dias sem dados. |
| `confiabilidade` | texto | não |  | confiavel \| limitada \| insuficiente \| desatualizada. |
| `motivos` | lista de MotivoConfiabilidade | não |  | Por que a confiabilidade não é "confiável" (motivo, impacto, ação). |
| `erro` | ErroMedido | sim |  | Erro medido nos dados do terminal (nulo na soma ou sem dias suficientes). |
| `janelas` | lista de JanelaEstimada | não |  | As próximas janelas de uma hora. |
| `resumo` | ResumoEstimativa | não |  | Resumo do horizonte. |

### EventoAlertaDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `quando` | data e hora | não |  |  |
| `autor` | texto | não |  |  |
| `acao` | texto | não |  |  |
| `texto` | texto | sim |  |  |

### EventoIntegracaoDto

Evento empurrado por sistema externo (terminal, balança, gate).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `tipo` | TipoEventoIntegracao | não | obrigatório |  |
| `movimentoId` | texto | não | obrigatório; formato `^[A-Za-z0-9-]{1,32}$` |  |
| `terminalId` | texto | sim | até 32 caracteres |  |
| `senha` | texto | sim | até 32 caracteres; TextoSimples |  |
| `convenio` | texto | sim | até 100 caracteres; TextoSimples |  |
| `codConvenio` | texto | sim | até 32 caracteres; TextoSimples |  |
| `operador` | texto | sim | até 50 caracteres; TextoSimples |  |
| `carga` | texto | sim | até 50 caracteres; TextoSimples |  |
| `ciclo` | inteiro? | sim | de 0 a 100000 |  |
| `dataMarcacao` | data e hora? | sim |  | Horário local do porto, sem fuso (ex.: 2026-07-31T15:03:09). |
| `dataLiberacao` | data e hora? | sim |  |  |

### FaixaRitmo

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `hora` | inteiro | não |  |  |
| `chegadas` | inteiro | não |  |  |
| `saidas` | inteiro | não |  |  |
| `lambda` | número | não |  |  |
| `mu` | número | não |  |  |
| `congestionado` | booleano | não |  |  |

### FilaVirtualDto

Fila virtual da portaria.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `noPatio` | inteiro | não |  | Carretas com situação No pátio. |
| `limiarCongestionamento` | inteiro | não |  | Limite configurado (Operacao:LimiarCongestionamento). |
| `congestionado` | booleano | não |  | NoPatio ≥ limite. |
| `fila` | lista de ItemFilaVirtualDto | não |  | Carretas No pátio ou Aguardando, em ordem. |
| `tempoTipico` | lista de TempoTipicoTerminalDto | não |  | Tempo típico de espera por terminal usado nas estimativas. |

### FluxoHorarioDto

Média de marcações (chegadas) e de liberações por dia, numa hora do dia.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `hora` | inteiro | não |  |  |
| `chegadasPorDia` | número | não |  |  |
| `liberacoesPorDia` | número | não |  |  |
| `razao` | número? | sim |  |  |

### FrescorTerminalDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `terminalId` | texto | não |  |  |
| `terminal` | texto | não |  |  |
| `ultimaMarcacao` | data e hora? | sim |  |  |
| `ultimaLiberacao` | data e hora? | sim |  |  |
| `ultimaImportacaoEm` | data e hora? | sim |  |  |
| `ultimaImportacaoArquivo` | texto | sim |  |  |
| `diasSemDados` | inteiro? | sim |  |  |
| `desatualizado` | booleano | não |  |  |

### GrupoDimensao

Um grupo de uma dimensão (terminal, operador, carga, convênio, origem).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `chave` | texto | não |  |  |
| `rotulo` | texto | não |  |  |
| `entradas` | inteiro | não |  |  |
| `pctEntradas` | número | não |  |  |
| `concluidas` | inteiro | não |  |  |
| `pendentes` | inteiro | não |  |  |
| `esperaMediana` | número? | sim |  |  |
| `esperaP90` | número? | sim |  |  |
| `liberadasAte24hPct` | número? | sim |  |  |
| `esperasLongas` | inteiro | não |  |  |

### HoraPico

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `hora` | inteiro | não |  |  |
| `total` | inteiro | não |  |  |
| `acimaMedia` | booleano | não |  |  |

### HoraTotal

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `hora` | inteiro | não |  |  |
| `total` | inteiro | não |  |  |

### HorarioCritico

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `diaSemana` | texto | não |  |  |
| `turno` | texto | não |  |  |
| `total` | inteiro | não |  |  |

### IndicadorPerformance

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `terminal` | texto | não |  |  |
| `tmaGeral` | número | não |  |  |
| `taxaD0Geral` | número | não |  |  |
| `totalGeral` | inteiro | não |  |  |
| `tmaAtual` | número | não |  |  |
| `taxaD0Atual` | número | não |  |  |
| `deltaTMA` | número? | sim |  |  |
| `deltaD0` | número? | sim |  |  |
| `serieTMA` | lista de SerieMensal | não |  |  |
| `serieD0` | lista de SerieMensal | não |  |  |
| `ultimoMesRotulo` | texto | não |  |  |

### IndicadorTerminalDto

Indicadores de um terminal no período.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `terminalId` | texto | não |  | Id do terminal. |
| `terminal` | texto | não |  | Nome. |
| `atual` | EstatisticasEspera | não |  | Período principal. |
| `anterior` | EstatisticasEspera | sim |  | Período de comparação, se pedido. |
| `picoDiario` | inteiro? | sim |  | Maior número de marcações num dia do período. |
| `picoData` | data? | sim |  | Dia do pico. |
| `capacidadeDiaria` | inteiro? | sim |  | Nula enquanto o porto não informar a capacidade do terminal. |
| `ocupacaoMediaPct` | número? | sim |  | Média diária ÷ capacidade (%). Nula sem capacidade configurada. |
| `ultimaMarcacao` | data e hora? | sim |  | Última marcação recebida do terminal (qualquer período). |

### ItemFilaVirtualDto

Carreta na fila virtual (situação No pátio ou Aguardando).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `posicao` | inteiro | não |  | Ordem pela hora em que entrou na situação atual. |
| `veiculoId` | inteiro | não |  | Id do veículo. |
| `placa` | texto | não |  | Placa. |
| `transportadora` | texto | sim |  | Transportadora. |
| `statusPortaria` | StatusPortaria | não |  | Situação atual. |
| `terminalId` | texto | não |  | Terminal de destino. |
| `terminal` | texto | sim |  | Nome do terminal. |
| `desde` | data e hora | não |  | Quando entrou na situação atual (UTC). |
| `minutosDecorridos` | inteiro | não |  | Minutos desde então. |
| `tempoTipicoTerminalHoras` | número? | sim |  | Mediana da espera no terminal (marcação → liberação), últimos 30 dias com dados. |
| `minutosRestantesEstimados` | inteiro? | sim |  | Tempo típico − decorrido (mínimo 0); nulo sem histórico. |
| `alemDoTempoTipico` | booleano | não |  | Já passou do tempo típico do terminal. |

### JanelaDistribuicao

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `chave` | texto | não |  |  |
| `rotulo` | texto | não |  |  |
| `min` | número | não |  |  |
| `max` | número? | sim |  |  |
| `cor` | texto | não |  |  |
| `total` | inteiro | não |  |  |
| `pct` | número | não |  |  |

### JanelaEstimada

Uma janela de uma hora: demanda prevista × capacidade de atendimento.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `hora` | inteiro | não |  | Hora de início da janela (0–23), horário do porto. |
| `demandaPrevista` | número | não |  | Carretas que costumam chegar nesta hora (média por dia na base). |
| `capacidadeAtendimento` | número | não |  | Referência usada na classificação: capacidade cadastrada ou, sem ela, o atendimento observado. |
| `fonteCapacidade` | texto | não |  | cadastrada \| observada \| mista (soma de terminais com fontes diferentes). |
| `atendimentoObservado` | número | não |  | Carretas que costumam ser liberadas nesta hora (média por dia na base). |
| `capacidadeCadastrada` | inteiro? | sim |  | Capacidade por hora cadastrada (soma, quando todos os terminais somados têm); nula se não configurada. |
| `saldo` | número? | sim |  | Capacidade − demanda. Positivo = folga; negativo = demanda acima da capacidade. Nulo sem dados. |
| `situacao` | texto | não |  | dentro \| atencao \| risco \| insuficiente. |
| `confiabilidade` | texto | não |  | confiavel \| limitada \| insuficiente \| desatualizada. |
| `mensagem` | texto | não |  | Conclusão em linguagem operacional, gerada a partir dos números. |
| `chegadasObservadas` | inteiro | não |  | Chegadas registradas nesta hora em toda a base (tamanho da amostra). |
| `terminaisEmRisco` | lista de texto | não |  | Na visão de todos os terminais: quais estão em risco de fila nesta janela. |

### KpisGerais

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `total` | inteiro | não |  |  |
| `mediaDiaria` | número | não |  |  |
| `diasOperados` | inteiro | não |  |  |
| `mesMaisMovimentado` | MesTotal | não |  |  |
| `tempoMedioEspera` | número | não |  |  |
| `tempoMedioPonderado` | número | não |  |  |
| `maiorAtraso` | RegistroMarcacao | não |  |  |

### LinhaImportacaoDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro longo | não |  |  |
| `numeroLinha` | inteiro | não |  |  |
| `classificacao` | ClassificacaoLinhaImportacao | não |  |  |
| `decisao` | DecisaoConflitoImportacao | não |  |  |
| `motivo` | texto | sim |  |  |
| `avisos` | lista de texto | não |  |  |
| `movimentoId` | texto | não |  |  |
| `senha` | texto | sim |  |  |
| `convenio` | texto | sim |  |  |
| `codConvenio` | texto | sim |  |  |
| `ciclo` | inteiro? | sim |  |  |
| `dataMarcacao` | data e hora? | sim |  |  |
| `dataLiberacao` | data e hora? | sim |  |  |
| `esperaHoras` | número? | sim |  |  |

### LogAuditoriaDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro longo | não |  |  |
| `autor` | texto | não |  |  |
| `acao` | texto | não |  |  |
| `detalhes` | texto | não |  |  |
| `quando` | data e hora | não |  |  |

### LoginRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `email` | texto | não | obrigatório; e-mail; até 200 caracteres |  |
| `senha` | texto | não | obrigatório; até 128 caracteres |  |
| `codigoMfa` | texto | sim | até 10 caracteres | Código de 6 dígitos do app autenticador (obrigatório se o MFA estiver ativo). |

### LoginRespostaDto

Restricao: null = acesso normal; "trocar-senha" = senha provisória, só dá pra trocar a senha; "configurar-mfa" = Gestor sem MFA, só dá pra configurar.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `token` | texto | não |  |  |
| `expiraEm` | data e hora | não |  |  |
| `restricao` | texto | sim |  |  |
| `usuario` | UsuarioDto | não |  |  |

### LoteImportacaoDto

Uma importação e o resultado da análise/confirmação. MesmoArquivoConfirmadoEm é preenchido quando um arquivo idêntico (mesmo SHA-256) já foi confirmado para o terminal.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro | não |  |  |
| `arquivo` | texto | não |  |  |
| `formato` | FormatoArquivoImportacao | não |  |  |
| `tamanhoBytes` | inteiro longo | não |  |  |
| `terminalId` | texto | não |  |  |
| `terminal` | texto | sim |  |  |
| `status` | StatusLoteImportacao | não |  |  |
| `criadoPor` | texto | não |  |  |
| `criadoEm` | data e hora | não |  |  |
| `concluidoPor` | texto | sim |  |  |
| `concluidoEm` | data e hora? | sim |  |  |
| `marcacaoInicial` | data e hora? | sim |  |  |
| `marcacaoFinal` | data e hora? | sim |  |  |
| `linhas` | inteiro | não |  |  |
| `novos` | inteiro | não |  |  |
| `iguais` | inteiro | não |  |  |
| `complementares` | inteiro | não |  |  |
| `conflitantes` | inteiro | não |  |  |
| `conflitosPendentes` | inteiro | não |  |  |
| `rejeitados` | inteiro | não |  |  |
| `comAvisos` | inteiro | não |  |  |
| `avisos` | AvisosImportacaoDto | não |  |  |
| `conveniosNovos` | lista de ConvenioDto | não |  |  |
| `inseridos` | inteiro | não |  |  |
| `atualizados` | inteiro | não |  |  |
| `ignorados` | inteiro | não |  |  |
| `mesmoArquivoConfirmadoEm` | data e hora? | sim |  |  |

### MarcacaoDetalheDto

Movimento com a rastreabilidade completa: arquivo de origem e quantas carretas foram liberadas no mesmo instante.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `movimento` | MarcacaoDto | não |  |  |
| `arquivoOrigem` | texto | sim |  |  |
| `importadoEm` | data e hora? | sim |  |  |
| `importadoPor` | texto | sim |  |  |
| `liberadosNoMesmoInstante` | inteiro | não |  |  |

### MarcacaoDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `movimentoId` | texto | não |  |  |
| `senha` | texto | não |  |  |
| `convenio` | texto | não |  |  |
| `codConvenio` | texto | sim |  |  |
| `operador` | texto | não |  |  |
| `carga` | texto | não |  |  |
| `ciclo` | inteiro | não |  |  |
| `dataMarcacao` | data e hora | não |  |  |
| `dataLiberacao` | data e hora? | sim |  |  |
| `esperaHoras` | número? | sim |  |  |
| `janela` | texto | sim |  |  |
| `terminalId` | texto | não |  |  |
| `terminal` | texto | sim |  |  |
| `origem` | OrigemMarcacao | não |  |  |
| `loteImportacaoId` | inteiro? | sim |  |  |
| `linhaOrigem` | inteiro? | sim |  |  |

### MesDetalhado

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `chave` | texto | não |  |  |
| `rotulo` | texto | não |  |  |
| `total` | inteiro | não |  |  |
| `representatividade` | número | não |  |  |
| `diasOperados` | inteiro | não |  |  |
| `mediaDiaria` | número | não |  |  |

### MesTotal

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `chave` | texto | não |  |  |
| `rotulo` | texto | não |  |  |
| `total` | inteiro | não |  |  |

### MotivoConfiabilidade

Por que a confiabilidade não é "confiável", o que isso muda e o que fazer.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `codigo` | texto | não |  | desatualizado \| base-incompleta \| erro-alto \| erro-nao-medido \| sem-base \| sem-dados. |
| `terminal` | texto | sim |  | Terminal a que o motivo se refere (na soma de todos). |
| `descricao` | texto | não |  | O que foi encontrado, com números. |
| `impacto` | texto | não |  | O que isso muda na leitura da estimativa. |
| `acao` | texto | não |  | O que fazer. |

### MotorMetricas.MatrizVolumeMensal

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `meses` | lista de MotorMetricas.MesRotulo | não |  |  |
| `empresas` | lista de texto | não |  |  |
| `valores` | mapa texto → mapa texto → inteiro | não |  |  |

### MotorMetricas.MesRotulo

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `chave` | texto | não |  |  |
| `rotulo` | texto | não |  |  |

### NegativarVeiculoRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `motivo` | texto | sim | até 1000 caracteres; TextoSimples |  |

### OcorrenciaDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro | não |  |  |
| `nivel` | NivelOcorrencia | não |  |  |
| `placa` | texto | não |  |  |
| `transportadoraId` | inteiro | não |  |  |
| `transportadora` | texto | sim |  |  |
| `condutorId` | inteiro? | sim |  |  |
| `condutor` | texto | sim |  |  |
| `descricao` | texto | não |  |  |
| `local` | texto | sim |  |  |
| `responsavel` | texto | sim |  |  |
| `status` | StatusOcorrencia | não |  |  |
| `criadoEm` | data e hora | não |  |  |

### OcorrenciaRegistradaDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `ocorrencia` | OcorrenciaDto | não |  |  |
| `veiculoBloqueado` | booleano | não |  |  |
| `condutorBloqueado` | booleano | não |  |  |
| `veiculoCadastrado` | booleano | não |  |  |

### OperacaoAgoraDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `noPatio` | inteiro | não |  |  |
| `emOperacao` | inteiro | não |  |  |
| `aguardando` | inteiro | não |  |  |
| `finalizados` | inteiro | não |  |  |
| `total` | inteiro | não |  |  |

### OperadorTotal

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `operador` | texto | não |  |  |
| `total` | inteiro | não |  |  |

### Pagina<AgendamentoDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de AgendamentoDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### Pagina<AlertaDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de AlertaDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### Pagina<CondutorDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de CondutorDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### Pagina<ContestacaoDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de ContestacaoDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### Pagina<LinhaImportacaoDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de LinhaImportacaoDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### Pagina<LogAuditoriaDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de LogAuditoriaDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### Pagina<LoteImportacaoDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de LoteImportacaoDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### Pagina<MarcacaoDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de MarcacaoDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### Pagina<OcorrenciaDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de OcorrenciaDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### Pagina<ResultadoAvaliacaoDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de ResultadoAvaliacaoDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### Pagina<UsuarioDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de UsuarioDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### Pagina<VeiculoDto>

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `itens` | lista de VeiculoDto | não |  |  |
| `total` | inteiro | não |  |  |
| `numeroPagina` | inteiro | não |  |  |
| `tamanhoPagina` | inteiro | não |  |  |
| `totalPaginas` | inteiro | não |  |  |

### ParametrosOperacionaisDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `limiteEsperaMedianaHoras` | número | não |  |  |
| `metaLiberadas24hPct` | número | não |  |  |
| `limiteEsperaLongaHoras` | número | não |  |  |
| `diasSemDadosAlerta` | inteiro | não |  |  |
| `limiteRazaoChegadaLiberacao` | número | não |  |  |
| `atualizadoEm` | data e hora? | sim |  |  |
| `atualizadoPor` | texto | sim |  |  |

### PeriodoDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `de` | data | não |  |  |
| `ate` | data | não |  |  |
| `dias` | inteiro | não |  |  |

### PicosEntradaSaida

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `entrada` | lista de HoraPico | não |  |  |
| `saida` | lista de HoraPico | não |  |  |
| `picoEntrada` | HoraPico | não |  |  |
| `picoSaida` | HoraPico | não |  |  |
| `valeEntrada` | HoraPico | não |  |  |
| `mediaEntrada` | número | não |  |  |
| `mediaSaida` | número | não |  |  |

### PontoDiaSemana

Dia da semana (0 = domingo): média de entradas por ocorrência daquele dia no período.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `dia` | inteiro | não |  |  |
| `entradasPorDia` | número | não |  |  |

### PontoHora

Faixa horária: médias por dia no período.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `hora` | inteiro | não |  |  |
| `entradasPorDia` | número | não |  |  |
| `saidasPorDia` | número | não |  |  |

### PontoSerie

Um ponto da série temporal (dia, semana ou mês).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `chave` | texto | não |  | yyyy-MM-dd (dia ou segunda-feira da semana) ou yyyy-MM. |
| `de` | data | não |  | Primeiro dia do ponto dentro do período. |
| `ate` | data | não |  | Último dia do ponto dentro do período. |
| `entradas` | inteiro | não |  | Marcações. |
| `saidas` | inteiro | não |  | Liberações. |
| `esperaMediana` | número? | sim |  | Mediana das esperas das marcadas neste ponto e já liberadas. |
| `parcial` | booleano | não |  | O ponto não cobre a semana/mês inteiro (corte do período). |

### PreferenciasDto

TelaInicial: escolhida pelo usuário, ou null (usa o padrão do papel — TelaPadrao). TerminalPreferido: só filtra a tela já de entrada; nunca amplia o vínculo.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `telaInicial` | texto | sim |  |  |
| `telaPadrao` | texto | não |  |  |
| `terminalPreferido` | texto | sim |  |  |

### ProjecaoMensal

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `chave` | texto | não |  |  |
| `rotulo` | texto | não |  |  |
| `total` | inteiro | não |  |  |
| `projetado` | booleano | não |  |  |
| `margemInferior` | inteiro | não |  |  |
| `margemSuperior` | inteiro | não |  |  |
| `confianca` | inteiro | não |  |  |

### ProximasHorasDto

Estimativa das próximas horas pelo padrão histórico (não é previsão por modelo). Geral = terminal filtrado ou todos somados; Terminais = cada terminal.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `geradoEm` | data e hora | não |  |  |
| `horas` | inteiro | não |  |  |
| `limiteRisco` | número | não |  |  |
| `geral` | EstimativaTerminal | não |  |  |
| `terminais` | lista de EstimativaTerminal | não |  |  |

### QualidadeDados

Qualidade e integridade dos registros do período (entradas).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `registros` | inteiro | não |  | Entradas analisadas. |
| `semLiberacao` | inteiro | não |  | Sem data de liberação (não entram nas esperas). |
| `semCodigoConvenio` | inteiro | não |  | Sem COD_CONVENIO (típico da carga inicial). |
| `possiveisRemarcacoes` | inteiro | não |  | Mesma senha, convênio e liberação, marcada de novo em até 10 min (contagem de registros repetidos). |
| `liberadosEmLote` | inteiro | não |  | Liberados no mesmo instante que ao menos outro movimento do terminal. |
| `duplicidadesMovimento` | inteiro | não |  | Mesmo número de movimento repetido: sempre 0, garantido pela chave do banco. |
| `ultimaMarcacao` | data e hora? | sim |  | Última marcação entre os registros do recorte (qualquer data). |

### Recomendacao

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `prioridade` | texto | não |  |  |
| `acao` | texto | não |  |  |
| `detalhe` | texto | não |  |  |

### RegistrarOcorrenciaRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `nivel` | NivelOcorrencia | não | obrigatório |  |
| `placa` | texto | não | obrigatório; 7 a 10 caracteres; PlacaValida |  |
| `transportadoraId` | inteiro | não | obrigatório |  |
| `condutorId` | inteiro? | sim |  | Condutor identificado (opcional). Em N3, também é bloqueado. |
| `descricao` | texto | não | obrigatório; 3 a 2000 caracteres; TextoSimples |  |
| `local` | texto | sim | até 150 caracteres; TextoSimples |  |
| `responsavel` | texto | sim | até 150 caracteres; TextoSimples | Padrão: nome do usuário autenticado. |

### RegistroMarcacao

Linha de marcação já liberada, no formato consumido pelas fórmulas (equivale ao objeto { id, senha, convenio, operador, carga, ciclo, marcadoEm, liberadoEm, esperaHoras, empresaId, empresaNome } do frontend).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `movimentoId` | texto | não |  |  |
| `senha` | texto | não |  |  |
| `convenio` | texto | não |  |  |
| `operador` | texto | não |  |  |
| `carga` | texto | não |  |  |
| `ciclo` | inteiro | não |  |  |
| `marcadoEm` | data e hora | não |  |  |
| `liberadoEm` | data e hora | não |  |  |
| `esperaHoras` | número | não |  |  |
| `terminalId` | texto | não |  |  |
| `terminalNome` | texto | não |  |  |

### ResponderContestacaoRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `respostaOperador` | texto | sim | até 2000 caracteres; TextoSimples |  |

### ResultadoAnalise

Resultado completo da análise 360°.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `de` | data | não |  |  |
| `ate` | data | não |  |  |
| `dias` | inteiro | não |  |  |
| `anteriorDe` | data | não |  |  |
| `anteriorAte` | data | não |  |  |
| `atual` | ResumoPeriodo | não |  |  |
| `anterior` | ResumoPeriodo | sim |  |  |
| `agrupamento` | texto | não |  |  |
| `serie` | lista de PontoSerie | não |  |  |
| `porHora` | lista de PontoHora | não |  |  |
| `porDiaSemana` | lista de PontoDiaSemana | não |  |  |
| `dimensoes` | lista de AnaliseDimensao | não |  |  |
| `qualidade` | QualidadeDados | não |  |  |
| `limiteEsperaLongaHoras` | número | não |  |  |

### ResultadoAvaliacaoDto

Resultado de uma avaliação das regras. Situacao: concluida | falhou | em-andamento. Comparacao (da confiabilidade com a avaliação bem-sucedida anterior): melhorou | igual | piorou | primeira.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro? | sim |  |  |
| `situacao` | texto | não |  |  |
| `inicio` | data e hora | não |  |  |
| `fim` | data e hora? | sim |  |  |
| `origem` | OrigemAvaliacao | não |  |  |
| `autor` | texto | não |  |  |
| `erro` | texto | sim |  |  |
| `dadosAte` | data e hora? | sim |  |  |
| `ultimaImportacaoEm` | data e hora? | sim |  |  |
| `registros` | inteiro | não |  |  |
| `dadosNovos` | booleano | não |  |  |
| `criados` | inteiro | não |  |  |
| `alterados` | inteiro | não |  |  |
| `encerrados` | inteiro | não |  |  |
| `ativos` | inteiro | não |  |  |
| `confiabilidade` | texto | sim |  |  |
| `confiabilidadeAnterior` | texto | sim |  |  |
| `comparacao` | texto | não |  |  |
| `resumo` | texto | não |  |  |
| `motivos` | lista de MotivoConfiabilidade | não |  |  |

### ResultadoDecisaoDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `decididas` | inteiro | não |  |  |
| `aindaPendentes` | inteiro | não |  |  |

### ResultadoImportacaoDto

Registros inválidos não são gravados; aparecem em Rejeitados (primeiros 100 listados em Erros).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `terminalId` | texto | não |  |  |
| `arquivo` | texto | não |  |  |
| `lidos` | inteiro | não |  |  |
| `inseridos` | inteiro | não |  |  |
| `ignoradosJaExistentes` | inteiro | não |  |  |
| `rejeitados` | inteiro | não |  |  |
| `erros` | lista de texto | não |  |  |

### ResultadoIntegracaoDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `recebidos` | inteiro | não |  |  |
| `criados` | inteiro | não |  |  |
| `atualizados` | inteiro | não |  |  |
| `erros` | lista de ErroEventoDto | não |  |  |

### ResumoAgendamentosDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `data` | data? | sim |  |  |
| `total` | inteiro | não |  |  |
| `confirmados` | inteiro | não |  |  |
| `emOperacao` | inteiro | não |  |  |
| `atrasados` | inteiro | não |  |  |
| `porHora` | lista de AgendamentosPorHoraDto | não |  |  |

### ResumoAlertasDto

Contagem de abertos; última avaliação bem-sucedida e a última tentativa (que pode ter falhado).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `ativos` | inteiro | não |  |  |
| `porTipo` | mapa TipoAlerta → inteiro | não |  |  |
| `ultimaAvaliacao` | data e hora? | sim |  |  |
| `ultimaTentativa` | ResultadoAvaliacaoDto | sim |  |  |

### ResumoEstimativa

Resumo do horizonte (próximas N horas).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `demandaPrevista` | número | não |  | Soma das demandas previstas das janelas com dados. |
| `capacidadeAtendimento` | número | não |  | Soma das capacidades de atendimento das mesmas janelas. |
| `saldo` | número | não |  | Capacidade − demanda no horizonte. |
| `piorSituacao` | texto | não |  | Situação mais grave entre as janelas. |
| `horaMaisCritica` | inteiro? | sim |  | Janela com o pior saldo entre as que estão em atenção ou risco; nula se todas dentro. |
| `horaMaiorMovimento` | inteiro? | sim |  | Janela com a maior demanda prevista. |
| `tendencia` | texto | não |  | Como a pressão evolui no horizonte, em uma frase. |
| `janelasEmRisco` | inteiro | não |  | Quantidade de janelas em risco de fila. |
| `janelasEmAtencao` | inteiro | não |  | Quantidade de janelas em atenção. |

### ResumoIndicadoresDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `periodo` | PeriodoDto | sim |  |  |
| `comparacao` | PeriodoDto | sim |  |  |
| `atual` | EstatisticasEspera | não |  |  |
| `anterior` | EstatisticasEspera | sim |  |  |
| `limiteEsperaLongaHoras` | número | não |  |  |

### ResumoPeriodo

Números de um período (todos observados nos registros; nada estimado).

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `entradas` | inteiro | não |  | Marcações (chegadas registradas) com data no período. |
| `saidas` | inteiro | não |  | Liberações com data no período (inclui carretas marcadas antes dele). |
| `concluidas` | inteiro | não |  | Marcadas no período e já liberadas. |
| `pendentes` | inteiro | não |  | Marcadas no período e ainda sem liberação. |
| `entradasPorDia` | número | não |  | Entradas ÷ dias corridos do período (compara períodos de durações diferentes). |
| `esperaMedia` | número? | sim |  | Média de (liberação − marcação) das concluídas, em horas. |
| `esperaMediana` | número? | sim |  | Mediana das mesmas esperas. |
| `esperaP90` | número? | sim |  | 90º percentil das mesmas esperas. |
| `liberadasAte24hPct` | número? | sim |  | Concluídas com espera ≤ 24 h ÷ concluídas, em %. |
| `esperasLongas` | inteiro | não |  | Concluídas com espera acima do limite de espera longa. |
| `comProblemaDeQualidade` | inteiro | não |  | Entradas sem código de convênio ou com possível remarcação. |

### RiscoHora

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `hora` | inteiro | não |  |  |
| `lambda` | número | não |  |  |
| `mu` | número | não |  |  |
| `rho` | número | não |  |  |
| `indice` | inteiro | não |  |  |
| `nivel` | texto | não |  |  |
| `explicacao` | texto | não |  |  |

### RitmoOperacional

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `faixas` | lista de FaixaRitmo | não |  |  |
| `alertas` | lista de AlertaRitmo | não |  |  |

### SalvarCondutorRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `nome` | texto | não | obrigatório; 2 a 150 caracteres; TextoSimples |  |
| `transportadoraId` | inteiro | não | obrigatório |  |
| `placaVinculada` | texto | sim | até 10 caracteres; PlacaValida |  |

### SalvarTransportadoraRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `nome` | texto | não | obrigatório; 2 a 150 caracteres; TextoSimples |  |
| `cnpj` | texto | não | obrigatório; 14 a 18 caracteres; CnpjValido |  |

### ScoreOperador

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `operador` | texto | não |  |  |
| `total` | inteiro | não |  |  |
| `esperaMedia` | número | não |  |  |
| `desvio` | número | não |  |  |
| `score` | inteiro | não |  |  |

### SenhaProvisoriaDto

Senha provisória gerada pelo Gestor — mostrada uma única vez.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `senhaProvisoria` | texto | não |  |  |

### SerieMensal

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `mes` | texto | não |  |  |
| `valor` | número | não |  |  |

### TempoTipicoTerminalDto

Tempo típico de espera de um terminal (mediana), com a base usada.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `terminalId` | texto | não |  | Id do terminal. |
| `terminal` | texto | não |  | Nome. |
| `medianaHoras` | número? | sim |  | Mediana de (liberação − marcação); nula sem liberações na base. |
| `baseDe` | data? | sim |  | Primeiro dia da base. |
| `baseAte` | data? | sim |  | Último dia da base (último dia com dados do terminal). |
| `liberadas` | inteiro | não |  | Carretas liberadas na base. |

### TerminalDto

Capacidades nulas = ainda não configuradas pelo porto.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | texto | não |  |  |
| `nome` | texto | não |  |  |
| `capacidadeDiariaCarretas` | inteiro? | sim |  |  |
| `capacidadeHoraria` | inteiro? | sim |  |  |

### TextoAlertaRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `texto` | texto | sim | até 1000 caracteres; TextoSimples |  |

### TmaTerminal

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `terminal` | texto | não |  |  |
| `tmaGeral` | número | não |  |  |
| `tmaUltimoMes` | número | não |  |  |
| `variacao` | número? | sim |  |  |
| `total` | inteiro | não |  |  |

### TransportadoraDto

Status é sempre calculado: negativada se ≥1 carreta negativada.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro | não |  |  |
| `nome` | texto | não |  |  |
| `cnpj` | texto | não |  |  |
| `status` | StatusNegativacao | não |  |  |
| `carretasNegativadas` | inteiro | não |  |  |
| `carretasTotal` | inteiro | não |  |  |
| `reincidenciasN2Ultimos30Dias` | inteiro | não |  |  |
| `aptaParaOperar` | booleano | não |  |  |

### TrocarSenhaRequest

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `senhaAtual` | texto | não | obrigatório; até 128 caracteres |  |
| `novaSenha` | texto | não | obrigatório; até 128 caracteres |  |

### TurnoConcentracao

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `rotulo` | texto | não |  |  |
| `min` | inteiro | não |  |  |
| `max` | inteiro | não |  |  |
| `total` | inteiro | não |  |  |
| `retidos` | inteiro | não |  |  |
| `pctRetencao` | número | não |  |  |

### UsuarioDto

TransportadoraId só vem preenchido para usuários do Portal (papel Transportadora). TerminalId = vínculo (escopo de dados, definido pelo Gestor). Preferencias = escolhas pessoais, que não dão acesso a nada.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro | não |  |  |
| `nome` | texto | não |  |  |
| `email` | texto | não |  |  |
| `papel` | PapelUsuario | não |  |  |
| `ativo` | booleano | não |  |  |
| `mfaAtivo` | booleano | não |  |  |
| `deveTrocarSenha` | booleano | não |  |  |
| `bloqueado` | booleano | não |  |  |
| `ultimoLoginEm` | data e hora? | sim |  |  |
| `transportadoraId` | inteiro? | sim |  |  |
| `terminalId` | texto | sim |  |  |
| `preferencias` | PreferenciasDto | sim |  |  |

### VeiculoDto

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `id` | inteiro | não |  |  |
| `placa` | texto | não |  |  |
| `transportadoraId` | inteiro | não |  |  |
| `transportadora` | texto | sim |  |  |
| `modelo` | texto | sim |  |  |
| `statusPortaria` | StatusPortaria | não |  |  |
| `statusPortariaDesde` | data e hora | não |  |  |
| `statusNegativacao` | StatusNegativacao | não |  |  |
| `terminalId` | texto | não |  |  |
| `terminal` | texto | sim |  |  |

### VisaoTerminal

Card da "Visão por Terminal". Semáforo: normal=verde, atencao=amarelo, critico=vermelho.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `terminalId` | texto | não |  |  |
| `terminal` | texto | não |  |  |
| `total` | inteiro | não |  |  |
| `mediaDiaria` | número | não |  |  |
| `capacidadeDiaria` | inteiro | não |  |  |
| `esperaMedia` | número | não |  |  |
| `ocupacaoPct` | número | não |  |  |
| `status` | texto | não |  |  |
| `semaforo` | texto | não |  |  |

### VolumeMensalDto

Volume e espera mediana de um terminal num mês. Parcial = o terminal não tem dados no mês inteiro.

| Campo | Tipo | Nulo | Regras | Descrição |
|---|---|---|---|---|
| `mes` | texto | não |  |  |
| `terminalId` | texto | não |  |  |
| `terminal` | texto | não |  |  |
| `movimentacoes` | inteiro | não |  |  |
| `esperaMediana` | número? | sim |  |  |
| `parcial` | booleano | não |  |  |
