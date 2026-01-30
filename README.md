# Distributed Playground

A .NET distributed system playground for AI exploration with multiple bounded contexts.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              INFRASTRUCTURE                                  │
├───────────────┬───────────────┬───────────────┬───────────────┬─────────────┤
│   PostgreSQL  │   RabbitMQ    │    Qdrant     │    Ollama     │   Jaeger    │
│   :5432       │ :5672/:15672  │  :6333/:6334  │    :11434     │   :16686    │
└───────────────┴───────────────┴───────────────┴───────────────┴─────────────┘
                                      │
                    ┌─────────────────┼─────────────────┐
                    │                 │                 │
            ┌───────▼──────┐  ┌───────▼──────┐  ┌───────▼──────┐
            │  Ordering    │  │  Invoicing   │  │  Customers   │
            │    API       │  │    API       │  │    API       │
            │   :5001      │  │   :5002      │  │   :5003      │
            └──────────────┘  └──────────────┘  └──────────────┘
```

## Bounded Contexts

| Context | Schema | Description |
|---------|--------|-------------|
| **Ordering** | `ordering` | Order management and processing |
| **Invoicing** | `invoicing` | Invoice generation and management |
| **Customers** | `customers` | Customer data and profiles |

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

## Quick Start

### 1. Start Infrastructure

```powershell
# Navigate to infra folder
cd infra

# Start only infrastructure (recommended for local development)
docker-compose --profile infra up -d

# Or start everything including .NET services in containers
docker-compose --profile full up -d
```

### 2. Initialize Ollama Models

```powershell
# Run the initialization script
.\infra\scripts\init-ollama.ps1
```

### 3. Run .NET Services Locally (Development)

```powershell
# Terminal 1 - Ordering API
dotnet run --project src/Services/Ordering.Api

# Terminal 2 - Invoicing API  
dotnet run --project src/Services/Invoicing.Api

# Terminal 3 - Customers API
dotnet run --project src/Services/Customers.Api
```

## Infrastructure URLs

| Service | URL | Credentials |
|---------|-----|-------------|
| PostgreSQL | `localhost:5432` | `playground` / `playground_pwd` |
| RabbitMQ Management | http://localhost:15672 | `playground` / `playground_pwd` |
| Qdrant Dashboard | http://localhost:6333/dashboard | - |
| Ollama API | http://localhost:11434 | - |
| Jaeger UI | http://localhost:16686 | - |

## API Endpoints

### Ordering API (`:5001`)
- `GET /` - Service info
- `GET /health` - Health check
- `GET /api/orders` - List orders

### Invoicing API (`:5002`)
- `GET /` - Service info
- `GET /health` - Health check
- `GET /api/invoices` - List invoices

### Customers API (`:5003`)
- `GET /` - Service info
- `GET /health` - Health check
- `GET /api/customers` - List customers

## Project Structure

```
DistributedPlayground/
├── src/
│   ├── Services/
│   │   ├── Ordering.Api/
│   │   ├── Invoicing.Api/
│   │   └── Customers.Api/
│   └── Shared/
│       ├── Contracts/      # MassTransit message types
│       └── Common/         # Shared utilities
├── infra/
│   ├── docker-compose.yml
│   ├── dockerfiles/
│   └── scripts/
└── DistributedPlayground.sln
```

## Technology Stack

- **.NET 9** - Web APIs
- **MassTransit** - Message broker abstraction (RabbitMQ)
- **Entity Framework Core** - ORM (PostgreSQL)
- **OpenTelemetry** - Distributed tracing
- **Qdrant** - Vector database for AI/RAG
- **Ollama** - Local LLM (llama3.2, nomic-embed-text)

## Ollama Models

| Model | Purpose |
|-------|---------|
| `llama3.2` | Text generation, chat |
| `nomic-embed-text` | Text embeddings for RAG |

## Stopping Infrastructure

```powershell
cd infra
docker-compose --profile infra down

# To also remove volumes (data)
docker-compose --profile infra down -v
```
