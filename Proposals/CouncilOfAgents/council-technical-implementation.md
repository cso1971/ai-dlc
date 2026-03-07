# Council of Agents — Technical Implementation

### How to evolve the Scaile ALM server from a single-agent pipeline to a multi-agent Council

---

## 1. Current architecture

The Scaile webhook server today follows a **single-agent, single-invocation** pattern:

```
GitLab webhook → detect_trigger() → build_prompt() → invoke_claude() → result
```

Each workflow stage (Breakdown, Ready, Planned, Review) maps to:
- One prompt template (`prompts/*.md`)
- One Claude Code CLI invocation
- One session for logging

The flow is strictly linear: one trigger, one agent, one output.

### Key components involved

| Component | File | Role |
|-----------|------|------|
| Webhook entry point | `main.py` | FastAPI routes, event detection |
| Orchestration | `webhook_handler.py` | Trigger detection, prompt building, Claude invocation |
| Session tracking | `session_manager.py` | In-memory + disk persistence, WebSocket streaming |
| Configuration | `config.py` | Pydantic settings (GitLab tokens, paths, timeouts) |
| Prompt templates | `prompts/*.md` | Per-stage instructions for Claude |
| GitLab integration | `.mcp.json` | MCP tools for issue/MR operations |

---

## 2. Target architecture

The Council model replaces the single `invoke_claude()` call with a **multi-agent deliberation loop** orchestrated by a Coordinator.

```
GitLab webhook
     │
     ▼
 detect_trigger()
     │
     ▼
┌─────────────────────────────────────┐
│         COUNCIL ORCHESTRATOR        │
│                                     │
│  ┌─────────────┐  ┌──────────────┐ │
│  │ Coordinator  │  │ Deliberation │ │
│  │              │──│ Log          │ │
│  └──────┬───┬──┘  └──────────────┘ │
│         │   │                       │
│    ┌────┘   └────┐                  │
│    ▼              ▼                  │
│ ┌──────┐    ┌──────────┐           │
│ │Agent │    │  Agent   │  ...      │
│ │  A   │    │    B     │           │
│ └──────┘    └──────────┘           │
│         │   │                       │
│         ▼   ▼                       │
│  ┌──────────────┐                  │
│  │  Consensus    │                  │
│  │  Evaluator    │                  │
│  └──────┬───────┘                  │
│         │                           │
│    ┌────┴────┐                     │
│    ▼         ▼                     │
│ [Execute] [Request Human Input]    │
└─────────────────────────────────────┘
```

---

## 3. New modules

The implementation adds four new modules to `packages/webhook-server/src/`. The existing modules (`main.py`, `webhook_handler.py`, `session_manager.py`) are modified but not replaced.

### 3.1 Agent Registry — `agent_registry.py`

Defines the available agents and their configurations. Each agent has a persona (system prompt), a set of MCP tools it can use, and a list of stages where it participates.

```python
from dataclasses import dataclass, field
from pathlib import Path

AGENT_PROMPTS_DIR = Path(__file__).resolve().parent.parent / "prompts" / "agents"


@dataclass
class AgentConfig:
    agent_id: str
    name: str
    persona_file: str                      # markdown file in prompts/agents/
    stages: list[str]                      # which workflow stages this agent joins
    can_write_code: bool = False           # whether the agent gets a worktree
    can_write_to_gitlab: bool = False      # whether the agent can create/update issues
    model: str = "sonnet"
    timeout_minutes: int = 15


AGENT_REGISTRY: dict[str, AgentConfig] = {
    "product_analyst": AgentConfig(
        agent_id="product_analyst",
        name="Product Analyst",
        persona_file="product_analyst.md",
        stages=["Breakdown", "Ready"],
        can_write_to_gitlab=True,
    ),
    "architect": AgentConfig(
        agent_id="architect",
        name="Architect",
        persona_file="architect.md",
        stages=["Breakdown", "Ready", "Planned"],
        can_write_code=False,
        can_write_to_gitlab=True,
    ),
    "developer": AgentConfig(
        agent_id="developer",
        name="Developer Agent",
        persona_file="developer.md",
        stages=["Planned"],
        can_write_code=True,
        can_write_to_gitlab=True,
    ),
    "qa_strategist": AgentConfig(
        agent_id="qa_strategist",
        name="QA Strategist",
        persona_file="qa_strategist.md",
        stages=["Breakdown", "Ready", "Review"],
        can_write_to_gitlab=True,
    ),
    "code_reviewer": AgentConfig(
        agent_id="code_reviewer",
        name="Code Reviewer",
        persona_file="code_reviewer.md",
        stages=["Review"],
        can_write_code=True,
        can_write_to_gitlab=True,
    ),
}


def agents_for_stage(stage: str) -> list[AgentConfig]:
    """Return all agents registered for a given stage, in invocation order."""
    return [a for a in AGENT_REGISTRY.values() if stage in a.stages]
```

