# reasoning-agents-dotnet

A **.NET 10** study tool that generates **scenario-based MCQ assessments** and grades your answers using **Azure AI Foundry Agent Service (Persistent Agents)**.

This repo is built as a personal learning tool (not a dump tool). It focuses on repeatable practice loops with a clean separation between **Domain**, **Application**, **Infrastructure**, and **Presentation** (**Console** + **API**).

---

## Solution structure (5 projects)

This solution follows a clean separation: **Domain/Application** at the center, **Infrastructure** for external integrations, and **Presentation** as entrypoints.

```text
ReasoningAgents.Domain         // pure models + contracts (no Azure SDK)
ReasoningAgents.Application    // workflow/orchestration + session services/contracts
ReasoningAgents.Infrastructure // Azure AI Foundry + MCP + prompts + adapters
ReasoningAgents.Console        // CLI entrypoint + output renderers
ReasoningAgents.Api            // ASP.NET Core Web API (Controllers) for UI/backend
```

### Dependency direction

```text
Domain:         -
Application ->  Domain
Infrastructure -> Application + Domain
Console ->      Application + Infrastructure
Api ->          Application + Infrastructure
```

---

## What’s implemented

### Agents / steps
- **Assessment Preflight (Microsoft Learn MCP)**  
  Fetches current official information and produces **guardrails JSON** so assessments use up-to-date naming and avoid deprecated services.
- **Assessment**  
  Generates **MCQs** with:
  - exactly 4 options (`A`–`D`)
  - exactly **one** correct answer
  - **scenario-based** prompts
  - **NO-MIGRATION** policy (no rename/retirement/deprecation trivia)
- **Critic**  
  Grades user answers and returns structured feedback:
  - `score`
  - `summary`
  - `issues`
  - `improvements`
- **Curator**  
  Produces a **Learning Path JSON** using Critic feedback + last assessment.
- **Planner**  
  Produces a **Study Plan JSON** (day-by-day sessions) from the Learning Path + time constraints.

### Workflow order (current)

1. **Preflight** → produces `[ASSESSMENT_GUARDRAILS_JSON]`
2. **Assessment** → generates scenario-based MCQs
3. **Human-in-the-loop** → user answers
4. **Critic** → grades answers
5. **Curator** → learning path
6. **Planner** → study plan

> Azure AI Foundry Agent Service uses a **thread → run → messages** execution model.

### Modes
- **Quick mode** → small set of questions
- **Exam mode** → **50 questions** distributed by domain (AI‑102 blueprint)

---

## Console app

### Answer format (strict)

The console expects answers in this exact format:

```text
Q1=A
Q2=B
Q3=C
Q4=D
```

Finish by pressing **Enter on an empty line**.

### Console output
The console renders:
- **Result summary**
- **Study Plan**
- **Learning Path**

Renderers live under:

```text
ReasoningAgents.Console/Outputs/
  SummaryRender.cs
  StudyPlanRender.cs
  LearningPathRender.cs
```

### Run (Console)

#### Build
```bash
dotnet build
```

#### Show help
```bash
dotnet run --project ReasoningAgents.Console -- --help
```

#### Quick mode
```bash
dotnet run --project ReasoningAgents.Console -- --cert=AI-102 --days=14 --minutes=90 --exam=false
```

#### Exam mode
```bash
dotnet run --project ReasoningAgents.Console -- --cert=AI-102 --days=14 --minutes=90 --exam=true
```

---

## API (Controllers)

`ReasoningAgents.Api` is an ASP.NET Core **controller-based Web API** intended to back a future UI.

### Current API flow

The API is **session-based** so the frontend can render the assessment first, then submit answers later.

#### 1) Create session
`POST /api/sessions`

Request:

```json
{
  "cert": "AI-102",
  "days": 14,
  "minutes": 90,
  "exam": false
}
```

Response:

```json
{
  "sessionId": "...",
  "createdUtc": "...",
  "questions": [
    {
      "number": 1,
      "prompt": "You need to create...",
      "options": [
        { "key": "A", "text": "Azure AI Language - CLU" },
        { "key": "B", "text": "Azure AI Language - Question Answering" },
        { "key": "C", "text": "Azure AI Document Intelligence" },
        { "key": "D", "text": "Azure AI Speech Services" }
      ]
    }
  ]
}
```

What happens internally:
- Preflight runs first
- slim guardrails are generated
- assessment text is produced
- assessment text is parsed into a structured `questions[]` payload for the frontend
- session state is stored in-memory

#### 2) Submit answers
`POST /api/sessions/{sessionId}/answers`

Request:

```json
{
  "answers": "Q1=A\nQ2=B\nQ3=C\nQ4=D"
}
```

Response (shape simplified):

```json
{
  "passed": false,
  "score": 6,
  "summary": "The user understands the basics but still confuses similar Azure AI services.",
  "issues": [
    "Selected the wrong service for invoice extraction."
  ],
  "improvements": [
    "Review Document Intelligence vs Question Answering."
  ],
  "learningPathJson": "{ ... }",
  "studyPlanJson": "{ ... }"
}
```

What happens internally:
- Critic evaluates the submitted answers
- Curator generates the learning path
- Planner generates the study plan

### Session storage

For now, API sessions are stored using **in-memory cache** (`IMemoryCache`).

This is enough for local development and single-server testing, but it should be replaced later if you need:
- multi-instance deployment
- distributed sessions
- persistence across restarts

---

## Configuration

Configuration is read from **User Secrets**, **environment variables**, or standard ASP.NET Core configuration.

### Required settings

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

### Notes
- `ProjectEndpoint` must be a valid Azure AI Foundry project endpoint.
- `DeploymentName` is the deployed model used by the agents.
- Azure SDK / MCP dependencies live in **ReasoningAgents.Infrastructure**.
- **Domain** and **Application** stay free of Azure SDK references.

---

## Microsoft Learn MCP notes

Preflight connects to Microsoft Learn MCP tools:
- `microsoft_docs_search`
- `microsoft_docs_fetch`

These are used only to keep naming and service guidance current.

The assessment prompt explicitly avoids turning MCP guardrails into migration trivia.

---

## Development notes

### Why the API returns structured questions
The frontend needs to:
- render one question at a time
- store selected options cleanly
- avoid parsing raw MCQ text in the browser

So the backend keeps the agent output format strict, then parses it into:

```json
{
  "questions": [
    {
      "number": 1,
      "prompt": "...",
      "options": [
        { "key": "A", "text": "..." }
      ]
    }
  ]
}
```

### Why results are now structured
The workflow now uses structured result models instead of fragile string parsing for:
- `Passed`
- `Score`
- `Summary`
- `Issues`
- `Improvements`

This makes both console rendering and API responses more stable.

---

## Security

- Do **not** commit secrets.
- Prefer `DefaultAzureCredential` locally.
- Prefer **Managed Identity** in hosted environments.

---

## Roadmap

- [x] End-to-end console loop (assessment → answers → critic)
- [x] Exam mode (50 questions using fixed blueprint)
- [x] Microsoft Learn MCP preflight
- [x] Evidence-driven Curator
- [x] Planner (Study Plan JSON)
- [x] Pretty-printed console outputs
- [x] Controller-based API scaffold
- [x] Session-based API workflow (`/api/sessions`, `/api/sessions/{id}/answers`)
- [x] Structured question payload for frontend rendering
- [ ] Replace in-memory session storage with a distributed option
- [ ] Add API authentication / authorization
- [ ] Add automated tests for parsers, DTOs, and workflow services
- [ ] Add persistence / history
- [ ] Add telemetry / evaluation harness
