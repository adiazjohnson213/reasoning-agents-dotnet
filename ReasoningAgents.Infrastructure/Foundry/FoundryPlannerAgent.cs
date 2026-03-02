using System.Text;
using Azure;
using Azure.AI.Agents.Persistent;
using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Models;
using ReasoningAgents.Infrastructure.Configuration;
using ReasoningAgents.Infrastructure.Foundry.Prompts;

namespace ReasoningAgents.Infrastructure.Foundry
{
    public sealed class FoundryPlannerAgent : IAgentStep<(CertificationGoal Goal, string LearningPath), string>
    {
        private readonly AgentOptions _options;
        private readonly PersistentAgentsClient _client;

        public FoundryPlannerAgent(AgentOptions options, PersistentAgentsClient client)
        {
            _options = options;
            _client = client;
        }

        public async Task<string> ExecuteAsync((CertificationGoal Goal, string LearningPath) input, CancellationToken ct)
        {
            PersistentAgent agent;
            if (!string.IsNullOrWhiteSpace(_options.PlannerAgentId))
            {
                agent = await _client.Administration.GetAgentAsync(_options.PlannerAgentId, ct);
            }
            else
            {
                agent = await _client.Administration.CreateAgentAsync(
                    model: _options.DeploymentName,
                    name: "ReasoningAgents.Planner.v1",
                    description: "Builds a day-by-day study plan from curated resources and performance evidence.",
                    instructions: PlannerPrompts.BuildPlannerAgentInstructions(),
                    cancellationToken: ct);
            }

            PersistentAgentThread thread = await _client.Threads.CreateThreadAsync(cancellationToken: ct);

            var prompt = PlannerPrompts.BuildPlannerRunPrompt(certificationCode: input.Goal.CertificationCode,
                                                              days: input.Goal.DaysAvailable,
                                                              minutesPerDay: input.Goal.DailyMinutes,
                                                              learningPath: input.LearningPath);

            await _client.Messages.CreateMessageAsync(thread.Id, MessageRole.User, prompt, cancellationToken: ct);

            ThreadRun run = await _client.Runs.CreateRunAsync(thread.Id, agent.Id, cancellationToken: ct);

            // Poll
            var started = DateTimeOffset.UtcNow;
            var timeout = TimeSpan.FromMinutes(2);

            while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress || run.Status == RunStatus.RequiresAction)
            {
                ct.ThrowIfCancellationRequested();

                if (DateTimeOffset.UtcNow - started > timeout)
                    throw new TimeoutException($"Run timed out. Last status: {run.Status}");

                await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
                run = await _client.Runs.GetRunAsync(thread.Id, run.Id, ct);
            }

            if (run.Status != RunStatus.Completed)
                throw new InvalidOperationException($"Run did not complete successfully. Status: {run.Status}. Error: {run.LastError?.Message}");

            return ReadLastAgentTextForRun(thread.Id, run.Id, ct);
        }

        private string ReadLastAgentTextForRun(string threadId, string runId, CancellationToken ct)
        {
            Pageable<PersistentThreadMessage> messages = _client.Messages.GetMessages(threadId: threadId,
                                                                                      runId: runId,
                                                                                      limit: 20,
                                                                                      order: ListSortOrder.Descending,
                                                                                      cancellationToken: ct);

            foreach (var msg in messages)
            {
                if (msg.Role != MessageRole.Agent) continue;

                var sb = new StringBuilder();
                foreach (var content in msg.ContentItems)
                    if (content is MessageTextContent t) sb.Append(t.Text);

                var text = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            throw new InvalidOperationException("No agent response text was returned.");
        }
    }
}