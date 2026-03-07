# Codebase Intelligence — Technical Implementation

### Approaches, evaluations, and roadmap for enriching agent knowledge

---

## 1. Current state

### What the agent receives today (Planned stage)

| Source | Content |
|--------|---------|
| **Prompt template** | Issue title, description, project_id, issue_iid, generic instructions |
| **Filesystem** | Git worktree with full codebase access |
| **MCP tools** | GitLab API (get_issue, create_issue, list_issue_links, create_merge_request, etc.) |
| **Skills** | dotnet-skills, angular-skills, docker-expert, task-executor, code-quality-checker, etc. (loaded by Claude Code CLI) |

### What is NOT injected (gaps)

- **`.cursorrules`** — Project-specific conventions (structure, naming, patterns)
- **`CONTEXT.md`** — Architectural decisions, history, constraints
- **`README.md`** — Project overview and setup
- **Project structure** — Folder map, services, bounded contexts
- **Existing patterns** — How similar features are implemented
- **Database schema** — Tables, relationships, EF conventions
- **API contracts** — Endpoints, DTOs, REST conventions

---

## 2. Approaches evaluated

### A. Project file injection into prompt

**Idea**: Read `.cursorrules`, `CONTEXT.md`, `README.md` (and optionally others) from the worktree and inject them into the prompt at build time.

| Pros | Cons |
|------|------|
| Simple to implement | Token limit: large files can overflow context |
| No new dependencies | Files may be outdated |
| Uses existing documentation | Requires truncation strategy |

**Implementation**: Extend `build_prompt()` in `webhook_handler.py` to read these files from `cwd` and add a "Project Context" section. Apply token budget (e.g. ~2000 tokens per file).

---

### B. Project knowledge base

**Idea**: Define a `docs/` or `.ai-dlc/` folder with curated markdown files (conventions, architecture, patterns, ADRs).

| Pros | Cons |
|------|------|
| Full control over what the agent sees | Requires maintenance |
| Versionable with the repo | New convention to adopt |
| Easy to extend | Risk of duplication with README/CONTEXT |

**Implementation**: Convention like `docs/ai-context/architecture.md`, `conventions.md`, `constraints.md`. If present, inject into prompt. Configurable via Back Office.

---

### C. Dynamic context from codebase analysis

**Idea**: Before invocation, run a script that analyzes the repo and generates a summary (structure, tech stack, recurring patterns).

| Pros | Cons |
|------|------|
| Always up to date with code | Requires analysis tooling (AST, grep, etc.) |
| No extra documentation needed | Can be expensive in time |
| Useful for projects without docs | Quality depends on heuristics |

**Implementation**: Pre-invocation script that outputs `tree` filtered, list of `Consumers/`, `Endpoints/`, naming patterns, etc. Inject as "Codebase summary".

---

### D. RAG over codebase

**Idea**: Embed source files and documentation, retrieve semantically relevant chunks for the task.

| Pros | Cons |
|------|------|
| Scales to large codebases | Requires Qdrant/vector DB, embedding pipeline |
| Retrieves only what is needed | Higher complexity and latency |
| Task-specific context | Embedding cost |

**Implementation**: Embed code/docs on clone or periodically. At task time, embed task description, retrieve top-k chunks, inject into prompt. Requires infrastructure (Qdrant already in Distributed Playground).

---

### E. Council deliberation context

**Idea**: In the Council model, Architect and QA produce analysis before the Developer; their output becomes context for implementation.

| Pros | Cons |
|------|------|
| Leverages multi-agent flow | Depends on Council implementation |
| Task-specific analysis | Depends on quality of other agents |
| No extra infrastructure | Only applies when Council is used |

**Implementation**: When Council runs, the Developer agent receives "Council Recommendations" with Architect's proposal (files to touch, patterns to use) and QA's test considerations. Add section to `_build_agent_prompt()`.

---

### F. Custom project-specific skills

**Idea**: Create Claude Code skills specific to the project (e.g. `distributed-playground-skills`) encoding conventions and patterns.

| Pros | Cons |
|------|------|
| Reusable across sessions | Requires skill creation and publishing |
| Integrated with Claude Code flow | Maintenance separate from repo |
| Can be shared across teams | Learning curve for skill authoring |

**Implementation**: Publish skill to npm/GitHub. Add to Dockerfile or Back Office config. Skill content = project conventions in markdown.

---

### G. Convention extraction from config files

**Idea**: Parse `.editorconfig`, `eslint.config`, `tsconfig.json`, `Directory.Build.props`, etc. and synthesize rules.

| Pros | Cons |
|------|------|
| Data already structured | Covers only a subset of conventions |
| No duplication | Requires parser per format |
| Always up to date | Limited for architectural decisions |

**Implementation**: Script that reads config files and outputs "Detected conventions: ...". Inject as supplementary context.

---

### H. Learning from past MRs

**Idea**: Analyze approved MRs and review comments to extract success patterns and anti-patterns.

