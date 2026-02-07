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
         │              │               │               │
         │    ┌─────────┼───────────────┼───────────────┘
         │    │         │               │
┌────────▼────▼────┐    │     ┌─────────▼─────────┐
│   Ordering API   │────┼────▶│   AI.Processor    │
│      :5001       │    │     │   (Worker)        │
└──────────────────┘    │     │   Ollama + Qdrant │
         │              │     └───────────────────┘
         │              │               ▲
┌────────▼────────┐     │               │ (Events)
│  Invoicing API  │─────┤               │
│     :5002       │     │               │
└─────────────────┘     │               │
         │              │               │
┌────────▼────────┐     │               │
│  Customers API  │─────┘───────────────┘
│     :5003       │
└─────────────────┘
```

## Bounded Contexts

| Context | Schema | Description |
|---------|--------|-------------|
| **Ordering** | `ordering` | Order management and processing with full workflow |
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

**Con GPU NVIDIA (Ollama usa la scheda video):** richiede [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html). Usa l’override:

```powershell
docker compose -f docker-compose.yml -f docker-compose.gpu.yml --profile infra up -d
```

Chi non ha GPU NVIDIA usi solo il comando standard sopra (senza `-f docker-compose.gpu.yml`).

### 2. Initialize Ollama Models

```powershell
# Run the initialization script
.\infra\scripts\init-ollama.ps1
```

### 3. Create Ordering database schema

Create the `ordering` schema and tables in PostgreSQL (required for Ordering API). Run from the repo root with infrastructure up:

**PowerShell (Windows):**
```powershell
Get-Content infra/scripts/create-ordering-schema.sql | docker exec -i playground-postgres psql -U playground -d playground_db
```

**Bash (Linux/macOS):**
```bash
cat infra/scripts/create-ordering-schema.sql | docker exec -i playground-postgres psql -U playground -d playground_db
```

Alternatively, if you have the EF Core tools and migrations are discovered correctly: `dotnet ef database update --project src/Services/Ordering.Api`.

**Customers API (schema `customers`):**  
`dotnet ef database update --project src/Services/Customers.Api`

### 4. Run .NET Services Locally (Development)

```powershell
# Terminal 1 - Ordering API
dotnet run --project src/Services/Ordering.Api

# Terminal 2 - AI Processor (Event Consumer)
dotnet run --project src/Services/AI.Processor

# Terminal 3 - Invoicing API  
dotnet run --project src/Services/Invoicing.Api

# Terminal 4 - Customers API
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

## Service URLs

| Service | Swagger | API Base |
|---------|---------|----------|
| Ordering API | http://localhost:5001/swagger | http://localhost:5001/api |
| Invoicing API | http://localhost:5002/swagger | http://localhost:5002/api |
| Customers API | http://localhost:5003/swagger | http://localhost:5003/api |
| AI Processor | http://localhost:5010/swagger | http://localhost:5010/api |
| Angular Frontend | http://localhost:4200 | - |

## Ordering API

### Swagger UI

**URL:** http://localhost:5001/swagger

### REST Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/orders` | List all orders (summary) |
| `GET` | `/api/orders/{id}` | Get order details |
| `POST` | `/api/orders` | Create a new order |
| `POST` | `/api/orders/{id}/start-processing` | Start order processing |
| `POST` | `/api/orders/{id}/ship` | Ship order |
| `POST` | `/api/orders/{id}/deliver` | Mark as delivered |
| `POST` | `/api/orders/{id}/invoice` | Mark as invoiced |
| `POST` | `/api/orders/{id}/cancel` | Cancel order |

### MassTransit Consumers (RabbitMQ)

Order commands can also be sent via RabbitMQ message bus. Each command has a dedicated consumer:

| Consumer | Command | Queue |
|----------|---------|-------|
| `CreateOrderConsumer` | `CreateOrder` | `create-order` |
| `StartOrderProcessingConsumer` | `StartOrderProcessing` | `start-order-processing` |
| `ShipOrderConsumer` | `ShipOrder` | `ship-order` |
| `DeliverOrderConsumer` | `DeliverOrder` | `deliver-order` |
| `InvoiceOrderConsumer` | `InvoiceOrder` | `invoice-order` |
| `CancelOrderConsumer` | `CancelOrder` | `cancel-order` |

All consumers:
- Use `OrderingService` for business logic (same as REST)
- Support request/response pattern with `OrderCommandResponse`
- Publish domain events on success
- Return error messages on failure

### Order Workflow

