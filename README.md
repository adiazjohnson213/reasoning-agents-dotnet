# reasoning-agents-dotnet

A **.NET 10** console app to practice Microsoft certification scenarios by generating **MCQ assessments** and grading your answers with **Azure AI Foundry Agent Service (Persistent Agents)**.

This repo is built as a personal study tool (not a dump tool). It focuses on repeatable practice loops with clear console UX and production-friendly plumbing.

---

## What’s implemented

### Core flow
- **Assessment** agent: generates **MCQ** questions (single-best-answer).
- **Critic** agent: grades your answers and returns **strict JSON** (score + issues + improvements).
- **Curator** agent: generates a **curated learning path** based on the Critic feedback + last assessment (evidence-driven).
- **Assessment Preflight** agent (**Microsoft Learn MCP**): fetches up-to-date official guidance and produces **guardrails JSON** used to keep service naming current and avoid deprecated services.

### Modes
- **Quick mode**: a small set of questions.
- **Exam mode**: **50 questions** distributed by domain using a fixed AI‑102 blueprint (domain → count).

---

## How it works (high-level)

The workflow runs as:

1) **Preflight** (Microsoft Learn MCP)  
   Produces `[ASSESSMENT_GUARDRAILS_JSON]` to keep names current and avoid deprecated services.

2) **Assessment**  
   Generates scenario-based MCQs using the domain + study plan context (and guardrails if present).

3) **Human-in-the-loop**  
   You answer in the console.

4) **Critic**  
   Grades the answers and returns JSON (score + actionable feedback).

5) **Curate**  
   Uses evidence (`Critic summary` + last assessment) to propose what to study next.

6) **Plan**  
   Currently **stubbed** (Planner not implemented yet).

> Azure AI Foundry Agent Service uses a **thread → run → messages** execution model.

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

This avoids ambiguity when the Critic maps answers to questions.

---

## Local run

### Prerequisites
- .NET 10 SDK
- Azure login via `DefaultAzureCredential`:
  - `az login`, **or**
  - any other supported credential chain for your environment

### Build
```bash
dotnet build
```

### Run (Quick mode)

Show help:
```bash
dotnet run --project ReasoningAgents.Console -- --help
```

Quick mode:
```bash
dotnet run --project ReasoningAgents.Console -- --cert=AI-102 --days=14 --minutes=90 --exam=false
```

Exam mode (50 questions):
```bash
dotnet run --project ReasoningAgents.Console -- --cert=AI-102 --days=14 --minutes=90 --exam=true
```

## Configuration (User Secrets or env vars)

This project uses:
- **Azure AI Foundry Project Endpoint**
- **Model deployment name**
- Optional agent IDs (to reuse already-created agents)

### User Secrets (recommended for local dev)
```json
{
  "Agent": {
    "ProjectEndpoint": "https://<aiservices-id>.services.ai.azure.com/api/projects/<project-name>",
    "DeploymentName": "<model-deployment-name>",

    "AssessmentAgentId": "<optional existing agent id>",
    "CriticAgentId": "<optional existing agent id>",
    "CuratorAgentId": "<optional existing agent id>",
    "PreflightAgentId": "<optional existing agent id>"
  }
}
```

---

## Notes on Microsoft Learn MCP integration

- The Preflight agent connects to Microsoft Learn via MCP tools (search/fetch) to produce guardrails.
- MCP tool support requires a **preview** version of the `Azure.AI.Agents.Persistent` SDK (as MCP types are not available in older stable releases).

---

## Security

- Do **not** commit secrets.
- Use `DefaultAzureCredential` locally; prefer Managed Identity in cloud environments.

---

## Roadmap

- [x] End-to-end console loop (assessment → answers → critic).
- [x] Add exam mode (50 questions) using a fixed blueprint.
- [x] Add Microsoft Learn MCP preflight for up-to-date guardrails.
- [x] Add evidence-driven Curator (post-critic).
- [ ] Implement Planner (currently stub).
- [ ] Add output persistence (save assessments/results to disk).
- [ ] Add lightweight tests (CLI parsing, prompt formatting, JSON validation).
- [ ] Add telemetry / evaluation harness (optional).
