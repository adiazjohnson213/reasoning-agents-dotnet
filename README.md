# reasoning-agents-dotnet

A **.NET 10** console application that helps you study for Microsoft certifications by generating practice assessments and grading your answers using **Azure AI Foundry Agent Service (Persistent Agents)**.

The app supports two assessment modes:
- **Quick mode** (small set of questions)
- **Exam mode** (**50 questions**) distributed by domain using the AI-102 study guide weighting ranges (implemented as a fixed, valid split).

## Current status

✅ Implemented:
- CLI parsing (`--cert`, `--days`, `--minutes`, `--exam true/false`)
- Foundry **Assessment** agent:
  - Generates **MCQ** questions by **domain + count**
- Foundry **Critic** agent:
  - Grades the user's answers and returns **JSON** (parsed into pass/fail + summary)
- Exam mode orchestration:
  - Generates **50 questions** using a blueprint (domain → count)

🚧 In progress / next:
- Replace stub Curator/Planner with real implementations (currently stubs)
- Improve question display (hide any answer/explanation text if the model returns it)
- Add automated persistence guidance for agent IDs (currently stored manually in User Secrets)

## How it works

High-level flow:
1) Build a study plan (currently stub)
2) Generate an assessment:
   - quick: `FoundryAssessmentAgent`
   - exam: `FoundryExamAssessmentAgent` (blueprint)
3) You answer in the console
4) `FoundryCriticAgent` evaluates your answers and returns pass/fail + feedback

Azure AI Foundry Agent Service uses a **thread → run → messages** model for executing agents.

## Local run (Console)

### Prerequisites
- .NET 10 SDK
- Azure CLI logged in (`az login`) or any valid `DefaultAzureCredential` flow

### Build
```bash
dotnet build
```

### CLI usage

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

This project uses **Azure AI Foundry project endpoint** + model deployment name.

Project endpoint format:
`https://<aiservices-id>.services.ai.azure.com/api/projects/<project-name>`

### User Secrets (recommended for local dev)
Set these keys (schema only; do not commit secrets):

```json
{
  "Agent": {
    "ProjectEndpoint": "https://<aiservices-id>.services.ai.azure.com/api/projects/<project-name>",
    "DeploymentName": "<model-deployment-name>",
    "AssessmentAgentId": "<optional existing agent id>",
    "CriticAgentId": "<optional existing agent id>"
  }
}
```

## Security
- No secrets committed (keys/tokens/connection strings).
- Use `DefaultAzureCredential` locally; use managed identity in cloud scenarios.

## Roadmap

- [x] Create .NET 10 Console app skeleton and first runnable flow (end-to-end).
- [x] Implement Foundry assessment generation (MCQ by domain + count).
- [x] Implement Foundry critic evaluation (JSON output + parsing).
- [x] Add exam mode (50 questions) using a blueprint.
- [ ] Replace stub Curator/Planner with real implementations.
- [ ] Improve assessment display (ensure answer/explanation is hidden from the user if present).
- [ ] Add lightweight unit tests for CLI parsing.
- [ ] Add telemetry / evaluation harness (optional).
