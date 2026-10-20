#!/usr/bin/env bash
# BarcaLog — sobe o ambiente local completo (Linux, macOS ou WSL).
#
#   ./scripts/iniciar-local.sh            SQL Server (Docker) + API + frontend
#   ./scripts/iniciar-local.sh --sem-front   só banco + API
#
# O que faz, nesta ordem:
#   1. confere .NET 8, Node >= 20.19 e Docker;
#   2. cria backend/.env com uma senha aleatória do SQL Server (só na 1ª vez;
#      o arquivo é ignorado pelo Git);
#   3. sobe o SQL Server com o backend/docker-compose.yml e espera ficar pronto;
#   4. sobe a API em Development (aplica migrations, cria usuários de teste e
#      importa os datasets reais na primeira vez — leva ~1 min);
#   5. sobe o frontend em http://localhost:5173.
# Ctrl+C encerra API e frontend; o banco continua rodando (docker compose down para parar).
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BACKEND="$RAIZ/backend"
LOGS="$RAIZ/.logs"
API_URL="http://localhost:5080"
SEM_FRONT=0
[[ "${1:-}" == "--sem-front" ]] && SEM_FRONT=1

falha() { echo "ERRO: $*" >&2; exit 1; }
passo() { echo; echo "==> $*"; }

passo "Conferindo pré-requisitos"
command -v dotnet >/dev/null || falha ".NET 8 SDK não encontrado. Instale em https://dotnet.microsoft.com/download/dotnet/8.0"
dotnet --list-sdks | grep -q '^8\.' || falha "É preciso o SDK 8.x (encontrado: $(dotnet --list-sdks | tr '\n' ' '))"
command -v docker >/dev/null || falha "Docker não encontrado (usado para o SQL Server)."
docker info >/dev/null 2>&1 || falha "O Docker está instalado mas não está rodando."
if [[ $SEM_FRONT -eq 0 ]]; then
  command -v node >/dev/null || falha "Node.js não encontrado (precisa >= 20.19)."
  node -e 'const [a,b]=process.versions.node.split(".").map(Number); process.exit(a>20||(a===20&&b>=19)?0:1)' \
    || falha "Node $(node -v) é antigo; o Vite 7 exige >= 20.19."
fi
echo "ok: dotnet $(dotnet --version), docker $(docker --version | cut -d' ' -f3 | tr -d ,)"

passo "Configuração local (backend/.env)"
if [[ ! -f "$BACKEND/.env" ]]; then
  # od lê 12 bytes e termina sozinho (tr|head quebraria com pipefail/SIGPIPE).
  SENHA="Bl_$(od -An -N12 -tx1 /dev/urandom | tr -d ' \n')#9A"
  printf 'MSSQL_SA_PASSWORD=%s\n' "$SENHA" > "$BACKEND/.env"
  chmod 600 "$BACKEND/.env"
  echo "criado com senha aleatória (não versionado)"
else
  echo "já existe — mantido"
fi
set -a; source "$BACKEND/.env"; set +a

passo "SQL Server"
(cd "$BACKEND" && docker compose up -d >/dev/null 2>&1) || falha "docker compose up falhou — rode: cd backend && docker compose up -d"
for i in $(seq 1 60); do
  if docker exec barcalog-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" >/dev/null 2>&1; then
    echo "pronto"; break
  fi
  [[ $i -eq 60 ]] && falha "SQL Server não respondeu em 2 min. Veja: docker logs barcalog-sql"
  sleep 2
done

export ConnectionStrings__BarcaLog="Server=localhost,1433;Database=BarcaLog;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True"
export ASPNETCORE_ENVIRONMENT=Development
mkdir -p "$LOGS"
PIDS=()
encerrar() { echo; echo "Encerrando API/frontend…"; for p in "${PIDS[@]}"; do kill "$p" 2>/dev/null || true; done; }
trap encerrar EXIT INT TERM

passo "API (.NET) — log em .logs/api.log"
(cd "$BACKEND" && dotnet tool restore >/dev/null && dotnet build src/BarcaLog.Api -v quiet -nologo >/dev/null) \
  || falha "build da API falhou — rode: cd backend && dotnet build"
(cd "$BACKEND/src/BarcaLog.Api" && exec dotnet run --no-build --urls "$API_URL") > "$LOGS/api.log" 2>&1 &
PIDS+=($!)
echo -n "aguardando $API_URL/health/ready (1ª vez importa ~103 mil registros)"
for i in $(seq 1 180); do
  curl -sf "$API_URL/health/ready" >/dev/null 2>&1 && { echo " — pronta"; break; }
  kill -0 "${PIDS[0]}" 2>/dev/null || { echo; tail -30 "$LOGS/api.log"; falha "a API parou ao subir (log acima)."; }
  [[ $i -eq 180 ]] && falha "API não ficou pronta em 6 min. Veja .logs/api.log"
  echo -n "."; sleep 2
done

if [[ $SEM_FRONT -eq 0 ]]; then
  passo "Frontend — log em .logs/front.log"
  [[ -f "$RAIZ/.env.local" ]] || echo "VITE_API_URL=$API_URL" > "$RAIZ/.env.local"
  [[ -d "$RAIZ/node_modules" ]] || (cd "$RAIZ" && npm ci --no-audit --no-fund)
  (cd "$RAIZ" && exec npx vite --port 5173 --strictPort) > "$LOGS/front.log" 2>&1 &
  PIDS+=($!)
  for i in $(seq 1 30); do curl -sf http://localhost:5173 >/dev/null 2>&1 && break; sleep 1; done
fi

cat <<EOF

BarcaLog rodando
  Sistema:  http://localhost:5173
  API:      $API_URL/swagger
  Saúde:    $API_URL/health/ready
  Usuários de teste (senha BarcaLog@2026, só em Development):
            gestor@barcalog.local · operador@barcalog.local · auditor@barcalog.local
            Portal do Transportador: norte@transportadora.local · agro@transportadora.local
Ctrl+C para encerrar API e frontend.
EOF
wait
