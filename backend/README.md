# BarcaLog — Backend (.NET 8 + SQL Server)

API REST do BarcaLog (Porto de Barcarena/PA). Substitui o que hoje roda no
navegador — datasets JSON estáticos, `localStorage` e cálculos em
`metricsEngine.js` — por um banco SQL Server e uma API que o frontend e
outros sistemas consomem.

```
backend/
├── BarcaLog.sln
├── src/
│   ├── BarcaLog.Domain          entidades, enums, regras puras (N1/N2/N3, janelas D0–EC). Zero dependências.
│   ├── BarcaLog.Application     DTOs, interfaces, serviços, motor de métricas (porte do metricsEngine.js/relatorio.js)
│   ├── BarcaLog.Infrastructure  EF Core + SQL Server, migrations, repositórios, interceptor de auditoria, seed
│   └── BarcaLog.Api             controllers, JWT, API Key, Swagger, CORS, Program.cs, comandos de linha
├── tests/
│   ├── BarcaLog.UnitTests         regras de negócio + motor de métricas
│   └── BarcaLog.IntegrationTests  API inteira contra SQL Server real (WebApplicationFactory)
└── tools/verificar-fidelidade.mjs compara a API com o JS original sobre os 103k registros
```

## Rodando localmente

Pré-requisitos: **.NET 8 SDK** e um **SQL Server** (LocalDB no Windows, ou
Docker em qualquer SO).

```bash
cd backend
dotnet tool restore                 # instala o dotnet-ef (versão fixada em .config/dotnet-tools.json)

# 1) Banco
dotnet ef database update --project src/BarcaLog.Infrastructure --startup-project src/BarcaLog.Api

# 2) API
cd src/BarcaLog.Api
dotnet run                          # http://localhost:5080  →  Swagger em http://localhost:5080/swagger
```

No ambiente **Development** a API, ao subir:

1. aplica migrations pendentes;
2. cria 3 usuários de teste (senha `BarcaLog@2026`): `gestor@barcalog.local`,
   `operador@barcalog.local`, `auditor@barcalog.local`;
3. cria o **mesmo cadastro de exemplo** do frontend (5 transportadoras, 7
   carretas, 3 condutores, ocorrências oc1/oc2, a contestação pendente e os 7
   agendamentos do dia), pra manter a continuidade visual;
4. se a tabela `Marcacoes` estiver vazia, importa os **103.325 registros
   reais** de `src/data/datasets/*.json` (primeira subida completa, com build + migrations + importação, levou ~45 s aqui).

Tudo isso é controlado pela seção `Seed` do `appsettings.{Ambiente}.json` —
em Production vem tudo desligado.

### Connection string

A API lê `ConnectionStrings:BarcaLog`. Nunca está fixa no código:

| Ambiente | Onde |
|---|---|
| Development (Windows) | `appsettings.Development.json` já aponta pro LocalDB: `Server=(localdb)\mssqllocaldb;Database=BarcaLog;Trusted_Connection=True;TrustServerCertificate=True` |
| Development (Linux/macOS/Docker) | sobrescreva sem editar arquivo: `dotnet user-secrets set "ConnectionStrings:BarcaLog" "Server=localhost,1433;Database=BarcaLog;User Id=sa;Password=...;TrustServerCertificate=True" --project src/BarcaLog.Api` ou a variável `ConnectionStrings__BarcaLog` |
| Production | variável de ambiente `ConnectionStrings__BarcaLog` (o `appsettings.Production.json` deixa vazio de propósito; a API não sobe sem ela) |

SQL Server via Docker (Linux/macOS): `export MSSQL_SA_PASSWORD='...'` e
`docker compose up -d` (arquivo `docker-compose.yml` nesta pasta).

O `dotnet ef database update` usa a mesma configuração (ambiente
Development por padrão), então o que valer pra API vale pra migration.

### Configuração de produção

Variáveis obrigatórias:

