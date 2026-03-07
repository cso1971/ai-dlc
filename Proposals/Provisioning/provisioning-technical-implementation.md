# Provisioning — Technical Implementation

### Approaches, current state, and roadmap for automated environment setup

---

## 1. Current state

### Scaile (DLC Server) bootstrap

| Step | Command / Action | Notes |
|------|------------------|-------|
| 1 | `pnpm docker:infra` | Start GitLab + Runner |
| 2 | Wait ~2–3 min | GitLab health check |
| 3 | `pnpm run access-token:gitlab` | Generate token via Rails runner |
| 4 | Update `.env` with `GITLAB_TOKEN` | Manual |
| 5 | `pnpm run setup:gitlab --force` | Create group, repo, labels, board, webhook, runner, bot token |
| 6 | `pnpm docker:app` | Start webhook + log-viewer |

**Pain points**: Multiple commands, manual token update, no single entry point, no VPS playbook.

### Distributed Playground (target repo)

| Step | Command | Notes |
|------|---------|-------|
| 1 | `just infra-up` | PostgreSQL, RabbitMQ, Qdrant, Redis, Jaeger, Keycloak |
| 2 | `just db-all` | Create schemas |
| 3 | `just ollama-init` | Pull models (optional) |
| 4 | `just run` | Start .NET services |

**Note**: Separate from Scaile; used as target repository for the workflow.

### VPS scenario

- **No standard playbook** — Deployment is ad-hoc
- **Configuration** — `.env` must be created manually; URLs differ (public domain vs. localhost)
- **Secrets** — No defined strategy (env vars, secret manager, etc.)

---

## 2. Approaches evaluated

### A. Unified provisioning script

**Idea**: Single script (e.g. `provision-local.mts`) that orchestrates all steps: start infra, wait for health, run setup, start app, verify.

| Pros | Cons |
|------|------|
| One entry point | Conditional logic can grow complex |
| Reproducible | Requires maintenance |
| Self-documenting | |

**Implementation**: TypeScript/Node script using `zx` or similar. Prerequisite checks, retries, clear output.

---

### B. Infrastructure as Code (Terraform / Pulumi)

**Idea**: Define VPS resources (VM, network, firewall) and optionally container orchestration as code.

| Pros | Cons |
|------|------|
| Reproducible infra | Overkill for local dev |
| Versioned | Learning curve |
| Idempotent | |

**Implementation**: Terraform modules for VPS; Docker Compose for containers. Separate from local provisioning.

---

### C. Pre-configured container stack

**Idea**: Docker Compose "production-ready" with config from env/Back Office; single `docker compose up` for full stack.

| Pros | Cons |
|------|------|
| Simple deploy | Less flexibility |
| Portable | Updates require image rebuild |

**Implementation**: Compose file with profiles; env template; health checks and dependencies.

---

### D. Interactive wizard

**Idea**: CLI wizard that asks target (local/VPS), ALM choice, credentials; generates `.env` and runs provisioning.

| Pros | Cons |
|------|------|
| Good UX | Not suitable for CI/CD |
| Guides user | Requires UI development |

**Implementation**: Inquirer.js or similar; outputs config files and invokes scripts.

---

### E. Provisioning profiles

**Idea**: Predefined profiles: `local-dev`, `local-full`, `vps-minimal`, `vps-full`. Each profile defines which components to start and how.

| Pros | Cons |
|------|------|
| Clear use cases | More variants to maintain |
| Optimized per scenario | |

**Implementation**: Profile config (YAML/JSON); script reads profile and executes corresponding steps.

---

## 3. Approach comparison

| Approach | Effort | Impact | Best for | Priority |
|----------|--------|--------|----------|----------|
| **A. Unified script** | Low | High | Local bootstrap | 1 |
| **E. Profiles** | Medium | High | Local + VPS variants | 2 |
| **C. Pre-configured stack** | Medium | Medium | VPS quick deploy | 3 |
| **B. IaC** | High | High | VPS infra | 4 |
| **D. Wizard** | Medium | Medium | Onboarding UX | 5 |

---

## 4. Implementation roadmap

### Phase 1: Local bootstrap automation (1–2 weeks)

1. **`provision-local.mts` script**
   - Check prerequisites (Docker, pnpm, Node)
   - Run `docker:infra`
   - Poll GitLab health (with timeout)
   - Run `access-token:gitlab`, parse output, update `.env`
   - Run `setup:gitlab`
   - Run `docker:app`
   - Verify all services (webhook `/health`, log-viewer, GitLab)

2. **Single command**
   - `pnpm provision:local` or `just provision-local`
   - Optional: `--skip-gitlab-setup` if already configured

