# Arquitetura atual — o que existe no código

Levantamento feito sobre o repositório em 23/10/2026 (branch `fase-8-gestor`). A fonte é o
código, não os documentos comerciais. Onde o código e os documentos antigos divergem, a
divergência está em `registro-de-decisoes.md`.

## 1. Visão geral

```
┌──────────────────────────┐  HTTPS + JWT (Bearer)   ┌────────────────────────────────┐    ┌──────────────────┐
│ Portal web (React/Vite)  │ ──────────────────────▶ │ API ASP.NET Core 8 (/api/v1)   │───▶│ SQL Server 2022   │
│ estático na Vercel       │                         │ camadas Domain/Application/    │    │ EF Core 8,        │
└──────────────────────────┘                         │ Infrastructure/Api             │    │ migrations        │
┌──────────────────────────┐  HTTPS + X-Api-Key      │                                │    └──────────────────┘
│ Sistema externo (futuro) │ ──────────────────────▶ │ POST /api/v1/integracao/eventos│
└──────────────────────────┘                         │ tarefa em segundo plano:       │
                                                     │ avaliação de alertas (10 min)  │
                                                     └────────────────────────────────┘
```

| Item | Implementação atual | Evidência |
|---|---|---|
| Frontend | React 18 + Vite 7, Recharts; SPA com rotas por hash (`#/tela?filtros`) | `package.json`, `src/App.jsx` |
| Backend | ASP.NET Core 8, controllers, camadas Domain → Application → Infrastructure → Api | `backend/src/*`, `ARCHITECTURE.md` |
| Banco | SQL Server 2022, EF Core 8, 18 tabelas, migrations versionadas | `docs/dicionario-de-dados/gerado/` |
| Autenticação interna | JWT (HMAC-SHA256), 8 h (15 min quando restrito), revogação por `VersaoToken`, MFA TOTP para Gestor, bloqueio por força bruta | `Api/Seguranca`, `AutenticacaoServico` |
| Autenticação de sistemas | `X-Api-Key` (só o hash SHA-256 fica na configuração) | `ApiKeyAuthenticationHandler` |
| Autorização | Políticas `Leitura`, `Escrita`, `Gestao`, `Autenticado`, `Portal`, `Integracao` + escopo por terminal no servidor (`EscopoTerminalFiltro`) + escopo por transportadora no Portal | `Politicas.cs`, `EscopoUsuarioHttp.cs` |
| Auditoria | Interceptor do EF grava toda alteração em `LogsAuditoria` na mesma transação | `AuditoriaInterceptor` |
| Idempotência | Cabeçalho `Idempotency-Key` nos POST de cadastro, ocorrência, contestação e integração | `Infra/Idempotente.cs` |
| Concorrência | `rowversion` + 409 | `VersaoLinha` nas tabelas de cadastro |
| Processamento assíncrono | Um `BackgroundService` em processo (alertas a cada 10 min; limpeza de idempotência a cada hora). **Não há fila de mensagens.** | `AvaliacaoPeriodicaAlertas`, `LimpezaIdempotencia` |
| Cache | Memória do processo (projeção das marcações, até 60 s; versão de token, 30 s). **Não há cache distribuído.** | `MetricasServico`, `ValidadorSessao` |
| Containers | `backend/Dockerfile` (imagem da API) e `backend/docker-compose.yml` (SQL Server para desenvolvimento). Usados em desenvolvimento e no CI; **nenhum ambiente de produção usa container hoje.** | arquivos citados |
| Orquestração | **Não existe** (sem Kubernetes, sem manifestos) | — |
| CI | GitHub Actions: build com avisos como erro, pacotes vulneráveis, migrations em dia, **dicionário de dados em dia**, testes unitários e de integração contra SQL Server real | `.github/workflows/*.yml` |
| Implantação | Frontend publicado na Vercel. **A API não está hospedada em lugar nenhum** — o portal publicado não tem backend. | `vercel.json`; decisão pendente |
| Aplicativo móvel | **Não existe neste repositório.** O app de fiscalização citado na proposta comercial (OCR, SQLite, PostGIS) não foi entregue nem integrado. | — |

## 2. Módulos e maturidade

Legenda: **Concluído** = fluxo de ponta a ponta com dados reais e testes de integração.
**Parcial** = funciona, mas falta parte do fluxo ou da validação. **Demonstração** = telas e API
funcionam, mas os dados do banco são fictícios porque a fonte real não existe ainda.
**Planejado** = só nos documentos. **Não implementado** = pedido, sem código.

