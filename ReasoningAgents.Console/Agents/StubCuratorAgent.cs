using ReasoningAgents.Core.Agents;
using ReasoningAgents.Core.Models;

namespace ReasoningAgents.Console.Agents
{
    public sealed class StubCuratorAgent : IAgentStep<CertificationGoal, string>
    {
        public Task<string> ExecuteAsync(CertificationGoal input, CancellationToken ct) =>
            Task.FromResult($"Learning path for {input.CertificationCode}: [docs placeholders]");
    }
}
