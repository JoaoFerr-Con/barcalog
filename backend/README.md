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
| `Jwt__Chave` | chave HMAC do JWT, ≥ 32 caracteres (a API não sobe sem) |
| `Integracao__ApiKeys__0__Sistema` / `Integracao__ApiKeys__0__Chave` | uma entrada por sistema integrado (`__1__`, `__2__`…) |
| `Cors__Origens__0` | só se o domínio da Vercel **não** for `https://barcalog.vercel.app` (valor presumido em `appsettings.Production.json` — confira) |

Tarefas administrativas (sem subir o HTTP):

```bash
dotnet run -- migrar                                   # aplica migrations
dotnet run -- importar-marcacoes [diretorio]           # importa os JSON (idempotente)
dotnet run -- seed                                     # cadastro de exemplo (se vazio)
BARCALOG_SENHA='...' dotnet run -- criar-usuario gestor@porto.com.br "Maria Silva" Gestor
```

## Autenticação e papéis

* **Usuários**: `POST /api/auth/login` (e-mail + senha, hash PBKDF2) → JWT.
  Mandar `Authorization: Bearer <token>`. No Swagger, botão **Authorize**.
* **Sistemas**: `POST /api/integracao/eventos` usa cabeçalho `X-Api-Key`
  (não aceita JWT; e a API Key não abre nenhum outro endpoint).

| Papel | Pode |
|---|---|
| Auditor | ler tudo (inclusive auditoria) |
| Operador | ler + criar/alterar (ocorrências, contestações, veículos, agendamentos…) |
| Gestor | tudo do Operador + remover cadastros, importar datasets, criar usuários |

O login "qualquer e-mail/senha" do frontend deixa de existir.

## Endpoints

Lista completa e documentada no Swagger (`/swagger`). Resumo:

| Recurso | Rotas |
|---|---|
| Auth | `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/usuarios` (Gestor) |
| Terminais | `GET /api/terminais` |
| Marcações | `GET /api/marcacoes` (paginado; `terminalId`, `de`, `ate`, `convenio`, `operador`, `carga`, `senha`), `GET /api/marcacoes/{movimentoId}` |
| Agregados pedidos | `GET /api/marcacoes/kpis`, `/visao-terminal`, `/por-mes`, `/ranking-esperas`, `/indice-risco-hora` |
| Demais métricas do motor | `/janelas-permanencia`, `/estouro-critico`, `/ritmo-operacional`, `/concentracao-turno`, `/horarios-criticos`, `/tma-terminal`, `/indicadores-performance`, `/alertas-operacionais`, `/picos-entrada-saida`, `/analise-preditiva`, `/projecao-volume`, `/alertas-saturacao`, `/capacidade-diaria`, `/top-dias`, `/dias-acima-capacidade`, `/matriz-volume-mensal`, `/detalhamento-diario`, `/por-operador`, `/por-carga`, `/por-terminal`, `/por-hora`, `/por-dia-semana`, `/por-ciclo`, `/tendencia-sla`, `/score-operadores`, `/atrasos-recorrentes`, `/recomendacoes`, `/business-case` — todos aceitam `terminalId`, `de`, `ate`, `convenio` (e `mes=yyyy-MM`) |
| Importação | `POST /api/marcacoes/importar` (diretório configurado), `POST /api/marcacoes/importar/{terminalId}` (upload de um JSON) — Gestor |
| Transportadoras | `GET/POST /api/transportadoras`, `GET/PUT/DELETE /api/transportadoras/{id}` |
| Veículos | `GET/POST /api/veiculos`, `GET/PUT/DELETE /api/veiculos/{id}`, `GET /api/veiculos/placa/{placa}`, `PATCH /api/veiculos/{id}/status-portaria`, `POST /api/veiculos/{id}/negativar`, `POST /api/veiculos/{id}/desnegativar` |
| Condutores | `GET/POST /api/condutores`, `GET/PUT/DELETE /api/condutores/{id}` |
| Ocorrências | `GET/POST /api/ocorrencias`, `GET /api/ocorrencias/{id}` |
| Contestações | `GET/POST /api/contestacoes`, `GET /api/contestacoes/{id}`, `POST /api/contestacoes/{id}/aprovar`, `POST /api/contestacoes/{id}/rejeitar` |
| Auditoria | `GET /api/auditoria?autor=&acao=&texto=&de=&ate=&pagina=&tamanhoPagina=` |
| Agendamentos | `GET/POST /api/agendamentos`, `GET /api/agendamentos/resumo`, `GET/PUT /api/agendamentos/{id}`, `PATCH /api/agendamentos/{id}/status` |
| Portaria | `GET /api/portaria/fila-virtual`, `GET /api/portaria/operacao-agora` |
| Integração | `POST /api/integracao/eventos` (X-Api-Key; lote de 1–1000 eventos `Marcacao`/`Liberacao`) |
| Saúde | `GET /health` |

