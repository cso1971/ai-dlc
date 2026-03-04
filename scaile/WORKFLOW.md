# Project Workflow

## Roles

| Role   | Description          |
| ------ | -------------------- |
| DEV    | Developer            |
| BA     | Business Analyst     |
| PO     | Product Owner        |
| TESTER | Tester               |
| OP     | Operations           |

> **HITL** = Human In The Loop (stages requiring manual intervention)

---

## Stages

### 1. REQUIREMENTS (HITL)

- **Actors:** PO, BA
- **Actions:** Create issues (epics)

### 2. BREAKDOWN

- **Actors:** PO, BA
- **Actions:** Move issues (epics) to this stage
- **AI Trigger:** On a specific label change, AI labels the issue there with epic and breaks down them and creates issues (stories) in the next stage (Refinement)

### 3. REFINEMENT (HITL)

- **Actors:** PO, BA, DEV
- **Actions:** Review stories; when satisfied, move to next stage (Ready)

### 4. READY

- **AI Trigger:** On a specific label change, AI:
  1. Clones the repo (git worktree) to analyze the codebase
  2. Creates child tasks inside of stories based on the actual code analysis
  3. When done, moves the story to next stage (Planned)

### 5. PLANNED

- **AI Trigger:** On a specific label change, AI:
  1. Creates a feature branch for the story
  2. Develops code for all connected tasks
  3. Opens a PR (CI must pass before merge)
  4. Moves the story to the next stage (Review)

### 6. REVIEW (HITL)

- **Actors:** DEV
- **Actions:** Review and approve the PR
- **On merge:** CD deploys to a staging environment; story moves to next stage (Test)

### 7. TEST (HITL)

- **Actors:** TESTER
- **Actions:** Test the story in staging; when verified, move to next stage (Done)

### 8. DONE

- No action required.