3. **Output**
   - Summary with URLs (GitLab, Log Viewer, Webhook) and status

**Files to create/modify**: `scaile/scripts/provision-local.mts`, `scaile/package.json`

---

### Phase 2: Profiles and parameters (2–3 weeks)

1. **Profiles**
   - `local-dev` — Minimal: webhook + log-viewer only (assumes external GitLab/ALM)
   - `local-full` — GitLab + Runner + webhook + log-viewer (current flow)
   - `vps-minimal` — Webhook + log-viewer (ALM cloud)
   - `vps-full` — GitLab + Runner + webhook + log-viewer (self-hosted ALM)

2. **Parameters**
   - `--profile=<name>`
   - `--skip-gitlab-setup`
   - `--alm=gitlab|jira` (for future ALM adapter)
   - `--target-repo=<path>` (for setup-gitlab)

3. **Profile config**
   - YAML/JSON defining which services to start, which scripts to run

---

### Phase 3: VPS playbook (2–3 weeks)

1. **Playbook** (Ansible or shell script)
   - Install Docker on fresh VPS (Ubuntu/Debian)
   - Clone repo (or copy artifacts)
   - Create `.env` from template (with placeholders for secrets)
   - Run `docker compose up -d` with appropriate profile
   - Configure webhook URL (public domain)
   - Optional: nginx reverse proxy, SSL (Let's Encrypt)

2. **Documentation**
   - VPS requirements (CPU, RAM, disk)
   - Ports to open (80, 443, 8090, 8000, 3000 as needed)
   - Firewall rules
   - Domain and DNS setup

3. **Secrets**
   - Document options: `.env` file, Docker secrets, cloud secret manager
   - Template `.env.example` for VPS with `GITLAB_URL` etc. as placeholders

---

### Phase 4: Back Office integration (when Back Office exists)

1. **Config source**
   - Provisioning reads config from Back Office if available
   - Fallback to `.env` for backward compatibility

2. **Pre-startup validation**
   - Back Office validates config before provisioning
   - Provisioning can trigger Back Office health check after deploy

---

### Phase 5: Local–VPS parity

1. **Unified flow**
   - Same script/entry point for local and VPS
   - `PROVISION_TARGET=local|vps` (or `--target`) drives differences

2. **Testing**
   - CI job that runs `provision-local` in a clean environment
   - Smoke tests: create epic, trigger workflow, verify session in Log Viewer

---

## 5. Technical details for Phase 1

### Script structure (`provision-local.mts`)

```typescript
// Pseudocode
async function provisionLocal() {
  await checkPrereqs();           // Docker, pnpm, .env has ANTHROPIC_API_KEY
  await run("pnpm docker:infra");
  await waitForGitLabHealthy();   // Poll /api/v4/version, timeout 5 min
  await run("pnpm access-token:gitlab");
  await updateEnvWithToken();     // Parse output, write to .env
  await run("pnpm setup:gitlab --force");
  await run("pnpm docker:app");
  await waitForServicesHealthy(); // webhook /health, log-viewer
  printSummary();
}
```

### Prerequisite checks

- Docker running (`docker ps`)
- pnpm installed
- `.env` exists with `ANTHROPIC_API_KEY` (or prompt user)
- `GITLAB_ROOT_PASSWORD` in `.env` (or use default)

### Health check helpers

- GitLab: `GET /api/v4/version` returns 200
- Webhook: `GET /health` returns 200
- Log Viewer: `GET /` returns 200 (or nginx serves SPA)

---

## 6. Open questions

1. **VPS provider** — AWS, Azure, Hetzner, DigitalOcean, other?
2. **ALM on VPS** — Self-hosted GitLab or cloud (Jira, GitHub)?
3. **Secrets on VPS** — Env file, Docker secrets, cloud secret manager?
4. **CI/CD** — Should provisioning run in pipeline (e.g. on merge to main)?
5. **Multi-tenant** — One VPS per team/project or shared?

---

## 7. Success metrics

- **Time to first workflow** — From clone to first epic breakdown: target under 15 minutes
- **Reproducibility** — Same result on fresh machine or VPS
- **Documentation** — Provisioning steps are the primary source of truth
- **Reduced support** — Fewer "how do I set this up?" questions

---

## 8. Related documents

- [`provisioning-concept.md`](./provisioning-concept.md) — Concept and audience
- [`Proposals/ALM/back-office-alm-proposal.md`](../ALM/back-office-alm-proposal.md) — Back Office (config source for provisioning)
- `CONTEXT.md` — Scaile and Distributed Playground state

---

*Document generated on March 7, 2026 — ai-dlc project*

**Christian Soliman** — [christian.soliman@adesso.ch](mailto:christian.soliman@adesso.ch)
