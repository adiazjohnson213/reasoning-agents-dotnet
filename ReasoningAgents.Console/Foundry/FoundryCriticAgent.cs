using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.Agents.Persistent;
using ReasoningAgents.Console.Foundry.Prompts;
using ReasoningAgents.Core.Agents;
using ReasoningAgents.Core.Common;
using ReasoningAgents.Core.Models;

namespace ReasoningAgents.Console.Foundry
{
    public sealed class FoundryCriticAgent : IAgentStep<(CertificationGoal Goal, string Assessment, string UserAnswers), (bool Passed, string Summary)>
    {
        private readonly AgentOptions _options;
        private readonly PersistentAgentsClient _client;

        public FoundryCriticAgent(AgentOptions options, PersistentAgentsClient client)
        {
            _options = options;
            _client = client;
        }
        public async Task<(bool Passed, string Summary)> ExecuteAsync((CertificationGoal Goal, string Assessment, string UserAnswers) input, CancellationToken ct)
        {
            PersistentAgent agent;
            if (!string.IsNullOrWhiteSpace(_options.CriticAgentId))
            {
                agent = await _client.Administration.GetAgentAsync(_options.CriticAgentId, ct);
            }
            else
            {
                agent = await _client.Administration.CreateAgentAsync(model: _options.DeploymentName,
                                                                      name: "ReasoningAgents.Critic.v1",
                                                                      description: "Grades certification practice answers with a strict JSON rubric (score 0–10) and returns pass/fail + actionable feedback.",
                                                                      instructions: CriticPrompts.BuildCriticAgentInstructions(),
                                                                      cancellationToken: ct);
            }

            PersistentAgentThread thread = await _client.Threads.CreateThreadAsync(cancellationToken: ct);

            var prompt = CriticPrompts.BuildCriticRunPrompt(input.Goal.CertificationCode,
                                                            input.Assessment,
                                                            input.UserAnswers);

            await _client.Messages.CreateMessageAsync(thread.Id,
                                                      MessageRole.User,
                                                      prompt,
                                                      cancellationToken: ct);

            ThreadRun run = await _client.Runs.CreateRunAsync(thread.Id,
                                                              agent.Id,
                                                              cancellationToken: ct);

            var started = DateTimeOffset.UtcNow;
            var timeout = TimeSpan.FromMinutes(2);

            while (run.Status == RunStatus.Queued
                || run.Status == RunStatus.InProgress
                || run.Status == RunStatus.RequiresAction)
            {
                ct.ThrowIfCancellationRequested();

                if (DateTimeOffset.UtcNow - started > timeout)
                    throw new TimeoutException($"Run timed out. Last status: {run.Status}");

                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                run = await _client.Runs.GetRunAsync(thread.Id, run.Id, ct);
            }

            if (run.Status != RunStatus.Completed)
            {
                throw new InvalidOperationException($"Run did not complete successfully. Status: {run.Status}");
            }

            Pageable<PersistentThreadMessage> messages = _client.Messages.GetMessages(threadId: thread.Id,
                                                                                      order: ListSortOrder.Ascending,
                                                                                      cancellationToken: ct);

            string? lastAssistantText = null;

            foreach (var msg in messages)
            {
                if (msg.Role != MessageRole.Agent)
                    continue;

                var sb = new StringBuilder();
                foreach (var content in msg.ContentItems)
                {
                    if (content is MessageTextContent textItem)
                        sb.Append(textItem.Text);
                }

                var text = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    lastAssistantText = text;
            }

            if (string.IsNullOrWhiteSpace(lastAssistantText))
                throw new InvalidOperationException("No agent response text was returned.");

            try
            {
                using var doc = JsonDocument.Parse(lastAssistantText);
                var root = doc.RootElement;

                var passed = root.GetProperty("passed").GetBoolean();
                var score = root.GetProperty("score").GetInt32();
                var summary = root.GetProperty("summary").GetString() ?? "";

                if (score < 0 || score > 10)
                    throw new InvalidOperationException($"Invalid score returned: {score}");

                var computedPassed = score >= 7;

                return (computedPassed, $"Score: {score}/10. {summary}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse critic response as JSON. Raw response:\n{lastAssistantText}", ex);
            }
        }
    }
}
