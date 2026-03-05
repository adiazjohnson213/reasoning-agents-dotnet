using System.Text;
using Azure;
using Azure.AI.Agents.Persistent;
using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Models;
using ReasoningAgents.Infrastructure.Configuration;
using ReasoningAgents.Infrastructure.Foundry.Prompts;

namespace ReasoningAgents.Infrastructure.Foundry
{
    public sealed class FoundryAssessmentPreflightAgent : IAgentStep<CertificationGoal, string>
    {
        private readonly AgentOptions _options;
        private readonly PersistentAgentsClient _client;

        // MCP config
        private const string McpServerLabel = "microsoft_learn";
        private const string McpServerUrl = "https://learn.microsoft.com/api/mcp";

        public FoundryAssessmentPreflightAgent(AgentOptions options, PersistentAgentsClient client)
        {
            _options = options;
            _client = client;
        }

        public async Task<string> ExecuteAsync(CertificationGoal input, CancellationToken ct)
        {
            MCPToolDefinition mcpTool = new(McpServerLabel, McpServerUrl);
            mcpTool.AllowedTools.Add("microsoft_docs_search");
            mcpTool.AllowedTools.Add("microsoft_docs_fetch");

            PersistentAgent agent;
            if (!string.IsNullOrWhiteSpace(_options.PreflightAgentId))
            {
                agent = await _client.Administration.GetAgentAsync(_options.PreflightAgentId, ct);
            }
            else
            {
                agent = await _client.Administration.CreateAgentAsync(model: "gpt-4.1-mini",
                                                                      name: "ReasoningAgents.AssessmentPreflight.v1",
                                                                      description: "Fetches up-to-date Microsoft Learn grounding to build assessment guardrails.",
                                                                      instructions: PreflightPrompts.BuildPreflightAgentInstructions(),
                                                                      tools: [mcpTool],
                                                                      cancellationToken: ct);
            }
            PersistentAgentThread? thread = null;

            try
            {
                thread = await _client.Threads.CreateThreadAsync(cancellationToken: ct);

                var prompt = PreflightPrompts.BuildPreflightRunPrompt(input.CertificationCode);
                await _client.Messages.CreateMessageAsync(thread.Id, MessageRole.User, prompt, cancellationToken: ct);

                MCPToolResource mcpToolResource = new(McpServerLabel);
                ToolResources toolResources = mcpToolResource.ToToolResources();

                ThreadRun run = await _client.Runs.CreateRunAsync(thread, agent, toolResources, cancellationToken: ct);

                var started = DateTimeOffset.UtcNow;
                var timeout = TimeSpan.FromMinutes(2);

                while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress || run.Status == RunStatus.RequiresAction)
                {
                    ct.ThrowIfCancellationRequested();

                    if (DateTimeOffset.UtcNow - started > timeout)
                        throw new TimeoutException($"Preflight run timed out. Last status: {run.Status}");

                    await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                    run = await _client.Runs.GetRunAsync(thread.Id, run.Id, ct);

                    if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolApprovalAction toolApprovalAction)
                    {
                        var approvals = new List<ToolApproval>();

                        foreach (var toolCall in toolApprovalAction.SubmitToolApproval.ToolCalls)
                        {
                            if (toolCall is RequiredMcpToolCall mcpToolCall)
                            {
                                approvals.Add(new ToolApproval(mcpToolCall.Id, approve: true));
                            }
                        }

                        if (approvals.Count > 0)
                        {
                            run = await _client.Runs.SubmitToolOutputsToRunAsync(
                                thread.Id,
                                run.Id,
                                toolApprovals: approvals,
                                cancellationToken: ct);
                        }
                    }
                }

                if (run.Status != RunStatus.Completed)
                    throw new InvalidOperationException($"Preflight run did not complete successfully. Status: {run.Status}. Error: {run.LastError?.Message}");

                return ReadLastAgentTextForRun(thread.Id, run.Id, ct);
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

            throw new InvalidOperationException("No preflight agent response text was returned.");
        }
    }
}