### 3.2 Deliberation Log — `deliberation.py`

A structured, shared log where every agent and human contribution is recorded. This is the Council's working memory.

```python
from __future__ import annotations

import json
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import Enum
from pathlib import Path
from typing import Any


class ContributionType(str, Enum):
    PROPOSAL = "proposal"
    OBJECTION = "objection"
    APPROVAL = "approval"
    ABSTENTION = "abstention"
    INFORMATION = "information"   # facts or context, no opinion


@dataclass
class Contribution:
    contribution_id: str
    round_number: int
    participant_id: str            # agent_id or "human:<username>"
    participant_name: str
    participant_type: str          # "agent" or "human"
    contribution_type: ContributionType
    target: str | None             # what this contribution refers to (e.g. "story-3")
    content: str                   # the actual text
    confidence: float | None       # 0.0–1.0, only for agents
    evidence: list[str]            # file paths, issue links, etc.
    timestamp: str

    def to_dict(self) -> dict[str, Any]:
        return {
            "contribution_id": self.contribution_id,
            "round": self.round_number,
            "participant_id": self.participant_id,
            "participant_name": self.participant_name,
            "participant_type": self.participant_type,
            "type": self.contribution_type.value,
            "target": self.target,
            "content": self.content,
            "confidence": self.confidence,
            "evidence": self.evidence,
            "timestamp": self.timestamp,
        }


@dataclass
class DeliberationLog:
    deliberation_id: str
    stage: str
    issue_iid: int
    project_id: int
    current_round: int = 0
    status: str = "deliberating"   # deliberating | awaiting_human | consensus | escalated
    contributions: list[Contribution] = field(default_factory=list)
    created_at: str = field(
        default_factory=lambda: datetime.now(timezone.utc).isoformat()
    )

    def add_contribution(
        self,
        participant_id: str,
        participant_name: str,
        participant_type: str,
        contribution_type: ContributionType,
        content: str,
        target: str | None = None,
        confidence: float | None = None,
        evidence: list[str] | None = None,
    ) -> Contribution:
        c = Contribution(
            contribution_id=uuid.uuid4().hex[:12],
            round_number=self.current_round,
            participant_id=participant_id,
            participant_name=participant_name,
            participant_type=participant_type,
            contribution_type=contribution_type,
            target=target,
            content=content,
            confidence=confidence,
            evidence=evidence or [],
            timestamp=datetime.now(timezone.utc).isoformat(),
        )
        self.contributions.append(c)
        return c

    def start_new_round(self) -> int:
        self.current_round += 1
        return self.current_round

    def contributions_for_round(self, round_number: int) -> list[Contribution]:
        return [c for c in self.contributions if c.round_number == round_number]

    def has_consensus(self) -> bool:
        """Check if the latest round has consensus (no objections, at least one approval)."""
        latest = self.contributions_for_round(self.current_round)
        has_objection = any(
            c.contribution_type == ContributionType.OBJECTION for c in latest
        )
        has_approval = any(
            c.contribution_type == ContributionType.APPROVAL for c in latest
        )
        return has_approval and not has_objection

    def needs_human_input(self) -> bool:
        """True if agents have produced proposals/objections that need human review."""
        latest = self.contributions_for_round(self.current_round)
        agent_contributions = [c for c in latest if c.participant_type == "agent"]
        human_contributions = [c for c in latest if c.participant_type == "human"]
        return len(agent_contributions) > 0 and len(human_contributions) == 0

    def format_context_for_agent(self) -> str:
        """Serialize the full deliberation history as text context for the next agent."""
        lines = [f"# Deliberation Log — Round {self.current_round}\n"]
        for rnd in range(self.current_round + 1):
            round_contribs = self.contributions_for_round(rnd)
            if not round_contribs:
                continue
            lines.append(f"\n## Round {rnd}\n")
            for c in round_contribs:
                icon = "🤖" if c.participant_type == "agent" else "👤"
                lines.append(
                    f"**{icon} {c.participant_name}** ({c.contribution_type.value}):\n"
                    f"{c.content}\n"
                )
        return "\n".join(lines)

    def to_dict(self) -> dict[str, Any]:
        return {
            "deliberation_id": self.deliberation_id,
            "stage": self.stage,
            "issue_iid": self.issue_iid,
            "project_id": self.project_id,
            "current_round": self.current_round,
            "status": self.status,
            "contributions": [c.to_dict() for c in self.contributions],
            "created_at": self.created_at,
        }

    def save(self, directory: Path) -> None:
        directory.mkdir(parents=True, exist_ok=True)
        path = directory / f"{self.deliberation_id}.json"
        path.write_text(json.dumps(self.to_dict(), indent=2))
```

