# Council of Agents

### A new way of working with Artificial Intelligence

---

## The problem today

In traditional software development processes, people and tools pass work along **sequentially**: someone writes the requirements, someone else analyzes them, another person writes the code, then someone tests it. Each handoff is a moment where work changes hands — and with it comes the risk of losing context, creating misunderstandings, or discovering problems too late.

When we introduce AI into this process, the risk is replicating the same pattern: AI does one piece, a human does the next, in a chain where no one has the full picture.

---

## Our vision: the Council

Imagine a **working table** where different specialists sit together — some human, some AI agents — each with their own expertise. They don't pass work back and forth: they **discuss together**, propose solutions, raise concerns, and reach shared decisions.

This is the **Council of Agents**: a model where humans and AI agents collaborate **as peers**, each bringing their own value.

```
┌─────────────────────────────────────────────────────────┐
│                                                         │
│                   🏛️  THE COUNCIL                       │
│                                                         │
│   👤 Product Owner    🤖 Product Analyst Agent         │
│   👤 Business Analyst 🤖 Architect Agent               │
│   👤 Developer        🤖 Developer Agent               │
│   👤 Tester           🤖 QA Strategist Agent           │
│                       🤖 Code Review Agent              │
│                                                         │
│              🎯 Coordinator (AI)                        │
│       Moderates, synthesizes, facilitates consensus     │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## How it works

### The participants

Each participant has a **clear role** and area of expertise. There is no distinction of "rank" between humans and agents: what matters is what they can do.


| Participant          | Type     | What they do                                                                     |
| -------------------- | -------- | -------------------------------------------------------------------------------- |
| **Product Owner**    | 👤 Human | Defines priorities and business value                                            |
| **Business Analyst** | 👤 Human | Clarifies requirements and processes                                             |
| **Developer**        | 👤 Human | Brings technical expertise and system knowledge                                  |
| **Tester**           | 👤 Human | Verifies quality and compliance with requirements                                |
| **Product Analyst**  | 🤖 Agent | Analyzes requirements, proposes user stories, validates acceptance criteria      |
| **Architect**        | 🤖 Agent | Analyzes the existing codebase, proposes technical solutions, identifies impacts |
| **Developer Agent**  | 🤖 Agent | Writes code, creates tests, implements solutions                                 |
| **QA Strategist**    | 🤖 Agent | Writes test plans, identifies critical scenarios and edge cases                  |
| **Code Reviewer**    | 🤖 Agent | Reviews code, suggests improvements, checks standards compliance                 |
| **Coordinator**      | 🤖 Agent | Moderates the discussion, decides who to involve, synthesizes consensus          |


### The deliberation cycle

When new work arrives (for example, a new feature to build), the Council convenes and proceeds through **discussion rounds**:

```
    ╔══════════════════════════════════╗
    ║      New work item arrives       ║
    ╚═══════════════╤══════════════════╝
                    │
                    ▼
    ┌───────────────────────────────┐
    │   The Coordinator convenes    │
    │   the relevant participants   │
    └───────────────┬───────────────┘
                    │
        ┌───────────▼───────────┐
        │    DISCUSSION ROUND   │◄──────────────────────┐
        └───────────┬───────────┘                       │
                    │                                    │
     ┌──────────────┼──────────────┐                    │
     ▼              ▼              ▼                    │
 ┌────────┐   ┌──────────┐  ┌──────────┐              │
 │Propose │   │  Object  │  │ Approve  │              │
 └───┬────┘   └────┬─────┘  └────┬─────┘              │
     │              │             │                     │
     └──────────────┼─────────────┘                    │
                    ▼                                   │
         ┌─────────────────────┐    NO                 │
         │  Consensus reached? ├───────────────────────┘
         └─────────┬───────────┘
                   │ YES
                   ▼
         ┌─────────────────────┐
         │   Execute the       │
         │   agreed actions    │
         └─────────────────────┘
