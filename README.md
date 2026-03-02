# reasoning-agents-dotnet

A **.NET 10** study tool that generates **scenario-based MCQ assessments** and grades your answers using **Azure AI Foundry Agent Service (Persistent Agents)**.

This repo is built as a personal learning tool (not a dump tool). It focuses on repeatable practice loops with clear console UX and a structure that can grow into an **HTTP API** for a future UI.

---

## Solution structure (5 projects)

This solution follows a clean separation: **Domain/Application** at the center, **Infrastructure** for external integrations, and **Presentation** as entrypoints.

```
ReasoningAgents.Domain         // pure models + contracts (no Azure SDK)
ReasoningAgents.Application    // workflow/orchestration (use cases)
ReasoningAgents.Infrastructure // Azure AI Foundry + MCP + prompts + adapters
ReasoningAgents.Console        // CLI entrypoint + output renderers
ReasoningAgents.Api            // ASP.NET Core Web API (Controllers) entrypoint for UI/backend
```

---

## What’s implemented

### Agents / steps
- **Assessment**: generates **MCQs** (exactly 4 options A–D, single best answer; no answers/rationales in output).
- **Critic**: grades your answers and returns **strict JSON** (`score`, `summary`, `issues`, `improvements`).
- **Curator**: produces a **Learning Path** JSON based on Critic feedback + last assessment (evidence-driven).
- **Planner**: produces a **Study Plan** JSON (day-by-day sessions) from the Learning Path + time constraints.
- **Assessment Preflight (Microsoft Learn MCP)**: fetches current official info and outputs **guardrails JSON** to keep naming up to date and avoid deprecated services.

### Workflow order (current)
1) **Preflight** → produces `[ASSESSMENT_GUARDRAILS_JSON]`
2) **Assessment** → scenario-based MCQs (NO‑MIGRATION policy)
3) **Human‑in‑the‑loop** → you answer
4) **Critic** → grades and returns JSON feedback
5) **Curate** → learning path
6) **Plan** → study plan

> Azure AI Foundry uses a **thread → run → messages** execution model.

### Modes
- **Quick mode**: small set of questions.
- **Exam mode**: **50 questions** distributed by domain (AI‑102 blueprint).

---

## Console UX

### Answer format (strict)
Enter answers as:

```
Q1=A
Q2=B
Q3=C
Q4=D
```

Finish input by pressing **Enter on an empty line**.

### Output
The console prints:
- **Result summary** (pretty-printed)
- **Study Plan** (pretty-printed from JSON)
- **Learning Path** (pretty-printed from JSON)

Renderers live under `ReasoningAgents.Console/Outputs/`.

---

## Local run (Console)

### Prerequisites
- .NET 10 SDK
- Azure login via `DefaultAzureCredential` (`az login`, etc.)

### Build
```bash
dotnet build
```

### Show help
```bash
dotnet run --project ReasoningAgents.Console -- --help
```

### Quick mode
```bash
dotnet run --project ReasoningAgents.Console -- --cert=AI-102 --days=14 --minutes=90 --exam=false
```

### Exam mode (50 questions)
```bash
dotnet run --project ReasoningAgents.Console -- --cert=AI-102 --days=14 --minutes=90 --exam=true
```

---

## Configuration (User Secrets or env vars)

Configuration is read from **User Secrets** and/or **environment variables**.

### User Secrets (recommended for local dev)
```json
{
  "Agent": {
    "ProjectEndpoint": "https://<aiservices-id>.services.ai.azure.com/api/projects/<project-name>",
    "DeploymentName": "<model-deployment-name>",

    "AssessmentAgentId": "<optional existing agent id>",
    "CriticAgentId": "<optional existing agent id>",
    "CuratorAgentId": "<optional existing agent id>",
    "PlannerAgentId": "<optional existing agent id>",
    "PreflightAgentId": "<optional existing agent id>"
  }
}
```

> Package dependencies for Azure AI Foundry / MCP live in **ReasoningAgents.Infrastructure** (Domain/Application stay clean).

---

## Microsoft Learn MCP notes

- Preflight connects to Microsoft Learn via MCP tools (`microsoft_docs_search` / `microsoft_docs_fetch`) to produce guardrails.
- MCP tool support may require a **preview** version of the `Azure.AI.Agents.Persistent` SDK.

---

## API (Controllers)

`ReasoningAgents.Api` is an ASP.NET Core **controller-based Web API** intended to back a future UI.

Current status:
- Project scaffold + project references (presentation layer).
- Endpoints will be added next (likely step-based workflow for UI: assessment → submit answers → critique → plan).

---

## Security

- Do **not** commit secrets.
- Prefer Managed Identity in hosted environments.

---

## Roadmap

- [x] End-to-end console loop (assessment → answers → critic).
- [x] Add exam mode (50 questions) using a fixed blueprint.
- [x] Add Microsoft Learn MCP preflight for up-to-date guardrails.
- [x] Add evidence-driven Curator (post-critic).
- [x] Add Planner (Study Plan JSON).
- [x] Pretty-print Study Plan + Learning Path in console.
- [ ] Implement API endpoints for UI (controller-based).
- [ ] Persist sessions/results (optional).
- [ ] Add lightweight tests (CLI parsing, prompt formatting, JSON validation).
- [ ] Add telemetry / evaluation harness (optional).