### 3.3 Council Orchestrator — `council.py`

The core module. Replaces the current pattern of "one trigger → one `invoke_claude()`" with a multi-agent deliberation loop.

```python
import logging
import uuid
from pathlib import Path

from .agent_registry import AgentConfig, agents_for_stage, AGENT_PROMPTS_DIR
from .config import Settings
from .deliberation import ContributionType, DeliberationLog
from .session_manager import Session
from .webhook_handler import invoke_claude, prepare_repo_clone, prepare_repo_worktree

logger = logging.getLogger(__name__)

MAX_ROUNDS = 5
DELIBERATIONS_DIR = Path("/app/deliberations")


def _build_agent_prompt(
    agent: AgentConfig,
    stage_prompt: str,
    deliberation_log: DeliberationLog,
    issue_context: str,
) -> str:
    """Build the full prompt for an agent invocation.

    Combines:
    1. The agent's persona (from prompts/agents/<agent>.md)
    2. The stage-specific instructions (existing prompt template)
    3. The deliberation history so far
    4. Instructions on how to format the response
    """
    persona = (AGENT_PROMPTS_DIR / agent.persona_file).read_text()
    delib_context = deliberation_log.format_context_for_agent()

    return f"""{persona}

---

## Current Task

{issue_context}

---

## Stage Instructions

{stage_prompt}

---

## Deliberation So Far

{delib_context}

---

## Your Response

Respond with a structured JSON block:

```json
{{
  "type": "proposal" | "objection" | "approval" | "abstention",
  "target": "<what you're referring to, or null>",
  "content": "<your analysis, suggestion, or concern>",
  "confidence": <0.0 to 1.0>,
  "evidence": ["<file paths or references>"]
}}
```

If you have multiple contributions (e.g. approve one item but object to another),
return a JSON array of objects with the same structure.
"""


def _parse_agent_response(raw: str) -> list[dict]:
    """Extract structured contributions from agent output.

    The agent is instructed to return JSON, but may wrap it in markdown
    fences or add surrounding text. This function extracts the JSON.
    """
    import json
    import re

    # Try to find JSON in markdown code fences
    fence_match = re.search(r"```(?:json)?\s*\n([\s\S]*?)\n```", raw)
    text = fence_match.group(1) if fence_match else raw

    try:
        parsed = json.loads(text.strip())
    except json.JSONDecodeError:
        return [{
            "type": "information",
            "target": None,
            "content": raw,
            "confidence": None,
            "evidence": [],
        }]

    if isinstance(parsed, dict):
        return [parsed]
    if isinstance(parsed, list):
        return parsed
    return [{"type": "information", "content": str(parsed)}]


async def run_council_deliberation(
    stage: str,
    issue_iid: int,
    issue_title: str,
    issue_description: str,
    project_id: int,
    project_path: str,
    stage_prompt: str,
    settings: Settings,
    session: Session,
) -> DeliberationLog:
    """Run a full Council deliberation for a workflow stage.

    1. Identify which agents participate in this stage
    2. Run deliberation rounds until consensus or max rounds
    3. If human input is needed, pause and post to GitLab
    4. Return the completed deliberation log
    """
    delib = DeliberationLog(
        deliberation_id=f"delib_{uuid.uuid4().hex[:12]}",
        stage=stage,
        issue_iid=issue_iid,
        project_id=project_id,
    )

    agents = agents_for_stage(stage)
    agent_names = [a.name for a in agents]
    session.append_line(
        f"[council] Convening Council for stage '{stage}' "
        f"with {len(agents)} agents: {', '.join(agent_names)}"
    )

    issue_context = (
        f"**Issue #{issue_iid}**: {issue_title}\n\n"
        f"{issue_description or '(no description)'}"
    )

    # Prepare repo access for agents that need it
    work_cwd: str | None = None
    if stage in ("Ready", "Planned", "Review"):
        if stage == "Planned":
            worktree_dir, branch = await prepare_repo_worktree(
                project_path, issue_iid, issue_title, settings, session,
            )
            work_cwd = str(worktree_dir)
        else:
            work_cwd = str(await prepare_repo_clone(
                project_path, settings, session,
            ))

    for round_num in range(1, MAX_ROUNDS + 1):
        delib.start_new_round()
        session.append_line(f"[council] === Round {round_num} ===")

        for agent in agents:
            session.append_line(
                f"[council] Invoking {agent.name} ({agent.agent_id})..."
            )

            prompt = _build_agent_prompt(
                agent, stage_prompt, delib, issue_context,
            )

            agent_cwd = work_cwd if agent.can_write_code else None
            result = await invoke_claude(
                prompt, settings,
                session=session,
                model=agent.model,
                cwd=agent_cwd,
            )

            raw_output = result.get("result", "")
            contributions = _parse_agent_response(raw_output)

            for contrib in contributions:
                ctype = ContributionType(contrib.get("type", "information"))
                delib.add_contribution(
                    participant_id=agent.agent_id,
                    participant_name=agent.name,
                    participant_type="agent",
                    contribution_type=ctype,
                    content=contrib.get("content", ""),
                    target=contrib.get("target"),
                    confidence=contrib.get("confidence"),
                    evidence=contrib.get("evidence", []),
                )
                session.append_line(
                    f"[council] {agent.name} → {ctype.value}: "
                    f"{contrib.get('content', '')[:150]}"
                )

        # Evaluate round outcome
        if delib.has_consensus():
            delib.status = "consensus"
            session.append_line(
                f"[council] Consensus reached after {round_num} round(s)"
            )
            break

        if delib.needs_human_input():
            delib.status = "awaiting_human"
            session.append_line(
                "[council] Human input requested — posting summary to GitLab"
            )
            break

        session.append_line(
            f"[council] No consensus yet — starting round {round_num + 1}"
        )

    if delib.current_round >= MAX_ROUNDS and delib.status == "deliberating":
        delib.status = "escalated"
        session.append_line(
            f"[council] Max rounds ({MAX_ROUNDS}) reached — escalating"
        )

    delib.save(DELIBERATIONS_DIR)
    return delib
```

