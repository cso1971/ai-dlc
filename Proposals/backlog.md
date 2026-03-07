# AI-DLC Proposals — Backlog

Overview of all proposals in the AI-DLC ecosystem. Each proposal typically has a concept document (generic audience) and a technical implementation document (design, roadmap).

---

## Active proposals

### 1. Council of Agents

**Folder:** [`CouncilOfAgents/`](./CouncilOfAgents/)

**Description:** Evolves the Scaile workflow from a single-agent pipeline to a **multi-agent Council** where humans and AI agents collaborate as peers. A Coordinator orchestrates deliberation rounds where Product Analyst, Architect, Developer, QA Strategist, and Code Reviewer agents propose, object, and reach consensus before execution.

**Key documents:**
- [council-of-agents.md](./CouncilOfAgents/council-of-agents.md) — Concept and vision
- [council-technical-implementation.md](./CouncilOfAgents/council-technical-implementation.md) — Technical design, agent registry, deliberation log

---

### 2. Back Office and Pluggable ALM

**Folder:** [`ALM/`](./ALM/)

**Description:** A **Back Office portal** for pre-startup configuration of the DLC Server. Allows selecting the ALM (GitLab, Jira, Azure DevOps, GitHub, etc.) among main market options, configuring credentials and webhooks, validating connections, and persisting configuration in a structured way — instead of hardcoded `.env` files.

**Key documents:**
- [back-office-alm-proposal.md](./ALM/back-office-alm-proposal.md) — Concept and stakeholders

---

### 3. Codebase Intelligence

**Folder:** [`CodebaseIntelligence/`](./CodebaseIntelligence/)

**Description:** Enriches **code-writing agents** with project-aware knowledge. Injects `.cursorrules`, `CONTEXT.md`, and structured docs so that agent output aligns with conventions, architecture, and team practices. Reduces review cycles and improves alignment with existing codebase patterns.

**Key documents:**
- [codebase-intelligence-concept.md](./CodebaseIntelligence/codebase-intelligence-concept.md) — Concept and audience
- [codebase-intelligence-technical-implementation.md](./CodebaseIntelligence/codebase-intelligence-technical-implementation.md) — Approaches (file injection, RAG, Council context), roadmap

---

### 4. Provisioning

**Folder:** [`Provisioning/`](./Provisioning/)

**Description:** **Automates** the setup of AI-DLC environments for local development and VPS deployment. Single-command bootstrap (e.g. `pnpm provision`) that replaces manual steps: start infra, wait for GitLab, generate tokens, run setup, start app. Profiles for different scenarios (local-full, vps-minimal, etc.). VPS playbook for standardized deployment.

**Key documents:**
- [provisioning-concept.md](./Provisioning/provisioning-concept.md) — Concept and audience
- [provisioning-technical-implementation.md](./Provisioning/provisioning-technical-implementation.md) — Current state, approaches, 5-phase roadmap

---

## Candidate proposals (backlog)

| Proposal | Brief description |
|----------|-------------------|
| **Observability & Monitoring** | Dashboard, metrics (sessions, throughput, errors), alerting, audit trail for the DLC Server |
| **CI/CD Integration** | Pipeline integration: build, test, deploy to staging on merge; gate workflow before Test stage |
| **Secrets & Security** | Centralized secrets management (Vault, cloud), token rotation, access control for Back Office |
| **Target Repository Flexibility** | Support any repo as target; multi-repo; configurable via Back Office |
| **Human-in-the-Loop UX** | Notifications, approval UI, escalation flow for human participants |
| **Multi-Model / LLM Provider** | Support Ollama, OpenAI, Azure OpenAI alongside Claude; per-stage or per-agent selection |
| **Prompt Engineering & Versioning** | Version prompts in Git; A/B testing; metrics on output quality |
| **Cost & Usage Analytics** | Token usage, cost tracking, budget alerts per stage/agent |
| **Quality Gates & Validation** | Automated pre-merge checks (lint, test, code quality); integration with regression checker |
| **Offline / Air-Gapped Mode** | Full local operation (Ollama only, self-hosted ALM) for restricted environments |

---

## Dependencies

```
Council of Agents
       │
       ├── Codebase Intelligence (Council context for Developer)
       │
Provisioning ──┬── ALM (Back Office config source)
               └── Secrets & Security (VPS)
```

---

*Document generated on March 7, 2026 — ai-dlc project*

**Christian Soliman** — [christian.soliman@adesso.ch](mailto:christian.soliman@adesso.ch)
