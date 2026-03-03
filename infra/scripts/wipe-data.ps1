# Wipe all orders and customers from PostgreSQL, Qdrant collections, and Redis projections.
# Run from repo root. Requires: Docker with playground-postgres, playground-qdrant and playground-redis running.
# Usage: .\infra\scripts\wipe-data.ps1

$ErrorActionPreference = "Stop"

Write-Host "=== Wipe data (PostgreSQL + Qdrant + Redis) ===" -ForegroundColor Cyan

# --- PostgreSQL ---
Write-Host "`nPostgreSQL: deleting orders and customers..." -ForegroundColor Yellow
$sql = @"
DELETE FROM ordering.order_lines;
DELETE FROM ordering.orders;
DELETE FROM customers.customers;
"@
$sql | docker exec -i playground-postgres psql -U playground -d playground_db -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) {
    Write-Host "PostgreSQL wipe failed. Is playground-postgres running? (docker ps)" -ForegroundColor Red
    exit 1
}
Write-Host "PostgreSQL: orders and customers deleted." -ForegroundColor Green

# --- Qdrant ---
Write-Host "`nQdrant: deleting collections 'orders' and 'customers'..." -ForegroundColor Yellow
$qdrantBase = "http://localhost:6333"
foreach ($coll in @("orders", "customers")) {
    try {
        Invoke-RestMethod -Uri "$qdrantBase/collections/$coll" -Method Delete -TimeoutSec 10 | Out-Null
        Write-Host "  Deleted collection: $coll" -ForegroundColor Green
    } catch {
        if ($_.Exception.Response.StatusCode -eq 404) {
            Write-Host "  Collection $coll did not exist (skip)." -ForegroundColor Gray
        } else {
            Write-Host "  Failed to delete $coll : $_" -ForegroundColor Red
        }
    }
}
# --- Redis ---
Write-Host "`nRedis: flushing projection keys..." -ForegroundColor Yellow
$luaScript = "local keys = redis.call('KEYS','projections:*'); if #keys > 0 then return redis.call('DEL', unpack(keys)) else return 0 end"
$redisResult = docker exec playground-redis redis-cli EVAL $luaScript 0 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Redis not reachable. Is playground-redis running? (docker ps)" -ForegroundColor Red
} else {
    $deleted = [int]($redisResult -replace '\D', '')
    if ($deleted -gt 0) {
        Write-Host "  Deleted $deleted projection keys." -ForegroundColor Green
    } else {
        Write-Host "  No projection keys found (skip)." -ForegroundColor Gray
    }
}

Write-Host "`nDone. PostgreSQL, Qdrant and Redis data wiped." -ForegroundColor Cyan