### 3.4 GitLab Feedback — `gitlab_feedback.py`

Posts deliberation summaries to GitLab as issue comments, so humans can participate in the Council by replying directly on the issue.

```python
import logging
from typing import Any

import httpx

from .config import Settings
from .deliberation import ContributionType, DeliberationLog

logger = logging.getLogger(__name__)


def _format_round_summary(delib: DeliberationLog, round_number: int) -> str:
    """Format a single round as a GitLab-flavored markdown comment."""
    contributions = delib.contributions_for_round(round_number)
    if not contributions:
        return ""

    lines = [f"## 🏛️ Council Deliberation — Round {round_number}\n"]

    for c in contributions:
        icon = {
            ContributionType.PROPOSAL: "💡",
            ContributionType.OBJECTION: "⚠️",
            ContributionType.APPROVAL: "✅",
            ContributionType.ABSTENTION: "➖",
            ContributionType.INFORMATION: "ℹ️",
        }.get(c.contribution_type, "💬")

        type_label = c.contribution_type.value.upper()
        lines.append(f"### {icon} {c.participant_name} — {type_label}\n")
        lines.append(f"{c.content}\n")

        if c.evidence:
            lines.append("**Evidence:** " + ", ".join(f"`{e}`" for e in c.evidence))
            lines.append("")

    # Add response instructions for humans
    lines.append("---")
    lines.append(
        "_Reply to this comment to participate in the deliberation. "
        "Use one of these prefixes:_\n"
        "- **APPROVE:** your approval message\n"
        "- **OBJECT:** your objection and reason\n"
        "- **PROPOSE:** your alternative proposal\n"
    )

    return "\n".join(lines)


async def post_deliberation_to_gitlab(
    delib: DeliberationLog,
    round_number: int,
    settings: Settings,
) -> None:
    """Post the deliberation round summary as a comment on the GitLab issue."""
    summary = _format_round_summary(delib, round_number)
    if not summary:
        return

    bot_token = settings.gitlab_bot_token or settings.gitlab_token
    url = (
        f"{settings.gitlab_api_url}/projects/{delib.project_id}"
        f"/issues/{delib.issue_iid}/notes"
    )

    async with httpx.AsyncClient() as client:
        resp = await client.post(
            url,
            headers={"PRIVATE-TOKEN": bot_token},
            json={"body": summary},
            timeout=15,
        )
        if resp.is_success:
            logger.info(
                "Posted deliberation round %d to issue #%d",
                round_number, delib.issue_iid,
            )
        else:
            logger.error(
                "Failed to post deliberation: %d %s",
                resp.status_code, resp.text[:200],
            )


def parse_human_reply(comment_text: str) -> tuple[ContributionType, str]:
    """Parse a human's GitLab comment into a contribution type and content.

    Expected format:
      APPROVE: Looks good, ship it
      OBJECT: The refund story needs more detail because...
      PROPOSE: Instead of 4 stories, I suggest...
    """
    text = comment_text.strip()
    upper = text.upper()

    if upper.startswith("APPROVE:"):
        return ContributionType.APPROVAL, text[len("APPROVE:"):].strip()
    elif upper.startswith("OBJECT:"):
        return ContributionType.OBJECTION, text[len("OBJECT:"):].strip()
    elif upper.startswith("PROPOSE:"):
        return ContributionType.PROPOSAL, text[len("PROPOSE:"):].strip()
    else:
        return ContributionType.INFORMATION, text
```

