# 🧠 CONTEXT.md - Session Summary

> Questo file contiene il contesto e la cronologia delle decisioni prese durante lo sviluppo dei progetti nel monorepo **ai-dlc**.
> Ultimo aggiornamento: 2026-03-05

---

# Progetto 1: Distributed Playground

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

---
---

# Progetto 2: Scaile

## 📋 Panoramica Progetto

**Scaile** è una piattaforma di automazione del workflow di sviluppo software basata su AI, che utilizza Claude Code CLI con GitLab per orchestrare il ciclo di vita completo: da requisiti ad alto livello → user stories → task di sviluppo → implementazione codice → merge request, con gate di approvazione umana (Human-In-The-Loop / HITL) nelle fasi critiche.

---

## 🏗️ Architettura

### Tech Stack

| Layer | Tecnologia |
|-------|-----------|
| **Backend** | Python 3.12, FastAPI, Uvicorn |
| **Frontend** | React 18.3, TypeScript, Vite 6, TanStack Query v5 |
| **AI Engine** | Claude Code CLI (subprocess con streaming JSON) |
| **Integrazione** | MCP (Model Context Protocol) → GitLab server |
| **Infrastruttura** | Docker Compose, GitLab CE, GitLab Runner |
| **Package Manager** | pnpm 10.28 (workspace) |
| **Node** | 24 |

### Servizi Docker

| Container | Porta | Uso |
|-----------|-------|-----|
| **GitLab** | 8090 | Source control, issue tracking, CI/CD, board |
| **GitLab Runner** | — | Docker executor per CI/CD pipeline |
| **Webhook Server** | 8000 | FastAPI: riceve webhook GitLab, orchestra Claude Code CLI |
| **Log Viewer** | 3000 | React SPA + nginx: monitoraggio sessioni in real-time |

---

## 🔄 Workflow a 8 Stadi

```
[1. Requirements] → [2. Breakdown] → [3. Refinement] → [4. Ready] → [5. Planned] → [6. Review] → [7. Test] → [8. Done]
     (HITL)            (AI)            (HITL)           (AI)          (AI)           (HITL)       (HITL)
```

| Stadio | Attore | Trigger | Azione |
|--------|--------|---------|--------|
| **1. Requirements** | PO, BA | Creazione epic | Epic issue con label "Requirements" |
| **2. Breakdown** | AI (Claude) | Label "Breakdown" → webhook | Legge epic, crea 3–7 user stories con acceptance criteria |
| **3. Refinement** | PO, BA, DEV | Manuale | Review e raffinamento delle stories |
| **4. Ready** | AI (Claude) | Label "Ready" → webhook | Clona repo, analizza codebase, crea 2–5 task per story |
| **5. Planned** | AI (Claude) | Label "Planned" → webhook | Crea git worktree, implementa codice, apre Merge Request |
| **6. Review** | DEV + AI | Commento su MR → webhook (note event) | Code review; Claude risponde ai commenti: corregge codice, chiede chiarimenti o ringrazia |
| **7. Test** | TESTER | Manuale | Test in staging, verifica acceptance criteria |
| **8. Done** | — | — | Completato |

### Flusso Dati

```
[GitLab Issue] ── label change ──→ [Webhook: POST /webhook/gitlab]
[GitLab MR]    ── note event ────→          │
                                    Detect trigger label / MR note
                                    Load prompt template
                                    Prepare env (clone/worktree)
                                          │
                                          ▼
                                   [Claude Code CLI]
                                    ├─ MCP tools (GitLab API)
                                    ├─ File system access
                                    └─ Git commands
                                          │
                                    Stream JSON events
                                          │
                                          ▼
                              [Session Manager] ── WebSocket ──→ [Log Viewer UI]
                                    │
                              Persist to disk
                              (/app/sessions/*.json)
```

---

## 📝 Decisioni Architetturali

### 1. **Claude Code CLI come subprocess**
- Invocato via `asyncio.create_subprocess_exec()` con output JSON streaming
- Accesso a MCP server, filesystem, git e reasoning AI in un unico tool
- Flag `--dangerously-skip-permissions` (ambiente trusted)
- Disaccoppia orchestrazione workflow da logica AI

### 2. **Persistenza sessioni (In-Memory + Disk)**
- Sessioni attive in `SessionManager._active` (dict in memoria)
- Sessioni completate flushed su `/app/sessions/*.json`
- Debounced flush (2s) per ridurre I/O
- Sopravvive a restart container; nessun DB esterno richiesto

### 3. **Git Worktree per implementazione (Planned stage)**
- `git worktree add -b feature/<iid>-<slug> <dir> origin/main`
- Ogni story isolata su branch separato
- Cleanup facile dopo merge

### 4. **Clone read-only per analisi (Ready stage)**
- Clone del repo una volta, fetch su invocazioni successive
- Analisi codebase senza modifiche
- Task creati basandosi sulla struttura codice reale

### 5. **Prompt-driven workflow**
- File `.md` separati per ogni stadio (`breakdown.md`, `ready.md`, `planned.md`)
- Contengono ruolo, contesto, istruzioni e output attesi
- Versionati in git, facili da modificare

### 6. **MCP Server per GitLab**
- Configurato in `.mcp.json` del webhook server
- Tools: `get_issue`, `create_issue`, `update_issue`, `create_issue_link`, `list_issue_links`, `create_merge_request`, `get_merge_request`, `get_merge_request_diffs`, `create_merge_request_note`
- Autenticazione via `GITLAB_PERSONAL_ACCESS_TOKEN`

