# Tabelas e campos

> Arquivo **gerado** por `dotnet run --project backend/src/BarcaLog.Api -- dicionario` a partir do código
> (modelo do EF Core, enums, ApiExplorer e comentários XML) e de `docs/dicionario-de-dados/curadoria.json`.
> Não edite à mão: o CI recusa o arquivo se ele divergir do código.

Modelo na migration `20261021135006_VinculoTerminalEPreferenciasUsuario` · 18 tabelas · 204 colunas.

Legenda: **Nulo** = aceita NULL no banco. **Fuso**: *UTC* = gravado e lido em UTC; *sem fuso no modelo* = o banco guarda a data/hora como recebida (ver descrição). Textos marcados *a validar* ainda não foram confirmados com o negócio.

## Índice

| Tabela | Nome lógico | Finalidade | Situação |
|---|---|---|---|
| [Agendamentos](#agendamentos) | Agendamento de carreta | Reserva de uma carreta (placa) para um terminal, dia e hora, com a janela D0–D3 e a situação do atendimento. Base da tela Agendamentos (grade hora × capacidade e QR). | Extraído do código; dados atuais são demonstração; regra de negócio a validar |
| [Alertas](#alertas) | Alerta operacional | Problema identificado por regra (histórico, operacional, previsão, dados) com evidência, limite, origem, ação recomendada e estado de tratamento. | Extraído do código; regras documentadas em docs/INDICADORES.md |
| [AvaliacoesAlertas](#avaliacoesalertas) | Avaliação das regras de alerta | Histórico de cada execução das regras: dados novos, alertas criados/alterados/encerrados, confiabilidade e falhas. Responde 'quando foi a última avaliação e o que mudou'. | Extraído do código |
| [ChavesIdempotencia](#chavesidempotencia) | Chave de idempotência | Garante que uma operação repetida (clique duplo, reenvio após falha de rede) tenha efeito uma vez só e devolva a mesma resposta. | Extraído do código |
| [Condutores](#condutores) | Condutor (motorista) | Motorista vinculado a uma transportadora, negativável junto com o veículo em ocorrência N3. | Extraído do código; dados atuais são demonstração |
| [Contestacoes](#contestacoes) | Contestação de ocorrência | Pedido da transportadora para rever uma ocorrência. Aprovada: regulariza só o veículo/condutor daquela ocorrência. Rejeitada: a ocorrência volta a ativa. | Extraído do código; dados atuais são demonstração |
| [Convenios](#convenios) | Convênio | Cadastro dos convênios das planilhas (COD_CONVENIO) com operador e carga — origem das dimensões 'operador' e 'carga' dos indicadores. | Extraído do código |
| [EventosAlerta](#eventosalerta) | Evento do alerta | Histórico de cada alerta: geração, mudanças, quem assumiu, ações registradas, resolução, descarte, reabertura. | Extraído do código |
| [LinhasImportacao](#linhasimportacao) | Linha de importação (área de revisão) | Cada linha da planilha analisada, com classificação (novo, completa dados, conflito, rejeitada), decisão e motivo, antes de tocar em Marcacoes. | Extraído do código; regras em docs/IMPORTACAO.md |
| [LogsAuditoria](#logsauditoria) | Log de auditoria | Quem fez o quê e quando em toda gravação (inclusive login, falhas e decisões). | Extraído do código; prazo de retenção pendente |
| [LotesImportacao](#lotesimportacao) | Lote de importação | Uma planilha importada: arquivo, hash, terminal, contagens de cada classificação e avisos, situação (analisado, confirmado, cancelado) e quem fez cada etapa. | Extraído do código |
| [Marcacoes](#marcacoes) | Movimentação (marcação → liberação) | Registro de cada carreta marcada num terminal e de sua liberação. Fonte de todos os indicadores de volume e espera, dos alertas e da Análise 360°. | Campos vindos das planilhas; significado de SENHA, CICLO e 'liberação' a confirmar com o porto (docs/PROTHEUS.md) |
| [Ocorrencias](#ocorrencias) | Ocorrência (Matriz de Disciplina N1/N2/N3) | Infração registrada contra uma placa. N3 negativa automaticamente o veículo (e o condutor, se identificado); N1 e N2 não bloqueiam. | Regra N3 confirmada na documentação original; dados atuais são demonstração |
| [ParametrosOperacionais](#parametrosoperacionais) | Parâmetros operacionais | Limites e metas usados pelos indicadores e alertas (linha única). | Valores iniciais são pontos de partida, não normas do porto (a validar) |
| [Terminais](#terminais) | Terminal portuário | Terminais atendidos (Unitapajós, TGPM, Hidrovias) e suas capacidades quando informadas. | Extraído do código; capacidades ainda não informadas pelo porto |
| [Transportadoras](#transportadoras) | Transportadora | Empresa dona das carretas. Situação de negativação é calculada (negativada se tiver ao menos um veículo negativado). | Extraído do código; dados atuais são demonstração |
| [Usuarios](#usuarios) | Usuário | Contas internas (Operador, Gestor, Auditor) e do Portal (Transportadora): credenciais, MFA, papel, vínculo de terminal e preferências. | Extraído do código |
| [Veiculos](#veiculos) | Veículo (carreta) | Carreta identificada pela placa: transportadora, terminal, situação de negativação e situação de portaria. | Extraído do código; dados atuais são demonstração; situação de portaria é registrada, não confirmada por evento |

## Agendamentos

- **Nome lógico:** Agendamento de carreta
- **Entidade (código):** `Agendamento`
- **Finalidade:** Reserva de uma carreta (placa) para um terminal, dia e hora, com a janela D0–D3 e a situação do atendimento. Base da tela Agendamentos (grade hora × capacidade e QR).
- **Origem dos registros:** Cadastro manual na tela Agendamentos. Hoje o banco contém só agendamentos fictícios de demonstração (as planilhas dos terminais não trazem agenda).
- **Quem cria/atualiza:** Operador ou Gestor (POST/PUT /api/v1/agendamentos)
- **Consumida por:** Tela Agendamentos; GET /api/v1/agendamentos/resumo
- **Retenção/histórico:** Sem regra de expurgo no código.
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Identificador | int | não |  | PK | identidade (gerado pelo banco) | Gerado pelo banco |  | O código exibido no QR é AGD-{Id com 6 dígitos} (calculado, não gravado). |
| `Carga` | Carga | nvarchar(50) | não |  |  |  | Cadastro manual na tela Agendamentos. Hoje o banco contém só agendamentos fictícios de demonstração (as planilhas dos terminais não trazem agenda). |  | a validar |
| `Data` | Data agendada | date | não |  |  |  | Cadastro manual na tela Agendamentos. Hoje o banco contém só agendamentos fictícios de demonstração (as planilhas dos terminais não trazem agenda). |  | a validar |
| `Hora` | Hora agendada | time | não |  |  |  | Cadastro manual na tela Agendamentos. Hoje o banco contém só agendamentos fictícios de demonstração (as planilhas dos terminais não trazem agenda). |  | a validar |
| `JanelaConformidade` | Janela (D0–D3) | nvarchar(32) | não |  |  | D0, D1, D2, D3 | Cadastro manual na tela Agendamentos. Hoje o banco contém só agendamentos fictícios de demonstração (as planilhas dos terminais não trazem agenda). |  | Significado diverge entre a documentação original (dia autorizado: D0 = hoje) e o código (prazo de permanência). Ver regras-de-negocio.md. |
| `Motorista` | Motorista | nvarchar(150) | sim |  |  |  | Cadastro manual na tela Agendamentos. Hoje o banco contém só agendamentos fictícios de demonstração (as planilhas dos terminais não trazem agenda). | Dado pessoal (nome) | a validar |
| `Placa` | Placa | nvarchar(10) | não |  |  |  | Cadastro manual na tela Agendamentos. Hoje o banco contém só agendamentos fictícios de demonstração (as planilhas dos terminais não trazem agenda). | Dado pessoal indireto (identifica veículo) | a validar |
| `Status` | Situação do agendamento | nvarchar(32) | não |  |  | Agendado, Confirmado, ACaminho, AguardandoEntrada, EmOperacao, Finalizado, Atrasado, Cancelado | Cadastro manual na tela Agendamentos. Hoje o banco contém só agendamentos fictícios de demonstração (as planilhas dos terminais não trazem agenda). |  | Alterada manualmente; o código não restringe transições. |
| `TerminalId` | Terminal | nvarchar(32) | não |  | FK → Terminais |  | Cadastro manual na tela Agendamentos. Hoje o banco contém só agendamentos fictícios de demonstração (as planilhas dos terminais não trazem agenda). |  | a validar |
| `TransportadoraId` | Transportadora | int | não |  | FK → Transportadoras |  | Cadastro manual na tela Agendamentos. Hoje o banco contém só agendamentos fictícios de demonstração (as planilhas dos terminais não trazem agenda). |  | a validar |
| `VersaoLinha` | Versão da linha | rowversion | sim |  |  | rowversion (concorrência otimista) | Gerado pelo banco |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_Agendamentos_Data_Hora` | Data, Hora | não |  |
| `IX_Agendamentos_Placa` | Placa | não |  |
| `IX_Agendamentos_TerminalId_Data` | TerminalId, Data | não |  |
| `IX_Agendamentos_TransportadoraId` | TransportadoraId | não |  |
| `UX_Agendamentos_PlacaHorarioAtivo` | Placa, Data, Hora | sim | [Status] <> N'Cancelado' |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_Agendamentos_JanelaConformidade` | `[JanelaConformidade] IN (N'D0', N'D1', N'D2', N'D3')` |
| `CK_Agendamentos_Status` | `[Status] IN (N'Agendado', N'Confirmado', N'ACaminho', N'AguardandoEntrada', N'EmOperacao', N'Finalizado', N'Atrasado', N'Cancelado')` |

## Alertas

- **Nome lógico:** Alerta operacional
- **Entidade (código):** `Alerta`
- **Descrição (código):** Alerta gerado por regra a partir dos dados. A Chave identifica "o mesmo problema" entre uma avaliação e outra (regra + terminal + período), para que estado e histórico de tratamento não se percam quando as regras rodam de novo.
- **Finalidade:** Problema identificado por regra (histórico, operacional, previsão, dados) com evidência, limite, origem, ação recomendada e estado de tratamento.
- **Origem dos registros:** Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora.
- **Quem cria/atualiza:** Sistema cria e atualiza; Operador/Gestor tratam (assumir, registrar ação, resolver, descartar, reabrir).
- **Consumida por:** Tela Gargalos e alertas; Visão Geral (alertas abertos); Relatório do período; Contador de alertas no menu
- **Retenção/histórico:** Sem expurgo; alerta que deixa de ocorrer fica com Ativo = false.
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Identificador | int | não |  | PK | identidade (gerado pelo banco) | Gerado pelo banco |  | a validar |
| `AcaoRecomendada` | Ação recomendada | nvarchar(500) | sim |  |  |  | Regra do alerta |  | a validar |
| `Ativo` | Ainda ocorrendo | bit | não |  |  |  | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | false quando a condição deixou de ocorrer na última avaliação. |
| `AtualizadoEm` | Atualizado em | datetime2 | não |  |  | fuso: UTC | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | a validar |
| `Chave` | Chave do problema | nvarchar(200) | não |  | único |  | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | Regra + terminal + período; identifica o mesmo problema entre avaliações. |
| `Estado` | Estado do tratamento | nvarchar(32) | não |  |  | Novo, EmAnalise, Resolvido, Descartado | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | a validar |
| `Evidencia` | Evidência | nvarchar(2000) | não |  |  |  | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | Texto com os números que sustentam o alerta. |
| `IdentificadoEm` | Identificado em | datetime2 | não |  |  | fuso: UTC | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | a validar |
| `Indicador` | Indicador | nvarchar(200) | não |  |  |  | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | a validar |
| `Limite` | Limite | nvarchar(50) | não |  |  |  | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | a validar |
| `Origem` | Origem dos dados | nvarchar(200) | não |  |  |  | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | De onde vieram os dados (planilhas importadas, integração, registro manual, padrão histórico…). |
| `Periodo` | Período | nvarchar(100) | não |  |  |  | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | Período ou janela de referência, por extenso ("agosto de 2026", "hoje, 16h–17h"). |
| `Prioridade` | Prioridade | nvarchar(32) | não |  |  | Baixa, Media, Alta | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | a validar |
| `Regra` | Regra | nvarchar(50) | não |  |  |  | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | Código da regra que gerou (ex.: "espera-mediana"). |
| `Responsavel` | Responsável | nvarchar(200) | sim |  |  |  | Usuário que assumiu | Dado pessoal (nome e e-mail do usuário) | a validar |
| `TerminalId` | Terminal | nvarchar(32) | sim |  |  |  | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | Nulo = alerta geral (todos os terminais); não aparece para usuário vinculado a um terminal. |
| `Tipo` | Tipo | nvarchar(32) | não |  |  | Historico, Operacional, Previsao, Dados | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | a validar |
| `Titulo` | Título | nvarchar(200) | não |  |  |  | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | a validar |
| `ValorObservado` | Valor observado | nvarchar(50) | não |  |  |  | Calculado pelo sistema (RegrasAlerta) a cada 10 min, após importação e em Reavaliar agora. |  | a validar |
| `VersaoLinha` | Versão da linha | rowversion | sim |  |  | rowversion (concorrência otimista) | Gerado pelo banco |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_Alertas_Ativo_Estado_Tipo` | Ativo, Estado, Tipo | não |  |
| `IX_Alertas_Chave` | Chave | sim |  |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_Alertas_Estado` | `[Estado] IN (N'Novo', N'EmAnalise', N'Resolvido', N'Descartado')` |
| `CK_Alertas_Prioridade` | `[Prioridade] IN (N'Baixa', N'Media', N'Alta')` |
| `CK_Alertas_Tipo` | `[Tipo] IN (N'Historico', N'Operacional', N'Previsao', N'Dados')` |

## AvaliacoesAlertas

- **Nome lógico:** Avaliação das regras de alerta
- **Entidade (código):** `AvaliacaoAlertas`
- **Descrição (código):** Registro de cada avaliação das regras (automática, manual ou após importação): quando, por quem, sobre quais dados, o que mudou e a confiabilidade resultante. Também registra falhas.
- **Finalidade:** Histórico de cada execução das regras: dados novos, alertas criados/alterados/encerrados, confiabilidade e falhas. Responde 'quando foi a última avaliação e o que mudou'.
- **Origem dos registros:** Sistema (avaliação periódica, manual em Reavaliar agora, após importação).
- **Quem cria/atualiza:** Sistema
- **Consumida por:** Tela Gargalos e alertas (resultado de Reavaliar agora, última avaliação); GET /api/v1/alertas/avaliacoes
- **Retenção/histórico:** Sem expurgo no código (uma linha a cada 10 min ≈ 52 mil por ano).
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | int | não |  | PK | identidade (gerado pelo banco) | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | a validar |
| `Alterados` | Alertas alterados | int | não |  |  |  | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | a validar |
| `Ativos` | Alertas abertos depois | int | não |  |  |  | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | a validar |
| `Autor` | Quem executou | nvarchar(200) | não |  |  |  | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). | Dado pessoal (nome e e-mail) quando manual | a validar |
| `Confiabilidade` | Confiabilidade | nvarchar(20) | sim |  |  |  | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | Confiabilidade da estimativa (todos os terminais) após a avaliação. |
| `Criados` | Alertas criados | int | não |  |  |  | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | a validar |
| `DadosAte` | Dados ate | datetime2 | sim |  |  | fuso: sem fuso no modelo | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | Última marcação recebida entre todos os terminais (horário do porto). |
| `DadosNovos` | Havia dados novos | bit | não |  |  |  | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | a validar |
| `Encerrados` | Alertas encerrados | int | não |  |  |  | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | a validar |
| `Erro` | Erro | nvarchar(500) | sim |  |  |  | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | Mensagem para o usuário em caso de falha (sem detalhes internos). |
| `Fim` | Fim | datetime2 | não |  |  | fuso: UTC | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | a validar |
| `Inicio` | Início | datetime2 | não |  |  | fuso: UTC | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | a validar |
| `Origem` | Disparo | nvarchar(32) | não |  |  | Automatica, Manual, Importacao | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | a validar |
| `Registros` | Registros | int | não |  |  |  | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | Total de marcações no banco no momento da avaliação. |
| `Sucesso` | Concluída | bit | não |  |  |  | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | a validar |
| `UltimaImportacaoEm` | Ultima importacao em | datetime2 | sim |  |  | fuso: UTC | Sistema (avaliação periódica, manual em Reavaliar agora, após importação). |  | Última importação confirmada (UTC). |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_AvaliacoesAlertas_Sucesso_Inicio` | Sucesso, Inicio | não |  |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_AvaliacoesAlertas_Origem` | `[Origem] IN (N'Automatica', N'Manual', N'Importacao')` |

## ChavesIdempotencia

- **Nome lógico:** Chave de idempotência
- **Entidade (código):** `ChaveIdempotencia`
- **Descrição (código):** Registro de uma requisição com cabeçalho Idempotency-Key. Garante que um POST repetido (clique duplo, retry após timeout, rede caindo) produza o efeito UMA vez e devolva a mesma resposta. Detalhe de infraestrutura — não é entidade de negócio, por isso não fica no Domain nem na auditoria.
- **Finalidade:** Garante que uma operação repetida (clique duplo, reenvio após falha de rede) tenha efeito uma vez só e devolva a mesma resposta.
- **Origem dos registros:** Sistema, quando a requisição traz o cabeçalho Idempotency-Key.
- **Quem cria/atualiza:** Sistema
- **Consumida por:** Filtro [Idempotente] da API
- **Retenção/histórico:** Respostas guardadas por 24 h; limpeza automática a cada hora (LimpezaIdempotencia).
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | bigint | não |  | PK | identidade (gerado pelo banco) | Sistema, quando a requisição traz o cabeçalho Idempotency-Key. |  | a validar |
| `Chave` | Chave | nvarchar(100) | não |  |  |  | Sistema, quando a requisição traz o cabeçalho Idempotency-Key. |  | a validar |
| `Concluida` | Concluida | bit | não |  |  |  | Sistema, quando a requisição traz o cabeçalho Idempotency-Key. |  | a validar |
| `CorpoResposta` | Resposta guardada | nvarchar(max) | sim |  |  |  | Sistema, quando a requisição traz o cabeçalho Idempotency-Key. | Pode conter dados pessoais da resposta original (24 h) | a validar |
| `CriadaEm` | Criada em | datetime2 | não |  |  | fuso: UTC | Sistema, quando a requisição traz o cabeçalho Idempotency-Key. |  | a validar |
| `Escopo` | Escopo (quem + rota) | nvarchar(300) | não |  |  |  | Sistema, quando a requisição traz o cabeçalho Idempotency-Key. |  | Quem pediu (usuário/sistema) + método + rota: a mesma chave de usuários diferentes não colide. |
| `HashRequisicao` | Hash requisicao | nvarchar(64) | não |  |  |  | Sistema, quando a requisição traz o cabeçalho Idempotency-Key. |  | SHA-256 do corpo: mesma chave com corpo diferente é erro do cliente. |
| `Location` | Location | nvarchar(500) | sim |  |  |  | Sistema, quando a requisição traz o cabeçalho Idempotency-Key. |  | a validar |
| `StatusHttp` | Status http | int | sim |  |  |  | Sistema, quando a requisição traz o cabeçalho Idempotency-Key. |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_ChavesIdempotencia_CriadaEm` | CriadaEm | não |  |
| `IX_ChavesIdempotencia_Escopo_Chave` | Escopo, Chave | sim |  |

## Condutores

- **Nome lógico:** Condutor (motorista)
- **Entidade (código):** `Condutor`
- **Descrição (código):** Motorista. Sem CPF — removido do produto de propósito.
- **Finalidade:** Motorista vinculado a uma transportadora, negativável junto com o veículo em ocorrência N3.
- **Origem dos registros:** Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração.
- **Quem cria/atualiza:** Operador ou Gestor; exclusão e anonimização só Gestor.
- **Consumida por:** Tela Cadastros; Tela Negativação; Portal do Transportador
- **Retenção/histórico:** Anonimização sob demanda (POST /api/v1/condutores/{id}/anonimizar, LGPD). Sem expurgo automático.
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | int | não |  | PK | identidade (gerado pelo banco) | Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração. |  | a validar |
| `Nome` | Nome do condutor | nvarchar(150) | não |  |  |  | Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração. | Dado pessoal | a validar |
| `PlacaVinculada` | Placa vinculada | nvarchar(10) | sim |  |  |  | Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração. | Dado pessoal indireto | a validar |
| `StatusNegativacao` | Situação de negativação | nvarchar(32) | não |  |  | Regular, Negativada | Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração. |  | a validar |
| `TransportadoraId` | Transportadora | int | não |  | FK → Transportadoras |  | Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração. |  | a validar |
| `VersaoLinha` | Versao linha | rowversion | sim |  |  | rowversion (concorrência otimista) | Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração. |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_Condutores_PlacaVinculada` | PlacaVinculada | não |  |
| `IX_Condutores_TransportadoraId` | TransportadoraId | não |  |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_Condutores_StatusNegativacao` | `[StatusNegativacao] IN (N'Regular', N'Negativada')` |

## Contestacoes

- **Nome lógico:** Contestação de ocorrência
- **Entidade (código):** `Contestacao`
- **Finalidade:** Pedido da transportadora para rever uma ocorrência. Aprovada: regulariza só o veículo/condutor daquela ocorrência. Rejeitada: a ocorrência volta a ativa.
- **Origem dos registros:** Portal do Transportador (abertura) e tela Negativação (resposta).
- **Quem cria/atualiza:** Transportadora abre; Operador ou Gestor responde.
- **Consumida por:** Tela Negativação; Portal do Transportador
- **Retenção/histórico:** Sem expurgo no código.
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | int | não |  | PK | identidade (gerado pelo banco) | Portal do Transportador (abertura) e tela Negativação (resposta). |  | a validar |
| `CriadoEm` | Aberta em | datetime2 | não |  |  | fuso: UTC | Portal do Transportador (abertura) e tela Negativação (resposta). |  | a validar |
| `Justificativa` | Justificativa da transportadora | nvarchar(4000) | não |  |  |  | Portal do Transportador (abertura) e tela Negativação (resposta). | Texto livre (pode conter dado pessoal) | a validar |
| `OcorrenciaId` | Ocorrência | int | não |  | FK → Ocorrencias, único |  | Portal do Transportador (abertura) e tela Negativação (resposta). |  | a validar |
| `RespondidoEm` | Respondida em | datetime2 | sim |  |  | fuso: UTC | Portal do Transportador (abertura) e tela Negativação (resposta). |  | a validar |
| `RespostaOperador` | Resposta do porto | nvarchar(2000) | sim |  |  |  | Portal do Transportador (abertura) e tela Negativação (resposta). | Texto livre (pode conter dado pessoal) | a validar |
| `Status` | Situação da contestação | nvarchar(32) | não |  |  | Pendente, Aprovada, Rejeitada | Portal do Transportador (abertura) e tela Negativação (resposta). |  | a validar |
| `TransportadoraId` | Transportadora | int | não |  | FK → Transportadoras |  | Portal do Transportador (abertura) e tela Negativação (resposta). |  | a validar |
| `VersaoLinha` | Versao linha | rowversion | sim |  |  | rowversion (concorrência otimista) | Portal do Transportador (abertura) e tela Negativação (resposta). |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_Contestacoes_OcorrenciaId_Status` | OcorrenciaId, Status | não |  |
| `IX_Contestacoes_TransportadoraId` | TransportadoraId | não |  |
| `UX_Contestacoes_OcorrenciaPendente` | OcorrenciaId | sim | [Status] = N'Pendente' |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_Contestacoes_Status` | `[Status] IN (N'Pendente', N'Aprovada', N'Rejeitada')` |

## Convenios

- **Nome lógico:** Convênio
- **Entidade (código):** `Convenio`
- **Descrição (código):** Convênio de um terminal, identificado pelo código do sistema de origem (COD_CONVENIO das planilhas). O nome pode mudar na origem — o código não —, por isso é a chave. Operador e carga são deduzidos do nome na primeira vez e podem ser corrigidos por um Gestor.
- **Finalidade:** Cadastro dos convênios das planilhas (COD_CONVENIO) com operador e carga — origem das dimensões 'operador' e 'carga' dos indicadores.
- **Origem dos registros:** Criado na importação de planilha (dedução pelo nome) e corrigido pelo Gestor em Configurações › Convênios.
- **Quem cria/atualiza:** Sistema (criação) e Gestor (correção)
- **Consumida por:** Configurações › Convênios; Importação; Movimentações (filtros operador/carga)
- **Retenção/histórico:** Sem expurgo.
- **Chave primária:** `Codigo`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Codigo` | Código do convênio (COD_CONVENIO) | nvarchar(32) | não |  | PK |  | Planilha do terminal |  | a validar |
| `Carga` | Carga | nvarchar(50) | não |  |  |  | Criado na importação de planilha (dedução pelo nome) e corrigido pelo Gestor em Configurações › Convênios. |  | a validar |
| `CriadoEm` | Criado em | datetime2 | não |  |  | fuso: UTC | Criado na importação de planilha (dedução pelo nome) e corrigido pelo Gestor em Configurações › Convênios. |  | a validar |
| `Deduzido` | Deduzido do nome (a revisar) | bit | não |  |  |  | Criado na importação de planilha (dedução pelo nome) e corrigido pelo Gestor em Configurações › Convênios. |  | true enquanto operador/carga forem só a dedução automática pelo nome. |
| `Nome` | Nome do convênio | nvarchar(100) | não |  |  |  | Planilha do terminal |  | a validar |
| `Operador` | Operador | nvarchar(50) | não |  |  |  | Criado na importação de planilha (dedução pelo nome) e corrigido pelo Gestor em Configurações › Convênios. |  | a validar |
| `VersaoLinha` | Versao linha | rowversion | sim |  |  | rowversion (concorrência otimista) | Criado na importação de planilha (dedução pelo nome) e corrigido pelo Gestor em Configurações › Convênios. |  | a validar |

## EventosAlerta

- **Nome lógico:** Evento do alerta
- **Entidade (código):** `EventoAlerta`
- **Descrição (código):** Uma entrada no histórico de tratamento do alerta.
- **Finalidade:** Histórico de cada alerta: geração, mudanças, quem assumiu, ações registradas, resolução, descarte, reabertura.
- **Origem dos registros:** Sistema e usuários (Operador/Gestor).
- **Quem cria/atualiza:** Sistema e usuários
- **Consumida por:** Tela Gargalos e alertas (detalhe do alerta)
- **Retenção/histórico:** Apagado junto com o alerta (CASCADE); alertas não são apagados pelo sistema.
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | bigint | não |  | PK | identidade (gerado pelo banco) | Sistema e usuários (Operador/Gestor). |  | a validar |
| `Acao` | Acao | nvarchar(50) | não |  |  |  | Sistema e usuários (Operador/Gestor). |  | a validar |
| `AlertaId` | Alerta id | int | não |  | FK → Alertas |  | Sistema e usuários (Operador/Gestor). |  | a validar |
| `Autor` | Autor | nvarchar(200) | não |  |  |  | Sistema e usuários (Operador/Gestor). | Dado pessoal (nome e e-mail) | a validar |
| `Quando` | Quando | datetime2 | não |  |  | fuso: UTC | Sistema e usuários (Operador/Gestor). |  | a validar |
| `Texto` | Texto registrado | nvarchar(2000) | sim |  |  |  | Sistema e usuários (Operador/Gestor). | Texto livre | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_EventosAlerta_AlertaId` | AlertaId | não |  |

## LinhasImportacao

- **Nome lógico:** Linha de importação (área de revisão)
- **Entidade (código):** `LinhaImportacao`
- **Descrição (código):** Uma linha da planilha guardada para revisão. Linhas idênticas ao que já existe não são guardadas (só contadas). Depois da confirmação ficam só as rejeitadas e as conflitantes — o histórico do que foi recusado ou mudado.
- **Finalidade:** Cada linha da planilha analisada, com classificação (novo, completa dados, conflito, rejeitada), decisão e motivo, antes de tocar em Marcacoes.
- **Origem dos registros:** Leitura da planilha enviada em Importação.
- **Quem cria/atualiza:** Sistema; o Gestor decide os conflitos.
- **Consumida por:** Tela Importação (revisão); Relatório por linha da importação
- **Retenção/histórico:** Após a confirmação ficam só rejeitadas e conflitantes; apagadas junto com o lote (CASCADE).
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | bigint | não |  | PK | identidade (gerado pelo banco) | Leitura da planilha enviada em Importação. |  | a validar |
| `Avisos` | Avisos (flags) | int | não |  |  | AvisoImportacao (combinação de flags gravada como número) | Leitura da planilha enviada em Importação. |  | a validar |
| `Carga` | Carga | nvarchar(50) | sim |  |  |  | Leitura da planilha enviada em Importação. |  | a validar |
| `Ciclo` | Ciclo | int | sim |  |  |  | Leitura da planilha enviada em Importação. |  | a validar |
| `Classificacao` | Classificação | nvarchar(32) | não |  |  | Novo, Complementar, Conflito, Rejeitado | Leitura da planilha enviada em Importação. |  | a validar |
| `CodConvenio` | Cod convenio | nvarchar(32) | sim |  |  |  | Leitura da planilha enviada em Importação. |  | a validar |
| `Convenio` | Convenio | nvarchar(100) | sim |  |  |  | Leitura da planilha enviada em Importação. |  | a validar |
| `DataLiberacao` | Data liberacao | datetime2(0) | sim |  |  | fuso: sem fuso no modelo | Leitura da planilha enviada em Importação. |  | a validar |
| `DataMarcacao` | Data marcacao | datetime2(0) | sim |  |  | fuso: sem fuso no modelo | Leitura da planilha enviada em Importação. |  | a validar |
| `Decisao` | Decisão do conflito | nvarchar(32) | não |  |  | NaoSeAplica, Pendente, Aplicar, Manter | Leitura da planilha enviada em Importação. |  | a validar |
| `LoteId` | Lote id | int | não |  | FK → LotesImportacao |  | Leitura da planilha enviada em Importação. |  | a validar |
| `Motivo` | Motivo | nvarchar(1000) | sim |  |  |  | Leitura da planilha enviada em Importação. |  | Motivo da rejeição ou lista das diferenças ("Convênio: A → B"). |
| `MovimentoId` | Movimento (COD_MOVIMENTO) | nvarchar(32) | não |  |  |  | Planilha do terminal |  | a validar |
| `NumeroLinha` | Número da linha | int | não |  |  |  | Leitura da planilha enviada em Importação. |  | a validar |
| `Operador` | Operador | nvarchar(50) | sim |  |  |  | Leitura da planilha enviada em Importação. |  | a validar |
| `Senha` | Senha (SENHA) | nvarchar(32) | sim |  |  |  | Planilha do terminal |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_LinhasImportacao_LoteId_Classificacao_NumeroLinha` | LoteId, Classificacao, NumeroLinha | não |  |
| `IX_LinhasImportacao_LoteId_MovimentoId` | LoteId, MovimentoId | não |  |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_LinhasImportacao_Classificacao` | `[Classificacao] IN (N'Novo', N'Complementar', N'Conflito', N'Rejeitado')` |
| `CK_LinhasImportacao_Decisao` | `[Decisao] IN (N'NaoSeAplica', N'Pendente', N'Aplicar', N'Manter')` |

## LogsAuditoria

- **Nome lógico:** Log de auditoria
- **Entidade (código):** `LogAuditoria`
- **Descrição (código):** "Quem fez o quê e quando". Populado automaticamente pelo interceptor do EF Core.
- **Finalidade:** Quem fez o quê e quando em toda gravação (inclusive login, falhas e decisões).
- **Origem dos registros:** Automático (interceptor do EF Core) e rótulos definidos pelos serviços.
- **Quem cria/atualiza:** Sistema
- **Consumida por:** Tela Auditoria; GET /api/v1/auditoria
- **Retenção/histórico:** Sem expurgo no código — prazo de guarda a definir com o porto (LGPD).
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | bigint | não |  | PK | identidade (gerado pelo banco) | Automático (interceptor do EF Core) e rótulos definidos pelos serviços. |  | a validar |
| `Acao` | Ação | nvarchar(200) | não |  |  |  | Automático (interceptor do EF Core) e rótulos definidos pelos serviços. |  | a validar |
| `Autor` | Autor | nvarchar(200) | não |  |  |  | Automático (interceptor do EF Core) e rótulos definidos pelos serviços. | Dado pessoal (nome e e-mail) | a validar |
| `Detalhes` | Detalhes (valores antes/depois) | nvarchar(max) | não |  |  |  | Automático (interceptor do EF Core) e rótulos definidos pelos serviços. | Pode conter dados pessoais; segredos são ocultados | a validar |
| `Quando` | Quando | datetime2 | não |  |  | fuso: UTC | Automático (interceptor do EF Core) e rótulos definidos pelos serviços. |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_LogsAuditoria_Acao` | Acao | não |  |
| `IX_LogsAuditoria_Autor` | Autor | não |  |
| `IX_LogsAuditoria_Quando` | Quando | não |  |

## LotesImportacao

- **Nome lógico:** Lote de importação
- **Entidade (código):** `LoteImportacao`
- **Descrição (código):** Uma importação de planilha: analisada primeiro (nada gravado em Marcacoes), confirmada depois por um Gestor. Guarda o arquivo de origem (nome + SHA-256) e o resultado de cada etapa.
- **Finalidade:** Uma planilha importada: arquivo, hash, terminal, contagens de cada classificação e avisos, situação (analisado, confirmado, cancelado) e quem fez cada etapa.
- **Origem dos registros:** Tela Importação.
- **Quem cria/atualiza:** Gestor (análise e confirmação)
- **Consumida por:** Tela Importação (histórico); Movimentações (arquivo de origem); Frescor dos dados
- **Retenção/histórico:** Sem expurgo.
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | int | não |  | PK | identidade (gerado pelo banco) | Tela Importação. |  | a validar |
| `Arquivo` | Nome do arquivo | nvarchar(260) | não |  |  |  | Tela Importação. |  | a validar |
| `Atualizados` | Atualizados | int | não |  |  |  | Tela Importação. |  | a validar |
| `AvisosConvenioNovo` | Avisos convenio novo | int | não |  |  |  | Tela Importação. |  | a validar |
| `AvisosEspera72h` | Avisos espera72h | int | não |  |  |  | Tela Importação. |  | a validar |
| `AvisosRemarcacao` | Avisos remarcacao | int | não |  |  |  | Tela Importação. |  | a validar |
| `AvisosSemLiberacao` | Avisos sem liberacao | int | não |  |  |  | Tela Importação. |  | a validar |
| `ComAvisos` | Com avisos | int | não |  |  |  | Tela Importação. |  | a validar |
| `Complementares` | Complementares | int | não |  |  |  | Tela Importação. |  | a validar |
| `ConcluidoEm` | Concluido em | datetime2 | sim |  |  | fuso: UTC | Tela Importação. |  | a validar |
| `ConcluidoPor` | Confirmado/cancelado por | nvarchar(200) | sim |  |  |  | Tela Importação. | Dado pessoal (nome e e-mail) | a validar |
| `Conflitantes` | Conflitantes | int | não |  |  |  | Tela Importação. |  | a validar |
| `ConveniosNovos` | Convenios novos | nvarchar(max) | sim |  |  |  | Tela Importação. |  | Convênios do arquivo que ainda não existem no cadastro (texto: "código · nome" por linha). |
| `CriadoEm` | Criado em | datetime2 | não |  |  | fuso: UTC | Tela Importação. |  | a validar |
| `CriadoPor` | Analisado por | nvarchar(200) | não |  |  |  | Tela Importação. | Dado pessoal (nome e e-mail) | a validar |
| `Formato` | Formato | nvarchar(32) | não |  |  | Xlsx, Csv | Tela Importação. |  | a validar |
| `HashSha256` | Hash SHA-256 do arquivo | nchar(64) | não |  |  |  | Tela Importação. |  | a validar |
| `Ignorados` | Ignorados | int | não |  |  |  | Tela Importação. |  | a validar |
| `Iguais` | Iguais | int | não |  |  |  | Tela Importação. |  | a validar |
| `Inseridos` | Inseridos | int | não |  |  |  | Tela Importação. |  | a validar |
| `Linhas` | Linhas | int | não |  |  |  | Tela Importação. |  | a validar |
| `MarcacaoFinal` | Marcacao final | datetime2(0) | sim |  |  | fuso: sem fuso no modelo | Tela Importação. |  | a validar |
| `MarcacaoInicial` | Marcacao inicial | datetime2(0) | sim |  |  | fuso: sem fuso no modelo | Tela Importação. |  | Período das marcações válidas no arquivo (horário local do porto). |
| `Novos` | Novos | int | não |  |  |  | Tela Importação. |  | a validar |
| `Rejeitados` | Rejeitados | int | não |  |  |  | Tela Importação. |  | a validar |
| `Status` | Situação do lote | nvarchar(32) | não |  |  | Analisado, Confirmado, Cancelado | Tela Importação. |  | a validar |
| `TamanhoBytes` | Tamanho bytes | bigint | não |  |  |  | Tela Importação. |  | a validar |
| `TerminalId` | Terminal | nvarchar(32) | não |  | FK → Terminais |  | Tela Importação. |  | a validar |
| `VersaoLinha` | Versao linha | rowversion | sim |  |  | rowversion (concorrência otimista) | Tela Importação. |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_LotesImportacao_CriadoEm` | CriadoEm | não |  |
| `IX_LotesImportacao_HashSha256_TerminalId_Status` | HashSha256, TerminalId, Status | não |  |
| `IX_LotesImportacao_TerminalId` | TerminalId | não |  |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_LotesImportacao_Formato` | `[Formato] IN (N'Xlsx', N'Csv')` |
| `CK_LotesImportacao_Status` | `[Status] IN (N'Analisado', N'Confirmado', N'Cancelado')` |

## Marcacoes

- **Nome lógico:** Movimentação (marcação → liberação)
- **Entidade (código):** `Marcacao`
- **Descrição (código):** Um movimento real de marcação → liberação de carreta (os 103k registros importados de src/data/datasets/*.json, e os que chegam via integração).
- **Finalidade:** Registro de cada carreta marcada num terminal e de sua liberação. Fonte de todos os indicadores de volume e espera, dos alertas e da Análise 360°.
- **Origem dos registros:** Carga inicial (JSON entregue com o projeto), planilhas dos terminais (Importação) e integração (POST /api/v1/integracao/eventos).
- **Quem cria/atualiza:** Gestor (importação confirmada) e sistemas integrados (API Key)
- **Consumida por:** Visão Geral; Movimentações (lista e Análise 360°); Gargalos e alertas; Relatório do período; Exportação CSV
- **Retenção/histórico:** Sem expurgo (histórico completo).
- **Chave primária:** `MovimentoId`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `MovimentoId` | Movimento (COD_MOVIMENTO) | nvarchar(32) | não |  | PK |  | Planilha / integração |  | a validar |
| `Carga` | Carga | nvarchar(50) | não |  |  |  | Cadastro de convênios (deduzido do nome) |  | a validar |
| `Ciclo` | Ciclo (CICLO) | int | não |  |  |  | Planilha / integração |  | Significado operacional a validar. |
| `CodConvenio` | Código do convênio (COD_CONVENIO) | nvarchar(32) | sim |  |  |  | Planilha; ausente na carga inicial |  | Código do convênio no sistema de origem. Os JSON atuais não trazem esse campo. |
| `Convenio` | Convênio (CONVENIO) | nvarchar(100) | não |  |  |  | Planilha / integração |  | a validar |
| `DataLiberacao` | Data/hora da liberação | datetime2(0) | sim |  |  | fuso: sem fuso no modelo; CK_Marcacoes_Liberacao | Planilha (DATA_LIBERACAO + HORA_LIBERACAO) / integração |  | Nulo enquanto a carreta ainda não foi liberada (evento de integração pendente). |
| `DataMarcacao` | Data/hora da marcação | datetime2(0) | não |  |  | fuso: sem fuso no modelo; CK_Marcacoes_Liberacao | Planilha (DATA_MARCACAO + HORA_MARCACAO) / integração |  | Horário local do porto (sem fuso), igual ao dado de origem. |
| `EsperaHoras` | Espera (h) | decimal(10,2) | sim |  |  | calculada e persistida; `CONVERT(decimal(10,2), ROUND(DATEDIFF(SECOND, [DataMarcacao], [DataLiberacao]) / 3600.0, 2))` | Calculada pelo banco |  | Coluna calculada no banco: (DataLiberacao − DataMarcacao) em horas, arredondada em 2 casas — mesma regra do campo esperaHoras dos JSON. |
| `LinhaOrigem` | Linha da planilha | int | sim |  |  |  | Sistema |  | Linha da planilha de origem (1 = cabeçalho). |
| `LoteImportacaoId` | Lote de importação | int | sim |  | FK → LotesImportacao |  | Sistema |  | Lote de importação que criou o movimento (Origem = Planilha). |
| `Operador` | Operador | nvarchar(50) | não |  |  |  | Cadastro de convênios (deduzido do nome) |  | a validar |
| `Origem` | Origem do registro | nvarchar(32) | não | CargaInicial |  | CargaInicial, Planilha, Integracao | Sistema |  | Carga inicial, planilha importada ou integração. |
| `Senha` | Senha (SENHA) | nvarchar(32) | não |  |  |  | Planilha / integração |  | Significado operacional a validar. |
| `TerminalId` | Terminal | nvarchar(32) | não |  | FK → Terminais |  | Escolhido na importação / informado na integração |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_Marcacoes_Convenio` | Convenio | não |  |
| `IX_Marcacoes_DataMarcacao` | DataMarcacao | não |  |
| `IX_Marcacoes_LoteImportacaoId` | LoteImportacaoId | não |  |
| `IX_Marcacoes_Senha` | Senha | não |  |
| `IX_Marcacoes_TerminalId_DataMarcacao` | TerminalId, DataMarcacao | não |  |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_Marcacoes_Ciclo` | `[Ciclo] >= 0` |
| `CK_Marcacoes_Liberacao` | `[DataLiberacao] IS NULL OR [DataLiberacao] >= [DataMarcacao]` |
| `CK_Marcacoes_Origem` | `[Origem] IN (N'CargaInicial', N'Planilha', N'Integracao')` |

## Ocorrencias

- **Nome lógico:** Ocorrência (Matriz de Disciplina N1/N2/N3)
- **Entidade (código):** `Ocorrencia`
- **Finalidade:** Infração registrada contra uma placa. N3 negativa automaticamente o veículo (e o condutor, se identificado); N1 e N2 não bloqueiam.
- **Origem dos registros:** Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe.
- **Quem cria/atualiza:** Operador ou Gestor
- **Consumida por:** Tela Negativação; Portal do Transportador (ocorrências da própria transportadora); Frota (situação)
- **Retenção/histórico:** Sem expurgo.
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | int | não |  | PK | identidade (gerado pelo banco) | Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe. |  | a validar |
| `CondutorId` | Condutor | int | sim |  | FK → Condutores |  | Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe. |  | Condutor identificado na ocorrência (opcional). Substitui o antigo cpfMotorista do frontend: quando presente, o N3 bloqueia também o condutor. |
| `CriadoEm` | Registrada em | datetime2 | não |  |  | fuso: UTC | Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe. |  | a validar |
| `Descricao` | Descrição da infração | nvarchar(2000) | não |  |  |  | Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe. | Texto livre | a validar |
| `Local` | Local | nvarchar(150) | sim |  |  |  | Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe. |  | a validar |
| `Nivel` | Nível (N1/N2/N3) | nvarchar(32) | não |  |  | N1, N2, N3 | Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe. |  | a validar |
| `Placa` | Placa | nvarchar(10) | não |  |  |  | Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe. | Dado pessoal indireto | a validar |
| `Responsavel` | Agente responsável | nvarchar(150) | sim |  |  |  | Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe. | Dado pessoal (nome) | a validar |
| `Status` | Situação da ocorrência | nvarchar(32) | não |  |  | Ativa, Contestada, Resolvida | Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe. |  | a validar |
| `TransportadoraId` | Transportadora | int | não |  | FK → Transportadoras |  | Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe. |  | a validar |
| `VersaoLinha` | Versao linha | rowversion | sim |  |  | rowversion (concorrência otimista) | Tela Negativação (registro manual). Hoje só registros fictícios de demonstração; o app de fiscalização ainda não existe. |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_Ocorrencias_CondutorId` | CondutorId | não |  |
| `IX_Ocorrencias_CriadoEm` | CriadoEm | não |  |
| `IX_Ocorrencias_Placa` | Placa | não |  |
| `IX_Ocorrencias_TransportadoraId_Nivel_CriadoEm` | TransportadoraId, Nivel, CriadoEm | não |  |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_Ocorrencias_Nivel` | `[Nivel] IN (N'N1', N'N2', N'N3')` |
| `CK_Ocorrencias_Status` | `[Status] IN (N'Ativa', N'Contestada', N'Resolvida')` |

## ParametrosOperacionais

- **Nome lógico:** Parâmetros operacionais
- **Entidade (código):** `ParametrosOperacionais`
- **Descrição (código):** Limites usados pelos alertas e indicadores. Uma única linha (Id = 1), editável pelo Gestor em Configurações. Os valores iniciais são pontos de partida, não normas do porto.
- **Finalidade:** Limites e metas usados pelos indicadores e alertas (linha única).
- **Origem dos registros:** Valores iniciais da migration; alterados pelo Gestor em Configurações › Limites e metas.
- **Quem cria/atualiza:** Gestor
- **Consumida por:** Indicadores; Alertas; Estimativa das próximas horas; Análise 360°
- **Retenção/histórico:** Linha única; histórico de alterações na auditoria.
- **Chave primária:** `Id`
- **Dados iniciais (migration):** 1 linha(s)

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | int | não |  | PK | CK_Parametros_Unico | Valores iniciais da migration; alterados pelo Gestor em Configurações › Limites e metas. |  | a validar |
| `AtualizadoEm` | Atualizado em | datetime2 | sim |  |  | fuso: UTC | Valores iniciais da migration; alterados pelo Gestor em Configurações › Limites e metas. |  | a validar |
| `AtualizadoPor` | Atualizado por | nvarchar(200) | sim |  |  |  | Valores iniciais da migration; alterados pelo Gestor em Configurações › Limites e metas. | Dado pessoal (nome e e-mail) | a validar |
| `DiasSemDadosAlerta` | Dias sem dados para alertar | int | não |  |  | CK_Parametros_Faixas | Valores iniciais da migration; alterados pelo Gestor em Configurações › Limites e metas. |  | Dias sem marcação nova a partir dos quais o terminal aparece como "dados desatualizados". |
| `LimiteEsperaLongaHoras` | Espera longa (h) | float | não |  |  | CK_Parametros_Faixas | Valores iniciais da migration; alterados pelo Gestor em Configurações › Limites e metas. |  | Espera individual (h) a partir da qual a carreta entra em "esperas longas". |
| `LimiteEsperaMedianaHoras` | Limite da espera mediana (h) | float | não |  |  | CK_Parametros_Faixas | Valores iniciais da migration; alterados pelo Gestor em Configurações › Limites e metas. |  | Espera mediana (h) acima da qual o terminal gera alerta. |
| `LimiteRazaoChegadaLiberacao` | Limite de risco de fila (×) | float | não |  |  | CK_Parametros_Faixas | Valores iniciais da migration; alterados pelo Gestor em Configurações › Limites e metas. |  | Razão chegadas ÷ liberações numa faixa horária que marca risco de fila. |
| `MetaLiberadas24hPct` | Meta de liberadas em até 24 h (%) | float | não |  |  | CK_Parametros_Faixas | Valores iniciais da migration; alterados pelo Gestor em Configurações › Limites e metas. |  | Meta de carretas liberadas em até 24 h (%). |
| `VersaoLinha` | Versao linha | rowversion | sim |  |  | rowversion (concorrência otimista) | Valores iniciais da migration; alterados pelo Gestor em Configurações › Limites e metas. |  | a validar |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_Parametros_Faixas` | `[LimiteEsperaMedianaHoras] > 0 AND [MetaLiberadas24hPct] BETWEEN 0 AND 100 AND [LimiteEsperaLongaHoras] > 0 AND [DiasSemDadosAlerta] > 0 AND [LimiteRazaoChegadaLiberacao] > 0` |
| `CK_Parametros_Unico` | `[Id] = 1` |

## Terminais

- **Nome lógico:** Terminal portuário
- **Entidade (código):** `Terminal`
- **Descrição (código):** Terminal portuário. O Id é o mesmo slug usado no frontend: "unitapajos", "tgpm", "hidrovias".
- **Finalidade:** Terminais atendidos (Unitapajós, TGPM, Hidrovias) e suas capacidades quando informadas.
- **Origem dos registros:** Migration (cadastro inicial); capacidade informada pelo Gestor.
- **Quem cria/atualiza:** Gestor (capacidade)
- **Consumida por:** Todas as telas com filtro de terminal; Indicadores (ocupação); Agendamentos; Vínculo de usuários
- **Retenção/histórico:** Cadastro permanente.
- **Chave primária:** `Id`
- **Dados iniciais (migration):** 3 linha(s)

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Identificador (slug) | nvarchar(32) | não |  | PK |  | Migration (cadastro inicial); capacidade informada pelo Gestor. |  | a validar |
| `CapacidadeDiariaCarretas` | Capacidade diária (carretas) | int | sim |  |  | CK_Terminais_Capacidade | Migration (cadastro inicial); capacidade informada pelo Gestor. |  | Carretas atendidas por dia. Nulo = ainda não informada pelo porto. |
| `CapacidadeHoraria` | Capacidade por hora (carretas) | int | sim |  |  |  | Migration (cadastro inicial); capacidade informada pelo Gestor. |  | Carretas por janela de 1 hora (agendamentos). Nulo = não configurada. |
| `Nome` | Nome do terminal | nvarchar(100) | não |  | único |  | Migration (cadastro inicial); capacidade informada pelo Gestor. |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_Terminais_Nome` | Nome | sim |  |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_Terminais_Capacidade` | `[CapacidadeDiariaCarretas] IS NULL OR [CapacidadeDiariaCarretas] > 0` |
| `CK_Terminais_CapacidadeHoraria` | `[CapacidadeHoraria] IS NULL OR [CapacidadeHoraria] > 0` |

## Transportadoras

- **Nome lógico:** Transportadora
- **Entidade (código):** `Transportadora`
- **Descrição (código):** Transportadora. Não existe coluna de status de negativação aqui de propósito: ela é "negativada" se tiver ≥1 veículo negativado — sempre calculado (ver CalcularStatusTransportadora).
- **Finalidade:** Empresa dona das carretas. Situação de negativação é calculada (negativada se tiver ao menos um veículo negativado).
- **Origem dos registros:** Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração.
- **Quem cria/atualiza:** Operador ou Gestor; exclusão só Gestor.
- **Consumida por:** Tela Cadastros; Negativação; Agendamentos; Portal do Transportador (escopo do usuário)
- **Retenção/histórico:** Sem expurgo.
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | int | não |  | PK | identidade (gerado pelo banco) | Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração. |  | a validar |
| `Cnpj` | CNPJ | nvarchar(18) | não |  | único |  | Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração. |  | Dado de pessoa jurídica (validado por dígito verificador). |
| `Nome` | Razão social / nome | nvarchar(150) | não |  | único |  | Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração. |  | a validar |
| `VersaoLinha` | Versao linha | rowversion | sim |  |  | rowversion (concorrência otimista) | Cadastro manual (tela Cadastros). Hoje só registros fictícios de demonstração. |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_Transportadoras_Cnpj` | Cnpj | sim |  |
| `IX_Transportadoras_Nome` | Nome | sim |  |

## Usuarios

- **Nome lógico:** Usuário
- **Entidade (código):** `Usuario`
- **Finalidade:** Contas internas (Operador, Gestor, Auditor) e do Portal (Transportadora): credenciais, MFA, papel, vínculo de terminal e preferências.
- **Origem dos registros:** Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development.
- **Quem cria/atualiza:** Gestor; o próprio usuário altera senha, MFA e preferências.
- **Consumida por:** Login e sessão; Configurações › Acesso por terminal; Minhas preferências; Auditoria (autor)
- **Retenção/histórico:** Desativação em vez de exclusão.
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | int | não |  | PK | identidade (gerado pelo banco) | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | a validar |
| `Ativo` | Ativo | bit | não |  |  |  | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | a validar |
| `BloqueadoAte` | Bloqueado até | datetime2 | sim |  |  | fuso: UTC | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | Conta bloqueada por força bruta até este instante (UTC). |
| `DeveTrocarSenha` | Senha provisória | bit | não |  |  |  | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | Senha provisória: o token só serve pra trocar a senha. |
| `Email` | E-mail (login) | nvarchar(200) | não |  | único |  | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. | Dado pessoal | a validar |
| `FalhasLoginConsecutivas` | Falhas de login seguidas | int | não |  |  | CK_Usuarios_Falhas | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. | Dado de segurança | Tentativas erradas seguidas; zera no login bem-sucedido. |
| `MfaAtivo` | MFA ativo | bit | não |  |  |  | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | a validar |
| `MfaSegredoCifrado` | Segredo do MFA (cifrado) | nvarchar(500) | sim |  |  |  | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. | Credencial — nunca exposta pela API | Segredo TOTP cifrado (AES-GCM). Presente com MfaAtivo=false = cadastro pendente. |
| `MfaUltimoPassoUsado` | Último código MFA aceito | bigint | sim |  |  |  | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. | Dado de segurança | Último passo TOTP aceito — impede reutilizar o mesmo código. |
| `Nome` | Nome | nvarchar(150) | não |  |  |  | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. | Dado pessoal | a validar |
| `Papel` | Perfil | nvarchar(32) | não |  |  | Operador, Gestor, Auditor, Transportadora; CK_Usuarios_EscopoTransportadora; CK_Usuarios_TerminalSoInterno | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | a validar |
| `SenhaHash` | Hash da senha | nvarchar(500) | não |  |  |  | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. | Credencial — nunca exposta pela API | a validar |
| `TelaInicial` | Tela ao entrar (preferência) | nvarchar(40) | sim |  |  |  | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | Preferência pessoal: tela aberta após o login (chave da tela). Null = padrão do papel. |
| `TerminalId` | Terminal de acesso (vínculo) | nvarchar(32) | sim |  | FK → Terminais | CK_Usuarios_TerminalSoInterno | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | Terminal ao qual o usuário interno está vinculado. Null = vê todos os terminais. Definido só pelo Gestor; restringe no servidor todas as consultas por terminal. — Permissão, definida pelo Gestor. |
| `TerminalPreferido` | Terminal preferido (preferência) | nvarchar(32) | sim |  |  |  | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | Preferência pessoal: terminal já selecionado nos filtros. Nunca amplia o escopo (validado contra TerminalId). |
| `TransportadoraId` | Transportadora (usuário do Portal) | int | sim |  | FK → Transportadoras | CK_Usuarios_EscopoTransportadora | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | Obrigatório (e só permitido) para o papel Transportadora — define o escopo de dados do Portal. |
| `UltimoLoginEm` | Último login | datetime2 | sim |  |  | fuso: UTC | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | a validar |
| `VersaoLinha` | Versao linha | rowversion | sim |  |  | rowversion (concorrência otimista) | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. |  | a validar |
| `VersaoToken` | Versão do token | int | não | 1 |  |  | Criado pelo Gestor (tela/endpoint de usuários) ou pela CLI do servidor; seed de usuários de teste só em Development. | Dado de segurança | Vai no JWT (claim "ver"). Incrementar invalida todos os tokens já emitidos: logout, troca/redefinição de senha, desativação, MFA. |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_Usuarios_Email` | Email | sim |  |
| `IX_Usuarios_TerminalId` | TerminalId | não |  |
| `IX_Usuarios_TransportadoraId` | TransportadoraId | não |  |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_Usuarios_EscopoTransportadora` | `([Papel] = N'Transportadora' AND [TransportadoraId] IS NOT NULL) OR ([Papel] <> N'Transportadora' AND [TransportadoraId] IS NULL)` |
| `CK_Usuarios_Falhas` | `[FalhasLoginConsecutivas] >= 0` |
| `CK_Usuarios_Papel` | `[Papel] IN (N'Operador', N'Gestor', N'Auditor', N'Transportadora')` |
| `CK_Usuarios_TerminalSoInterno` | `[Papel] <> N'Transportadora' OR [TerminalId] IS NULL` |

## Veiculos

- **Nome lógico:** Veículo (carreta)
- **Entidade (código):** `Veiculo`
- **Descrição (código):** Carreta. É o VEÍCULO (placa) que é negativado, não a transportadora.
- **Finalidade:** Carreta identificada pela placa: transportadora, terminal, situação de negativação e situação de portaria.
- **Origem dos registros:** Cadastro manual (tela Cadastros); situação de portaria informada manualmente na Portaria. Hoje só registros fictícios de demonstração.
- **Quem cria/atualiza:** Operador ou Gestor
- **Consumida por:** Portaria; Frota; Cadastros; Negativação; Agendamentos (validação da placa); Portal do Transportador
- **Retenção/histórico:** Sem expurgo.
- **Chave primária:** `Id`

| Campo | Nome amigável | Tipo | Nulo | Padrão | Chave | Domínio / regra | Origem | Sensibilidade | Descrição |
|---|---|---|---|---|---|---|---|---|---|
| `Id` | Id | int | não |  | PK | identidade (gerado pelo banco) | Cadastro manual (tela Cadastros); situação de portaria informada manualmente na Portaria. Hoje só registros fictícios de demonstração. |  | a validar |
| `Modelo` | Modelo | nvarchar(100) | sim |  |  |  | Cadastro manual (tela Cadastros); situação de portaria informada manualmente na Portaria. Hoje só registros fictícios de demonstração. |  | a validar |
| `Placa` | Placa | nvarchar(10) | não |  | único |  | Cadastro manual (tela Cadastros); situação de portaria informada manualmente na Portaria. Hoje só registros fictícios de demonstração. | Dado pessoal indireto | a validar |
| `StatusNegativacao` | Situação de negativação | nvarchar(32) | não |  |  | Regular, Negativada | Cadastro manual (tela Cadastros); situação de portaria informada manualmente na Portaria. Hoje só registros fictícios de demonstração. |  | a validar |
| `StatusPortaria` | Situação na portaria (informada) | nvarchar(32) | não |  |  | NoPatio, Aguardando, NoPorto, DescargaFinalizada, ForaDoPorto | Cadastro manual (tela Cadastros); situação de portaria informada manualmente na Portaria. Hoje só registros fictícios de demonstração. |  | Registrada manualmente; não há leitura de placa, cancela ou checkpoint integrado. |
| `StatusPortariaDesde` | Na situação desde | datetime2 | não |  |  | fuso: UTC | Cadastro manual (tela Cadastros); situação de portaria informada manualmente na Portaria. Hoje só registros fictícios de demonstração. |  | a validar |
| `TerminalId` | Terminal | nvarchar(32) | não |  | FK → Terminais |  | Cadastro manual (tela Cadastros); situação de portaria informada manualmente na Portaria. Hoje só registros fictícios de demonstração. |  | a validar |
| `TransportadoraId` | Transportadora | int | não |  | FK → Transportadoras |  | Cadastro manual (tela Cadastros); situação de portaria informada manualmente na Portaria. Hoje só registros fictícios de demonstração. |  | a validar |
| `VersaoLinha` | Versao linha | rowversion | sim |  |  | rowversion (concorrência otimista) | Cadastro manual (tela Cadastros); situação de portaria informada manualmente na Portaria. Hoje só registros fictícios de demonstração. |  | a validar |

**Índices**

| Nome | Colunas | Único | Filtro |
|---|---|---|---|
| `IX_Veiculos_Placa` | Placa | sim |  |
| `IX_Veiculos_StatusNegativacao` | StatusNegativacao | não |  |
| `IX_Veiculos_StatusPortaria_StatusPortariaDesde` | StatusPortaria, StatusPortariaDesde | não |  |
| `IX_Veiculos_TerminalId` | TerminalId | não |  |
| `IX_Veiculos_TransportadoraId` | TransportadoraId | não |  |

**Restrições (CHECK)**

| Nome | Regra |
|---|---|
| `CK_Veiculos_StatusNegativacao` | `[StatusNegativacao] IN (N'Regular', N'Negativada')` |
| `CK_Veiculos_StatusPortaria` | `[StatusPortaria] IN (N'NoPatio', N'Aguardando', N'NoPorto', N'DescargaFinalizada', N'ForaDoPorto')` |