| Módulo | Situação | Dados | O que existe | O que falta | Testes |
|---|---|---|---|---|---|
| Visão Geral (indicadores) | Concluído | Reais (planilhas) | Resumo, comparação, por terminal, volume mensal, fluxo horário, frescor, relatório imprimível | Capacidades dos terminais não informadas → sem ocupação | Integração (cálculo manual) + unitários |
| Movimentações — lista | Concluído | Reais | Filtros, ordenação, origem da linha, detalhe, CSV | — | Integração |
| Movimentações — Análise 360° | Concluído | Reais | Resumo × período anterior, série, hora, dia da semana, dimensões, qualidade, detalhamento | Remarcações e lotes sem filtro de lista | Integração + unitários |
| Importação de planilhas | Concluído | Reais | Análise, revisão (novo/completa/conflito/rejeitada), decisão, confirmação transacional, relatório | — | Integração |
| Gargalos e alertas | Concluído | Reais | Regras, tratamento, Reavaliar com registro, confiabilidade em 4 classes, estimativa 6 h | Erro da estimativa ~50% (backtest); só indicativa | Integração + unitários + backtest |
| Configurações | Concluído | Reais | Capacidade, limites, convênios, acesso por terminal | Capacidades ainda não informadas pelo porto | Integração |
| Usuários, perfis e escopo | Concluído | Reais | Papéis, MFA, vínculo a terminal, preferências | Tela de criação de usuários (hoje via API/CLI) | Integração |
| Auditoria | Concluído | Reais | Log automático e consulta | Prazo de retenção (LGPD) não definido | Integração |
| Portaria | Demonstração | Fictícios | Busca por placa, situação manual, fila virtual | Situação é **informada**, não confirmada por evento (sem leitura de placa, cancela ou checkpoint) | Integração |
| Agendamentos | Demonstração | Fictícios | Agenda por hora × capacidade, conflito de placa, QR | Fonte real da agenda; transições de status livres; significado de D0–D3 diverge do documento original | Integração (parcial) |
| Negativação (N1/N2/N3) e contestação | Demonstração | Fictícios | Regra N3 bloqueia veículo/condutor; contestação regulariza só a ocorrência | Origem real das ocorrências (app de campo) | Integração + unitários |
| Frota e Cadastros | Demonstração | Fictícios | Placas, transportadoras, condutores, anonimização de condutor | Cadastro real | Integração |
| Portal do Transportador | Demonstração | Fictícios | Login próprio, veículos, condutores, ocorrências, contestação, escopo por transportadora | Agenda da transportadora; usuários reais | Integração |
| Integração de eventos (marcação/liberação) | Parcial | — | Endpoint com API Key, lote até 1.000, idempotência, validação | Nenhum sistema conectado; Protheus sem especificação (`docs/PROTHEUS.md`) | Integração |
| Checkpoints A–D | Planejado | — | — | Tudo | — |
| Janelas de descarga D0–D3 (dia autorizado) | Planejado | — | Campo `JanelaConformidade` com outro significado | Regra e fonte | — |
| App móvel de fiscalização (OCR, offline) | Planejado | — | — | Tudo (ver `arquitetura-futura.md`) | — |
| Visão do Posto Mirian | Não implementado | — | — | Dados do Posto Mirian ainda não recebidos (ver pendências) | — |
| Mapa de calor de irregularidades | Planejado | — | — | Geolocalização não é coletada hoje | — |

## 3. Onde ficam as regras de negócio hoje

Todas no backend (Domain e Application). O frontend só formata e chama a API.

| Regra | Onde |
|---|---|
| N3 bloqueia veículo e condutor; transportadora é negativada se tiver ≥ 1 veículo negativado (calculado) | `Domain/Regras/RegrasNegativacao.cs` |
| Classificação das linhas importadas, remarcação, avisos | `Domain/Regras/RegrasImportacao.cs`, `ImportacaoPlanilhaServico` |
| Indicadores, Análise 360°, estimativa e confiabilidade | `Application/Metricas/*` (regras em `docs/INDICADORES.md`) |
| Regras de alerta | `Application/Metricas/RegrasAlerta.cs` |
| Escopo por terminal e por transportadora | `Application/Seguranca/EscopoTerminal.cs`, `PortalServico` |
| Política de senha, placa, CNPJ | `Domain/Regras/Documentos.cs`, `PoliticaSenha` |

## 4. Contratos e documentação viva

- Endpoints, parâmetros, validações e contratos: `docs/dicionario-de-dados/gerado/api.md`
  (gerado do código) e Swagger em `/swagger` no ambiente de desenvolvimento.
- Tabelas, campos, índices e restrições: `docs/dicionario-de-dados/gerado/tabelas-e-campos.md`.
- O CI falha se o dicionário gerado divergir do código.
