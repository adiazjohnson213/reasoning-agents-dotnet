using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Domain.Inputs
{
    public sealed record CuratorInput(CertificationGoal Goal, string PerformanceJson);
}
