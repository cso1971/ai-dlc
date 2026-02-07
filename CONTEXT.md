# 🧠 CONTEXT.md - Session Summary

> Questo file contiene il contesto e la cronologia delle decisioni prese durante lo sviluppo del progetto.
> Ultimo aggiornamento: 2026-02-07

---

## 📋 Panoramica Progetto

**Distributed Playground** è un ambiente di esplorazione per architetture distribuite con AI, creato come sandbox per sperimentare con:
- Microservizi .NET 9
- Message-driven architecture (MassTransit + RabbitMQ)
- RAG (Retrieval-Augmented Generation) con Qdrant + Ollama
- Distributed tracing con OpenTelemetry + Jaeger

---

## 🏗️ Architettura

### Bounded Contexts
| Servizio | Porta | Descrizione |
|----------|-------|-------------|
| **Ordering.Api** | 5001 | Gestione ordini, aggregato Order, REST + MassTransit |
| **Invoicing.Api** | 5002 | Fatturazione (placeholder) |
| **Customers.Api** | 5003 | Clienti: CRUD (create, get, update, cancel soft-delete), REST + MassTransit, EF schema `customers` |
| **AI.Processor** | 5010 | Elaborazione AI, RAG, embedding, chat |

### Infrastruttura (Docker)
| Container | Porta | Uso |
|-----------|-------|-----|
| PostgreSQL | 5432 | Database (schema `ordering`) |
| RabbitMQ | 5672/15672 | Message broker |
| Qdrant | 6333/6334 | Vector database per RAG |
| Ollama | 11434 | LLM locale (llama3.2 + nomic-embed-text) |
| Jaeger | 16686/4317 | Distributed tracing |

### Frontend
- **Angular 17** su porta 4200
- Pagine: lista ordini, dettaglio, creazione, workflow actions
- **AI Chat Assistant**: sempre visibile, usa RAG per rispondere

---

## Sviluppo assistito

### Ad ogni modifica che chiedo e applichi 
- Esegui il commit sul branch corrente mettendo nel commento i dettagli delle modifiche
- Tieni aggiornato il file README.md 
- Tieni aggiornato il file CONTEXT.md


## 🔄 Flusso Dati

```
[OrderSimulator] 
     │ MassTransit (CreateOrder command)
     ▼
[Ordering.Api]
     │ Salva in PostgreSQL
     │ Pubblica OrderCreated event
     ▼
[RabbitMQ] ──────────────────────────────────┐
     │                                        │
     ▼                                        ▼
[AI.Processor]                          [Altri consumer]
     │ 1. Fetch order via REST API
     │ 2. Genera embedding (nomic-embed-text)
     │ 3. Salva in Qdrant
     │ 4. (Opzionale) Analisi AI con llama3.2
     ▼
[Qdrant] ← Vector storage per RAG
     │
     ▼
[Chat RAG] 
     │ 1. Embedding della domanda
     │ 2. Ricerca semantica in Qdrant
     │ 3. Contesto ordini → Ollama
     │ 4. Risposta basata su dati reali
```

---

## 📝 Decisioni Architetturali

### 1. **Single PostgreSQL con schema separati**
- Un'unica istanza PostgreSQL per semplicità
- Ogni bounded context usa schema dedicato (es. `ordering.orders`)
- Entity Framework Core con migrations

### 2. **Riferimento Order → Customer (integrazione tra bounded context)**
- L’aggregato **Order** (Ordering) riferisce il **Customer** (Customers) tramite **CustomerId** (stesso Guid di `Customer.Id`). Nessuna FK fisica tra DB; riferimento logico tra context.
- **CustomerReference** resta un campo opzionale: riferimento ordine lato cliente (es. numero PO, loro riferimento), non l’identità del cliente.
- **Implementazione**: Ordering.Api chiama Customers.Api (HTTP client) per validare che il CustomerId esista in fase di CreateOrder; in caso contrario restituisce 400. Frontend (creazione ordine) e Order Simulator usano la lista clienti da Customers API per selezionare/assegnare il CustomerId.

### 3. **AI.Processor come servizio separato**
- Disaccoppia elaborazione AI dal dominio business
- Consuma eventi da RabbitMQ (OrderCreated, CustomerCreated, CustomerUpdated, CustomerCancelled, ecc.)
- Espone REST API per chat/search
- Indicia ordini e clienti in Qdrant (collections `orders`, `customers`) per RAG
- **Customer**: CustomerCreated/CustomerUpdated → fetch da Customers API, embedding, upsert in Qdrant; CustomerCancelled → rimozione punto dalla collection `customers`

### 4. **RAG implementato nel chat endpoint**
- `/api/ai/chat` cerca prima in Qdrant
- Passa i risultati come contesto a Ollama
- Risponde basandosi su dati reali, non conoscenza generica
- **MaxResults: 100** (default) - numero massimo di ordini usati come contesto

### 5. **Embedding vs Analisi AI**
- **Embedding** (nomic-embed-text): ~200ms, necessario per RAG
- **Analisi AI** (llama3.2): ~40 sec, opzionale, può causare timeout

---

## 🐛 Problemi Risolti

### Timeout Ollama (100 sec → 600 sec)
**Problema**: Con 50 ordini in batch, Ollama non riusciva a completare le richieste.
**Soluzione**: Aumentato `HttpClient.Timeout` a 10 minuti in `OllamaService.cs`.
**Config**: `appsettings.json` → `Ollama:TimeoutMinutes: 10`

