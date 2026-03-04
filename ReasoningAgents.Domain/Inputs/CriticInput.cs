using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Domain.Inputs
{
    public sealed record CriticInput(CertificationGoal Goal, string Assessment, string UserAnswers);
}
