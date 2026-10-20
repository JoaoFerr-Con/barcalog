# BarcaLog

Sistema de gestão logística do Porto de Barcarena (PA): marcação/liberação de
carretas nos terminais (Unitapajós, TGPM, Hidrovias), indicadores operacionais,
negativação N1/N2/N3, portaria, agendamentos, Portal do Transportador e auditoria.

| Parte | Tecnologia | Pasta |
|---|---|---|
| Frontend | React 18 + Vite 7 + Recharts | `src/` |
| API | ASP.NET Core 8 (camadas Domain/Application/Infrastructure/Api) | `backend/src/` |
| Banco | SQL Server 2022 (EF Core, migrations versionadas) | `backend/src/BarcaLog.Infrastructure/Persistencia/Migrations` |
| Testes | xUnit — unitários e integração contra SQL Server real | `backend/tests/` |

Arquitetura e decisões: [`ARCHITECTURE.md`](ARCHITECTURE.md) · Segurança:
[`SECURITY.md`](SECURITY.md) · API, endpoints e configuração de produção:
[`backend/README.md`](backend/README.md).

## Rodar localmente (um comando)

Pré-requisitos: **.NET 8 SDK**, **Node.js ≥ 20.19** e um SQL Server —
**LocalDB** (Windows, vem com o Visual Studio ou o SQL Server Express) ou
**Docker**.

**Windows (PowerShell)**

```powershell
powershell -ExecutionPolicy Bypass -File scripts\iniciar-local.ps1               # LocalDB se existir, senão Docker
powershell -ExecutionPolicy Bypass -File scripts\iniciar-local.ps1 -Banco Docker
```

**Linux, macOS ou WSL**

```bash
./scripts/iniciar-local.sh          # SQL Server em Docker + API + frontend
./scripts/iniciar-local.sh --sem-front
```

O script confere as versões, sobe o banco, compila e inicia a API em
`Development` e abre o frontend. Na **primeira** subida a API aplica as
migrations, cria os usuários de teste e importa os 103.325 movimentos reais de
`src/data/datasets/` (cerca de 1 minuto). Nas seguintes, sobe em segundos.

| Endereço | O quê |
|---|---|
| http://localhost:5173 | Sistema |
| http://localhost:5080/swagger | Documentação interativa da API |
| http://localhost:5080/health/ready | Saúde da API + banco |

Usuários de teste — senha `BarcaLog@2026`, existem **só** em Development:
`gestor@barcalog.local`, `operador@barcalog.local`, `auditor@barcalog.local`
e, no Portal do Transportador (`/portal`), `norte@transportadora.local` e
`agro@transportadora.local`. O cadastro de transportadoras, carretas,
ocorrências e agendamentos que aparece nesse ambiente é **dado de
demonstração**; as marcações/liberações são reais.

Logs da execução local: `.logs/api.log` e `.logs/front.log`.

### Problemas comuns

| Sintoma | Causa provável |
|---|---|
| "API indisponível" na tela de login | API não subiu — veja `.logs/api.log` ou `http://localhost:5080/health/ready` |
| `health/ready` responde 503 | Banco parado: `sqllocaldb start MSSQLLocalDB` ou `docker start barcalog-sql` |
| Porta 5080 ou 5173 em uso | Outra instância rodando — encerre-a antes |
| `Login failed for user 'sa'` (Docker) | O volume do banco foi criado com outra senha: `cd backend && docker compose down -v` e rode o script de novo (apaga o banco local) |

## Testes

```bash
cd backend
dotnet test tests/BarcaLog.UnitTests            # 106 testes, sem banco
# integração: cria e apaga um banco BarcaLog_Testes_<guid>
export BARCALOG_TESTES_SQL='Server=localhost,1433;User Id=sa;Password=<senha do backend/.env>;TrustServerCertificate=True'
dotnet test tests/BarcaLog.IntegrationTests      # 63 testes
node tools/verificar-fidelidade.mjs             # API rodando: métricas × cálculo JS original (25 endpoints)
```

No Windows, sem `BARCALOG_TESTES_SQL`, a integração usa o LocalDB.

```bash
npm run build                                   # build do frontend
```

## Atualizar

```bash
git pull
npm ci                                          # se package-lock.json mudou
cd backend && dotnet build                      # migrations novas são aplicadas na subida em Development
```