### RAG non funzionante
**Problema**: Il chatbot rispondeva con conoscenza generica invece di usare i dati degli ordini.
**Soluzione**: Modificato `/api/ai/chat` per:
1. Generare embedding della domanda
2. Cercare in Qdrant ordini rilevanti
3. Costruire prompt con contesto reale
4. Far rispondere Ollama con i dati trovati

### Messaggi in coda errori (order-created_error)
**Problema**: Messaggi finivano in `order-created_error` (es. 14 su 20 ordini).
**Causa verificata**: Non timeout, ma **Qdrant RpcException "Collection already exists"** – race tra consumer: il primo crea la collection `orders`, i successivi chiamano CreateCollection e ricevono AlreadyExists, eccezione → fault queue.
**Soluzione**: In `QdrantService.EnsureCollectionExistsAsync` gestire l’eccezione quando il messaggio contiene "AlreadyExists" / "already exists" (trattare come successo e uscire). Alternativa per riprocessare: usare RabbitMQ Management API per spostare messaggi dalla coda errori alla coda principale.

### Ollama sovraccarico (978% CPU)
**Problema**: Troppe richieste parallele saturavano Ollama.
**Causa**: Analisi AI sincrona per ogni ordine (~40 sec ciascuna).
**Mitigazione**: Timeout esteso, ma potrebbe servire rimuovere analisi AI o usare modello più leggero.

---

## 🛠️ Tools Creati

### OrderSimulator
Console app per generare ordini di test:
```bash
dotnet run --project src/Tools/OrderSimulator -- -n 50 -w false
```
- `-n`: numero ordini
- `-c`/`--customers`: numero clienti da creare se non ne esistono (default 10); inviati come comandi **CreateCustomer** su MassTransit (`queue:create-customer`), così **CustomerCreated** viene pubblicato e consumato da AI.Processor per Qdrant
- `-w`: simula workflow (Start → Ship → Deliver → Invoice)
- `-d`: delay tra ordini (ms)

---

## 📊 Stato Attuale

### Database
- **50 ordini** creati in PostgreSQL
- Schema: `ordering.orders`, `ordering.order_lines`

### Qdrant
- Collection: `orders`
- Embedding: 768 dimensioni (nomic-embed-text)
- Payload: order_id, customer_id, status, total_amount, etc.

### Code RabbitMQ
- `order-created`: consumer attivo
- `order-shipped`, `order-delivered`, etc.: consumer attivi
- `order-created_error`: riprocessabile con script

### Servizi
- Ordering.Api: ✅ Running (http://localhost:5001)
- AI.Processor: ✅ Running (http://localhost:5010)
- Angular Frontend: ✅ Running (http://localhost:4200)

---

## 🚀 Quick Start per nuovo PC

```bash
# 1. Clona repo
git clone <repo-url>
cd DistributedPlayground

# 2. Avvia infrastruttura Docker
cd infra && docker-compose up -d

# 3. Inizializza modelli Ollama
./infra/scripts/init-ollama.ps1  # o .sh

# 4. Crea schema DB Ordering (schema ordering + tabelle)
# Da root repo, con Docker avviato:
#   PowerShell: Get-Content infra/scripts/create-ordering-schema.sql | docker exec -i playground-postgres psql -U playground -d playground_db
#   Bash:      cat infra/scripts/create-ordering-schema.sql | docker exec -i playground-postgres psql -U playground -d playground_db
# Alternativa: dotnet ef database update --project src/Services/Ordering.Api

# 5. Avvia servizi
dotnet run --project src/Services/Ordering.Api --urls "http://localhost:5001"
dotnet run --project src/Services/AI.Processor --urls "http://localhost:5010"

# 6. Avvia frontend
cd src/Frontend/ordering-web && npm install && npm start

# 7. Genera ordini di test
dotnet run --project src/Tools/OrderSimulator -- -n 20 -w false
```

---

## 💡 Prossimi Passi Suggeriti

1. **Ottimizzare AI.Processor**
   - Rimuovere analisi AI sincrona dai consumer
   - Usare modello più leggero (llama3.2:1b)
   - Parallelizzare consumer con prefetch

2. **Completare bounded contexts**
   - Implementare Invoicing.Api
   - Customers.Api: completato layer persistenza (CustomersDbContext, CustomersRepository, CustomerService, migration InitialCreate per schema `customers`)

3. **Migliorare RAG**
   - Aggiungere filtri (status, date range)
   - Implementare aggregazioni (count, sum)

4. **Aggiungere test**
   - Unit test per domain services
   - Integration test per consumer

---

## 📁 Struttura Progetto

```
DistributedPlayground/
├── infra/
│   ├── docker-compose.yml
│   ├── dockerfiles/
│   └── scripts/
├── src/
│   ├── Services/
│   │   ├── Ordering.Api/
│   │   ├── Invoicing.Api/
│   │   ├── Customers.Api/
│   │   └── AI.Processor/
│   ├── Shared/
│   │   └── Contracts/
│   ├── Frontend/
│   │   └── ordering-web/
│   └── Tools/
│       └── OrderSimulator/
├── README.md
├── CONTEXT.md          ← Questo file
└── .cursorrules        ← Regole per Cursor AI
```
