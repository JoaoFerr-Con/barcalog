# API — regras comuns

Cada endpoint, com parâmetros, validações, respostas e contratos, está em `gerado/api.md`
(gerado do código; 133 endpoints). Este arquivo reúne o que vale para todos.

## Versão e formato

- Base: `/api/v1`. Mudança incompatível vai para `/api/v2`, mantendo a anterior durante a transição.
- JSON com nomes em camelCase; enums pelo nome (`"Planilha"`), nunca pelo número (número é recusado).
- Datas: marcação e liberação no horário local do porto, sem fuso (`2026-07-31T15:03:09`);
  datas de sistema em UTC com `Z`. Datas puras como `yyyy-MM-dd`.
- Documentação interativa (Swagger) em `/swagger` no ambiente de desenvolvimento, ou com
  `Swagger:Habilitado = true`.
- Saúde: `/health/live` (processo) e `/health/ready` (banco) — fora do `/api/v1`.

## Autenticação e autorização

| Cliente | Como se autentica | Política |
|---|---|---|
| Usuário interno | `POST /api/v1/auth/login` → JWT no cabeçalho `Authorization: Bearer` (8 h; 15 min quando restrito) | `Leitura` (Operador, Gestor, Auditor), `Escrita` (Operador, Gestor), `Gestao` (Gestor) |
| Usuário com senha provisória ou Gestor sem MFA | JWT **restrito** | `Autenticado`: só perfil, troca de senha, MFA e logout |
| Transportadora (Portal) | Mesmo login, papel Transportadora | `Portal`: só dados da própria transportadora |
| Sistema externo | `X-Api-Key` (a API guarda só o hash) | `Integracao` |

Escopo no servidor (não depende da interface):
- **Terminal**: usuário vinculado tem todo filtro de terminal forçado ao próprio; pedir outro
  terminal → 403; registro avulso de outro terminal → 404.
- **Transportadora**: o Portal só devolve dados da transportadora do token.

Revogação: logout, troca de senha, troca de papel, mudança de vínculo, desativação e MFA
invalidam **todos** os tokens do usuário (verificação a cada requisição, com cache de 30 s).

## Erros (ProblemDetails, RFC 7807)

Toda resposta de erro tem `status`, `title`, `detail` e `traceId` (o mesmo do cabeçalho
`X-Request-Id`, para achar no log). Detalhes internos (SQL, pilha) nunca vão para o cliente.

| Status | Quando |
|---|---|
| 400 | Validação dos campos (`errors` traz campo → mensagens), JSON inválido, lote vazio ou grande demais |
| 401 | Sem token, token inválido/expirado/revogado; no login, `codigo` = `credenciais_invalidas` ou `mfa_requerido` |
| 403 | Papel sem permissão, token restrito, ou recurso fora do escopo (terminal/transportadora) |
| 404 | Recurso inexistente ou fora do escopo do usuário (registro avulso) |
| 409 | Edição concorrente (`rowversion`), registro duplicado, registro com vínculos, idempotência em andamento |
| 413 | Corpo acima do limite |
| 422 | Regra de negócio violada (mensagem pode ser mostrada ao usuário) |
| 429 | Limite de requisições (cabeçalho `Retry-After`) |
| 499 | Cliente desistiu da requisição |
| 500 | Erro inesperado (mensagem genérica + `traceId`) |
| 504 | Operação excedeu o tempo |

## Paginação

Listas paginadas aceitam `pagina` (a partir de 1) e `tamanhoPagina` (1–500, padrão 50) e
respondem `{ itens, total, numeroPagina, tamanhoPagina, totalPaginas }`. Os endpoints paginados
estão marcados em `gerado/api.md`.

## Idempotência

Operações marcadas como idempotentes em `gerado/api.md` aceitam `Idempotency-Key`:
- 1ª chamada executa e guarda a resposta por 24 h;
- repetição com o mesmo corpo devolve a mesma resposta (`Idempotent-Replayed: true`) sem executar de novo;
- mesma chave com corpo diferente → 422; repetição enquanto a 1ª ainda roda → 409.

## Limites de requisição (padrões, configuráveis)

| Limite | Padrão |
|---|---|
| Geral, por usuário (ou IP se anônimo) | 300/min |
| Login, troca de senha e MFA, por IP | 10/min (além do bloqueio da conta após 5 falhas) |
| Integração, por sistema | 120/min |
| Importação de planilha | 6/hora |
| Exportação CSV | 10/min |

## Endpoints descontinuados

15 endpoints legados de `/api/v1/marcacoes/*` (portados das primeiras telas) respondem com
`Deprecation: true` e `X-BarcaLog-Aviso`, com o motivo (ex.: capacidade presumida de 1.000
carretas/dia, projeção sem medição de erro) e o substituto quando existe. A lista está em
`gerado/api.md` (marcados como "descontinuado").

## Documentado × implementado

| Situação | Itens |
|---|---|
| Implementado e **não usado pelo portal atual** (busca textual no frontend) | Cerca de 35 endpoints analíticos legados em `/api/v1/marcacoes/*` (kpis, por-carga, por-hora, ranking-esperas, janelas-permanencia, etc.), `/api/v1/marcacoes/importar` (importação antiga sem revisão), `/api/v1/integracao/eventos` (aguarda sistema externo), `GET /api/v1/alertas/avaliacoes` e `POST /api/v1/auth/mfa/desativar` |
| Previsto em documentos e **sem implementação** | API B2B para transportadoras injetarem viagens, cargas, janelas e motoristas; eventos de checkpoint A–D; leituras de OCR; notificações de N2/N3; endpoints do app móvel (ver `../arquitetura/arquitetura-futura.md`) |
| Usado pelo portal e sem endpoint | Nenhum encontrado |

Pendente: decidir se os endpoints legados não usados serão removidos em `/api/v2` ou mantidos
para integração.
