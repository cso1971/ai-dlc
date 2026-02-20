# Commands Reference

Quick reference for all project commands. Run from the repo root unless noted.

---

## Setup

```powershell
# Install tools (dotnet, ollama, pwsh) via mise
mise install

# Start Ollama natively (Apple Silicon Metal GPU)
ollama serve
```

## Docker Infrastructure

```powershell
# Start infra (without Docker Ollama — use native Ollama on Apple Silicon)
cd infra; docker-compose --profile infra up -d

# Start infra WITH Docker Ollama (CPU only)
cd infra; docker-compose --profile infra --profile ollama up -d

# Start everything including .NET services in containers
cd infra; docker-compose --profile full up -d

# NVIDIA GPU override
cd infra; docker compose -f docker-compose.yml -f docker-compose.gpu.yml --profile infra --profile ollama up -d

# Stop infra
cd infra; docker-compose --profile infra down

# Stop infra + Docker Ollama
cd infra; docker-compose --profile infra --profile ollama down

# Stop and remove volumes (all data)
cd infra; docker-compose --profile infra down -v
```

## Ollama Models

```powershell
# Auto-detect native vs Docker, pull models
pwsh infra/scripts/init-ollama.ps1

# Force native Ollama
pwsh infra/scripts/init-ollama.ps1 -Mode native

# Force Docker Ollama
pwsh infra/scripts/init-ollama.ps1 -Mode docker
```

## Database

```powershell
# Create ordering schema (SQL script)
Get-Content infra/scripts/create-ordering-schema.sql | docker exec -i playground-postgres psql -U playground -d playground_db

# Ordering: EF migrations (alternative)
dotnet ef database update --project src/Services/Ordering.Api

# Customers: EF migrations
dotnet ef database update --project src/Services/Customers.Api

# Wipe all data (PostgreSQL + Qdrant)
pwsh infra/scripts/wipe-data.ps1
```

## Run Services

```powershell
# Ordering API (port 5001)
dotnet run --project src/Services/Ordering.Api --urls "http://localhost:5001"

# Customers API (port 5003)
dotnet run --project src/Services/Customers.Api --urls "http://localhost:5003"

# AI Processor (port 5010)
dotnet run --project src/Services/AI.Processor --urls "http://localhost:5010"

# Orchestrator API (port 5020)
dotnet run --project src/Services/Orchestrator.Api --urls "http://localhost:5020"

# Invoicing API (port 5002)
dotnet run --project src/Services/Invoicing.Api --urls "http://localhost:5002"
```

## Frontend

```powershell
cd src/Frontend/ordering-web
npm install
npm start
# Runs on http://localhost:4200
```

## Order Simulator

```powershell
# Default: 10 customers + 10 orders + workflow simulation
dotnet run --project src/Tools/OrderSimulator

# 15 customers, 50 orders, no workflow
dotnet run --project src/Tools/OrderSimulator -- -c 15 -n 50 -w false

# 20 orders with 1s delay
dotnet run --project src/Tools/OrderSimulator -- -n 20 -d 1000

# Help
dotnet run --project src/Tools/OrderSimulator -- --help
```

## API Examples

```powershell
# Chat with AI
curl -X POST http://localhost:5010/api/ai/chat `
  -H "Content-Type: application/json" `
  -d '{"prompt": "What is machine learning?", "systemPrompt": "You are a helpful assistant."}'

# Semantic search
curl -X POST http://localhost:5010/api/ai/search `
  -H "Content-Type: application/json" `
  -d '{"query": "orders shipped to Italy", "limit": 5}'

# Orchestrator chat (Semantic Kernel)
curl -X POST http://localhost:5020/api/orchestrator/chat `
  -H "Content-Type: application/json" `
  -d '{"prompt": "list all customers"}'
```

## URLs

| Service | URL |
|---------|-----|
| Ordering API | http://localhost:5001/swagger |
| Invoicing API | http://localhost:5002/swagger |
| Customers API | http://localhost:5003/swagger |
| AI Processor | http://localhost:5010/swagger |
| Orchestrator API | http://localhost:5020/swagger |
| Angular Frontend | http://localhost:4200 |
| RabbitMQ Management | http://localhost:15672 (playground / playground_pwd) |
| Qdrant Dashboard | http://localhost:6333/dashboard |
| Jaeger UI | http://localhost:16686 |
| Ollama API | http://localhost:11434 |
| PostgreSQL | localhost:5432 (playground / playground_pwd) |