```
CreateOrder ──▶ StartProcessing ──▶ Ship ──▶ Deliver ──▶ Invoice
     │                │               │          │
     ▼                ▼               ▼          ▼
  Created ────────▶ InProgress ───▶ Shipped ──▶ Delivered ──▶ Invoiced
     │                │               │          │              (final)
     └────────────────┴───────────────┴──────────┘
                            │
                       CancelOrder
                            │
                            ▼
                       Cancelled
```

**Order Statuses:**
- `Created` (0) - Order created, not yet processed
- `InProgress` (1) - Order being prepared
- `Shipped` (2) - Order shipped to customer
- `Delivered` (3) - Order delivered
- `Invoiced` (4) - Order invoiced (final state)
- `Cancelled` (99) - Order cancelled

### Order Domain Model

**Order (Aggregate Root)**
- Header: CustomerId, CustomerReference, RequestedDeliveryDate, Priority, CurrencyCode, PaymentTerms, ShippingMethod, ShippingAddress, Notes
- Status tracking: Status, CreatedAt, UpdatedAt
- Shipping: TrackingNumber, Carrier, EstimatedDeliveryDate, ShippedAt
- Delivery: DeliveredAt, ReceivedBy, DeliveryNotes
- Invoice: InvoiceId, InvoicedAt
- Cancellation: CancellationReason, CancelledAt
- Lines: Collection of OrderLine
- Totals: Subtotal, TotalTax, GrandTotal (calculated)

**OrderLine (Entity)**
- LineNumber, ProductCode, Description
- Quantity, UnitOfMeasure
- UnitPrice, DiscountPercent, TaxPercent
- LineTotal, TaxAmount, LineTotalWithTax (calculated)

**ShippingAddress (Value Object)**
- RecipientName, AddressLine1, AddressLine2
- City, StateOrProvince, PostalCode, CountryCode
- PhoneNumber, Notes

## Customers API

### Swagger UI

**URL:** http://localhost:5003/swagger

### REST Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/customers/{id}` | Get customer by ID (used by AI.Processor for RAG indexing) |
| `POST` | `/api/customers` | Create a new customer (DDD-style data) |

### MassTransit Consumers (RabbitMQ)

| Consumer | Command | Queue |
|----------|---------|-------|
| `CreateCustomerConsumer` | `CreateCustomer` | `create-customer` |

REST `POST /api/customers` calls `CustomerService` and persists to PostgreSQL (schema `customers`, table `customers`). The MassTransit consumer uses the same service. Both publish `CustomerCreated` when a customer is created.

### Database (EF Core)

- Schema: `customers`
- Table: `customers` (Id, CompanyName, DisplayName, Email, Phone, TaxId, VatNumber, BillingAddress columns, ShippingAddress columns, PreferredLanguage, PreferredCurrency, Notes, CreatedAt, UpdatedAt)
- Apply migrations: `dotnet ef database update --project src/Services/Customers.Api`

### CreateCustomer payload (DDD-style)

- **CompanyName**, **Email** (required)
- **DisplayName**, **Phone**, **TaxId**, **VatNumber**, **Notes**
- **BillingAddress**, **ShippingAddress** (PostalAddress: RecipientName, AddressLine1, City, PostalCode, CountryCode, etc.)
- **PreferredLanguage** (default `en`), **PreferredCurrency** (default `EUR`)

## AI.Processor (AI Service with REST API)

Hybrid service combining event-driven processing with REST API for AI capabilities.

### Swagger UI

**URL:** http://localhost:5010/swagger

### REST API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/ai/chat` | Send prompt to Ollama LLM |
| `POST` | `/api/ai/analyze` | Analyze text with AI |
| `POST` | `/api/ai/summarize` | Summarize text |
| `POST` | `/api/ai/embed` | Generate text embedding |
| `POST` | `/api/ai/embed/batch` | Generate multiple embeddings |
| `POST` | `/api/ai/search` | Semantic search over orders |
| `GET` | `/api/ai/info` | Get AI service configuration |
| `GET` | `/api/ai/health/ollama` | Check Ollama connectivity |
| `GET` | `/api/ai/health/qdrant` | Check Qdrant connectivity |

### Example: Chat Request

```bash
curl -X POST http://localhost:5010/api/ai/chat \
  -H "Content-Type: application/json" \
  -d '{"prompt": "What is machine learning?", "systemPrompt": "You are a helpful assistant."}'
```

### Example: Semantic Search

```bash
curl -X POST http://localhost:5010/api/ai/search \
  -H "Content-Type: application/json" \
  -d '{"query": "orders shipped to Italy", "limit": 5}'
```