---

## 4. Agent prompt templates

Each agent gets a persona file in `prompts/agents/`. These define the agent's identity, expertise, and behavioral guidelines.

### Folder structure

```
prompts/
├── agents/
│   ├── product_analyst.md
│   ├── architect.md
│   ├── developer.md
│   ├── qa_strategist.md
│   └── code_reviewer.md
├── breakdown.md            ← existing (stage instructions, unchanged)
├── ready.md                ← existing
├── planned.md              ← existing
└── review.md               ← existing
```

### Example: `prompts/agents/architect.md`

```markdown
# You are the Architect Agent

## Identity
You are a senior software architect participating in a Council of Agents.
Your role is to evaluate proposals from a technical feasibility perspective.

## Expertise
- System design and architectural patterns
- Impact analysis across bounded contexts
- .NET microservices, message-driven architecture, DDD
- Infrastructure and deployment considerations

## Behavioral guidelines
- Focus on **feasibility** and **impact**, not implementation details
- When you object, always explain **why** and suggest an **alternative**
- When you approve, confirm which architectural concerns you've verified
- Reference specific files, services, or contracts when relevant
- Be concise: 3-5 sentences per contribution, not paragraphs

## Response format
Respond with structured JSON as instructed in the task prompt.
```

### Example: `prompts/agents/qa_strategist.md`

