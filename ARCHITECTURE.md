# Arquitetura — BarcaLog

Sistema de gestão logística do Porto de Barcarena (PA): marcação/liberação de
carretas, negativação (N1/N2/N3), portaria, agendamentos, auditoria e
indicadores operacionais.

```
┌──────────────────────┐   HTTPS + JWT    ┌───────────────────────────────┐    ┌──────────────┐
│ Frontend React/Vite  │ ───────────────▶ │ API ASP.NET Core (.NET 8)     │───▶│ SQL Server   │
│ (Vercel, estático)   │                  │  /api/v1/*   /health/*        │    │ (EF Core)    │
└──────────────────────┘                  │                               │    └──────────────┘
┌──────────────────────┐  HTTPS + X-Api-Key│                               │
│ Sistemas de terminal │ ───────────────▶ │ /api/v1/integracao/eventos    │
└──────────────────────┘                  └───────────────────────────────┘
```

> Estado atual: o **backend está pronto e testado**; o frontend ainda roda
> 100% no navegador (JSON estático + `localStorage`). A migração do frontend
> para a API é a próxima etapa — roteiro em `backend/README.md`.

## Backend em camadas

```
BarcaLog.Api  ──▶  BarcaLog.Infrastructure  ──▶  BarcaLog.Application  ──▶  BarcaLog.Domain
(HTTP)             (EF Core, SQL, cripto)        (casos de uso, DTOs,        (entidades e regras
                                                  métricas, interfaces)       puras, sem dependência)
```

| Camada | Responsabilidade | Não pode |
|---|---|---|
| **Domain** | Entidades, enums, regras puras: N3 bloqueia, status da transportadora é calculado, janelas D0–EC, placa/CNPJ, política de senha | Depender de qualquer outra camada ou pacote |
| **Application** | Serviços (casos de uso), DTOs e validação, interfaces de repositório, motor de métricas (porte de `metricsEngine.js`/`relatorio.js`), TOTP | Conhecer EF Core, HTTP ou SQL |
| **Infrastructure** | `DbContext`, configurações, migrations, repositórios, interceptor de auditoria, idempotência, hash de senha, AES-GCM, seed/importação | Conter regra de negócio |
| **Api** | Controllers, autenticação/autorização, rate limit, headers, logs de requisição, erros, Swagger, CLI | Acessar o banco direto (exceto infraestrutura transversal como idempotência) |

Por que assim: a regra de negócio (o que o porto validou no frontend) fica
isolada e testável sem banco; trocar SQL Server, framework web ou mecanismo de
autenticação não toca nas regras.

## Decisões principais

| Decisão | Motivo | Trade-off |
|---|---|---|
| Regras portadas **fielmente** do JS | O porto já validou os números; `tools/verificar-fidelidade.mjs` compara a API com o JS original sobre os 103k registros | Algumas esquisitices do JS foram mantidas (ex.: espera negativa cairia em "Estouro"); documentadas |
| Métricas calculadas em memória sobre uma projeção cacheada (~103k linhas, ~20 MB) | Porte 1:1 das fórmulas, sem reescrever tudo em SQL; resposta < 200 ms com cache | Não escala para dezenas de milhões de linhas — aí mover agregações para SQL/views materializadas. Cache por instância, recarga no máx. a cada 60 s após novos dados |
| `EsperaHoras` = coluna computada persistida no SQL Server | Única fonte da verdade, indexável, mesma regra dos JSON | Difere em 0,01 h em 1,4% dos registros dos JSON (a planilha original tinha frações de segundo) |
| Auditoria por `SaveChangesInterceptor` | Automática: nenhum endpoint precisa lembrar de logar; mesma transação | Operações em lote via SQL (`ExecuteUpdate`) gravam o próprio evento — hoje só a contabilidade de login |
| Enum gravado como texto + `CHECK` | Legível em SQL, estável se a ordem do enum mudar, banco recusa valor inválido | Um pouco mais de espaço |
| Concorrência otimista (`rowversion`) | Sem locks longos; conflito vira 409 explícito | Cliente precisa tratar 409 (recarregar e tentar de novo) |
| `Idempotency-Key` com tabela própria | Clique duplo, retry após timeout e rede instável não duplicam operações | Uma escrita extra por requisição com a chave; limpeza a cada hora |
| JWT no cabeçalho, sem cookie | Frontend e API em domínios diferentes; sem CSRF | Token acessível ao JS → frontend deve guardar em memória e manter CSP rígida (ver SECURITY.md) |
| Rota versionada `/api/v1` | Outros sistemas vão integrar; mudanças incompatíveis vão para `/api/v2` sem quebrar ninguém | — |
| Terminal com id textual (`unitapajos`, `tgpm`, `hidrovias`) | Mesmo id do frontend (`registry.js`), facilita a migração | — |