### Features

- **REST API**: Full Swagger-documented API for AI interactions
- **Event Consumption**: Listens to Ordering and Customers domain events via MassTransit/RabbitMQ (e.g. OrderCreated, CustomerCreated)
- **LLM Integration**: Uses Ollama for text generation and event analysis
- **Vector Storage**: Stores order and customer embeddings in Qdrant (collections `orders`, `customers`) for semantic search
- **Distributed Tracing**: Full OpenTelemetry integration with Jaeger

### Event Consumers

| Consumer | Event | Processing |
|----------|-------|------------|
| `OrderCreatedConsumer` | `OrderCreated` | Fetch order via REST, generate embedding, store in Qdrant, analyze with LLM |
| `OrderStatusChangedConsumer` | `OrderStatusChanged` | Update vector store, analyze status transition |
| `OrderShippedConsumer` | `OrderShipped` | Store shipping details, analyze logistics |
| `OrderDeliveredConsumer` | `OrderDelivered` | Update delivery status, completion analysis |
| `OrderCancelledConsumer` | `OrderCancelled` | Analyze cancellation reasons for insights |
| `OrderCompletedConsumer` | `OrderCompleted` | Generate order summary, final embedding |
| `CustomerCreatedConsumer` | `CustomerCreated` | Fetch customer via REST, generate embedding, store in Qdrant (collection `customers`), analyze with LLM |

### Configuration

```json
{
  "OrderingApi": {
    "BaseUrl": "http://localhost:5001"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.2",
    "EmbeddingModel": "nomic-embed-text"
  },
  "Qdrant": {
    "Host": "localhost",
    "Port": 6334,
    "CollectionName": "orders"
  }
}
```

### RAG Architecture

When an event is received, AI.Processor:
1. **Fetches complete order** from Ordering API via HTTP (`GET /api/orders/{id}`)
2. **Generates text representation** of the order with all details
3. **Creates embedding** using `nomic-embed-text` model
4. **Stores in Qdrant** with rich payload (order ID, customer, products, financials, etc.)
5. **Analyzes with LLM** for business insights

```
┌─────────────────┐     Event      ┌─────────────────┐
│  Ordering.Api   │ ────────────▶  │  AI.Processor   │
└─────────────────┘                └────────┬────────┘
        ▲                                   │
        │ GET /api/orders/{id}              │ 1. Fetch order details
        └───────────────────────────────────┘
                                            │
                                            ▼
                                   ┌─────────────────┐
                                   │     Ollama      │ 2. Generate embedding
                                   └─────────────────┘
                                            │
                                            ▼
                                   ┌─────────────────┐
                                   │     Qdrant      │ 3. Store for RAG
                                   └─────────────────┘
```

### Services

| Service | Interface | Description |
|---------|-----------|-------------|
| `OllamaService` | `IOllamaService` | LLM completions and embeddings |
| `QdrantService` | `IQdrantService` | Vector storage and semantic search |

## Project Structure

