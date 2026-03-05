using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Domain.Inputs
{
    public sealed record PlannerInput(CertificationGoal Goal, string PerformanceJson, string LearningPath);
}
