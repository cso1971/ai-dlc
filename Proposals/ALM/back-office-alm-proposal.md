# Back Office and Pluggable ALM — Concept Proposal

### Pre-startup configuration portal and ALM selection for AI-DLC

---

## 1. What is AI-DLC

**AI-DLC** (AI Development Lifecycle) is the ecosystem that integrates Application Lifecycle Management (ALM) tools with AI agents to automate and support the software development lifecycle: from high-level requirements through merge requests and delivery, with human approval gates (Human-In-The-Loop) at critical stages.

### Main components

| Component | Role |
|-----------|------|
| **DLC Server** | Centralized server (e.g. on VPS) that orchestrates workflow, webhooks, AI agents and ALM integrations |
| **ALM** | Planning and tracking tool: epics, user stories, tasks, boards, merge requests |
| **AI Agents** | Claude Code CLI, Council of Agents — requirements analysis, breakdown, implementation, code review |
| **Log Viewer** | Real-time AI session monitoring interface |

### Typical workflow (8 stages)

```
Requirements → Breakdown → Refinement → Ready → Planned → Review → Test → Done
   (HITL)        (AI)        (HITL)      (AI)     (AI)     (HITL)  (HITL)
```

---

## 2. The proposal: Back Office + Pluggable ALM

### Current problem

- The ALM (currently GitLab) is **hardcoded** in the configuration
- GitLab free tier has **limited planning** (basic board, few advanced features)
- Configuration is done via `.env` files and environment variables, with no dedicated interface
- **Changing ALM** requires code modifications and deployment changes

### Proposed solution

A **Back Office portal** that allows, **before DLC Server startup**, to:

1. **Configure** the entire ecosystem (ALM, webhooks, credentials, parameters)
2. **Choose the ALM** among the main market options (on-premise or cloud)
3. **Validate** connection and configuration before starting services
4. **Persist** configuration in a structured and secure way

### Expected benefits

- **Flexibility**: adopt the ALM best suited to the context (GitLab, Jira, Azure DevOps, GitHub, etc.)
- **Usability**: configuration via UI instead of environment files
- **Scalability**: support for multiple projects and ALMs over time
- **Maintainability**: ALM integration abstraction, reduced coupling

---

## 3. Audience: stakeholders involved in AI-DLC

This document addresses all potential stakeholders who interact with AI-DLC. Below are the roles and their interests regarding the Back Office and ALM selection.

### 3.1 Product Owner (PO) / Business Analyst (BA)

| Interest | Description |
|----------|-------------|
| **Advanced planning** | Epics, roadmap, prioritized backlog, sprint planning |
| **Traceability** | Link requirements → stories → tasks → code |
| **Human-In-The-Loop** | Approval gates on breakdown and refinement |
| **Visibility** | Boards and reports on work status |

**Back Office / ALM relevance**: The PO benefits from the ability to choose an ALM with sophisticated planning (e.g. Jira) when GitLab free is not enough. The Back Office does not involve them directly in configuration, but they benefit indirectly.

---

### 3.2 Developer (DEV)

| Interest | Description |
|----------|-------------|
| **Clear tasks** | AI-generated tasks with acceptance criteria |
| **Assisted code review** | AI responding to MR comments |
| **Integration with their workflow** | Use of Git, MR, CI/CD in their existing tools |
| **Transparency** | Logs and status of AI sessions |

**Back Office / ALM relevance**: The DEV works daily with the ALM (issues, MR). ALM choice impacts their workflow. The Back Office can align the DLC with the ALM already used by the team.

---

### 3.3 QA / Tester

| Interest | Description |
|----------|-------------|
| **Acceptance criteria** | Clear acceptance criteria for testing |
| **Item status** | Visibility on what is ready for test |
| **Test tool integration** | Link with bug tracker, test cases |

**Back Office / ALM relevance**: Like the DEV, QA uses the ALM to track status and criteria. An ALM with testing integrations (e.g. Jira + Xray) may be preferable.

---

### 3.4 DevOps / Infrastructure

| Interest | Description |
|----------|-------------|
| **VPS deployment** | DLC Server runnable on Virtual Private Server |
| **Centralized configuration** | Single configuration point instead of N files |
| **Security** | Secure management of tokens and credentials |
| **Health check** | Verify ALM, webhooks and services are operational |

**Back Office / ALM relevance**: The Back Office is the main tool for DevOps. Configuration, ALM selection, credential management and connection validation all simplify deployment and maintenance.

---

### 3.5 CTO / Tech Lead

| Interest | Description |
|----------|-------------|
| **Standardization** | Single DLC for multiple teams/projects |
| **Costs** | ALM cost evaluation (free vs paid, cloud vs on-premise) |
| **Extensibility** | Ability to add new ALMs and integrations |
| **Governance** | Control over who configures and how |

**Back Office / ALM relevance**: The CTO decides which ALM to adopt at the organizational level. The Back Office with pluggable ALM allows aligning the DLC to the organization's ALM strategy without rewriting the system.

---

### 3.6 Project Manager (PM)

| Interest | Description |
|----------|-------------|
| **Timeline and capacity** | Sprints, milestones, estimates |
| **Reports** | Burndown, velocity, progress status |
| **Collaboration** | Shared boards, comments, notifications |

**Back Office / ALM relevance**: The PM uses the ALM for planning and reporting. ALM choice (Jira, Azure DevOps, etc.) often depends on tools already in use. The Back Office allows configuring the DLC for the chosen ALM.

---

### 3.7 System Administrator / DLC Admin

| Interest | Description |
|----------|-------------|
| **Initial configuration** | DLC Server setup on first boot |
| **Configuration changes** | Update ALM, tokens, URLs when needed |
| **Troubleshooting** | Verify connections, logs, service status |

**Back Office / ALM relevance**: The Admin is the primary Back Office user. They configure the ALM, enter credentials, validate the connection and start the DLC.

---

### 3.8 Vendor / Integrator

| Interest | Description |
|----------|-------------|
| **Installation** | DLC deployment at customer site |
| **Customization** | Configuration based on customer's ALM |
| **Support** | Assistance with configuration and integrations |

**Back Office / ALM relevance**: A well-designed Back Office reduces onboarding and support time. ALM selection can be done by the customer without development changes.

---

## 4. Stakeholder summary and Back Office priority

| Stakeholder | Back Office priority | Typical role |
|-------------|---------------------|--------------|
| **DLC Admin** | High — direct user | Configure, validate, start |
| **DevOps** | High — infrastructure management | Deploy, security, monitoring |
| **CTO / Tech Lead** | Medium — strategic decisions | ALM selection, governance |
| **PO / BA** | Low — indirect benefits | Planning, traceability |
| **Developer** | Low — indirect benefits | Daily work with ALM |
| **QA** | Low — indirect benefits | Testing, acceptance criteria |
| **PM** | Low — indirect benefits | Planning, reporting |
| **Vendor** | Medium — reduced support | Installation, customization |

---

## 5. Next steps

This document provides the foundation for:

1. **Validation** with stakeholders
2. **Refinement** of requirements and user stories
3. **Prioritization** of Back Office features
4. **Technical design** (config schema, ALM adapter, UI)

Related documents:

- `Proposals/CouncilOfAgents/council-technical-implementation.md` — multi-agent evolution
- `CONTEXT.md` — current state of Scaile and Distributed Playground

---

*Document generated on March 7, 2026 — ai-dlc project*

**Christian Soliman** — [christian.soliman@adesso.ch](mailto:christian.soliman@adesso.ch)