```
DistributedPlayground/
├── src/
│   ├── Services/
│   │   ├── Ordering.Api/
│   │   │   ├── Domain/           # Aggregates, Entities, Value Objects
│   │   │   │   ├── Order.cs
│   │   │   │   ├── OrderLine.cs
│   │   │   │   └── ShippingAddress.cs
│   │   │   ├── Services/         # Domain Services
│   │   │   │   ├── IOrderRepository.cs
│   │   │   │   └── OrderingService.cs
│   │   │   ├── Infrastructure/   # EF Core, Repositories
│   │   │   │   ├── OrderingDbContext.cs
│   │   │   │   └── OrderRepository.cs
│   │   │   ├── Endpoints/        # REST API
│   │   │   │   ├── OrderEndpoints.cs
│   │   │   │   └── OrderDtos.cs
│   │   │   ├── Consumers/        # MassTransit Consumers
│   │   │   │   ├── CreateOrderConsumer.cs
│   │   │   │   ├── StartOrderProcessingConsumer.cs
│   │   │   │   ├── ShipOrderConsumer.cs
│   │   │   │   ├── DeliverOrderConsumer.cs
│   │   │   │   ├── InvoiceOrderConsumer.cs
│   │   │   │   ├── CancelOrderConsumer.cs
│   │   │   │   └── OrderCommandResponse.cs
│   │   │   └── Program.cs
│   │   ├── AI.Processor/          # AI Event Consumer Worker
│   │   │   ├── Services/          # Ollama & Qdrant services
│   │   │   │   ├── IOllamaService.cs
│   │   │   │   ├── OllamaService.cs
│   │   │   │   ├── IQdrantService.cs
│   │   │   │   └── QdrantService.cs
│   │   │   ├── Consumers/         # MassTransit Event Consumers
│   │   │   │   ├── OrderCreatedConsumer.cs
│   │   │   │   ├── OrderStatusChangedConsumer.cs
│   │   │   │   ├── OrderShippedConsumer.cs
│   │   │   │   ├── OrderDeliveredConsumer.cs
│   │   │   │   ├── OrderCancelledConsumer.cs
│   │   │   │   └── OrderCompletedConsumer.cs
│   │   │   └── Program.cs
│   │   ├── Invoicing.Api/
│   │   └── Customers.Api/
│   ├── Tools/
│   │   └── OrderSimulator/      # CLI tool for test data generation
│   └── Shared/
│       ├── Contracts/            # MassTransit message types
│       │   ├── Commands/
│       │   │   ├── Ordering/
│       │   │   │   ├── CreateOrder.cs
│       │   │   │   ├── StartOrderProcessing.cs
│       │   │   │   ├── ShipOrder.cs
│       │   │   │   ├── DeliverOrder.cs
│       │   │   │   ├── InvoiceOrder.cs
│       │   │   │   └── CancelOrder.cs
│       │   │   ├── Invoicing/
│       │   │   └── Customers/
│       │   ├── Events/
│       │   │   ├── Ordering/
│       │   │   │   ├── OrderCreated.cs
│       │   │   │   ├── OrderStatusChanged.cs
│       │   │   │   ├── OrderShipped.cs
│       │   │   │   ├── OrderDelivered.cs
│       │   │   │   ├── OrderCompleted.cs
│       │   │   │   └── OrderCancelled.cs
│       │   │   ├── Invoicing/
│       │   │   └── Customers/
│       │   ├── Enums/
│       │   │   └── OrderStatus.cs
│       │   └── ValueObjects/
│       │       └── Ordering/
│       │           ├── OrderLineItem.cs
│       │           └── ShippingAddress.cs
│       └── Common/               # Shared utilities
│           ├── Configuration/
│           │   ├── OllamaSettings.cs
│           │   └── QdrantSettings.cs
│           └── Extensions/
├── infra/
│   ├── docker-compose.yml
│   ├── dockerfiles/
│   │   ├── Ordering.Api.Dockerfile
│   │   ├── Invoicing.Api.Dockerfile
│   │   └── Customers.Api.Dockerfile
│   └── scripts/
│       ├── init-ollama.ps1
│       └── init-ollama.sh
└── DistributedPlayground.sln
```

## Frontend - Ordering Web (Angular)

Angular 17 SPA for managing orders.

**Location:** `src/Frontend/ordering-web`

### Pages

| Page | Route | Description |
|------|-------|-------------|
| Order List | `/orders` | List all orders with summary |
| Order Detail | `/orders/:id` | View order details and execute workflow actions |
| Create Order | `/orders/new` | Create a new order with lines |

### Running the Frontend

```powershell
cd src/Frontend/ordering-web
npm install
npm start
```

**URL:** http://localhost:4200

### Features

- View all orders with status badges
- Create orders with multiple line items
- Execute workflow actions (Start Processing, Ship, Deliver, Invoice, Cancel)
- Modal dialogs for actions requiring input
- **AI Chat Assistant** - Always visible floating panel with:
  - 💬 Chat mode: Ask questions about orders
  - 🔍 Search mode: Semantic search over orders in Qdrant
  - 📊 Analyze mode: AI analysis of text
- Responsive design

### AI Chat Assistant

The AI Chat panel is always visible in the bottom-right corner. It connects to AI.Processor and provides:

- **Chat**: Ask questions, get AI-powered answers
- **Search Orders**: Find similar orders using semantic search
- **Analyze**: Send text for AI analysis

Click the 🤖 button to open/close the panel.

## Technology Stack

| Technology | Purpose |
|------------|---------|
| **Angular 17** | Frontend SPA |
| **.NET 9** | Web APIs (Minimal APIs) |
| **MassTransit** | Message broker abstraction |
| **RabbitMQ** | Message broker |
| **Entity Framework Core** | ORM |
| **PostgreSQL** | Database |
| **OpenTelemetry** | Distributed tracing |
| **Jaeger** | Trace visualization |
| **Swashbuckle** | OpenAPI/Swagger |
| **Qdrant** | Vector database for AI/RAG |
| **Ollama** | Local LLM |

