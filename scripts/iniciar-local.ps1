# BarcaLog — sobe o ambiente local completo no Windows.
#
#   powershell -ExecutionPolicy Bypass -File scripts\iniciar-local.ps1
#   powershell -ExecutionPolicy Bypass -File scripts\iniciar-local.ps1 -Banco Docker
#   powershell -ExecutionPolicy Bypass -File scripts\iniciar-local.ps1 -SemFront
#
# Banco:
#   LocalDB (padrão no Windows) — vem com o Visual Studio ou o instalador do
#   SQL Server Express; usa a connection string de appsettings.Development.json.
#   Docker — SQL Server 2022 em container (backend\docker-compose.yml), com
#   senha aleatória gravada em backend\.env (ignorado pelo Git).
#
# Na primeira subida a API aplica as migrations, cria os usuários de teste e
# importa os datasets reais (~1 min). Enter ou Ctrl+C encerra API e frontend.
[CmdletBinding()]
param(
    [ValidateSet("Auto", "LocalDB", "Docker")] [string]$Banco = "Auto",
    [switch]$SemFront
)
$ErrorActionPreference = "Stop"

$Raiz = Split-Path -Parent $PSScriptRoot
$Backend = Join-Path $Raiz "backend"
$Logs = Join-Path $Raiz ".logs"
$ApiUrl = "http://localhost:5080"