Erros seguem ProblemDetails (RFC 7807): 400 validação, 401/403 acesso, 404
não encontrado, 409 duplicado/vínculo no banco, 422 regra de negócio.

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
dotnet test tests/BarcaLog.UnitTests          # 58 testes: N1/N2/N3, status calculado, contestação, janelas D0–EC, ρ, métricas

# integração: precisa de um SQL Server (cria e apaga um banco BarcaLog_Testes_<guid>)
export BARCALOG_TESTES_SQL='Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True'
dotnet test tests/BarcaLog.IntegrationTests
```

No Windows, sem a variável, os testes de integração usam o LocalDB. Sem SQL
Server acessível eles aparecem como *Skipped* com o motivo. Entre eles:
**registrar uma ocorrência N3 pela API e conferir no banco que o veículo ficou
negativado**, N1/N2 não bloqueiam, aprovação de contestação regulariza só a
carreta certa, autorização por papel, e integração por API Key com a espera
calculada pelo SQL Server.

## O que o frontend precisa mudar (próxima tarefa)

1. **Base da API**: `VITE_API_URL` (ex.: `http://localhost:5080` em dev) e um
   `api.js` com `fetch` que injeta `Authorization: Bearer` e trata 401
   (voltar pro login).
2. **Login** (`App.jsx`, `ProvedorAutenticacao.entrar`): trocar o login
   "qualquer e-mail" por `POST /api/auth/login`; guardar `token` e `usuario`
   (`nome`, `papel`). `sessao.js` deixa de ser necessário — o autor da
   auditoria vem do token.
3. **Dados reais** (`registry.js`, `useRegistrosReais`, `relatorio.js`,
   `metricsEngine.js`): parar de baixar os 22 MB de JSON e de calcular no
   navegador. Cada card/gráfico chama o endpoint equivalente com
   `?terminalId=` do `SeletorEmpresa`:

   | Função JS | Endpoint |
   |---|---|
   | `obterKpisGerais` | `/api/marcacoes/kpis` |
   | `visaoPorTerminal` | `/api/marcacoes/visao-terminal` |
   | `agruparPorMes` / `agruparPorMesDetalhado` | `/api/marcacoes/por-mes` |
   | `rankingMaioresEsperas` | `/api/marcacoes/ranking-esperas?limite=10` |
   | `indiceRiscoGargalo` | `/api/marcacoes/indice-risco-hora` |
   | `distribuicaoJanelas` | `/api/marcacoes/janelas-permanencia` |
   | `picosEntradaSaida(registrosDoMes)` | `/api/marcacoes/picos-entrada-saida?mes=2026-05` |
   | `analisePreditiva` | `/api/marcacoes/analise-preditiva` |
   | `alertasOperacionais` | `/api/marcacoes/alertas-operacionais` |
   | `previsaoGargaloPortaria` + fila | `/api/portaria/fila-virtual` |
   | demais | rota com o mesmo nome em kebab-case (ver tabela acima) |

   Os JSON de `src/data/datasets/` podem sair do bundle depois disso (ficam só
   como fonte da importação, ou são movidos pra fora do `src/`).
4. **`negativacaoStore.js`**: cada função vira um `fetch` — `listarTransportadoras`
   → `GET /api/transportadoras`, `registrarOcorrencia` → `POST /api/ocorrencias`,
   `abrirContestacao` → `POST /api/contestacoes`, `responderContestacao` →
   `POST /api/contestacoes/{id}/aprovar|rejeitar`, `negativarVeiculo` →
   `POST /api/veiculos/{id}/negativar`, `atualizarStatusPortaria` →
   `PATCH /api/veiculos/{id}/status-portaria`, `listarFilaAtual` →
   `GET /api/portaria/fila-virtual`, etc. O evento `barcalog:negativacao:mudou`
   vira "refazer o GET depois da mutação" (ou SWR/React Query).
5. **Referências por id**: o frontend identifica transportadora pelo **nome**
   e terminal pelo slug; a API usa `transportadoraId` numérico (o terminal
   continua `"unitapajos" | "tgpm" | "hidrovias"`).
6. **Enums** vêm sem espaço/acento: `NoPatio`, `Aguardando`, `NoPorto`,
   `DescargaFinalizada`; `Regular`/`Negativada`; `Ativa`/`Contestada`/`Resolvida`;
   `Pendente`/`Aprovada`/`Rejeitada`; agendamento `ACaminho`,
   `AguardandoEntrada`, `EmOperacao`… — um mapa de rótulos no frontend resolve.
7. **Agendamentos.jsx**: trocar o `useState(SEED_AGENDAMENTOS)` por
   `GET /api/agendamentos?data=`, `POST`, `PATCH /{id}/status` e
   `GET /api/agendamentos/resumo`. O QR passa a codificar `codigo` (`AGD-000123`).
8. **Auditoria.jsx**: `GET /api/auditoria?texto=` (paginado, no servidor).
9. Remover os avisos `AvisoDadosSimulados` das telas que passarem a usar a API.
10. Configurar `Cors:Origens` com o domínio real da Vercel.