| Variável | Para quê |
|---|---|
| `ConnectionStrings__BarcaLog` | banco |
| `Jwt__Chave` | chave HMAC do JWT, ≥ 32 caracteres aleatórios (`dotnet run -- gerar-chave`) |
| `Seguranca__ChaveCriptografia` | AES-256 (Base64 de 32 bytes, `dotnet run -- gerar-chave`) que cifra os segredos de MFA. **Guarde com backup**: perder a chave invalida o MFA de todos |
| `Integracao__ApiKeys__0__Sistema` / `Integracao__ApiKeys__0__ChaveSha256` | uma entrada por sistema (`__1__`…). Só o **hash** vai para a config: `dotnet run -- gerar-api-key "Nome do Sistema"` imprime a chave (entregue ao sistema) e o hash |
| `Proxy__Habilitado=true` + `Proxy__RedesConfiaveis__0=10.0.0.0/8` | se a API estiver atrás de proxy/load balancer (IP real para rate limit e HTTPS) |
| `Cors__Origens__0` | só se o domínio da Vercel **não** for `https://barcalog.vercel.app` (valor presumido em `appsettings.Production.json` — confira) |

Em Production a API **se recusa a subir** se faltar alguma dessas, se alguma for
a de desenvolvimento (`DEV-ONLY…`), se houver seed de usuários/dados de exemplo
ou se o CORS não for HTTPS. Segurança completa: [`SECURITY.md`](../SECURITY.md);
arquitetura: [`ARCHITECTURE.md`](../ARCHITECTURE.md); container: `Dockerfile`;
banco com menor privilégio e backup: `database/`.

Tarefas administrativas (sem subir o HTTP):

```bash
dotnet run -- migrar                                   # aplica migrations
dotnet run -- importar-marcacoes [diretorio]           # importa os JSON (idempotente)
dotnet run -- seed                                     # cadastro de exemplo (se vazio)
BARCALOG_SENHA='...' dotnet run -- criar-usuario gestor@porto.com.br "Maria Silva" Gestor
dotnet run -- gerar-chave                              # chave aleatória (JWT / AES)
dotnet run -- gerar-api-key "Sistema TGPM"             # chave de integração + hash para a config
```

O primeiro Gestor (criado pelo `criar-usuario`) é obrigado a cadastrar o MFA
no primeiro login; depois ele cria os demais usuários pela API.

## Autenticação e papéis

* **Usuários**: `POST /api/v1/auth/login` (e-mail, senha e, se ativo, `codigoMfa`) → JWT
  de 8h. Mandar `Authorization: Bearer <token>`. No Swagger, botão **Authorize**.
  - 5 falhas seguidas bloqueiam a conta por 15 min; 10 tentativas/min por IP.
  - Resposta `restricao: "trocar-senha"` (senha provisória) ou `"configurar-mfa"`
    (Gestor sem MFA): o token só serve para `auth/me`, `auth/logout`,
    `auth/trocar-senha` e `auth/mfa/*`.
  - `auth/logout`, troca de senha/papel e desativação **revogam** os tokens na hora.
  - MFA: `auth/mfa/configurar` (devolve segredo + URI `otpauth://` para QR code),
    `auth/mfa/ativar`, `auth/mfa/desativar`.
* **Sistemas**: `POST /api/v1/integracao/eventos` usa cabeçalho `X-Api-Key`
  (não aceita JWT; e a API Key não abre nenhum outro endpoint).
* **Idempotência**: operações que criam/alteram estado aceitam
  `Idempotency-Key: <uuid>` — o frontend deve gerar um por clique em "salvar"
  e reenviar o mesmo em caso de retry.

| Papel | Pode |
|---|---|
| Auditor | ler tudo (inclusive auditoria) |
| Operador | ler + criar/alterar (ocorrências, contestações, veículos, agendamentos…) |
| Gestor | tudo do Operador + remover cadastros, importar datasets, administrar usuários, dados pessoais (LGPD) |

O login "qualquer e-mail/senha" do frontend deixa de existir.

## Endpoints

Lista completa e documentada no Swagger (`/swagger`). Resumo:

