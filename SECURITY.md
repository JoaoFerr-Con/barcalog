# Segurança — BarcaLog

Documento vivo. Descreve o modelo de ameaças, os controles implementados, as
decisões (e o porquê), os riscos que **ainda existem** e como reportar
problemas. Toda mudança que afete segurança deve atualizar este arquivo.

## Como reportar uma vulnerabilidade

Não abra issue pública. Envie para o responsável técnico do projeto
(e-mail do mantenedor no GitHub) com passos para reproduzir. Resposta em até
3 dias úteis.

## Modelo de ameaças (resumo)

| Ativo | Quem ameaça | Impacto |
|---|---|---|
| Status de negativação das carretas (N3 = bloqueio) | Transportadora querendo se desbloquear; operador mal-intencionado | Carreta irregular operando; prejuízo e responsabilidade do porto |
| Log de auditoria | Qualquer um querendo apagar rastros | Perda de prova / não conformidade |
| Contas de Gestor | Atacante externo (força bruta, phishing) | Controle total do sistema |
| Integração (API key) | Sistema comprometido, chave vazada | Injeção de marcações falsas nas métricas |
| Dados pessoais (nome de condutores, e-mail de usuários) | Vazamento | LGPD — incidente reportável à ANPD |
| Disponibilidade | Abuso / DoS de aplicação | Portaria e agendamento parados |

## Controles implementados

### Autenticação
- **Senhas**: PBKDF2-HMAC-SHA512, salt aleatório de 128 bits, **210.000 iterações** (mínimo OWASP 2023 para SHA-512), via `PasswordHasher` do ASP.NET Core Identity. Hashes antigos são regravados no login (`SuccessRehashNeeded`).
- **Política de senha** (NIST SP 800-63B): 12–128 caracteres, fora de lista de senhas comuns, não pode conter nome/e-mail. Sem regra de "símbolo obrigatório" (NIST desaconselha).
- **Força bruta**: 5 falhas seguidas bloqueiam a conta por 15 min. O contador é um `UPDATE ... SET Falhas = Falhas + 1` atômico — tentativas em paralelo não escapam da contagem (bug encontrado e corrigido nos testes). Mais rate limit de 10 tentativas/min por IP em login, troca de senha e MFA.
- **Enumeração de usuários**: e-mail inexistente, senha errada, conta inativa e conta bloqueada devolvem a mesma resposta; e-mail inexistente ainda verifica um hash fictício para o tempo de resposta ser parecido.
- **MFA (TOTP, RFC 6238)**: implementado com a BCL (sem pacote de terceiros), testado contra os vetores oficiais da RFC. Segredo cifrado no banco com **AES-256-GCM**. Código já usado não vale de novo (proteção contra replay, atômica). **Obrigatório para Gestor em produção**: Gestor sem MFA recebe token restrito que só serve para cadastrá-lo.
- **Recuperação de senha**: sem e-mail por enquanto — o Gestor gera uma senha provisória aleatória (mostrada uma vez) e o usuário é obrigado a trocá-la no próximo acesso (token restrito, 15 min).

### Sessão e tokens
- JWT HS256 (algoritmo fixo: `alg: none` e troca de algoritmo são recusados — testado), emissor/audiência validados, expiração de 8h (um turno).
- **Revogação imediata**: o token carrega `ver` (VersaoToken). Logout, troca/redefinição de senha, troca de papel, desativação e alterações de MFA incrementam a versão; o token antigo deixa de valer na próxima requisição (cache de 30 s por usuário, invalidado na hora na mesma instância).
- **Cookies/CSRF**: a API não usa cookies — o token vai no cabeçalho `Authorization`. Sem credencial automática do navegador não há CSRF. **Decisão para o frontend** (implementada em `src/api/cliente.js`): token **em memória** + `sessionStorage` (sobrevive a F5, some ao fechar a aba), nunca em `localStorage`, e manter CSP rígida para reduzir o risco de XSS roubar o token. Alternativa considerada: cookie `HttpOnly; Secure; SameSite=Strict` — exige API e frontend no mesmo site (ex.: `api.barcalog.com.br` + `app.barcalog.com.br`); como o frontend está na Vercel em outro domínio, o cookie seria `SameSite=None` e aí passaria a precisar de proteção CSRF. Se os domínios forem unificados, migrar para cookie é recomendado.

