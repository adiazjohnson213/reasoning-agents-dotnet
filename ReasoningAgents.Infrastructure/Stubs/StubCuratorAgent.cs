using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Infrastructure.Stubs
{
    public sealed class StubCuratorAgent : IAgentStep<CertificationGoal, string>
    {
        public Task<string> ExecuteAsync(CertificationGoal input, CancellationToken ct) =>
            Task.FromResult($"Learning path for {input.CertificationCode}: [docs placeholders]");
    }
}