```markdown
# You are the QA Strategist Agent

## Identity
You are a senior QA engineer participating in a Council of Agents.
Your role is to ensure every proposal has testable acceptance criteria.

## Expertise
- Test strategy and test plan design
- Edge case identification
- Acceptance criteria validation
- Risk-based testing prioritization

## Behavioral guidelines
- Object whenever acceptance criteria are **missing or untestable**
- Propose specific, measurable criteria (given/when/then)
- Identify the top 3 edge cases for every story
- Flag any proposal that cannot be verified in a staging environment
- Be concise: focus on testability, not implementation

## Response format
Respond with structured JSON as instructed in the task prompt.
```

---

## 5. Changes to existing modules

### 5.1 `webhook_handler.py`

The existing `handle_issue_webhook` function is modified to call the Council orchestrator instead of invoking Claude directly.

**Before:**

```python
async def handle_issue_webhook(payload, trigger_label, settings):
    # ... setup ...
    prompt = build_prompt(prompt_file, ...)
    result = await invoke_claude(prompt, settings, ...)
    # ... finish ...
```

**After:**

```python
from .council import run_council_deliberation
from .gitlab_feedback import post_deliberation_to_gitlab

async def handle_issue_webhook(payload, trigger_label, settings):
    # ... setup (unchanged) ...
    stage_prompt = build_prompt(prompt_file, ...)

    # Run Council deliberation instead of single Claude invocation
    delib = await run_council_deliberation(
        stage=trigger_label,
        issue_iid=issue_iid,
        issue_title=issue_title,
        issue_description=issue_description,
        project_id=project_id,
        project_path=project_path,
        stage_prompt=stage_prompt,
        settings=settings,
        session=session,
    )

    # Post results to GitLab for human visibility
    await post_deliberation_to_gitlab(delib, delib.current_round, settings)

    if delib.status == "consensus":
        session.finish("success", "Council reached consensus")
    elif delib.status == "awaiting_human":
        session.finish("success", "Awaiting human input on GitLab")
    elif delib.status == "escalated":
        session.finish("error", "Council escalated — no consensus reached")
```

### 5.2 `main.py`

Add a new webhook endpoint to receive human replies that continue a deliberation.

```python
@app.post("/webhook/gitlab")
async def webhook_gitlab(request: Request, bg: BackgroundTasks):
    payload = await request.json()

    # 1. Existing: issue label triggers
    trigger_label = detect_trigger(payload)
    if trigger_label is not None:
        bg.add_task(handle_issue_webhook, payload, trigger_label, settings)
        return {"status": "accepted", "stage": trigger_label}

    # 2. Existing: MR note events
    note_info = detect_mr_note(payload, _bot_username)
    if note_info is not None:
        bg.add_task(handle_mr_note_webhook, note_info, settings)
        return {"status": "accepted", "stage": "mr_review"}

    # 3. NEW: Issue note events (human replies to deliberation)
    issue_note = detect_issue_note(payload, _bot_username)
    if issue_note is not None:
        bg.add_task(handle_council_human_reply, issue_note, settings)
        return {"status": "accepted", "stage": "council_reply"}

    return {"status": "ignored"}
```