### Autorização
- Toda regra está no backend. Política padrão **fechada**: endpoint sem `[Authorize]` explícito exige login (`FallbackPolicy`).
- Papéis: Auditor (leitura), Operador (leitura + escrita), Gestor (tudo + remoções, importação, usuários). Testes cobrem cada negação (403).
- Token restrito (senha provisória / MFA pendente) só acessa `me`, `logout`, `trocar-senha` e `mfa/*`.
- Gestor não pode desativar/rebaixar a si mesmo nem o último Gestor ativo.
- **IDOR/BOLA**: funcionários do porto têm visão global por papel. Usuários do **Portal** (papel `Transportadora`) só acessam `/api/v1/portal/*`, e o backend força o filtro pelo `TransportadoraId` do token (claim `transportadora`); ocorrência de outra empresa devolve 404. Eles não passam na política `Leitura` dos endpoints internos (testado).
- Integração: API key autentica **só** `/api/v1/integracao/*`; não abre endpoints de usuário e JWT não abre integração (testado).

### Entrada e saída
- DTOs com validação declarativa (tamanho, formato, faixa), placas (padrão antigo e Mercosul), **CNPJ com dígito verificador, inclusive o CNPJ alfanumérico** (IN RFB 2.229/2024, em vigor desde jul/2026), texto sem caracteres de controle, enum só por nome (`"nivel": 7` é recusado), período `de ≤ ate`, busca com tamanho mínimo/máximo.
- **SQL Injection**: só EF Core com parâmetros; nenhuma SQL concatenada com entrada do usuário. Testes enviam payloads clássicos.
- **XSS**: a API só devolve JSON, com `Content-Security-Policy: default-src 'none'` e `nosniff`; o React escapa por padrão; o frontend não usa `dangerouslySetInnerHTML`. Exportação CSV do frontend neutraliza **injeção de fórmula** (`=`, `+`, `-`, `@`).
- **SSRF**: o backend não faz requisições para URLs vindas do usuário.
- **Erros**: ProblemDetails (RFC 7807) com mensagem de negócio ou genérica + `traceId`; sem stack trace, SQL ou nome de tipo .NET (`AllowInputFormatterExceptionMessages = false`). O detalhe fica só no log do servidor.
- Limite de corpo 1 MB (2 MB na integração, 51 MB no upload), checado por `Content-Length` antes de ler e também no Kestrel; profundidade máxima de JSON; timeout de 30 s por requisição (5 min na importação).

### Upload (importação de marcações)
- Somente Gestor, com rate limit próprio (6/h). Extensão `.json`, content-type JSON, até 50 MB, conteúdo precisa começar com array.
- O arquivo **não é gravado em disco**: é lido em streaming, validado registro a registro (tamanhos, datas, liberação ≥ marcação) e registros inválidos são rejeitados e listados no resultado (não descartados em silêncio). Nome do arquivo reduzido ao nome base (sem caminho).
- Idempotente (reenviar não duplica) e uma importação por vez.

### Abuso e disponibilidade
- Rate limiting nativo do ASP.NET Core: geral 300/min por usuário, login 10/min por IP, integração 120/min por sistema, importação 6/h. Resposta 429 com `Retry-After`.
- Atrás de proxy/CDN, habilite `Proxy:Habilitado` e `Proxy:RedesConfiaveis`; sem isso, todos os clientes aparecem com o IP do proxy (rate limit por IP perde efeito) — e só redes confiáveis podem definir `X-Forwarded-For` (senão qualquer um forja o IP).

### Concorrência e duplicidade
- **Idempotência**: `Idempotency-Key` em todas as operações que criam/alteram estado relevantes. Repetição devolve a mesma resposta; corpo diferente → 422; em andamento → 409. Reserva feita por índice único (sem corrida).
- **Concorrência otimista**: `rowversion` em transportadoras, veículos, condutores, ocorrências, contestações, agendamentos e usuários — duas edições simultâneas: a segunda recebe 409 em vez de sobrescrever.
- **Índices únicos filtrados**: uma só contestação pendente por ocorrência; mesma carreta não tem dois agendamentos ativos no mesmo horário. Testado com requisições paralelas.

### Segredos
- Nenhum segredo de produção no código. Dev: valores com prefixo `DEV-ONLY`, públicos de propósito.
- **Trava de subida**: em Production a API não inicia com chave JWT/AES/API key de desenvolvimento, sem MFA obrigatório para Gestor, com seed de usuários/dados de exemplo ou com CORS sem HTTPS.
- API keys de integração guardadas **só como hash SHA-256** (`dotnet run -- gerar-api-key <sistema>` gera a chave e o hash). Comparação em tempo constante.
- `.gitignore` bloqueia `.env*`, chaves e `appsettings.*.local.json`.

### Logs e auditoria
- Log estruturado (JSON fora de dev) com `request_id`, usuário (id), **template da rota** (não a URL real nem query string — evita placa e texto de busca no log), status e duração. Headers (`Authorization`, `X-Api-Key`) e corpos nunca são logados. Verificado: 0 ocorrências de senha, token ou chave no log de uma sessão real.
- Auditoria de negócio automática (interceptor do EF Core, mesma transação da mudança); campos sensíveis (`SenhaHash`, segredo MFA) aparecem só como "alterado". Eventos de login (sucesso, falha, bloqueio) também vão para a auditoria.
- Script de banco nega `UPDATE`/`DELETE` na tabela de auditoria para o usuário da aplicação.