## Fluxos críticos

**Ocorrência N3** — `POST /api/v1/ocorrencias` → valida placa/transportadora/condutor → `RegrasNegativacao.AplicarOcorrencia` negativa veículo (e condutor) → uma transação grava ocorrência + veículo + log de auditoria. Com `Idempotency-Key`, repetir não cria outra ocorrência.

**Contestação** — abrir marca a ocorrência como "Contestada" (índice único garante uma pendente por ocorrência); aprovar regulariza **só** o veículo/condutor daquela ocorrência; aprovações simultâneas → uma vence, as outras recebem 409/422.

**Login** — rate limit por IP → busca usuário → verifica hash (ou hash fictício) → falha: `UPDATE` atômico do contador + auditoria → MFA (TOTP, consumo atômico do passo) → token com `ver` e, se for o caso, restrição.

**Toda requisição autenticada** — assinatura/emissor/validade do JWT → `ver` confere com o banco (cache 30 s) → política do endpoint (papel + sem restrição) → rate limit por usuário.

## Dados

Tabelas: `Terminais`, `Marcacoes` (103k+, PK `MovimentoId`, índices por terminal+data, data, senha, convênio), `Transportadoras`, `Veiculos` (placa única), `Condutores`, `Ocorrencias`, `Contestacoes`, `Agendamentos`, `Usuarios`, `LogsAuditoria`, `ChavesIdempotencia`. Todas as FKs com `Restrict` (nada apaga em cascata histórico), exceto `Ocorrencia.CondutorId` (`SET NULL`).

Migrations versionadas em `backend/src/BarcaLog.Infrastructure/Persistencia/Migrations`. Em produção: aplicar com o usuário de migração (`dotnet run -- migrar`), nunca na subida da API. Usuários de menor privilégio e backup: `backend/database/*.sql`.

## Operação

- **Configuração** por ambiente (`appsettings.{Ambiente}.json` + variáveis de ambiente). Produção falha ao subir se a configuração for insegura.
- **Container**: `backend/Dockerfile` (multi-stage, usuário sem privilégio, sem segredos na imagem).
- **Sondas**: `/health/live` (processo) e `/health/ready` (banco).
- **Logs**: JSON estruturado com `request_id`; o mesmo id volta no cabeçalho `X-Request-Id` e no `traceId` dos erros.
- **CI** (`.github/workflows/backend.yml`): build com avisos como erro, pacotes vulneráveis, migrations em dia, testes unitários + integração contra SQL Server real (teste pulado = falha).

## Testes

- `backend/tests/BarcaLog.UnitTests` — regras de domínio, métricas, TOTP (vetores da RFC), CNPJ/placa, política de senha.
- `backend/tests/BarcaLog.IntegrationTests` — API inteira contra SQL Server real: fluxos de negócio, autenticação/autorização, ataques (força bruta, token adulterado, `alg: none`, SQLi, enum inválido, upload malicioso, corpo gigante), concorrência (aprovações e contestações paralelas), idempotência, headers, CORS, LGPD.
- `backend/tools/verificar-fidelidade.mjs` — métricas da API × JS original.
