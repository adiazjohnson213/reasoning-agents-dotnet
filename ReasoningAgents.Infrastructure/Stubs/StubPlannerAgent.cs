using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Infrastructure.Stubs
{
    public sealed class StubPlannerAgent : IAgentStep<(CertificationGoal Goal, string LearningPath), string>
    {
        public Task<string> ExecuteAsync((CertificationGoal Goal, string LearningPath) input, CancellationToken ct) =>
            Task.FromResult($"Study plan: {input.Goal.DaysAvailable} days x {input.Goal.DailyMinutes} min/day.");
    }
}
