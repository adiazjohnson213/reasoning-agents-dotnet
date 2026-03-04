using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Application.Models
{
    public sealed record AssessmentSession(
        string SessionId,
        CertificationGoal Goal,
        bool ExamMode,
        string GuardrailsJsonSlim,
        string AssessmentText,
        DateTimeOffset CreatedUtc
    );
}
