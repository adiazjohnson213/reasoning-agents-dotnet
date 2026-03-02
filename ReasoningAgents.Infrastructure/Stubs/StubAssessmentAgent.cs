using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Infrastructure.Stubs
{
    public sealed class StubAssessmentAgent : IAgentStep<(CertificationGoal Goal, string StudyPlan), string>
    {
        public Task<string> ExecuteAsync((CertificationGoal Goal, string StudyPlan) input, CancellationToken ct) =>
            Task.FromResult("Q1) Explain retrieval vs grounding.\nQ2) When to use a Critic agent?\n");
    }
}
