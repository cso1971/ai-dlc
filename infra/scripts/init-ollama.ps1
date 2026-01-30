# PowerShell script to initialize Ollama with required models
# Run this after starting the Docker infrastructure

Write-Host "Initializing Ollama models..." -ForegroundColor Cyan

# Wait for Ollama to be ready
$maxRetries = 30
$retryCount = 0

while ($retryCount -lt $maxRetries) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:11434/api/tags" -Method Get -TimeoutSec 5
        Write-Host "Ollama is ready!" -ForegroundColor Green
        break
    }
    catch {
        $retryCount++
        Write-Host "Waiting for Ollama to be ready... ($retryCount/$maxRetries)" -ForegroundColor Yellow
        Start-Sleep -Seconds 2
    }
}

if ($retryCount -eq $maxRetries) {
    Write-Host "Ollama failed to start. Make sure Docker is running and infrastructure is up." -ForegroundColor Red
    exit 1
}

# Pull required models
$models = @("llama3.2", "nomic-embed-text")

foreach ($model in $models) {
    Write-Host "Pulling model: $model ..." -ForegroundColor Cyan
    
    try {
        # Use curl for streaming pull (Invoke-RestMethod doesn't handle streaming well)
        docker exec playground-ollama ollama pull $model
        Write-Host "Model $model pulled successfully!" -ForegroundColor Green
    }
    catch {
        Write-Host "Failed to pull model $model : $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Ollama initialization complete!" -ForegroundColor Green
Write-Host "Available models:" -ForegroundColor Cyan
docker exec playground-ollama ollama list