## Contracts (MassTransit Messages)

### Commands

| Command | Bounded Context | Description |
|---------|-----------------|-------------|
| `CreateOrder` | Ordering | Create a new order with lines |
| `StartOrderProcessing` | Ordering | Start processing (Created → InProgress) |
| `ShipOrder` | Ordering | Ship order (InProgress → Shipped) |
| `DeliverOrder` | Ordering | Mark delivered (Shipped → Delivered) |
| `InvoiceOrder` | Ordering | Mark invoiced (Delivered → Invoiced) |
| `CancelOrder` | Ordering | Cancel order (Any → Cancelled) |
| `CreateCustomer` | Customers | Create a new customer |
| `GenerateInvoice` | Invoicing | Generate invoice for order |

### Events

| Event | Bounded Context | Description |
|-------|-----------------|-------------|
| `OrderCreated` | Ordering | Order was created |
| `OrderStatusChanged` | Ordering | Order status changed |
| `OrderShipped` | Ordering | Order was shipped |
| `OrderDelivered` | Ordering | Order was delivered |
| `OrderCompleted` | Ordering | Order workflow completed (invoiced) |
| `OrderCancelled` | Ordering | Order was cancelled |
| `CustomerCreated` | Customers | Customer was created |
| `InvoiceGenerated` | Invoicing | Invoice was generated |

## Database Schema

### ordering schema

**orders**
| Column | Type | Description |
|--------|------|-------------|
| Id | uuid | Primary key |
| CustomerId | uuid | Customer reference |
| CustomerReference | varchar(100) | External reference |
| Status | int | Order status |
| CurrencyCode | varchar(3) | Currency (EUR, USD) |
| Priority | int | 1-5 priority |
| shipping_* | - | Embedded ShippingAddress |
| CreatedAt | timestamp | Creation date |
| ... | | |

**order_lines**
| Column | Type | Description |
|--------|------|-------------|
| Id | uuid | Primary key |
| OrderId | uuid | Foreign key to orders |
| LineNumber | int | Line sequence |
| ProductCode | varchar(50) | SKU |
| Quantity | decimal(18,4) | |
| UnitPrice | decimal(18,4) | |
| ... | | |

## Ollama Models

| Model | Size | Purpose |
|-------|------|---------|
| `llama3.2` | 2.0 GB | Text generation, chat |
| `nomic-embed-text` | 274 MB | Text embeddings for RAG |

## Stopping Infrastructure

```powershell
cd infra
docker-compose --profile infra down

# Se avevi avviato con GPU: usa gli stessi file
# docker compose -f docker-compose.yml -f docker-compose.gpu.yml --profile infra down

# To also remove volumes (data)
docker-compose --profile infra down -v
```

## Order Simulator Tool

Console application to generate test orders and simulate workflow transitions.

### Usage

```powershell
# Generate 10 orders with workflow simulation (default)
dotnet run --project src/Tools/OrderSimulator

# Generate 50 orders without workflow
dotnet run --project src/Tools/OrderSimulator -- -n 50 -w false

# Generate 20 orders with 1 second delay between commands
dotnet run --project src/Tools/OrderSimulator -- -n 20 -d 1000

# Show help
dotnet run --project src/Tools/OrderSimulator -- --help
```

### Options

| Option | Alias | Description | Default |
|--------|-------|-------------|---------|
| `--orders` | `-n` | Number of orders to create | 10 |
| `--simulate-workflow` | `-w` | Simulate status transitions | true |
| `--delay` | `-d` | Delay between commands (ms) | 500 |
| `--rabbit-host` | | RabbitMQ host | localhost |
| `--rabbit-user` | | RabbitMQ username | playground |
| `--rabbit-password` | | RabbitMQ password | playground_pwd |

### Simulation Phases

1. **Create Orders**: Generates random orders with fake customer data, products, addresses
2. **Fetch Orders**: Gets created order IDs from Ordering API
3. **Workflow Transitions**: Randomly applies:
   - Start Processing
   - Ship (with random carrier and tracking)
   - Deliver
   - Invoice
   - Cancel (10-20% of orders)

### Prerequisites

Requires Ordering.Api to be running on `http://localhost:5001` for workflow simulation.

## Development Notes

- Services are configured to run locally (not in Docker) during development
- Infrastructure runs in Docker containers
- Each bounded context uses a separate schema in the shared PostgreSQL database
- OpenTelemetry traces are sent to Jaeger for visualization
- Health checks available at `/health` endpoint for each service