### Dependências
- `dotnet list package --vulnerable --include-transitive`: **zero** (os testes usavam xunit antigo que puxava `System.Net.Http 4.3.0` e `Regex 4.3.0` com CVEs altos — atualizado). O CI falha se aparecer vulnerabilidade alta/crítica.
- Frontend: `npm audit` com **zero** vulnerabilidades. O Vite 5 tinha 4 falhas do servidor de desenvolvimento (uma alta: `server.fs.deny` bypass). Em vez de `npm audit fix --force` (que pularia para o Vite 8), atualizei para o Vite 7.3.6 + `@vitejs/plugin-react` 5.2, validei o `npm run build` e que o HTML gerado segue compatível com a CSP (sem script inline nem `eval`). Exige Node ≥ 20.19 (`engines` no `package.json`). `package-lock.json` agora é versionado.

## Decisões registradas

**PBKDF2 em vez de Argon2id/bcrypt.** Argon2id é o primeiro da lista da OWASP, mas no .NET exige pacote de terceiros (ex.: Konscious/Isopoh) — mais uma dependência dentro do caminho mais sensível do sistema. PBKDF2-SHA512 com 210k iterações está na lista de aceitáveis da OWASP, é FIPS-140, mantido pela Microsoft e já está no framework. O formato do hash é versionado e o rehash automático permite migrar para Argon2id no futuro sem resetar senhas. Se houver requisito formal de Argon2id, trocar `HashSenha` é local e testado.

**MFA próprio (TOTP) em vez de biblioteca.** ~60 linhas da BCL, validadas contra os vetores da RFC 6238.

**Token de 8h com revogação por versão, sem refresh token.** Um turno inteiro sem relogar; o risco de token longo é mitigado pela revogação imediata. Refresh token rotativo acrescentaria tabela, fluxo e superfície de ataque sem ganho claro hoje.

## Riscos que permanecem (e o que fazer)

| Risco | Gravidade | Mitigação atual | Próximo passo |
|---|---|---|---|
| Token JWT acessível ao JavaScript (`sessionStorage`) | Média | CSP rígida, sem HTML dinâmico, token some ao fechar a aba, logout revoga no servidor | Unificar domínios e migrar para cookie `HttpOnly; SameSite=Strict` |
| Anexos de contestação (GED) não implementados | Baixa | Portal orienta a enviar documentos pelo canal oficial | Upload com validação de tipo/tamanho, antivírus e armazenamento fora do banco |
| Recuperação de senha depende do Gestor (sem e-mail) | Média | Senha provisória + troca obrigatória | Fluxo por e-mail com token de uso único e expiração curta |
| Revogação leva até 30 s em outras instâncias | Baixa | Cache curto | Redis/backplane se houver várias instâncias |
| Rate limit e bloqueio de importação são por instância | Baixa | Bloqueio de conta é no banco (global) | Rate limit distribuído (Redis) com várias instâncias |
| Sem monitoramento/alertas externos (APM) | Média | Logs JSON + health checks + request id | Enviar logs para um agregador e criar alertas (5xx, 429, bloqueios de conta) |
| Chaves (JWT/AES) sem rotação automatizada | Média | Chaves fora do código | Cofre de segredos (Key Vault/Secrets Manager) e procedimento de rotação; o formato `v1:` do AES já permite duas chaves |
| Auditoria guarda nomes/e-mails por tempo indeterminado | Média (LGPD) | Acesso só por papel | Definir prazo de retenção com o jurídico |

## LGPD

Dados pessoais tratados e base legal presumida (**validar com o jurídico/DPO**):

| Dado | Onde | Finalidade | Base legal provável |
|---|---|---|---|
| Nome e e-mail de usuários internos | `Usuarios`, auditoria | Controle de acesso e rastreabilidade | Execução de contrato / legítimo interesse |
| Nome do condutor | `Condutores`, `Agendamentos.Motorista` | Identificação na portaria e nas ocorrências | Legítimo interesse / obrigação regulatória |
| Placa do veículo | vários | Operação portuária (pode ser dado pessoal se o dono for pessoa física) | Legítimo interesse |

Minimização: **CPF foi removido** e não é coletado. Direitos do titular:
`GET /api/v1/condutores/{id}/dados-pessoais` (exportação) e
`POST /api/v1/condutores/{id}/anonimizar` (anonimização irreversível que
preserva o histórico operacional; o log da ação não copia o nome). Acesso a
esses endpoints só por Gestor e registrado em log.

Pendências: política de privacidade, prazo de retenção da auditoria, registro
das operações de tratamento (art. 37) e plano de resposta a incidente (art. 48).
