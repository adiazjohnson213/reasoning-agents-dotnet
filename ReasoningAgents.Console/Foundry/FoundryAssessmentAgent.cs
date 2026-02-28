using System.Text;
using Azure;
using Azure.AI.Agents.Persistent;
using ReasoningAgents.Console.Foundry.Prompts;
using ReasoningAgents.Core.Agents;
using ReasoningAgents.Core.Common;
using ReasoningAgents.Core.Models;

namespace ReasoningAgents.Console.Foundry
{
    public sealed class FoundryAssessmentAgent : IAgentStep<(CertificationGoal Goal, string StudyPlan), string>
    {
        private readonly AgentOptions _options;
        private readonly PersistentAgentsClient _client;

        public FoundryAssessmentAgent(AgentOptions options, PersistentAgentsClient client)
        {
            _options = options;
            _client = client;
        }

        public async Task<string> ExecuteAsync((CertificationGoal Goal, string StudyPlan) input, CancellationToken ct)
        {
            return await GenerateDomainAsync(input.Goal, input.StudyPlan, input.Goal.CertificationCode, 4, ct);
        }

        public async Task<string> GenerateDomainAsync(CertificationGoal goal,
                                                string studyPlan,
                                                string domain,
                                                int count,
                                                CancellationToken ct)
        {
            PersistentAgent agent;
            if (!string.IsNullOrWhiteSpace(_options.AssessmentAgentId))
            {
                agent = await _client.Administration.GetAgentAsync(_options.AssessmentAgentId, ct);
            }
            else
            {
                agent = await _client.Administration.CreateAgentAsync(model: _options.DeploymentName,
                                                                      name: "ReasoningAgents.Assessment.v1",
                                                                      description: "Generates multiple-choice certification practice questions (single correct answer) and returns them in a strict, parseable format.",
                                                                      instructions: AssessmentPrompts.BuildAssessmentAgentInstructions(),
                                                                      cancellationToken: ct);
            }

            PersistentAgentThread thread = await _client.Threads.CreateThreadAsync(cancellationToken: ct);

            var prompt = AssessmentPrompts.BuildAssessmentRunPrompt(goal.CertificationCode,
                                                                    studyPlan,
                                                                    domain,
                                                                    count);

            await _client.Messages.CreateMessageAsync(thread.Id, MessageRole.User, prompt, cancellationToken: ct);

            ThreadRun run = await _client.Runs.CreateRunAsync(thread.Id, agent.Id, cancellationToken: ct);

            var started = DateTimeOffset.UtcNow;
            var timeout = TimeSpan.FromMinutes(2);

            while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress || run.Status == RunStatus.RequiresAction)
            {
                ct.ThrowIfCancellationRequested();

                if (DateTimeOffset.UtcNow - started > timeout)
                    throw new TimeoutException($"Run timed out. Last status: {run.Status}");

                await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                run = await _client.Runs.GetRunAsync(thread.Id, run.Id, ct);
            }

            if (run.Status != RunStatus.Completed)
                throw new InvalidOperationException($"Run did not complete successfully. Status: {run.Status}");

            Pageable<PersistentThreadMessage> messages = _client.Messages.GetMessages(threadId: thread.Id, order: ListSortOrder.Ascending, cancellationToken: ct);

            string? last = null;
            foreach (var msg in messages)
            {
                if (msg.Role != MessageRole.Agent) continue;

                var textSb = new StringBuilder();
                foreach (var content in msg.ContentItems)
                    if (content is MessageTextContent t) textSb.Append(t.Text);

                var text = textSb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(text)) last = text;
            }

            return last ?? throw new InvalidOperationException("No agent response text was returned.");
        }
    }
}