```

In each round, every participant (human or agent) can:

- **Propose** — suggest a solution, an idea, an improvement
- **Object** — raise a concern, a risk, a problem
- **Approve** — give their OK to the current proposal
- **Abstain** — no relevant input for this topic

The Coordinator collects all contributions, evaluates whether consensus has been reached, and if needed starts a new round with updated proposals.

---

## A concrete example

> *The Product Owner creates a request: "Add return management for delivered orders"*

### Round 1 — Initial analysis


| Who                    | What they say                                                                                                                                            |
| ---------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 🤖 **Product Analyst** | "I propose 4 user stories: return request, approval, return shipment, refund. Here are the acceptance criteria for each."                                |
| 🤖 **Architect**       | "Heads up: the 'refund' story impacts the invoicing module, which is currently just a placeholder. We need an infrastructure task first."                |
| 🤖 **QA Strategist**   | "The 'return approval' story has no testable criteria. I propose adding: 'given an order delivered more than 30 days ago, the return must be rejected'." |
| 👤 **Product Owner**   | "The Architect's point is valid. For now, let's exclude automatic refund and handle it manually."                                                        |


### Round 2 — Revision


| Who                     | What they say                                                                                                               |
| ----------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| 🤖 **Product Analyst**  | "Updated: removed the automatic refund story, added the time-based criterion to the approval story. Now we have 3 stories." |
| 🤖 **Architect**        | "Consistent with the current system. OK."                                                                                   |
| 👤 **Business Analyst** | "Approved. The criteria are clear."                                                                                         |
| 👤 **Product Owner**    | "Approved. ✓"                                                                                                               |


### Result

The Coordinator records the consensus and creates the 3 user stories in GitLab, complete with acceptance criteria validated by all participants — humans and agents together.

**What happened differently compared to the traditional model?**

- The architectural objection surfaced **before** writing a single line of code
- Test criteria were defined **alongside** the stories, not after
- The decision to exclude the refund was made **with all the information on the table**

---

## Benefits for everyone

### For product people (PO, BA)

- AI proposals arrive **already structured** with acceptance criteria
- Technical risks emerge **immediately**, not at the end of development
- The process is **transparent**: every decision has a traceable rationale

### For developers (DEV)

- Specifications are **clearer** because they've been validated from multiple perspectives
- Code review is **continuous**, not just at the end
- AI handles the repetitive parts; the developer focuses on the important decisions

### For testers (TESTER)

- Test plans are suggested by AI **in parallel** with development
- Edge cases are identified **early**, not discovered in production
- Acceptance criteria are **testable by design**

### For management

- **Full traceability**: every decision has an audit trail (who proposed, who objected, why)
- **Less rework**: problems caught early = lower correction costs
- **Scalability**: specialized agents (security, performance) can be added without changing the process
- **Metrics**: how many rounds to reach consensus, where objections cluster, which phases benefit most from AI

---

## How it fits into our current workflow

The Council doesn't replace the process stages — it **enriches** them. Each stage becomes a moment of collaboration instead of a handoff.


| Stage            | Today                              | With the Council                                                                                   |
| ---------------- | ---------------------------------- | -------------------------------------------------------------------------------------------------- |
| **Requirements** | PO writes the epic                 | PO writes the epic. The Council provides early feedback on feasibility and risks                   |
| **Analysis**     | AI creates stories on its own      | The Council proposes stories, Architect validates, QA verifies testability. Rounds until consensus |
| **Refinement**   | PO/BA review manually              | Humans and agents comment on the same channel. Feedback is integrated in real time                 |
| **Planning**     | AI analyzes code and creates tasks | Architect + Developer Agent propose tasks. QA proposes the test plan. Humans approve               |
| **Development**  | AI writes code on its own          | Developer Agent implements. Code Reviewer does a pre-review before presenting the work             |
| **Review**       | A human reviews the PR             | Code Reviewer + human Developer review **together**, commenting on the same thread                 |
| **Testing**      | The tester works alone             | QA Agent suggests test cases, the human Tester executes them and reports results                   |


---

## The core principle: parity

At the heart of this approach is a simple principle:

> **A valid objection carries the same weight, whether it comes from a human or an AI agent.**

This does not mean AI decides for us. It means that:

- **Humans** retain **veto power** over business and product decisions
- **AI agents** have a voice on technical matters where they have demonstrable expertise
- **Final decisions** are based on the **quality of the arguments**, not on who made them
- **Transparency** is guaranteed: every participant shows their reasoning

---

## Adoption roadmap

There's no need to change everything at once. We propose a gradual path:

### Phase 1 — First additional agent

Add a second agent (Architect) to the analysis stage. The Coordinator is simple: it invokes both agents in sequence, posts the results as comments, and waits for the human.

**Expected outcome**: stories produced already have technical validation before human review.

### Phase 2 — Deliberation protocol

Introduce the proposal → objection → revision → consensus cycle. Participants (humans and agents) interact on the same channel (GitLab comments).

**Expected outcome**: discussions are structured and traceable. Decisions have an audit trail.

### Phase 3 — Council in code review

Extend the model to the code review stage: Code Reviewer Agent and human Developer collaborate as peers on the same PR.

**Expected outcome**: faster and more thorough reviews, technical issues caught before merge.

### Phase 4 — Full Council

Generalize the Coordinator and involve all specialized agents at every stage of the workflow.

**Expected outcome**: the development process becomes a **continuous collaboration** between humans and agents.

---

## Frequently asked questions

**Will AI replace my role?**
No. AI is a participant at the table, not a replacement. Business decisions, priorities, and final validations remain with humans. AI accelerates analysis, reduces repetitive work, and brings complementary expertise.

**How can we trust AI proposals?**
Every AI proposal includes the reasoning behind it. Humans can always see *why* an agent suggested something, not just *what* it suggested. Additionally, the round-based model ensures that every proposal is examined by multiple participants before being accepted.

**What happens if humans and agents disagree?**
The Coordinator manages disagreements with additional rounds. If after a defined number of rounds consensus is not reached, the matter is escalated — for example, by involving a human with decision-making authority on the specific topic.

**Won't all these rounds slow the process down?**
AI agents respond in seconds, not days. A complete round (all agents + human input) takes far less time than a traditional review-rework-review cycle. The time invested in initial deliberation is recovered through less rework in later stages.

**Can we start small?**
Yes. The roadmap is designed to be incremental. Start with a single additional agent in a single stage, measure the impact, and extend gradually.

---

*Document generated on March 7, 2026 — ai-dlc project*

**Christian Soliman** — [christian.soliman@adesso.ch](mailto:christian.soliman@adesso.ch)