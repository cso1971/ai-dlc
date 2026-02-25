# 🧠 CONTEXT.md - Session Summary

> Questo file contiene il contesto e la cronologia delle decisioni prese durante lo sviluppo del progetto.
> Ultimo aggiornamento: 2026-02-20

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
| **Gateway** | 5000 | API Gateway (YARP); validazione JWT Keycloak (Step 2); CORS per :4200; `/` e `/health` pubblici, route proxy richiedono Bearer (audience `playground-api`, `ordering-web`, `account`); **Swagger UI aggregata** su `/swagger` (dropdown per tutti i servizi, route pubbliche via YARP con metadata `Public`); **OpenTelemetry** tracing verso Jaeger (ASP.NET Core + HttpClient instrumentation) |
| **Ordering.Api** | 5001 | Gestione ordini, aggregato Order, REST + MassTransit |
| **Invoicing.Api** | 5002 | Fatturazione (placeholder) |
| **Customers.Api** | 5003 | Clienti: CRUD (create, get, update, cancel soft-delete), REST + MassTransit, EF schema `customers` |
| **AI.Processor** | 5010 | Elaborazione AI, RAG, embedding, chat |
| **Orchestrator.Api** | 5020 | Semantic Kernel + Ollama: REST chat, MassTransit trigger, plugin HTTP e comandi |
| **Projections** | 5030 | CQRS read model: consumer MassTransit proiettano eventi ordine su Redis; endpoint `/api/projections/stats` e `/api/projections/flush` |

### Infrastruttura (Docker)
| Container | Porta | Uso |
|-----------|-------|-----|
| PostgreSQL | 5432 | Database (schema `ordering`) |
| RabbitMQ | 5672/15672 | Message broker |
| Qdrant | 6333/6334 | Vector database per RAG |
| Ollama | 11434 | LLM locale (llama3.2 + nomic-embed-text) — **opzionale in Docker**: consigliato Ollama nativo per GPU acceleration (Metal/CUDA/ROCm, 3-5x più veloce) |
| Jaeger | 16686/4317 | Distributed tracing |
| Redis | 6379 | Cache e read model per proiezioni CQRS (StackExchange.Redis) |
| Keycloak | 8180 | IdP; realm `playground`, client `playground-api` (backend), `ordering-web` (SPA). Import automatico da `infra/keycloak/playground-realm.json`. Admin: admin/admin. |

