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
         │    ┌─────────┼───────────────┼───────────────┬───────────────┐
         │    │         │               │               │               │
┌────────▼────▼────┐    │     ┌─────────▼─────────┐     ┌─▼────────────────┐
│   Ordering API   │────┼────▶│   AI.Processor    │     │ Orchestrator API  │
│      :5001       │    │     │   (Worker)        │     │     :5020         │
└────────┬─────────┘    │     │   Ollama + Qdrant │     │ Semantic Kernel  │
         ▲              │     └───────────────────┘     │ Ollama + plugins  │
         │              │               ▲               └────────┬────────┘
┌────────┴────────┐     │               │ (Events)               │ HTTP
│  Invoicing API  │─────┤               │                        ▼
│     :5002       │     │               │               (Ordering, Customers)
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

- [mise](https://mise.jdx.dev/) — run `mise install` to get .NET 9, Ollama, PowerShell, and just
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

## Quick Start

All commands are in the `justfile`. Run `just` to see the full list.

```powershell
just setup              # Install tools via mise
just infra-up           # Start Docker infra (without Docker Ollama)
just ollama-serve       # Start native Ollama (Apple Silicon Metal GPU) — separate terminal
just ollama-init        # Pull models (llama3.2 + nomic-embed-text)
just db-all             # Create all DB schemas (ordering + customers)
just run-ordering       # Start Ordering API (:5001) — separate terminal
just run-customers      # Start Customers API (:5003) — separate terminal
just run-ai             # Start AI Processor (:5010) — separate terminal
just run-orchestrator   # Start Orchestrator API (:5020) — separate terminal
just frontend-install   # Install Angular dependencies
just frontend           # Start Angular frontend (:4200)
just simulate           # Generate test data (10 customers + 10 orders + workflow)
```

**GPU acceleration:** Use `just infra-up` (without Docker Ollama) + `just ollama-serve` to run Ollama natively with GPU support (Metal, CUDA, ROCm — 3-5x faster). This is the recommended setup for any machine with a GPU.

**Docker Ollama (CPU only):** `just infra-up-ollama` starts Ollama in Docker without GPU access (NVIDIA GPU in Docker requires [NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/install-guide.html)).

**Wipe all data:** `just db-wipe` deletes all rows in PostgreSQL and removes Qdrant collections.

**Stop infra:** `just infra-down` or `just infra-down-volumes` (removes data).

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
| **Orchestrator API** | http://localhost:5020/swagger | http://localhost:5020/api |
| Angular Frontend | http://localhost:4200 | - |

## Orchestrator API (Semantic Kernel)

New service that uses **Semantic Kernel** with **Ollama** (local LLM only). It exposes REST APIs and a MassTransit endpoint to run orchestrations.

- **URL:** http://localhost:5020/swagger
- **REST:** `POST /api/orchestrator/chat` — send a prompt; the LLM can use plugins to call Ordering/Customers APIs (HTTP) or send MassTransit commands.
- **MassTransit:** Send `RequestOrchestration` (contract: `Prompt`, optional `CorrelationId`) to queue `request-orchestration` to trigger an orchestration from the bus.
- **Plugins:** `ServicesApi` (GetOrders, GetOrderStats, GetOrderById, GetCustomers via HTTP); `MassTransitCommands` (SendCreateOrder).
- **Config:** `Ollama:Endpoint`, `Ollama:ModelId`, `OrderingApi:BaseUrl`, `CustomersApi:BaseUrl`, `RabbitMQ` (same as other services).

Run: `just run-orchestrator` (Ollama and Ordering/Customers APIs should be up).

## Ordering API

### Swagger UI

**URL:** http://localhost:5001/swagger

### REST Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/orders` | List all orders (summary) |
| `GET` | `/api/orders/stats` | Order aggregates (total count, total value per currency; used by AI chat for correct totals) |
| `GET` | `/api/orders/{id}` | Get order details |
| `GET` | `/api/metrics/rabbitmq` | RabbitMQ queue metrics (total messages, per-queue; used by frontend navbar widget) |
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
- **CustomerId**: reference to the Customer aggregate in the Customers bounded context (same Guid as `Customer.Id`). The customer must exist; Ordering API validates it via Customers API when creating an order.
- **CustomerReference**: optional order reference from the customer’s side (e.g. PO number), not the customer identity.
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
| `GET` | `/api/customers` | List all customers (summary) |
| `GET` | `/api/customers/{id}` | Get customer by ID (used by AI.Processor for RAG indexing) |
| `POST` | `/api/customers` | Create a new customer (DDD-style data) |
| `PUT` | `/api/customers/{id}` | Partial update (only provided fields; cannot update cancelled customer) |
| `POST` | `/api/customers/{id}/cancel` | Cancel customer – soft delete with reason (idempotent) |

### MassTransit Consumers (RabbitMQ)

| Consumer | Command | Queue |
|----------|---------|-------|
| `CreateCustomerConsumer` | `CreateCustomer` | `create-customer` |
| `UpdateCustomerConsumer` | `UpdateCustomer` | `update-customer` |
| `CancelCustomerConsumer` | `CancelCustomer` | `cancel-customer` |

REST `POST /api/customers` calls `CustomerService` and persists to PostgreSQL (schema `customers`, table `customers`). The MassTransit consumer uses the same service. Both publish `CustomerCreated` when a customer is created.

### Database (EF Core)

- Schema: `customers`
- Table: `customers` (Id, CompanyName, DisplayName, Email, … PreferredLanguage, PreferredCurrency, Notes, CreatedAt, UpdatedAt, CancelledAt, CancellationReason)
- Apply migrations: `just db-customers`

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

```powershell
curl -X POST http://localhost:5010/api/ai/chat `
  -H "Content-Type: application/json" `
  -d '{"prompt": "What is machine learning?", "systemPrompt": "You are a helpful assistant."}'
```

### Example: Semantic Search

```powershell
curl -X POST http://localhost:5010/api/ai/search `
  -H "Content-Type: application/json" `
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
| `CustomerUpdatedConsumer` | `CustomerUpdated` | Fetch customer via REST, generate embedding, upsert in Qdrant (collection `customers`) |
| `CustomerCancelledConsumer` | `CustomerCancelled` | Remove customer point from Qdrant (collection `customers`) |

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

**Chat RAG** (`POST /api/ai/chat`): the user's question is embedded, then **both** Qdrant collections are queried in parallel—**orders** and **customers**. The top similar orders and top similar customers are merged into a single context (sections "DATI ORDINI" and "DATI CLIENTI"), and Ollama answers based on that combined context. So the chatbot can answer questions about both orders and customers (e.g. "Which customers are in Italy?", "Orders for company X").

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
│       └── wipe-data.ps1
├── justfile                 # All project commands (run `just` to list)
├── mise.toml                # Tool versions (dotnet, ollama, pwsh, just)
└── DistributedPlayground.sln
```

## Frontend - Ordering Web (Angular)

Angular 17 SPA for managing orders and customers.

**Location:** `src/Frontend/ordering-web`

### Pages

| Page | Route | Description |
|------|-------|-------------|
| Order List | `/orders` | List orders with customer display name, customer ref, status, total, actions |
| Order Detail | `/orders/:id` | View order details and execute workflow actions; customer shown by display name with link to customer page |
| Create Order | `/orders/new` | Create a new order with lines |
| Customer List | `/customers` | List all customers (Active / Cancelled) |
| Customer Detail | `/customers/:id` | View customer, edit (partial update), or cancel (soft delete) |
| Create Customer | `/customers/new` | Create a new customer with company, contact, and optional addresses |

### Running the Frontend

```powershell
just frontend-install   # npm install (first time)
just frontend           # npm start
```

**URL:** http://localhost:4200 (or http://127.0.0.1:4200 — CORS allows both)

For the app to work fully, the **backends** must be running: Ordering.Api (5001), Customers.Api (5003), and optionally AI.Processor (5010), Orchestrator.Api (5020). Otherwise lists will be empty or the AI panel will show connection errors.

### Features

- **Orders:** List, create, detail with workflow actions (Start Processing, Ship, Deliver, Invoice, Cancel)
- **Customers:** List, create, detail with edit (partial update) and cancel (soft delete with reason)
- Modal dialogs for actions requiring input (e.g. cancel reason)
- **AI Chat Assistant** - Always visible floating panel with:
  - **Chat with:** RAG (AI.Processor) or Semantic Kernel (Orchestrator.Api) — switch in the panel
  - 💬 Chat mode: Ask questions (uses selected backend)
  - 🔍 Search mode: Semantic search over orders in Qdrant (RAG only)
  - 📊 Analyze mode: AI analysis of text (RAG only)
- Responsive design

### AI Chat Assistant

- **RabbitMQ queue stats**: In the navbar, a small widget shows total messages across all RabbitMQ queues and a sparkline of recent values (polling Ordering API `GET /api/metrics/rabbitmq` every 5s).

The AI Chat panel is always visible in the bottom-right corner. You can choose the chat backend:

- **RAG** (default): Connects to AI.Processor (Qdrant + Ollama). Chat, Search Orders, and Analyze are all available.
- **Semantic Kernel**: Connects to Orchestrator.Api (Ollama + plugins). Only Chat is available; Search and Analyze are disabled in this mode. You can ask to create a customer (e.g. “crea un cliente Acme SPA con email x@y.it”) and the assistant will call the plugin to send the command.

Click the 🤖 button to open/close the panel. Use "Chat with: RAG" or "Chat with: Semantic Kernel" to switch backends.

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
| `CustomerUpdated` | Customers | Customer was updated |
| `CustomerCancelled` | Customers | Customer was cancelled (soft delete) |
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
just infra-down           # Stop infra (without Docker Ollama)
just infra-down-ollama    # Stop infra + Docker Ollama
just infra-down-volumes   # Stop and remove all volumes (data loss!)
```

## Order Simulator Tool

Console application to **create test customers** (via MassTransit) and **generate test orders**, then optionally simulate workflow transitions. Uses RabbitMQ for both `CreateCustomer` and `CreateOrder` commands.

### Usage

```powershell
just simulate                   # Default: 10 customers + 10 orders + workflow
just simulate-quick 50          # 50 orders, no workflow
just simulate-custom c=15 n=50  # 15 customers, 50 orders, no workflow
just simulate-help              # Show all options
```

### Options

| Option | Alias | Description | Default |
|--------|-------|-------------|---------|
| `--orders` | `-n` | Number of orders to create | 10 |
| `--customers` | `-c` | Number of customers to create via MassTransit when no active customers exist | 10 |
| `--simulate-workflow` | `-w` | Simulate status transitions after creating orders | true |
| `--delay` | `-d` | Delay between commands (ms) | 500 |
| `--rabbit-host` | | RabbitMQ host | localhost |
| `--rabbit-user` | | RabbitMQ username | playground |
| `--rabbit-password` | | RabbitMQ password | playground_pwd |
| `--customers-api` | | Customers API base URL (fetch customer list after creating via RabbitMQ or when reusing existing) | http://localhost:5003 |

### Simulation Phases

0. **Create Customers (if none)**  
   If no active customers exist, the tool sends `--customers` (default 10) **CreateCustomer** commands to RabbitMQ (`queue:create-customer`). Customers.Api consumes them and creates the customers; the simulator then waits ~3 s and fetches the new customer IDs via GET `api/customers`. **CustomerCreated** events are published so AI.Processor can index them in Qdrant. If customers already exist, they are reused and no new ones are created.
1. **Create Orders**  
   Uses the customer IDs (from Phase 0 or existing). Sends **CreateOrder** commands to RabbitMQ (`queue:create-order`) with random products, addresses, and one random customer per order.
2. **Fetch Orders**  
   Gets created order IDs from Ordering API (GET `api/orders`).
3. **Workflow Transitions**  
   If `--simulate-workflow` is true, randomly applies: Start Processing → Ship (carrier, tracking) → Deliver → Invoice, or Cancel for 10–20% of orders.

### Prerequisites

- **RabbitMQ** running (e.g. Docker infra): the simulator sends **CreateCustomer** and **CreateOrder** commands on the message bus.
- **Customers.Api** on `http://localhost:5003`: consumes CreateCustomer; the simulator calls GET `api/customers` to obtain customer IDs (after creating or to reuse existing).
- **Ordering.Api** on `http://localhost:5001`: consumes CreateOrder; required for workflow simulation (fetch orders and apply transitions).

## Development Notes

- Services are configured to run locally (not in Docker) during development
- Infrastructure runs in Docker containers
- Each bounded context uses a separate schema in the shared PostgreSQL database
- OpenTelemetry traces are sent to Jaeger for visualization
- Health checks available at `/health` endpoint for each service