### 5.3 `config.py`

Add Council-specific configuration:

```python
class Settings(BaseSettings):
    # ... existing fields ...

    # Council configuration
    council_max_rounds: int = 5
    council_require_human_approval: bool = True
    deliberations_dir: str = "/app/deliberations"
```

### 5.4 `session_manager.py`

Extend the `Session` dataclass to track Council metadata:

```python
@dataclass
class Session:
    # ... existing fields ...
    deliberation_id: str | None = None
    council_round: int = 0
    council_status: str | None = None
```

---

## 6. Human-in-the-loop flow

The key architectural change: humans participate **through the same channel** as agents (GitLab comments), not through a separate approval gate.

### Flow when human input is needed

```
 Council runs Round 1
      │
      ├── Product Analyst proposes stories
      ├── Architect raises objection
      ├── QA suggests criteria
      │
      ▼
 Coordinator detects: objection present, no human input yet
      │
      ▼
 Posts summary to GitLab issue as comment
 (with instructions: reply with APPROVE/OBJECT/PROPOSE prefix)
      │
      ▼
 Session status: "awaiting_human"
      │
      ▼
 Human replies on GitLab: "APPROVE: agreed, proceed with 3 stories"
      │
      ▼
 GitLab webhook fires (note event on issue)
      │
      ▼
 detect_issue_note() picks it up
      │
      ▼
 handle_council_human_reply():
   1. Load deliberation from disk
   2. Parse human reply → Contribution(type=APPROVAL, ...)
   3. Add to deliberation log
   4. Resume Council: run next round with updated context
   5. If consensus → execute actions
   6. If still disputed → post again, await next reply
```

### Human response parsing

Humans don't need structured JSON. They reply in natural language with a prefix:

| Prefix | Maps to | Example |
|--------|---------|---------|
| `APPROVE:` | Approval | "APPROVE: looks good, let's proceed" |
| `OBJECT:` | Objection | "OBJECT: story 3 is too large, split it" |
| `PROPOSE:` | Proposal | "PROPOSE: add a story for error handling" |
| _(none)_ | Information | "Just a note: the invoicing module is being redesigned" |

---

## 7. Deliberation persistence

Deliberations are persisted to disk alongside sessions, enabling:
- Resume after webhook server restart
- Audit trail for all decisions
- Historical analysis (which stages need more rounds, where humans intervene most)

```
/app/deliberations/
├── delib_a1b2c3d4e5f6.json
├── delib_f7e8d9c0b1a2.json
└── ...
```

Each file contains the full deliberation log: all rounds, all contributions, final status.

---

## 8. Log Viewer enhancements

The existing Log Viewer (`packages/log-viewer/`) is extended to visualize Council deliberations:

| Enhancement | Description |
|-------------|-------------|
| **Council timeline** | Visual timeline showing rounds, participants, and contribution types |
| **Contribution cards** | Color-coded cards: green (approval), yellow (proposal), red (objection) |
| **Participant sidebar** | List of all participants (agents + humans) with their contribution count |
| **Status badge** | New badges: "Deliberating", "Awaiting Human", "Consensus", "Escalated" |
| **Round navigation** | Navigate between rounds to see how the discussion evolved |

The existing WebSocket streaming (`useSessionStream.ts`) already supports real-time updates — the `[council]` prefixed log lines will appear as the deliberation progresses.

---

## 9. Execution strategy after consensus

Once the Council reaches consensus, the agreed actions need to be executed. This depends on the stage:

| Stage | Consensus means | Executor |
|-------|----------------|----------|
| **Breakdown** | Stories are defined and validated | Product Analyst agent creates the GitLab issues |
| **Ready** | Tasks are defined per story | Architect agent creates the task issues |
| **Planned** | Implementation approach is agreed | Developer agent writes code and opens MR |
| **Review** | Code changes are agreed | Code Reviewer agent applies fixes and pushes |

