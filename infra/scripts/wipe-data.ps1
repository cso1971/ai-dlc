# Wipe all orders and customers from PostgreSQL, and all data from Qdrant.
# Run from repo root. Requires: Docker with playground-postgres and playground-qdrant running.
# Usage: .\infra\scripts\wipe-data.ps1

$ErrorActionPreference = "Stop"

Write-Host "=== Wipe data (PostgreSQL + Qdrant) ===" -ForegroundColor Cyan

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
Write-Host "`nDone. PostgreSQL and Qdrant data wiped." -ForegroundColor Cyan