| Recurso | Rotas |
|---|---|
| Auth | `POST /api/v1/auth/login`, `GET /api/v1/auth/me`, `POST /api/v1/auth/logout`, `POST /api/v1/auth/trocar-senha`, `POST /api/v1/auth/mfa/configurar`, `/mfa/ativar`, `/mfa/desativar` |
| Usuários (Gestor) | `GET/POST /api/v1/usuarios`, `GET /api/v1/usuarios/{id}`, `PUT /{id}/papel`, `POST /{id}/desativar`, `/reativar`, `/redefinir-senha`, `/redefinir-mfa` |
| Terminais | `GET /api/v1/terminais` |
| Marcações | `GET /api/v1/marcacoes` (paginado; `terminalId`, `de`, `ate`, `convenio`, `operador`, `carga`, `senha`), `GET /api/v1/marcacoes/{movimentoId}` |
| Agregados pedidos | `GET /api/v1/marcacoes/kpis`, `/visao-terminal`, `/por-mes`, `/ranking-esperas`, `/indice-risco-hora` |
| Demais métricas do motor | `/janelas-permanencia`, `/estouro-critico`, `/ritmo-operacional`, `/concentracao-turno`, `/horarios-criticos`, `/tma-terminal`, `/indicadores-performance`, `/alertas-operacionais`, `/picos-entrada-saida`, `/analise-preditiva`, `/projecao-volume`, `/alertas-saturacao`, `/capacidade-diaria`, `/top-dias`, `/dias-acima-capacidade`, `/matriz-volume-mensal`, `/detalhamento-diario`, `/por-operador`, `/por-carga`, `/por-terminal`, `/por-hora`, `/por-dia-semana`, `/por-ciclo`, `/tendencia-sla`, `/score-operadores`, `/atrasos-recorrentes`, `/recomendacoes`, `/business-case` — todos aceitam `terminalId`, `de`, `ate`, `convenio` (e `mes=yyyy-MM`) |
| Importação | `POST /api/v1/marcacoes/importar` (diretório configurado), `POST /api/v1/marcacoes/importar/{terminalId}` (upload de um JSON) — Gestor |
| Transportadoras | `GET/POST /api/v1/transportadoras`, `GET/PUT/DELETE /api/v1/transportadoras/{id}` |
| Veículos | `GET/POST /api/v1/veiculos`, `GET/PUT/DELETE /api/v1/veiculos/{id}`, `GET /api/v1/veiculos/placa/{placa}`, `PATCH /api/v1/veiculos/{id}/status-portaria`, `POST /api/v1/veiculos/{id}/negativar`, `POST /api/v1/veiculos/{id}/desnegativar` |
| Condutores | `GET/POST /api/v1/condutores`, `GET/PUT/DELETE /api/v1/condutores/{id}`, LGPD: `GET /api/v1/condutores/{id}/dados-pessoais`, `POST /api/v1/condutores/{id}/anonimizar` (Gestor) |
| Ocorrências | `GET/POST /api/v1/ocorrencias`, `GET /api/v1/ocorrencias/{id}` |
| Contestações | `GET/POST /api/v1/contestacoes`, `GET /api/v1/contestacoes/{id}`, `POST /api/v1/contestacoes/{id}/aprovar`, `POST /api/v1/contestacoes/{id}/rejeitar` |
| Auditoria | `GET /api/v1/auditoria?autor=&acao=&texto=&de=&ate=&pagina=&tamanhoPagina=` |
| Agendamentos | `GET/POST /api/v1/agendamentos`, `GET /api/v1/agendamentos/resumo`, `GET/PUT /api/v1/agendamentos/{id}`, `PATCH /api/v1/agendamentos/{id}/status` |
| Portaria | `GET /api/v1/portaria/fila-virtual`, `GET /api/v1/portaria/operacao-agora` |
| Integração | `POST /api/v1/integracao/eventos` (X-Api-Key; lote de 1–1000 eventos `Marcacao`/`Liberacao`) |
| Saúde | `GET /health/live`, `GET /health/ready` (checa o banco) |

Listagens são **paginadas** (`pagina`, `tamanhoPagina` até 500) e devolvem
`{ itens, total, numeroPagina, tamanhoPagina, totalPaginas }` — exceto
terminais e transportadoras (cadastros pequenos).

Erros seguem ProblemDetails (RFC 7807) com `traceId` (igual ao cabeçalho
`X-Request-Id` e ao log): 400 validação, 401/403 acesso, 404 não encontrado,
409 duplicado/conflito de edição simultânea, 413 corpo grande, 422 regra de
negócio, 429 limite de requisições (com `Retry-After`), 504 tempo esgotado.

## Regras de negócio portadas