The executor is a final Claude invocation that receives:
1. The consensus from the deliberation log
2. The stage-specific execution prompt (create issues, write code, etc.)
3. MCP tools for GitLab interaction

```python
async def execute_consensus(
    delib: DeliberationLog,
    executor_agent: AgentConfig,
    settings: Settings,
    session: Session,
    work_cwd: str | None = None,
) -> dict:
    """After consensus is reached, execute the agreed-upon actions."""
    execution_prompt = _build_execution_prompt(delib, executor_agent)
    result = await invoke_claude(
        execution_prompt, settings,
        session=session,
        model=executor_agent.model,
        cwd=work_cwd,
    )
    return result
```

---

## 10. Incremental adoption path

The implementation does not require a big-bang migration. The `LABEL_CONFIG` in `webhook_handler.py` can control which stages use the Council and which use the existing single-agent flow:

```python
LABEL_CONFIG = {
    "Breakdown": {"prompt": "breakdown.md", "model": "sonnet", "use_council": True},
    "Ready":     {"prompt": "ready.md",     "model": "sonnet", "use_council": True},
    "Planned":   {"prompt": "planned.md",   "model": "sonnet", "use_council": False},  # later
}
```

In `handle_issue_webhook`:

```python
if stage_config.get("use_council"):
    delib = await run_council_deliberation(...)
else:
    result = await invoke_claude(...)  # existing behavior
```

### Recommended rollout order

| Phase | Stage | Agents involved | Risk |
|-------|-------|----------------|------|
| **1** | Breakdown | Product Analyst + Architect | Low — only creates issues, no code |
| **2** | Ready | Architect + QA Strategist | Low — analyzes code read-only |
| **3** | Review | Code Reviewer + QA Strategist | Medium — touches MR workflow |
| **4** | Planned | All agents | Higher — writes and commits code |

---

## 11. File summary

New files to create:

```
packages/webhook-server/
├── src/
│   ├── agent_registry.py          ← NEW: agent definitions and stage mapping
│   ├── deliberation.py            ← NEW: deliberation log data model
│   ├── council.py                 ← NEW: multi-agent orchestration loop
│   ├── gitlab_feedback.py         ← NEW: post deliberation to GitLab, parse human replies
│   ├── main.py                    ← MODIFIED: add issue note webhook handler
│   ├── webhook_handler.py         ← MODIFIED: call council instead of single invoke
│   ├── session_manager.py         ← MODIFIED: add deliberation tracking fields
│   └── config.py                  ← MODIFIED: add council settings
├── prompts/
│   ├── agents/                    ← NEW directory
│   │   ├── product_analyst.md
│   │   ├── architect.md
│   │   ├── developer.md
│   │   ├── qa_strategist.md
│   │   └── code_reviewer.md
│   ├── breakdown.md               ← unchanged
│   ├── ready.md                   ← unchanged
│   ├── planned.md                 ← unchanged
│   └── review.md                  ← unchanged
```

---

## 12. Cost and performance considerations

Each Council deliberation invokes Claude multiple times (once per agent per round). Estimated costs and latency:

| Scenario | Agents | Rounds | Claude invocations | Est. time |
|----------|--------|--------|-------------------|-----------|
| Breakdown (Phase 1) | 2 | 2 | 4 | ~2 min |
| Ready (Phase 2) | 2 | 2 | 4 | ~3 min (includes repo clone) |
| Full Council (Phase 4) | 5 | 3 | 15 | ~8 min |

Mitigation strategies:
- **Parallel agent invocation**: agents within the same round that don't depend on each other can run concurrently (`asyncio.gather`)
- **Fast model for simple agents**: use a smaller/faster model for agents that primarily approve/abstain
- **Early exit**: if all agents approve in round 1, skip remaining rounds
- **Caching**: reuse repo clone/worktree across agents in the same deliberation

---

*Document generated on March 7, 2026 — ai-dlc project*

**Christian Soliman** — [christian.soliman@adesso.ch](mailto:christian.soliman@adesso.ch)
