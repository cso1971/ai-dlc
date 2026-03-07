# Codebase Intelligence — Concept

### Enriching AI agents with project knowledge for more effective code output

---

## 1. The challenge

AI-powered development agents (such as Claude Code CLI in the Scaile workflow) can write code, create merge requests, and respond to review comments. However, their effectiveness depends heavily on **how well they understand the project** they are working on.

Today, agents receive:
- The task description (user story, acceptance criteria)
- Access to the codebase (filesystem)
- Generic skills (e.g. .NET, Angular, Docker best practices)

What they often **lack**:
- Project-specific conventions (naming, structure, patterns)
- Architectural decisions and constraints
- Context from similar past implementations
- Team practices and coding standards

Without this knowledge, agents may produce code that is technically correct but **misaligned** with the project: wrong folder structure, inconsistent naming, violations of bounded contexts, or patterns that the team has moved away from.

---

## 2. The initiative: Codebase Intelligence

**Codebase Intelligence** is the capability to enrich AI code-writing agents with **project-aware knowledge** so that their output is as effective as possible: aligned with conventions, architecture, and team practices.

### Goals

1. **Improve output quality** — Code that follows existing patterns and conventions from the first iteration
2. **Reduce review cycles** — Fewer back-and-forth corrections on style, structure, and architecture
3. **Preserve consistency** — New code that feels "native" to the project
4. **Leverage existing documentation** — Use `.cursorrules`, `CONTEXT.md`, `README.md`, and other project docs that teams already maintain

### Scope

This initiative applies to all agents that **write or modify code** in the AI-DLC ecosystem:
- The Developer agent (Planned stage)
- The Code Reviewer agent (Review stage)
- Any future agents that produce code

---

## 3. Who benefits

| Audience | Benefit |
|----------|---------|
| **Developers** | Less time fixing convention violations in AI-generated code; more time on logic and design |
| **Tech Leads / Architects** | Confidence that AI output respects architectural boundaries and decisions |
| **Product Owners** | Faster delivery with fewer rework cycles |
| **DevOps** | Consistent patterns across services, easier maintenance |
| **Teams adopting AI-DLC** | Smoother onboarding: the system "learns" from their project docs |

---

## 4. Principles

- **Use what exists** — Prefer injecting existing documentation over creating new formats
- **Incremental value** — Start with simple, high-impact changes (e.g. `.cursorrules` injection)
- **Configurable** — Allow projects to opt in and choose what context to include
- **Transparent** — Agents should know when they are using project context vs. generic knowledge

---

## 5. Expected outcomes

- **Shorter time to merge** — Fewer iterations on style and structure
- **Higher acceptance rate** — Code that passes review on first or second pass
- **Better alignment** — New features that fit naturally into the existing codebase
- **Scalability** — As more projects adopt AI-DLC, each can bring its own context

---

## 6. Related documents

- [`codebase-intelligence-technical-implementation.md`](./codebase-intelligence-technical-implementation.md) — Technical design, approaches, and roadmap
- [`Proposals/CouncilOfAgents/council-technical-implementation.md`](../CouncilOfAgents/council-technical-implementation.md) — Multi-agent architecture (Council context integration)
- `CONTEXT.md` — Current state of Scaile and Distributed Playground

---

*Document generated on March 7, 2026 — ai-dlc project*

**Christian Soliman** — [christian.soliman@adesso.ch](mailto:christian.soliman@adesso.ch)
