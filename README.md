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

### 2. Initialize Ollama Models

```powershell
# Run the initialization script
.\infra\scripts\init-ollama.ps1
```

### 3. Run .NET Services Locally (Development)

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

## AI.Processor (Event Consumer Worker)

Background worker service that consumes all Ordering events and processes them with AI capabilities.

### Features

- **Event Consumption**: Listens to all Ordering domain events via MassTransit/RabbitMQ
- **LLM Integration**: Uses Ollama for text generation and event analysis
- **Vector Storage**: Stores order embeddings in Qdrant for semantic search
- **Distributed Tracing**: Full OpenTelemetry integration with Jaeger

### Consumed Events

| Consumer | Event | Processing |
|----------|-------|------------|
| `OrderCreatedConsumer` | `OrderCreated` | Generate embedding, store in Qdrant, analyze with LLM |
| `OrderStatusChangedConsumer` | `OrderStatusChanged` | Update vector store, analyze status transition |
| `OrderShippedConsumer` | `OrderShipped` | Store shipping details, analyze logistics |
| `OrderDeliveredConsumer` | `OrderDelivered` | Update delivery status, completion analysis |
| `OrderCancelledConsumer` | `OrderCancelled` | Analyze cancellation reasons for insights |
| `OrderCompletedConsumer` | `OrderCompleted` | Generate order summary, final embedding |

### Configuration

```json
{
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
- Responsive design

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

# To also remove volumes (data)
docker-compose --profile infra down -v
```

## Development Notes

- Services are configured to run locally (not in Docker) during development
- Infrastructure runs in Docker containers
- Each bounded context uses a separate schema in the shared PostgreSQL database
- OpenTelemetry traces are sent to Jaeger for visualization
- Health checks available at `/health` endpoint for each service
