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
    public sealed class FoundryCuratorAgent : IAgentStep<CuratorInput, string>
    {
        private readonly AgentOptions _options;
        private readonly PersistentAgentsClient _client;
        private readonly HttpClient _http;

        private const string ToolGetCertificationStudyGuide = "get_certification_study_guide";
        private const string ToolExtractAi102Blueprint = "extract_ai102_blueprint";

        public FoundryCuratorAgent(AgentOptions options, PersistentAgentsClient client, HttpClient http)
        {
            _options = options;
            _client = client;
            _http = http;
        }

        public async Task<string> ExecuteAsync(CuratorInput input, CancellationToken ct)
        {
            PersistentAgent agent;

            if (!string.IsNullOrWhiteSpace(_options.CuratorAgentId))
            {
                agent = await _client.Administration.GetAgentAsync(_options.CuratorAgentId, ct);
            }
            else
            {
                List<ToolDefinition> tools =
                [
                    BuildGetCertificationStudyGuideTool(),
                    BuildExtractAi102BlueprintTool()
                ];

                agent = await _client.Administration.CreateAgentAsync(
                    model: _options.DeploymentName,
                    name: "ReasoningAgents.Curator.v1",
                    description: "Curates learning resources and priorities for certification prep (tool-enabled).",
                    instructions: CuratorPrompts.BuildCuratorAgentInstructions(),
                    tools: tools,
                    cancellationToken: ct);
            }

            PersistentAgentThread? thread = null;

            try
            {
                thread = await _client.Threads.CreateThreadAsync(cancellationToken: ct);

                var prompt = CuratorPrompts.BuildCuratorRunPrompt(
                    certificationCode: input.Goal.CertificationCode,
                    performanceJson: input.PerformanceJson);

                await _client.Messages.CreateMessageAsync(thread.Id, MessageRole.User, prompt, cancellationToken: ct);

                ThreadRun run = await _client.Runs.CreateRunAsync(thread.Id, agent.Id, cancellationToken: ct);

                run = await WaitForRunCompletionHandlingToolsAsync(thread.Id, run, ct);

                var last = ReadLastAgentTextForRun(thread.Id, run.Id, ct);
                return last;
            }
            finally
            {
                if (thread is not null)
                {
                    try
                    {
                        await _client.Threads.DeleteThreadAsync(thread.Id, cancellationToken: CancellationToken.None);
                        await _client.Administration.DeleteAgentAsync(agent.Id, cancellationToken: CancellationToken.None);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private async Task<ThreadRun> WaitForRunCompletionHandlingToolsAsync(string threadId, ThreadRun run, CancellationToken ct)
        {
            var started = DateTimeOffset.UtcNow;
            var timeout = TimeSpan.FromMinutes(3);

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                run = await _client.Runs.GetRunAsync(threadId, run.Id, ct);

                if (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
                {
                    if (DateTimeOffset.UtcNow - started > timeout)
                        throw new TimeoutException($"Run timed out. Last status: {run.Status}");

                    await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
                    continue;
                }

                if (run.Status == RunStatus.RequiresAction)
                {
                    if (run.RequiredAction is not SubmitToolOutputsAction submitAction)
                        throw new InvalidOperationException("Run requires an unsupported action type.");

                    var toolOutputs = await ExecuteRequiredToolsAsync(submitAction, ct);

                    run = await _client.Runs.SubmitToolOutputsToRunAsync(
                        run,
                        toolOutputs,
                        cancellationToken: ct);

                    continue;
                }

                if (run.Status == RunStatus.Completed)
                    return run;

                throw new InvalidOperationException(
                    $"Run did not complete successfully. Status: {run.Status}. Error: {run.LastError?.Message}");
            }
        }

        private async Task<List<ToolOutput>> ExecuteRequiredToolsAsync(SubmitToolOutputsAction action, CancellationToken ct)
        {
            var outputs = new List<ToolOutput>();

            foreach (var toolCall in action.ToolCalls)
            {
                if (toolCall is not RequiredFunctionToolCall fn)
                    throw new InvalidOperationException($"Unsupported tool call type: {toolCall.GetType().Name}");

                var resultJson = await DispatchFunctionToolAsync(fn.Name, fn.Arguments, ct);

                outputs.Add(new ToolOutput
                {
                    ToolCallId = fn.Id,
                    Output = resultJson
                });
            }

            return outputs;
        }

        private async Task<string> DispatchFunctionToolAsync(string name, string argumentsJson, CancellationToken ct)
        {
            using var argsDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            var args = argsDoc.RootElement;

            return name switch
            {
                ToolGetCertificationStudyGuide => await Tool_GetCertificationStudyGuideAsync(args, ct),
                ToolExtractAi102Blueprint => Tool_ExtractAi102Blueprint(args),
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {name}" })
            };
        }

        private async Task<string> Tool_GetCertificationStudyGuideAsync(JsonElement args, CancellationToken ct)
        {
            var cert = args.TryGetProperty("certificationCode", out var c) ? (c.GetString() ?? "") : "";
            if (string.IsNullOrWhiteSpace(cert))
                return JsonSerializer.Serialize(new { error = "certificationCode is required" });

            var url = cert.Equals("AI-102", StringComparison.OrdinalIgnoreCase)
                ? "https://learn.microsoft.com/en-us/credentials/certifications/resources/study-guides/ai-102"
                : "";

            if (string.IsNullOrWhiteSpace(url))
                return JsonSerializer.Serialize(new { certificationCode = cert, url = "", excerpt = "" });

            var html = await _http.GetStringAsync(url, ct);
            var excerpt = html.Length > 2000 ? html[..2000] : html;

            return JsonSerializer.Serialize(new { certificationCode = cert, url, excerpt });
        }

        private static string Tool_ExtractAi102Blueprint(JsonElement args)
        {
            var blueprint = new[]
            {
                new { domain = "Plan and manage an Azure AI solution", count = 13 },
                new { domain = "Implement generative AI solutions", count = 10 },
                new { domain = "Implement an agentic solution", count = 5 },
                new { domain = "Implement computer vision solutions", count = 8 },
                new { domain = "Implement natural language processing solutions", count = 7 },
                new { domain = "Implement knowledge mining and information extraction solutions", count = 7 }
            };

            return JsonSerializer.Serialize(new { certificationCode = "AI-102", blueprint });
        }

        private static FunctionToolDefinition BuildGetCertificationStudyGuideTool() =>
            new(
                name: ToolGetCertificationStudyGuide,
                description: "Gets the official Microsoft Learn study guide URL and a short excerpt for a certification (when available).",
                parameters: BinaryData.FromString("""
                {
                  "type": "object",
                  "properties": {
                    "certificationCode": { "type": "string", "description": "Certification code like AI-102" }
                  },
                  "required": ["certificationCode"]
                }
                """)
            );

        private static FunctionToolDefinition BuildExtractAi102BlueprintTool() =>
            new FunctionToolDefinition(
                name: ToolExtractAi102Blueprint,
                description: "Returns the AI-102 domain blueprint distribution (custom 50 question split).",
                parameters: BinaryData.FromString("""
                {
                  "type": "object",
                  "properties": {
                    "certificationCode": { "type": "string" }
                  }
                }
                """)
            );

        private string ReadLastAgentTextForRun(string threadId, string runId, CancellationToken ct)
        {
            Pageable<PersistentThreadMessage> messages = _client.Messages.GetMessages(
                threadId: threadId,
                runId: runId,
                limit: 20,
                order: ListSortOrder.Descending,
                cancellationToken: ct);

            foreach (var msg in messages)
            {
                if (msg.Role != MessageRole.Agent) continue;

                var textSb = new StringBuilder();
                foreach (var content in msg.ContentItems)
                    if (content is MessageTextContent t) textSb.Append(t.Text);

                var text = textSb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            throw new InvalidOperationException("No agent response text was returned.");
        }
    }
}