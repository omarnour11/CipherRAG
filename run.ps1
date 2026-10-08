# CipherRAG — Smart Launcher
# Starts all infrastructure via Docker Compose, then runs the .NET API
# Usage: .\run.ps1

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location -LiteralPath $projectRoot

Write-Host ""
Write-Host "  ██████╗  █████╗  ██████╗ ███████╗██╗  ██╗██╗███████╗██╗     ██████╗ " -ForegroundColor Cyan
Write-Host "  ██╔══██╗██╔══██╗██╔════╝ ██╔════╝██║  ██║██║██╔════╝██║     ██╔══██╗" -ForegroundColor Cyan
Write-Host "  ██████╔╝███████║██║  ███╗███████╗███████║██║█████╗  ██║     ██║  ██║" -ForegroundColor Cyan
Write-Host "  ██╔══██╗██╔══██║██║   ██║╚════██║██╔══██║██║██╔══╝  ██║     ██║  ██║" -ForegroundColor Cyan
Write-Host "  ██║  ██║██║  ██║╚██████╔╝███████║██║  ██║██║███████╗███████╗██████╔╝" -ForegroundColor Cyan
Write-Host "  ╚═╝  ╚═╝╚═╝  ╚═╝ ╚═════╝ ╚══════╝╚═╝  ╚═╝╚═╝╚══════╝╚══════╝╚═════╝ " -ForegroundColor Cyan
Write-Host ""

# ── 1. Stop any running .NET API process ────────────────────────────────────
Write-Host "[1/4] Stopping any existing CipherRAG processes..." -ForegroundColor Yellow
$procs = Get-Process -Name "CipherRAG.API" -ErrorAction SilentlyContinue
if ($procs) {
    $procs | ForEach-Object {
        try {
            Stop-Process -Id $_.Id -Force -ErrorAction Stop
        } catch {
            Write-Host "  [ERROR] Could not stop CipherRAG.API (PID $($_.Id)). Close the running API window or run this script as Administrator." -ForegroundColor Red
            exit 1
        }
    }
    Start-Sleep -Seconds 1
    $remaining = Get-Process -Name "CipherRAG.API" -ErrorAction SilentlyContinue
    if ($remaining) {
        Write-Host "  [ERROR] CipherRAG.API is still running. Close it before starting the launcher." -ForegroundColor Red
        exit 1
    }
}
$portInUse = Get-NetTCPConnection -LocalPort 5000 -ErrorAction SilentlyContinue
if ($portInUse) {
    $portInUse | Select-Object -ExpandProperty OwningProcess | Sort-Object -Unique | ForEach-Object {
        if ($_ -and $_ -gt 0) {
            try {
                Stop-Process -Id $_ -Force -ErrorAction Stop
            } catch {
                Write-Host "  [ERROR] Could not stop the process using port 5000 (PID $_). Close it or run this script as Administrator." -ForegroundColor Red
                exit 1
            }
        }
    }
    Start-Sleep -Seconds 1
}

# ── 2. Start infrastructure via Docker Compose ──────────────────────────────
Write-Host "[2/4] Starting infrastructure (Microsoft Presidio + Seq + Prometheus)..." -ForegroundColor Yellow
docker compose up -d --remove-orphans
if ($LASTEXITCODE -ne 0) {
    Write-Host "  [ERROR] Docker Compose failed. Make sure Docker Desktop is running." -ForegroundColor Red
    exit 1
}

# ── 3. Wait for Presidio to be healthy ──────────────────────────────────────
Write-Host "[3/4] Waiting for Microsoft Presidio Analyzer to be ready..." -ForegroundColor Yellow
$maxAttempts = 30
$attempt = 0
$presidioReady = $false
while ($attempt -lt $maxAttempts -and -not $presidioReady) {
    try {
        $resp = Invoke-WebRequest -Uri "http://localhost:5001/health" -TimeoutSec 2 -ErrorAction Stop
        if ($resp.StatusCode -eq 200) {
            $presidioReady = $true
        }
    } catch {
        $attempt++
        Write-Host "  Presidio not ready yet ($attempt/$maxAttempts)... " -NoNewline -ForegroundColor DarkGray
        Start-Sleep -Seconds 2
    }
}

if ($presidioReady) {
    Write-Host ""
    Write-Host "  [OK] Microsoft Presidio Analyzer is ready on http://localhost:5001" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "  [WARN] Presidio did not respond in time — CipherRAG will use Local NER fallback." -ForegroundColor Yellow
}

# ── Check Ollama Llama-Guard 3 ───────────────────────────────────────────────
$ollamaReady = $false
try {
    $oresp = Invoke-WebRequest -Uri "http://localhost:11434/" -TimeoutSec 2 -ErrorAction Stop
    if ($oresp.StatusCode -eq 200) { $ollamaReady = $true }
} catch {}

if (-not $ollamaReady) {
    Write-Host "  Starting local Ollama server..." -ForegroundColor DarkGray
    Start-Process -FilePath "ollama" -ArgumentList "serve" -WindowStyle Hidden -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}
Write-Host "  [OK] Ollama Llama-Guard 3 is active on http://localhost:11434" -ForegroundColor Green

# ── 4. Launch CipherRAG .NET API ─────────────────────────────────────────────
Write-Host "[4/4] Launching CipherRAG Gateway on http://localhost:5000..." -ForegroundColor Cyan
Write-Host ""
Write-Host "  Services available:" -ForegroundColor White
Write-Host "    CipherRAG UI      -> http://localhost:5000" -ForegroundColor Green
Write-Host "    Presidio Analyzer -> http://localhost:5001" -ForegroundColor Green
Write-Host "    Presidio Anonymiz -> http://localhost:5002" -ForegroundColor Green
Write-Host "    Ollama Guardrail  -> http://localhost:11434" -ForegroundColor Green
Write-Host "    Seq Logs          -> http://localhost:8081" -ForegroundColor Green
Write-Host "    Prometheus        -> http://localhost:9090" -ForegroundColor Green
Write-Host "    Grafana           -> http://localhost:3001  (password: GRAFANA_ADMIN_PASSWORD)" -ForegroundColor Green
Write-Host ""

dotnet run --project ./CipherRAG.API/CipherRAG.API.csproj --urls http://localhost:5000
