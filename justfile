# Distributed Playground — just commands
# Run `just` to see all available recipes

set shell := ["pwsh", "-NoProfile", "-Command"]

# List all recipes
default:
    @just --list

# --- Setup ---

# Install tools via mise (dotnet, ollama, pwsh, just)
setup:
    mise install

# --- Docker Infrastructure ---

# Start infra without Docker Ollama (use native Ollama on Apple Silicon)
infra-up:
    cd infra; docker-compose --profile infra up -d

# Start infra with Docker Ollama (CPU only)
infra-up-ollama:
    cd infra; docker-compose --profile infra --profile ollama up -d

# Start everything (infra + .NET services in containers)
infra-up-full:
    cd infra; docker-compose --profile full up -d

# Stop infra (without Docker Ollama)
infra-down:
    cd infra; docker-compose --profile infra down

# Stop infra + Docker Ollama
infra-down-ollama:
    cd infra; docker-compose --profile infra --profile ollama down

# Stop infra and remove all volumes (data loss!)
infra-down-volumes:
    cd infra; docker-compose --profile infra down -v

# Show running containers
infra-status:
    docker ps --format "table {{{{.Names}}}}\t{{{{.Status}}}}\t{{{{.Ports}}}}"

# --- Ollama ---

# Start native Ollama (Apple Silicon Metal GPU)
ollama-serve:
    ollama serve

# Init Ollama models (auto-detect native vs Docker)
ollama-init:
    pwsh infra/scripts/init-ollama.ps1

# Init Ollama models (force native)
ollama-init-native:
    pwsh infra/scripts/init-ollama.ps1 -Mode native

# Init Ollama models (force Docker)
ollama-init-docker:
    pwsh infra/scripts/init-ollama.ps1 -Mode docker

# List Ollama models
ollama-list:
    ollama list

# --- Database ---

# Create ordering schema via SQL script
db-ordering:
    Get-Content infra/scripts/create-ordering-schema.sql | docker exec -i playground-postgres psql -U playground -d playground_db

# Run Customers EF migrations
db-customers:
    dotnet ef database update --project src/Services/Customers.Api

# Create all database schemas
db-all: db-ordering db-customers

# Wipe all data (PostgreSQL + Qdrant)
db-wipe:
    pwsh infra/scripts/wipe-data.ps1

# --- Build ---

# Restore all NuGet packages
restore:
    dotnet restore src/Services/Ordering.Api/Ordering.Api.csproj
    dotnet restore src/Services/Customers.Api/Customers.Api.csproj
    dotnet restore src/Services/AI.Processor/AI.Processor.csproj
    dotnet restore src/Services/Orchestrator.Api/Orchestrator.Api.csproj

# Build all services
build:
    dotnet build src/Services/Ordering.Api -c Release
    dotnet build src/Services/Customers.Api -c Release
    dotnet build src/Services/AI.Processor -c Release
    dotnet build src/Services/Orchestrator.Api -c Release

# --- Run Services ---

# Run Ordering API (port 5001)
run-ordering:
    dotnet run --project src/Services/Ordering.Api --urls "http://localhost:5001"

# Run Customers API (port 5003)
run-customers:
    dotnet run --project src/Services/Customers.Api --urls "http://localhost:5003"

# Run AI Processor (port 5010)
run-ai:
    dotnet run --project src/Services/AI.Processor --urls "http://localhost:5010"

# Run Orchestrator API (port 5020)
run-orchestrator:
    dotnet run --project src/Services/Orchestrator.Api --urls "http://localhost:5020"

# Run Invoicing API (port 5002)
run-invoicing:
    dotnet run --project src/Services/Invoicing.Api --urls "http://localhost:5002"

# --- Frontend ---

# Install frontend dependencies
frontend-install:
    cd src/Frontend/ordering-web; npm install

# Run Angular frontend (port 4200)
frontend:
    cd src/Frontend/ordering-web; npm start

# --- Order Simulator ---

# Run simulator with defaults (10 customers + 10 orders + workflow)
simulate:
    dotnet run --project src/Tools/OrderSimulator

# Run simulator: n orders, no workflow
simulate-quick n="20":
    dotnet run --project src/Tools/OrderSimulator -- -n {{n}} -w false

# Run simulator: c customers, n orders, no workflow
simulate-custom c="10" n="50":
    dotnet run --project src/Tools/OrderSimulator -- -c {{c}} -n {{n}} -w false

# Simulator help
simulate-help:
    dotnet run --project src/Tools/OrderSimulator -- --help
