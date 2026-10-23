# Dicionário de Dados do BarcaLog

Descreve os dados **do BarcaLog como está implementado**: banco, API, estados, indicadores e
integrações. É gerado a partir do código e completado por descrições funcionais mantidas por
pessoas. O modelo futuro (aplicativo móvel, novas integrações) fica separado, em
`../arquitetura/arquitetura-futura.md`, e nada dele aparece aqui como se existisse.

> Não confundir com o dicionário do **banco do Posto Mirian** previsto na especificação de
> 03/08/2026 (engenharia reversa do sistema legado do cliente). Esse depende do recebimento
> dos dados e ainda não foi feito — ver `../arquitetura/registro-de-decisoes.md`.

## Como está organizado

| Arquivo | Conteúdo | Como é mantido |
|---|---|---|
| `gerado/tabelas-e-campos.md` | Cada tabela e cada campo: tipo, tamanho, nulo, padrão, chave, domínio, regra, origem, sensibilidade, descrição, índices, CHECKs | **Gerado** |
| `gerado/relacionamentos.md` | Chaves estrangeiras, cardinalidade, regra de exclusão, diagrama | **Gerado** |
| `gerado/enums.md` | Enums e valores gravados | **Gerado** |
| `gerado/api.md` | 133 endpoints (rota, verbo, acesso, parâmetros, validações, respostas) e os contratos (DTOs) | **Gerado** |
| `gerado/campos.csv` | Uma linha por campo, para abrir no Excel (separador `;`, UTF-8) | **Gerado** |
| `gerado/esquema.json` | Tudo o que foi extraído, em formato de máquina | **Gerado** |
| `curadoria.json` | Nome lógico, finalidade, origem, responsável, consumidores, retenção, sensibilidade e nomes amigáveis | Pessoas |
| `modelo-relacional.md` | Visão geral, fluxo dos dados e associações **sem** chave estrangeira | Pessoas |
| `regras-de-negocio.md` | Estados, transições, validações e efeitos | Pessoas |
| `api.md` | Regras comuns da API: autenticação, erros, paginação, idempotência, limites, versões | Pessoas |
| `indicadores.md` | Onde está o catálogo de indicadores e o que cada um consome | Pessoas |
| `integracoes.md` | Inventário de integrações existentes e previstas e o contrato de eventos | Pessoas |

## O que é extraído automaticamente e o que não é

| Extraído do código (confiável, sempre atual) | Exige pessoa (marcado *a validar* enquanto não confirmado) |
|---|---|
| Tabelas, colunas, tipos SQL, tamanho, nulidade, padrão, colunas calculadas | Nome amigável e nome lógico |
| Chaves primárias e estrangeiras, regra de exclusão, índices, CHECKs | Finalidade, origem do registro, quem cria/atualiza |
| Valores de enum e onde são usados | Significado de negócio de cada estado e transições permitidas |
| Endpoints, verbo, rota, política de acesso, parâmetros, validações, respostas, idempotência, limite de taxa | Telas e relatórios consumidores |
| Contratos (DTOs) e regras de validação | Sensibilidade (LGPD) e retenção |
| Comentários `///` do código como descrição técnica | Divergências com o negócio |

Nada é deduzido do nome do campo. Se o código não comenta e a curadoria não descreve, o texto
sai como **a validar**.

## Como atualizar

```bash
cd backend
dotnet run --project src/BarcaLog.Api -- dicionario              # regrava gerado/
dotnet run --project src/BarcaLog.Api -- dicionario --verificar  # confere (o mesmo que o CI faz)
```

O comando não abre conexão com o banco: lê o modelo do EF Core, os enums, o ApiExplorer e os
comentários XML compilados. No CI (`.github/workflows/backend.yml`), o passo "Dicionário de
dados em dia com o código" **falha** quando:

- uma migration, entidade, enum, endpoint ou DTO mudou e `gerado/` não foi regenerado;
- surgiu uma tabela sem entrada em `curadoria.json`;
- `curadoria.json` cita tabela ou campo que não existe mais.

Fluxo para quem altera o modelo: criar a migration → rodar `dicionario` → completar
`curadoria.json` para o que for novo → commitar tudo junto.

## Convenções

| Item | Convenção |
|---|---|
| Nomes físicos | Tabelas no plural (`Marcacoes`), colunas em PascalCase sem acento |
| Identificadores | `int`/`bigint` gerados pelo banco; exceções: `Marcacoes.MovimentoId` (COD_MOVIMENTO da origem, texto, para preservar zeros à esquerda), `Terminais.Id` (slug: `unitapajos`, `tgpm`, `hidrovias`), `Convenios.Codigo` (COD_CONVENIO) |
| Enums | Gravados como texto (nome do valor) com CHECK; `[Flags]` como número |
| Data e hora operacional | Marcação e liberação: **horário local do porto** (America/Belem, UTC−3), sem fuso, como na fonte |
| Data e hora de sistema | `CriadoEm`, `AtualizadoEm`, `Quando` etc.: **UTC**, devolvidas pela API com `Z` |
| Unidades | Espera em horas (decimal, 2 casas); capacidades em carretas/dia e carretas/hora; percentuais de 0 a 100 |
| Moeda | Não há valores monetários no modelo atual |
| Concorrência | `VersaoLinha` (`rowversion`) nas tabelas editáveis; conflito → HTTP 409 |
| Exclusão | Quase sempre impedida quando há vínculos (`NO ACTION`); exceções em `gerado/relacionamentos.md` |

## Sensibilidade (LGPD) — resumo

Campos com dado pessoal ou credencial estão marcados na coluna **Sensibilidade** de
`gerado/tabelas-e-campos.md`. Credenciais (`Usuarios.SenhaHash`, `Usuarios.MfaSegredoCifrado`)
nunca saem pela API. Condutor pode ser anonimizado (`POST /api/v1/condutores/{id}/anonimizar`).
Prazos de retenção ainda não foram definidos com o porto.