| Pros | Cons |
|------|------|
| Based on real feedback | High complexity, data access |
| Improves over time | Requires storage and pipeline |
| Very team-specific | Needs sufficient history |

**Implementation**: Pipeline that fetches MR history, extracts review comments, clusters patterns. Long-term research direction.

---

## 3. Approach comparison

| Approach | Effort | Impact | Dependencies | Priority |
|----------|--------|--------|--------------|----------|
| **A. File injection** | Low | High | None | 1 |
| **B. Knowledge base** | Medium | High | Docs convention | 2 |
| **E. Council context** | Medium | High | Council | 3 |
| **C. Dynamic context** | Medium | Medium | Analysis scripts | 4 |
| **F. Custom skills** | Medium | Medium | Skill repo | 5 |
| **G. Config parsing** | Low | Low | Parsers | 6 |
| **D. RAG** | High | High | Qdrant, embedding | 7 |
| **H. MR learning** | High | Medium | Storage, analysis | 8 |

---

## 4. Implementation roadmap

### Phase 1: Quick wins (1–2 weeks)

1. **Inject `.cursorrules` and `CONTEXT.md`**
   - In `build_prompt()` (or equivalent for Planned/Ready), read these files from the worktree.
   - Add a "Project Context" section to the prompt template.
   - Apply token budget: truncate if exceeding ~2000 tokens per file.

2. **Prompt template update**
   - Add section:
     ```
     ## Project Context
     {project_context}
     ```
   - `project_context` = content of `.cursorrules` + excerpt of `CONTEXT.md` (e.g. first 100 lines or "Architecture" + "Decisions" sections).

3. **Fallback**
   - If `.cursorrules` does not exist, skip or use generic placeholder.

**Files to modify**: `webhook_handler.py`, `prompts/planned.md`, `prompts/ready.md`

---

### Phase 2: Structured knowledge base (2–3 weeks)

1. **Convention `docs/ai-context/`**
   - `architecture.md` — Bounded contexts, services, data flows
   - `conventions.md` — Naming, folder structure, patterns
   - `constraints.md` — Technical constraints (timeouts, integrations)

2. **Merge into prompt**
   - If `docs/ai-context/` exists, include files in prompt (with priority and truncation).

3. **Back Office integration**
   - Option to configure which files to include (whitelist) and max length.

---

### Phase 3: Council integration (when Council is active)

1. **Architect output → Developer**
   - Architect's proposal (files to touch, patterns to use) becomes context for Developer.

2. **QA output → Developer**
   - Test considerations and edge cases.

3. **Structured format**
   - "Council Recommendations" section in Developer prompt with approved proposals.

---

### Phase 4: Dynamic context (optional)

1. **Analysis script**
   - Generate `tree` filtered, list `Consumers/`, `Endpoints/`, naming patterns.

2. **Injection**
   - "Codebase summary" section in prompt, generated before each invocation.

---

### Phase 5: RAG (if needed)

- Only if codebase grows large and direct injection is insufficient.
- Requires: Qdrant (or similar), embedding pipeline, retrieval before invocation.

---

## 5. Technical details for Phase 1

### `build_prompt()` extension

```python
def _load_project_context(cwd: Path, max_chars_per_file: int = 4000) -> str:
    """Load .cursorrules and CONTEXT.md from worktree. Truncate if needed."""
    parts = []
    for filename in [".cursorrules", "CONTEXT.md"]:
        path = cwd / filename
        if path.exists():
            content = path.read_text(encoding="utf-8", errors="replace")
            if len(content) > max_chars_per_file:
                content = content[:max_chars_per_file] + "\n\n[... truncated ...]"
            parts.append(f"### {filename}\n\n{content}")
    if not parts:
        return "(No project context files found.)"
    return "\n\n---\n\n".join(parts)
```

### Prompt template change (`planned.md`)

Insert after "## Working Environment":

```markdown
## Project Context

The following project-specific context should guide your implementation. Follow these conventions and constraints.

{project_context}
```

### `build_prompt()` signature

Add `cwd: Path` parameter. Call `_load_project_context(cwd)` and pass result to `template.format(..., project_context=...)`.

---

## 6. Success metrics

- **Code quality** — Fewer review comments about conventions and patterns
- **Architectural alignment** — Fewer violations of bounded context or documented decisions
- **Velocity** — Fewer review iterations before merge
- **Qualitative feedback** — "Does the output respect project conventions?" survey

---

## 7. Related documents

- [`codebase-intelligence-concept.md`](./codebase-intelligence-concept.md) — Concept and audience
- [`Proposals/CouncilOfAgents/council-technical-implementation.md`](../CouncilOfAgents/council-technical-implementation.md) — Council architecture
- `CONTEXT.md` — Scaile and Distributed Playground state

---

*Document generated on March 7, 2026 — ai-dlc project*

**Christian Soliman** — [christian.soliman@adesso.ch](mailto:christian.soliman@adesso.ch)
