# Provisioning — Concept

### Automated setup of AI-DLC environments for local development and VPS deployment

---

## 1. The challenge

The AI-DLC ecosystem (Scaile DLC Server, GitLab, webhook, log-viewer, target repositories) requires a **multi-step bootstrap** that today is largely manual:

- Start Docker infrastructure
- Wait for GitLab to become healthy
- Generate and configure access tokens
- Run setup scripts (groups, projects, labels, board, webhooks, runner)
- Start application services
- Verify that everything works

On a **VPS** (Virtual Private Server), the situation is even more fragmented: there is no standard playbook for deploying the DLC Server, and configuration differs from local development.

This leads to:
- **Long onboarding** — New team members spend hours getting the environment right
- **Inconsistency** — Local and VPS setups diverge, causing "works on my machine" issues
- **Fragility** — Manual steps are error-prone and hard to reproduce
- **No parity** — Difficult to test "as in production" locally

---

## 2. The initiative: Provisioning

**Provisioning** is the capability to **automate and standardize** the setup of AI-DLC environments, so that both **local development** and **VPS deployment** can be achieved with minimal manual intervention and maximum reproducibility.

### Goals

1. **One-command setup** — A single entry point (e.g. `just provision` or `pnpm provision`) that brings up a working environment
2. **Local–VPS parity** — Same conceptual flow for local and VPS, with differences handled by configuration
3. **Reproducibility** — Idempotent, documented steps that can be re-run safely
4. **Reduced friction** — Shorter time from clone to first workflow run

### Scope

This initiative covers:
- **Local development** — Bootstrap Scaile (GitLab, webhook, log-viewer) and optionally target repo infrastructure
- **VPS deployment** — Deploy the DLC Server stack on a Virtual Private Server
- **Profiles** — Different configurations for different use cases (minimal, full, ALM cloud vs. self-hosted)

---

## 3. Who benefits

| Audience | Benefit |
|----------|---------|
| **Developers** | Faster onboarding; one command to get a working environment |
| **DevOps** | Standardized VPS deployment; less custom scripting |
| **Tech Leads** | Consistent environments across team members; easier troubleshooting |
| **Vendors / Integrators** | Repeatable installation at customer sites |
| **New team members** | Reduced setup time from hours to minutes |

---

## 4. Principles

- **Automate the boring** — Eliminate manual, repetitive steps
- **Fail fast** — Check prerequisites early and report clearly
- **Document by doing** — Provisioning scripts serve as living documentation
- **Profile-based** — Support different scenarios (local-dev, local-full, vps-minimal, vps-full) without one-size-fits-all complexity

---

## 5. Expected outcomes

- **Faster onboarding** — New developers productive in minutes, not hours
- **Reproducible deployments** — VPS setup that can be recreated reliably
- **Consistency** — Local and VPS environments aligned where it matters
- **Maintainability** — Clear, versioned provisioning logic instead of tribal knowledge

---

## 6. Related documents

- [`provisioning-technical-implementation.md`](./provisioning-technical-implementation.md) — Technical design, approaches, and roadmap
- [`Proposals/ALM/back-office-alm-proposal.md`](../ALM/back-office-alm-proposal.md) — Back Office and ALM configuration (provisioning integration)
- `CONTEXT.md` — Current state of Scaile and Distributed Playground

---

*Document generated on March 7, 2026 — ai-dlc project*

**Christian Soliman** — [christian.soliman@adesso.ch](mailto:christian.soliman@adesso.ch)
