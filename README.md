# reasoning-agents-dotnet

A **.NET 10** console application that builds a **multi-agent** study assistant for Microsoft certifications.  
It generates a learning path, turns it into a time-boxed study plan, creates assessments, grades results using a rubric, and iterates until the learner is ready.

The system runs **locally** using **Microsoft Agent Framework** and can also run in the cloud using **Azure AI Foundry Agent Service** for a production-ready hosted agent experience. :contentReference[oaicite:1]{index=1}

## What this project does

Given a target certification (e.g., AI-102) and a time window, the app will:

1. **Curate a learning path** from trusted sources (docs / learning resources).
2. **Generate a study plan** with milestones and clear outcomes.
3. **Create an assessment** (practice questions + expected answers + explanations).
4. **Evaluate performance** using a rubric (scoring + feedback).
5. **Decide PASS/RETRY** and refine the plan if needed (bounded iteration).

## Reasoning approach (multi-agent)

This project explicitly uses multi-agent reasoning patterns:

- **Planner–Executor**: create a step plan first, then execute it step-by-step.
- **Critic/Verifier**: a separate agent validates quality, consistency, and scoring.
- **Bounded Iteration**: controlled retries with explicit changes (max N loops).

Microsoft Agent Framework supports building AI agents and multi-agent workflows in .NET, including orchestration patterns and production-grade capabilities. :contentReference[oaicite:2]{index=2}

## Agents (roles)

- **LearningPathCuratorAgent**  
  Selects and structures learning resources into a coherent path.

- **StudyPlanGeneratorAgent**  
  Produces a day-by-day (or week-by-week) plan with milestones and checkpoints.

- **AssessmentAgent**  
  Generates practice questions aligned to the plan and the selected resources.

- **CriticAgent**  
  Grades answers using a rubric, provides feedback, and decides whether to iterate.

## Knowledge / Retrieval (options)

This project can evolve across retrieval strategies:

- **MVP**: curated list of URLs + lightweight extraction.
- **Learn MCP (optional)**: connect to the **Microsoft Learn MCP Server** for trusted, up-to-date documentation retrieval via the MCP endpoint (`https://learn.microsoft.com/api/mcp`). :contentReference[oaicite:3]{index=3}
- **Enterprise (optional)**: index curated content in **Azure AI Search** and ground responses from it (later step).

## Local run (Console)

> TODO: Will be added once the first executable version is committed.

Planned prerequisites:
- .NET 10 SDK
- Optional: Azure CLI for sign-in when testing cloud integration

## Cloud run (Azure AI Foundry Agent Service)

> TODO: Will be added once the Foundry integration is implemented.

Foundry Agent Service provides a managed, cloud-hosted way to run agents with tools and enterprise-ready foundations. :contentReference[oaicite:4]{index=4}

## Security

- No secrets committed (keys, tokens, connection strings).
- Prefer `DefaultAzureCredential` for local development and Managed Identity in cloud environments.
- Environment-specific values should be provided via environment variables (or local user secrets).

## Roadmap

- [ ] Create .NET 10 Console app skeleton and first runnable flow (end-to-end).
- [ ] Implement the four agents (Curator, Plan, Assessment, Critic).
- [ ] Add rubric-based scoring and PASS/RETRY bounded iteration.
- [ ] Integrate Azure AI Foundry Agent Service (hosted agent execution).
- [ ] Optional: Learn MCP retrieval integration.
- [ ] Optional: Telemetry and evaluation harness.