| Regra | Onde no JS | Onde no backend |
|---|---|---|
| N3 bloqueia automaticamente o veículo (e o condutor identificado) no registro; N1/N2 não bloqueiam | `registrarOcorrencia` | `RegrasNegativacao.AplicarOcorrencia`, chamada por `OcorrenciaServico` na mesma transação |
| Status da transportadora é sempre calculado (≥1 carreta negativada) — não existe coluna | `listarTransportadoras` | `RegrasNegativacao.CalcularStatusTransportadora` |
| Aprovar contestação regulariza só o veículo (e condutor) daquela ocorrência; rejeitar reativa a ocorrência | `responderContestacao` | `RegrasNegativacao.ResponderContestacao` |
| Negativação/desnegativação manual | `negativarVeiculo` / `desnegativarVeiculo` | `VeiculoServico` |
| Reincidência N2 em 30 dias | `reincidenciasN2` | `TransportadoraDto.ReincidenciasN2Ultimos30Dias` |
| Janelas D0/D1/D2/D3/Alerta/Estouro Crítico | `JANELAS`, `classificarJanela` | `JanelaPermanencia` |
| Capacidade nominal 1000/dia, ocupação e semáforo | `visaoPorTerminal` | `MotorMetricas.VisaoPorTerminal` usando `Terminal.CapacidadeDiariaCarretas` |
| Índice de risco por hora = ρ = λ/μ (≥1.4 crítico, ≥1.2 alto, ≥1.0 atenção) | `indiceRiscoGargalo` | `MotorMetricas.IndiceRiscoGargalo` |
| Fila virtual com estimativa pela espera média histórica real do terminal | `Portaria.jsx` | `PortariaServico.FilaVirtualAsync` |
| Log de auditoria | `registrarLog` espalhado | **automático**: `AuditoriaInterceptor` (EF Core) grava 1 linha por `SaveChanges`, na mesma transação, com autor do JWT/API Key e o diff das entidades. Os serviços só dão um rótulo opcional (`"Ocorrência N3 registrada (bloqueio automático)"` etc., mesmos textos do JS) |

**Fidelidade das métricas.** Todas as funções de `metricsEngine.js` e
`relatorio.js` foram portadas (`BarcaLog.Application/Metricas`). Com a API
rodando e os dados importados, `node tools/verificar-fidelidade.mjs` roda o
**JS original** sobre os mesmos 103k registros e compara com a API campo a
campo: hoje os 25 endpoints comparados batem (até detalhes como o
arredondamento de `toFixed` nos textos).

### Decisões e diferenças em relação ao frontend

* **`esperaHoras`** é coluna computada no SQL Server a partir de
  `DataLiberacao − DataMarcacao` (2 casas). Em 1.491 dos 103.325 registros
  (1,4%) isso difere em 0,01h do campo `esperaHoras` gravado nos JSON, porque
  a planilha de origem tinha frações de segundo que o JSON não guardou. Efeito
  nas médias: ~0,0001h.
* **Ordem de desempate.** No JS, empates (ex.: dois horários críticos com o
  mesmo total) saem na ordem em que os arquivos foram lidos; aqui, em ordem
  de data. Os valores são os mesmos.
* **Condutor na ocorrência**: o JS bloqueava o condutor pelo `cpfMotorista`.
  Como o CPF foi removido, a ocorrência tem `CondutorId` opcional — quando
  informado, o N3 bloqueia também esse condutor (e a aprovação o regulariza).
* **Carreta negativada não pode ser agendada** (`422`). É a aplicação direta
  do "bloqueio para novos carregamentos" do N3, que a tela de Agendamentos
  simulada não checava.
* **Placa de outra transportadora**: registrar ocorrência ou agendamento
  informando transportadora diferente da cadastrada na placa dá `422`.
* **Horários**: `DataMarcacao`/`DataLiberacao` ficam no horário local do porto
  (igual ao dado de origem). Timestamps operacionais (`CriadoEm`,
  `StatusPortariaDesde`, `Quando`…) ficam em UTC e saem com `Z`. "Hora atual"
  da previsão de gargalo usa `Operacao:FusoHorario` (`America/Belem`).
* **Não portado** (fica pra próximas tarefas): anexos/evidências do GED
  (`evidencias`, `documentos`), login do Portal da Transportadora (senha demo
  `1234` — precisa de um papel/usuário por transportadora), e o
  `normalizarDadosDemo` (artefato de simulação do `localStorage`).
* `CodConvenio` não existe nos JSON atuais: vem `null` na importação e pode
  ser preenchido pela integração.

## Testes

```bash
cd backend
dotnet test tests/BarcaLog.UnitTests          # 106 testes: regras N1/N2/N3, janelas D0–EC, ρ, métricas, TOTP (vetores RFC 6238), CNPJ/placa, política de senha

# integração: precisa de um SQL Server (cria e apaga um banco BarcaLog_Testes_<guid>)
export BARCALOG_TESTES_SQL='Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True'
dotnet test tests/BarcaLog.IntegrationTests
```