### 7. **MR Comment Review (Review stage)**
- Webhook `note_events: true` rileva commenti sulle Merge Request
- **Prevenzione loop infinito** (3 livelli): (1) filtro `bot_username` — ignora commenti del bot stesso, (2) filtro `system: true` — ignora note di sistema GitLab, (3) filtro `action != "create"` — ignora edit/update delle note
- **Bot username** auto-rilevato all'avvio via `GET /api/v4/user` con bot token; override manuale via `GITLAB_BOT_USERNAME`
- Claude riceve il commento del reviewer, legge diff MR via MCP, corregge il codice (commit + push sul source branch) oppure chiede chiarimenti, e risponde sulla MR via `create_merge_request_note`
- Worktree creato su branch esistente (a differenza del Planned stage che crea un nuovo branch)

### 8. **Claude Code Skills (domain + workflow)**
- Skills installate nel Dockerfile del webhook server via `npx skills add` (globali, `/home/claude/.claude/skills/`)
- **Domain skills**: dotnet-skills (30 skills .NET/C#), angular-skills (10 skills Angular), docker-expert
- **Workflow skills**: breakdown-epic-pm (epic decomposition), code-review-excellence (structured review)
- **Quality skills** (levnikolaevich): task-executor, task-reviewer, story-quality-gate, code-quality-checker, regression-checker
- Ogni prompt template (`breakdown.md`, `ready.md`, `planned.md`, `review.md`) include una sezione "Available Skills" che elenca le skill rilevanti per quello stadio
- Le skill vengono caricate automaticamente da Claude Code CLI come contesto aggiuntivo

---

## 🛠️ Componenti Principali

### Webhook Server (`packages/webhook-server/`)
| File | Responsabilità |
|------|---------------|
| `main.py` | App FastAPI: middleware, CORS, routes, startup, bot username auto-detection |
| `webhook_handler.py` | Orchestrazione: detect trigger/MR note, prepare env, invoke Claude, stream logs |
| `session_manager.py` | Tracking sessioni attive + completate, persistenza su disco |
| `sessions_router.py` | REST + WebSocket API per sessioni |
| `config.py` | Configurazione ambiente (pydantic-settings) |

### Prompt Templates (`packages/webhook-server/prompts/`)
| File | Ruolo | Output |
|------|-------|--------|
| `breakdown.md` | Senior product owner | 3–7 story issues con acceptance criteria |
| `ready.md` | Senior technical lead | 2–5 task issues per story (linked come children) |
| `planned.md` | Senior software engineer | Codice committato + Merge Request creata |
| `review.md` | Senior software engineer | Risposta a commento MR: fix codice o chiarimento |

### Log Viewer (`packages/log-viewer/`)
- Dark-themed terminal UI (JetBrains Mono)
- Session list (sidebar) con status badges (Running/Success/Error)
- Session detail con log streaming real-time via WebSocket
- Auto-refresh ogni 5s via React Query

### Target Repository
- Usa il progetto **Distributed Playground** dalla root del monorepo (`../distributed-playground/`)
- .NET 9 microservizi + AI (Ordering, Customers, AI.Processor, Orchestrator, Projections)
- Pushato su GitLab dallo script di setup via `git push`

### Scripts (`scripts/`)
| Script | Scopo |
|--------|-------|
| `setup-gitlab.mts` | Bootstrap GitLab: gruppo, repo, labels, board, webhook, runner, bot token |
| `access-token-gitlab.mts` | Genera personal access token via Rails runner |

---

## 🚀 Quick Start

```bash
cd scaile

# 1. Configurazione
cp .env.example .env
# Editare .env con ANTHROPIC_API_KEY

# 2. Avvio infrastruttura (solo GitLab + Runner)
pnpm docker:infra

# 3. Setup GitLab (dopo che GitLab è healthy ~2-3 min)
#    Crea gruppo, repo, labels, board, webhook, runner e bot token
#    token user token must be manually updated in .env
pnpm run access-token:gitlab

# this can be run everytime you want to start from scratch
pnpm run setup:gitlab --force

# 4. Avvio app (webhook + log-viewer, ora con bot token corretto)
pnpm docker:app

# 5. Accesso
# GitLab:     http://localhost:8090 (root / <GITLAB_ROOT_PASSWORD>)
# Log Viewer: http://localhost:3000
# Webhook:    http://localhost:8000/health

# 6. Test workflow
# Creare un epic in GitLab → aggiungere label "Breakdown" → osservare nel Log Viewer
```

---

## 📁 Struttura Progetto

```
scaile/
├── packages/
│   ├── webhook-server/          ← Python FastAPI + Claude orchestrator
│   │   ├── src/
│   │   │   ├── main.py
│   │   │   ├── config.py
│   │   │   ├── webhook_handler.py
│   │   │   ├── session_manager.py
│   │   │   └── sessions_router.py
│   │   ├── prompts/
│   │   │   ├── breakdown.md
│   │   │   ├── ready.md
│   │   │   └── planned.md
│   │   ├── Dockerfile
│   │   ├── .mcp.json
│   │   └── requirements.txt
│   ├── log-viewer/              ← React SPA monitoring
│   │   ├── src/
│   │   │   ├── App.tsx
│   │   │   ├── api.ts
│   │   │   ├── useSessionStream.ts
│   │   │   └── components/
│   │   ├── Dockerfile
│   │   └── nginx.conf
│   └── n8n-nodes-refinement/    ← Custom n8n node (opzionale, non usato)
├── (no sample-repository)       ← usa ../distributed-playground/ dal monorepo
├── scripts/
│   ├── setup-gitlab.mts
│   └── access-token-gitlab.mts
├── docker-compose.yml
├── package.json                 ← pnpm workspace root
├── pnpm-workspace.yaml
├── .mise.toml                   ← Tool versions (Node 24, pnpm 10.28)
├── .env.example
└── WORKFLOW.md                  ← Descrizione stadi workflow
```
