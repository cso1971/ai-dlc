# Initialize Ollama with required models.
# Supports both native (Apple Silicon Metal) and Docker Ollama.
#
# Usage:
#   .\init-ollama.ps1          # auto-detect: prefer native, fallback to Docker
#   .\init-ollama.ps1 -Mode native  # force native Ollama
#   .\init-ollama.ps1 -Mode docker  # force Docker Ollama

param(
    [ValidateSet("", "native", "docker")]
    [string]$Mode = ""
)

Write-Host "Initializing Ollama models..." -ForegroundColor Cyan

# Auto-detect mode if not specified
if (-not $Mode) {
    $nativeOllama = Get-Command ollama -ErrorAction SilentlyContinue
    $dockerRunning = docker ps --format '{{.Names}}' 2>$null | Select-String "playground-ollama"
    $ollamaReady = $false
    try {
        Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 2 | Out-Null
        $ollamaReady = $true
    } catch {}

    if ($nativeOllama -and $ollamaReady -and -not $dockerRunning) {
        $Mode = "native"
        Write-Host "Detected native Ollama running locally (Metal GPU acceleration)" -ForegroundColor Green
    } elseif ($dockerRunning) {
        $Mode = "docker"
        Write-Host "Detected Docker Ollama (CPU only)" -ForegroundColor Yellow
    } elseif ($nativeOllama) {
        $Mode = "native"
        Write-Host "Native Ollama found but not running. Checking if it starts..." -ForegroundColor Yellow
    } else {
        $Mode = "docker"
        Write-Host "No native Ollama found, using Docker Ollama" -ForegroundColor Yellow
    }
}

function Pull-Model {
    param([string]$Model, [string]$PullMode)
    Write-Host "Pulling model: $Model ($PullMode)..." -ForegroundColor Cyan
    try {
        if ($PullMode -eq "native") {
            ollama pull $Model
        } else {
            docker exec playground-ollama ollama pull $Model
        }
        Write-Host "Model $Model pulled successfully!" -ForegroundColor Green
    } catch {
        Write-Host "Failed to pull model $Model : $_" -ForegroundColor Red
    }
}

# Wait for Ollama to be ready
$maxRetries = 30
$retryCount = 0

while ($retryCount -lt $maxRetries) {
    try {
        Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 5 | Out-Null
        Write-Host "Ollama is ready!" -ForegroundColor Green
        break
    } catch {
        $retryCount++
        Write-Host "Waiting for Ollama to be ready... ($retryCount/$maxRetries)" -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }
}

if ($retryCount -eq $maxRetries) {
    Write-Host "Ollama failed to start." -ForegroundColor Red
    if ($Mode -eq "native") {
        Write-Host "Make sure Ollama is installed and running: ollama serve" -ForegroundColor Red
    } else {
        Write-Host "Make sure Docker is running and Ollama container is up:" -ForegroundColor Red
        Write-Host "  cd infra && docker-compose --profile infra --profile ollama up -d" -ForegroundColor Red
    }
    exit 1
}

# Pull required models
$models = @("llama3.2", "nomic-embed-text")

foreach ($model in $models) {
    Pull-Model -Model $model -PullMode $Mode
}

Write-Host ""
Write-Host "Ollama initialization complete! (mode: $Mode)" -ForegroundColor Green
Write-Host "Available models:" -ForegroundColor Cyan
if ($Mode -eq "native") {
    ollama list
} else {
    docker exec playground-ollama ollama list
}

if ($Mode -eq "native") {
    Write-Host ""
    Write-Host "Using native Ollama with GPU acceleration" -ForegroundColor Green
}