### Frontend
- **Angular 17** su porta 4200
- **Auth (Step 4):** Login con **Authorization Code + PKCE** (nessun flusso implicito). keycloak-angular + keycloak-js; init con `flow: 'standard'`, `pkceMethod: 'S256'`, `onLoad: 'login-required'`. Guard reindirizza a Keycloak se non autenticato; Bearer inviato al Gateway; Logout in navbar. Client Keycloak `ordering-web` (pubblico). Usare **http://localhost:4200** (non 127.0.0.1).
- Chiamate API tutte tramite **Gateway** (http://localhost:5000); environment con `apiUrl`, `keycloak` (url, realm, clientId).
- Pagine: lista ordini, dettaglio, creazione, workflow actions (tutte protette da guard).
- **AI Chat Assistant**: sempre visibile; selettore "Chat with: RAG | Semantic Kernel" per usare AI.Processor (RAG) o Orchestrator.Api (Semantic Kernel). Search e Analyze solo con RAG.

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
- `/api/ai/chat` cerca in Qdrant **sia** la collection `orders` **sia** la collection `customers` (ricerca semantica in parallelo), unisce i risultati in un unico contesto e lo invia a Ollama
- **Aggregati reali**: per risposte corrette sul valore totale, numero e conteggio per stato degli ordini, il chat recupera gli aggregati da Ordering.Api (`GET /api/orders/stats`) e li inietta nel contesto come sezione "DATI DI SISTEMA (aggregati reali)". Include: totale ordini, valore per valuta, **conteggio per stato** (Delivered, Shipped, Cancelled, ecc.). Il system prompt istruisce il LLM a usare solo questi dati per totali/somme/conteggi per stato, non i singoli ordini dalla search (che sono un sottoinsieme).
- Passa i risultati come contesto a Ollama
- Risponde basandosi su dati reali, non conoscenza generica
- **MaxResults** (default 10): limite totale contesto; viene ripartito tra ordini e clienti (5+5). Ottimizzato per llama3.2 (3B): top-5 coprono i risultati rilevanti, totali gestiti via stats aggregati

### 5. **Orchestrator.Api (Semantic Kernel)**
- Nuovo servizio che usa **Semantic Kernel** con **Ollama** (solo LLM locale, nessuna chiamata a API esterne).
- Espone REST (`POST /api/orchestrator/chat`) e consumer MassTransit per comando `RequestOrchestration` (queue `request-orchestration`).
- **Plugin**: `ServicesApi` (chiamate HTTP a Ordering.Api e Customers.Api: ordini, stats, clienti); `MassTransitCommands` (invio comandi `CreateOrder`, `CreateCustomer` su code MassTransit). Il plugin usa **IBus** (singleton) invece di ISendEndpointProvider (scoped) così il Kernel può restare singleton.
- **Chat**: uso di **PromptExecutionSettings** con **FunctionChoiceBehavior.Auto()** per abilitare l’auto-invocazione delle funzioni; **system prompt** che istruisce il modello a usare i plugin (es. SendCreateCustomer quando l’utente chiede di creare un cliente) e a non suggerire JSON o altri metodi.
- Il kernel può quindi sia leggere dati (ordini, clienti) sia inviare comandi (es. crea ordine, crea cliente) in base al prompt.

### 7. **CQRS Projections su Redis**
- Servizio dedicato `Projections` (porta 5030) consuma tutti gli eventi ordine (Created, StatusChanged, Shipped, Delivered, Cancelled, Completed) via MassTransit e proietta aggregazioni su Redis (contatori atomici INCR/DECR)
- Order snapshot salvati in Redis per lookup cross-evento; dimensioni aggregate: status, currency, customer-ref, shipping-method, created-month/year, delivered-month/year, product — ognuna con count, subtotal, grandTotal
- Endpoint REST: `GET /api/projections/stats` (tutte le dimensioni), `GET /api/projections/stats/{dimension}`, `POST /api/projections/flush` (reset proiezioni)
- Gateway YARP route: `/api/projections/*` → projections-cluster (porta 5030)
- **Frontend**: pagina Projections Dashboard (`/projections`) nel frontend Angular — mostra summary cards (total orders, last updated, active dimensions) e breakdown per dimensione con count, subtotal, grandTotal e barra distribuzione. Auto-refresh ogni 10s
- **Integrazione RAG**: `AI.Processor` chiama `GET /api/projections/stats` in parallelo all'embedding e alla ricerca Qdrant. I dati aggregati vengono formattati in testo italiano semanticamente stabile (frasi chiare per ogni dimensione) e inclusi nel contesto RAG come "STATISTICHE E PROIEZIONI ORDINI". Se il servizio Projections non è disponibile, fallback sulle SQL stats di `Ordering.Api`. Il system prompt istruisce il LLM a usare le proiezioni per tutte le domande su totali, conteggi e distribuzioni.

### 8. **Embedding vs Analisi AI**
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

### Ollama sovraccarico (978–1900% CPU)
**Problema**: Troppe richieste parallele o singola inferenza LLM saturano tutti i core CPU.
**Causa**: Senza GPU, l'inferenza llama3.2 usa tutti i thread CPU disponibili.
**Soluzioni applicate**:
- `OLLAMA_NUM_PARALLEL=1` per evitare richieste parallele
- `OLLAMA_MAX_LOADED_MODELS=2` per tenere llama3.2 + nomic-embed-text in VRAM simultaneamente (evita model swapping ~10s per chiamata)
- `OLLAMA_FLASH_ATTENTION=1` per ottimizzare l'uso memoria durante inferenza
- Limiti risorse Docker: `cpus: 8`, `memory: 8G` (modificabili in docker-compose.yml)
- **Raccomandazione**: abilitare GPU NVIDIA con `docker-compose.gpu.yml` per ridurre CPU al minimo e velocizzare di 10-20x

### Ollama in Docker non usa GPU
**Problema**: Docker Ollama non ha accesso alla GPU di default (su macOS nessun passthrough Metal; su Linux serve NVIDIA Container Toolkit per CUDA).
**Soluzione**: Ollama è ora **opzionale** in Docker (profile `ollama` separato da `infra`). Consigliato Ollama nativo (`mise install` include Ollama, poi `ollama serve`) per GPU acceleration automatica su qualsiasi piattaforma (Metal, CUDA, ROCm — 3-5x più veloce). Lo script `init-ollama.ps1` auto-detecta se usare nativo o Docker.

### Cannot resolve MassTransitCommandsPlugin (ISendEndpointProvider scoped)
**Problema**: `Cannot resolve 'Orchestrator.Api.Plugins.MassTransitCommandsPlugin' from root provider because it requires scoped service 'MassTransit.ISendEndpointProvider'.`
**Causa**: Il Kernel è registrato come singleton e alla costruzione risolve i plugin dal root provider; `ISendEndpointProvider` in MassTransit è scoped, quindi non risolvibile dal root.
**Soluzione**: In `MassTransitCommandsPlugin` usare **IBus** invece di `ISendEndpointProvider`. `IBus` è singleton e espone `GetSendEndpoint`, così il plugin può essere risolto quando si costruisce il Kernel.

### Keycloak init failed (nonce mismatch) – pagina bianca dopo login
**Problema**: Dopo login Keycloak, il token exchange (`/token`) restituiva 200 ma `keycloak.init()` rigettava con `undefined`, causando pagina bianca. Nessun Error object nel reject.
**Causa**: `onLoad: 'check-sso'` lancia un silent iframe login (`prompt=none`) che genera un nuovo nonce e lo salva in `sessionStorage`, sovrascrivendo il nonce del login principale. Quando il code exchange ritorna il token, il nonce nel token non corrisponde più al nonce in storage → `keycloak-js` chiama `promise.setError()` senza argomenti → reject con `undefined`.
**Soluzione**: Cambiato `onLoad` da `'check-sso'` a `'login-required'` e disabilitato nonce validation (`useNonce: false`) in `app.init.ts`. Il nonce mismatch persiste anche con `login-required` (probabile incompatibilità versione Keycloak server/keycloak-js); PKCE già protegge il code exchange, quindi l'impatto sicurezza è minimo. Semplificato anche il catch handler. Ripristinato `keycloak-js` a `^23.0.7`.

### Chat Semantic Kernel non crea il cliente (risponde con JSON)
**Problema**: Alla richiesta "crea un cliente Acme SPA" il chatbot rispondeva suggerendo un file JSON invece di eseguire la creazione; in un secondo caso restituiva il JSON della “chiamata” (name/parameters) senza eseguirla.
**Causa**: (1) Nessuna execution settings/system prompt (risolto con FunctionChoiceBehavior.Auto() e system prompt). (2) **Ollama** a volte restituisce la tool call come **testo** (un blocco JSON con name e parameters) invece che come messaggio tool_call, quindi Semantic Kernel non la invoca.
**Soluzione**: (1) PromptExecutionSettings + FunctionChoiceBehavior.Auto() e system prompt. (2) **Fallback** in `OrchestratorEndpoints`: dopo `InvokePromptAsync`, se la risposta assomiglia a un JSON `{"name":"Plugin-Function","parameters":{...}}`, si estrae il JSON (anche da blocchi \`\`\`json), si invoca manualmente `kernel.InvokeAsync(pluginName, functionName, kernelArgs)` e si restituisce il risultato del plugin (es. "CreateCustomer command sent successfully...") all’utente.

---

## 🛠️ Tools Creati

### justfile
Tutti i comandi del progetto sono nel `justfile` (root). Eseguire `just` per la lista completa. Esempi:
- `just infra-up` — avvia Docker (senza Ollama)
- `just ollama-init` — pull modelli Ollama
- `just db-all` — crea tutti gli schema DB
- `just db-wipe` — cancella tutti i dati (PostgreSQL + Qdrant)
- `just run` — avvia tutti i servizi in parallelo
- `just run-ordering` / `just run-customers` / `just run-ai` / `just run-orchestrator` — avvia singolo servizio
- `just frontend` — avvia Angular
- `just simulate` — genera ordini di test (default 10 clienti + 10 ordini + workflow)
- `just simulate-quick 50` — 50 ordini senza workflow
- `just simulate-custom c=15 n=50` — 15 clienti, 50 ordini

### OrderSimulator
Console app per generare ordini di test (vedi `just simulate-help` per opzioni):
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

### Frontend – widget RabbitMQ in navbar
- **Ordering.Api** espone `GET /api/metrics/rabbitmq` (chiama RabbitMQ Management API e restituisce totale messaggi e dettaglio per coda).
- Il portale Angular mostra in navbar (sempre visibile) il totale messaggi in tutte le code e uno sparkline aggiornato ogni 5 secondi.

### Servizi
- Ordering.Api: ✅ Running (http://localhost:5001)
- AI.Processor: ✅ Running (http://localhost:5010)
- Angular Frontend: ✅ Running (http://localhost:4200)

---

## 🚀 Quick Start per nuovo PC

```powershell
git clone <repo-url>
cd DistributedPlayground

just setup              # 1. Installa tool (dotnet, ollama, pwsh, just)
just infra-up           # 2. Avvia Docker (senza Ollama Docker su Apple Silicon)
just ollama-serve       # 3. Avvia Ollama nativo (Metal GPU) — in un terminale separato
just ollama-init        # 4. Pull modelli (llama3.2 + nomic-embed-text)
just db-all             # 5. Crea schema DB (ordering + customers)

// TODO: in just
# 6. Crea utente Keycloak (user1 / user1) — vedi README.md step 4 per dettagli

just run-ordering       # 7. Avvia servizi — ognuno in un terminale separato
just run-customers
just run-ai
just run-orchestrator
just frontend-install   # 8. Installa dipendenze frontend
just run-frontend       # 9. Avvia Angular (http://localhost:4200)
just simulate           # 10. Genera ordini di test
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
├── .github/
│   └── workflows/
│       └── build.yml          ← CI: build on PR
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
