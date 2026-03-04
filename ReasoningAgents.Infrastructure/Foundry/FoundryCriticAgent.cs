using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.Agents.Persistent;
using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Inputs;
using ReasoningAgents.Infrastructure.Configuration;
using ReasoningAgents.Infrastructure.Foundry.Prompts;

namespace ReasoningAgents.Infrastructure.Foundry
{
    public sealed class FoundryCriticAgent
        : IAgentStep<CriticInput, (bool Passed, string Summary)>
    {
        private readonly AgentOptions _options;
        private readonly PersistentAgentsClient _client;

        public FoundryCriticAgent(AgentOptions options, PersistentAgentsClient client)
        {
            _options = options;
            _client = client;
        }

        public async Task<(bool Passed, string Summary)> ExecuteAsync(CriticInput input,
                                                                      CancellationToken ct)
        {
            PersistentAgent agent;
            if (!string.IsNullOrWhiteSpace(_options.CriticAgentId))
            {
                agent = await _client.Administration.GetAgentAsync(_options.CriticAgentId, ct);
            }
            else
            {
                agent = await _client.Administration.CreateAgentAsync(
                    model: _options.DeploymentName,
                    name: "ReasoningAgents.Critic.v1",
                    description: "Grades certification practice answers with a strict JSON rubric (score 0–10) and returns actionable feedback.",
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

            run = await WaitForRunCompletionAsync(thread.Id, run.Id, ct);

            var lastAssistantText = GetLastAgentTextForRun(thread.Id, run.Id, ct);

            return ParseCriticJson(lastAssistantText);
        }

        private async Task<ThreadRun> WaitForRunCompletionAsync(string threadId, string runId, CancellationToken ct)
        {
            var started = DateTimeOffset.UtcNow;
            var timeout = TimeSpan.FromMinutes(2);

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                ThreadRun run = await _client.Runs.GetRunAsync(threadId, runId, ct);

                if (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
                {
                    if (DateTimeOffset.UtcNow - started > timeout)
                        throw new TimeoutException($"Run timed out. Last status: {run.Status}");

                    await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                    continue;
                }

                if (run.Status == RunStatus.RequiresAction)
                {
                    throw new InvalidOperationException("Run requires action (tool calls) but this client does not handle tool execution.");
                }

                if (run.Status != RunStatus.Completed)
                {
                    throw new InvalidOperationException($"Run did not complete successfully. Status: {run.Status}. Error: {run.LastError?.Message}");
                }

                return run;
            }
        }

        private string GetLastAgentTextForRun(string threadId, string runId, CancellationToken ct)
        {
            Pageable<PersistentThreadMessage> messages = _client.Messages.GetMessages(threadId: threadId,
                                                                                      runId: runId,
                                                                                      limit: 20,
                                                                                      order: ListSortOrder.Descending,
                                                                                      cancellationToken: ct);

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
                    return text;
            }

            throw new InvalidOperationException("No agent response text was returned.");
        }

        private static (bool Passed, string Summary) ParseCriticJson(string jsonText)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;

                var score = root.GetProperty("score").GetInt32();

                if (score < 0 || score > 10)
                    throw new InvalidOperationException($"Invalid score returned: {score}");

                var summary = root.TryGetProperty("summary", out var summaryEl)
                    ? (summaryEl.GetString() ?? "")
                    : "";

                var issues = TryReadStringArray(root, "issues");
                var improvements = TryReadStringArray(root, "improvements");
                var domain = root.TryGetProperty("domain", out var domainEl) ? (domainEl.GetString() ?? "") : "";

                var passed = score >= 7;

                var final = new StringBuilder();
                final.Append($"Score: {score}/10. ");
                if (!string.IsNullOrWhiteSpace(domain))
                    final.Append($"Domain: {domain}. ");
                if (!string.IsNullOrWhiteSpace(summary))
                    final.Append(summary.Trim());

                if (issues.Count > 0)
                {
                    final.Append(" Issues: ");
                    final.Append(string.Join(" | ", issues));
                    final.Append('.');
                }

                if (improvements.Count > 0)
                {
                    final.Append(" Improvements: ");
                    final.Append(string.Join(" | ", improvements));
                    final.Append('.');
                }

                return (passed, final.ToString().Trim());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse critic response as JSON. Raw response:\n{jsonText}", ex);
            }
        }

        private static List<string> TryReadStringArray(JsonElement root, string propertyName)
        {
            var result = new List<string>();

            if (!root.TryGetProperty(propertyName, out var arr))
                return result;

            if (arr.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        result.Add(s.Trim());
                }
            }

            return result;
        }
    }
}