function Falha([string]$msg) { Write-Host "ERRO: $msg" -ForegroundColor Red; exit 1 }
function Passo([string]$msg) { Write-Host ""; Write-Host "==> $msg" -ForegroundColor Cyan }
function Existe([string]$cmd) { [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }

Passo "Conferindo pré-requisitos"
if (-not (Existe "dotnet")) { Falha ".NET 8 SDK não encontrado. Instale em https://dotnet.microsoft.com/download/dotnet/8.0" }
if (-not ((dotnet --list-sdks) -match "^8\.")) { Falha "É preciso o SDK 8.x." }
if (-not $SemFront) {
    if (-not (Existe "node")) { Falha "Node.js não encontrado (precisa >= 20.19)." }
    $v = (node -p "process.versions.node").Split(".") | ForEach-Object { [int]$_ }
    if ($v[0] -lt 20 -or ($v[0] -eq 20 -and $v[1] -lt 19)) { Falha "Node $(node -v) é antigo; o Vite 7 exige >= 20.19." }
}
if ($Banco -eq "Auto") { $Banco = if (Existe "sqllocaldb") { "LocalDB" } elseif (Existe "docker") { "Docker" } else { "" } }
if (-not $Banco) { Falha "Nenhum SQL Server disponível: instale o SQL Server Express LocalDB ou o Docker Desktop." }
Write-Host "ok: dotnet $(dotnet --version), banco: $Banco"

Passo "SQL Server ($Banco)"
if ($Banco -eq "LocalDB") {
    if (-not (Existe "sqllocaldb")) { Falha "sqllocaldb não encontrado. Instale o SQL Server Express LocalDB ou use -Banco Docker." }
    sqllocaldb start MSSQLLocalDB | Out-Null
    if ($LASTEXITCODE -ne 0) { sqllocaldb create MSSQLLocalDB -s | Out-Null }
    if ($LASTEXITCODE -ne 0) { Falha "Não consegui iniciar a instância MSSQLLocalDB." }
    Remove-Item Env:ConnectionStrings__BarcaLog -ErrorAction SilentlyContinue  # usa appsettings.Development.json
    Write-Host "LocalDB (mssqllocaldb) iniciado"
}
else {
    if (-not (Existe "docker")) { Falha "Docker não encontrado." }
    docker info *> $null
    if ($LASTEXITCODE -ne 0) { Falha "O Docker Desktop está instalado mas não está rodando." }
    $envFile = Join-Path $Backend ".env"
    if (-not (Test-Path $envFile)) {
        $bytes = New-Object byte[] 12
        [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
        $senhaNova = "Bl_" + (($bytes | ForEach-Object { $_.ToString("x2") }) -join "") + "#9A"
        Set-Content -Path $envFile -Value "MSSQL_SA_PASSWORD=$senhaNova" -Encoding ascii
        Write-Host "backend\.env criado com senha aleatória (não versionado)"
    }
    $senha = ((Get-Content $envFile | Where-Object { $_ -like "MSSQL_SA_PASSWORD=*" }) -split "=", 2)[1]
    $env:MSSQL_SA_PASSWORD = $senha
    Push-Location $Backend; docker compose up -d | Out-Null; Pop-Location
    $pronto = $false
    for ($i = 0; $i -lt 60 -and -not $pronto; $i++) {
        docker exec barcalog-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $senha -C -Q "SELECT 1" *> $null
        if ($LASTEXITCODE -eq 0) { $pronto = $true } else { Start-Sleep 2 }
    }
    if (-not $pronto) { Falha "SQL Server não respondeu em 2 min. Veja: docker logs barcalog-sql" }
    $env:ConnectionStrings__BarcaLog = "Server=localhost,1433;Database=BarcaLog;User Id=sa;Password=$senha;TrustServerCertificate=True"
    Write-Host "pronto"
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
New-Item -ItemType Directory -Force -Path $Logs | Out-Null
$processos = @()

try {
    Passo "API (.NET) — log em .logs\api.log"
    Push-Location $Backend
    dotnet tool restore | Out-Null
    dotnet build src\BarcaLog.Api -v quiet -nologo | Out-Null
    $build = $LASTEXITCODE
    Pop-Location
    if ($build -ne 0) { Falha "build da API falhou — rode: cd backend; dotnet build" }
    $api = Start-Process dotnet -ArgumentList "run", "--no-build", "--urls", $ApiUrl `
        -WorkingDirectory (Join-Path $Backend "src\BarcaLog.Api") -NoNewWindow -PassThru `
        -RedirectStandardOutput (Join-Path $Logs "api.log") -RedirectStandardError (Join-Path $Logs "api.err.log")
    $processos += $api
    Write-Host -NoNewline "aguardando $ApiUrl/health/ready (1ª vez importa ~103 mil registros)"
    $ok = $false
    for ($i = 0; $i -lt 180 -and -not $ok; $i++) {
        try { Invoke-WebRequest "$ApiUrl/health/ready" -UseBasicParsing -TimeoutSec 2 | Out-Null; $ok = $true }
        catch {
            if ($api.HasExited) { Write-Host ""; Get-Content (Join-Path $Logs "api.log") -Tail 30; Falha "a API parou ao subir (log acima)." }
            Write-Host -NoNewline "."; Start-Sleep 2
        }
    }
    if (-not $ok) { Falha "API não ficou pronta em 6 min. Veja .logs\api.log" }
    Write-Host " — pronta"

    if (-not $SemFront) {
        Passo "Frontend — log em .logs\front.log"
        $envLocal = Join-Path $Raiz ".env.local"
        if (-not (Test-Path $envLocal)) { Set-Content -Path $envLocal -Value "VITE_API_URL=$ApiUrl" -Encoding ascii }
        if (-not (Test-Path (Join-Path $Raiz "node_modules"))) { Push-Location $Raiz; npm ci --no-audit --no-fund; Pop-Location }
        $front = Start-Process cmd -ArgumentList "/c", "npx vite --port 5173 --strictPort" -WorkingDirectory $Raiz `
            -NoNewWindow -PassThru -RedirectStandardOutput (Join-Path $Logs "front.log") -RedirectStandardError (Join-Path $Logs "front.err.log")
        $processos += $front
        Start-Sleep 3
        Start-Process "http://localhost:5173"
    }

    Write-Host ""
    Write-Host "BarcaLog rodando" -ForegroundColor Green
    Write-Host "  Sistema:  http://localhost:5173"
    Write-Host "  API:      $ApiUrl/swagger"
    Write-Host "  Saúde:    $ApiUrl/health/ready"
    Write-Host "  Usuários de teste (senha BarcaLog@2026, só em Development):"
    Write-Host "            gestor@barcalog.local · operador@barcalog.local · auditor@barcalog.local"
    Write-Host "            Portal do Transportador: norte@transportadora.local · agro@transportadora.local"
    Write-Host ""
    Read-Host "Enter para encerrar API e frontend" | Out-Null
}
finally {
    foreach ($p in $processos) {
        if ($p -and -not $p.HasExited) { & taskkill /PID $p.Id /T /F 2>$null | Out-Null }
    }
    Write-Host "API e frontend encerrados (o banco continua rodando)."
}
