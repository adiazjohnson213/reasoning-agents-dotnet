using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Domain.Inputs
{
    public sealed record AssessmentInput(CertificationGoal Goal, string StudyPlan);
}
