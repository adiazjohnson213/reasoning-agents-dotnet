using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Infrastructure.Stubs
{
    public sealed class StubCriticAgent : IAgentStep<(CertificationGoal Goal, string Assessment, string UserAnswers), (bool Passed, string Summary)>
    {
        public Task<(bool Passed, string Summary)> ExecuteAsync((CertificationGoal Goal, string Assessment, string UserAnswers) input, CancellationToken ct)
        {
            var passed = !string.IsNullOrWhiteSpace(input.UserAnswers) && input.UserAnswers.Length > 20;
            var summary = passed ? "Good enough for MVP. Next: real rubric + real model calls."
                                 : "Answers too short. Provide more detail and examples.";
            return Task.FromResult((passed, summary));
        }
    }
}