No Windows, sem a variável, os testes de integração usam o LocalDB. Sem SQL
Server acessível eles aparecem como *Skipped* com o motivo. Entre eles:
**registrar uma ocorrência N3 pela API e conferir no banco que o veículo ficou
negativado**, N1/N2 não bloqueiam, aprovação de contestação regulariza só a
carreta certa, autorização por papel, e integração por API Key com a espera
calculada pelo SQL Server. E testes de ataque (48 no total): força bruta e
bloqueio, e-mail inexistente × senha errada, token adulterado e `alg: none`,
revogação no logout/desativação/troca de papel, MFA com replay, rate limit 429,
Idempotency-Key, aprovações e contestações **simultâneas**, SQL injection e
enum inválido, corpo de 1 MB+, upload malicioso, headers de segurança, CORS e
anonimização LGPD. O CI (`.github/workflows/backend.yml`) roda tudo contra um
SQL Server real e falha se algum teste for pulado.

## Frontend ligado à API

O frontend (raiz do repositório) não baixa mais os datasets nem calcula nada no
navegador: tudo vem desta API.

**Rodando local**

```bash
# 1. API (este README, seção "Como rodar") em http://localhost:5080
# 2. Frontend
cp .env.example .env.local     # VITE_API_URL=http://localhost:5080
npm ci
npm run dev                    # http://localhost:5173 (já liberado no Cors de Development)
```

Usuários de desenvolvimento (senha `BarcaLog@2026`, **só no seed de Development**):
`gestor@`, `operador@`, `auditor@barcalog.local` (painel interno) e
`norte@`, `agro@transportadora.local` (Portal do Transportador, em `/portal`).

**Como está organizado**

| Arquivo | Papel |
|---|---|
| `src/api/cliente.js` | Único ponto de acesso à API: token no `Authorization`, `Idempotency-Key`, erros `ProblemDetails` → mensagem amigável, 401 encerra a sessão |
| `src/api/rotulos.js` | Enums da API ↔ rótulos da tela e adaptadores de DTO |
| `src/auth/autenticacao.js` + `components/Acesso.jsx` | Login (com etapa MFA), logout (revoga no servidor), troca de senha provisória e cadastro obrigatório de MFA |
| `src/data/negativacaoStore.js` | Mesmo contrato de antes (`listarVeiculos()`…), agora um cache da API; gravações chamam a API e recarregam |
| `src/hooks/useMetricas.js` | Indicadores da Visão Geral / Relatório PDF (endpoints `/api/v1/marcacoes/*` em paralelo) |
| `src/hooks/useApi.js`, `useAcao.js` | Leitura com cancelamento/carregando/erro; gravação sem clique duplo, com toast |
| `src/data/metricsEngine.js`, `relatorio.js` | **Não usados pelas telas** — especificação de referência para `tools/verificar-fidelidade.mjs` |

**Deploy (Vercel)** — a API ainda não está publicada; quando estiver:

1. Publique a API com HTTPS (ex.: `https://api.barcalog.com.br`) e anote o
   domínio exato.
2. Em `vercel.json`, acrescente esse domínio ao `connect-src` da CSP:
   `connect-src 'self' https://api.barcalog.com.br;` (só o domínio, sem caminho).
3. Na Vercel (Settings → Environment Variables), defina
   `VITE_API_URL=https://api.barcalog.com.br` (sem barra no fim) para
   Production e Preview, e faça um novo deploy (a variável entra no build).
4. Na API, `Cors__Origens__0=https://barcalog.vercel.app` (domínio exato do
   site; previews da Vercel usam outros domínios e só funcionam se forem
   adicionados também).

Por que não um rewrite `/api/*` → API na Vercel (mesma origem): todo acesso
chegaria à API com IPs da Vercel, e o limite de tentativas de login por IP
passaria a valer para todos os usuários juntos (a Vercel não publica faixas
fixas de IP para configurar `Proxy:KnownNetworks`).

Enquanto a API não existir, o site mostra "API indisponível" no login (o
`vercel.json` não devolve `index.html` para `/api/*`, e o cliente trata
resposta HTML como API ausente).

- Vite 7 exige Node ≥ 20.19; use `npm ci`. Nunca `npm audit fix --force` sem avaliar (salta versões maiores).
