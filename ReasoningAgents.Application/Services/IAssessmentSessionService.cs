using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Application.Services
{
    public interface IAssessmentSessionService
    {
        Task<CreateSessionResult> CreateSessionAsync(CertificationGoal goal, bool examMode, CancellationToken ct);

        Task<SubmitAnswersResult> SubmitAnswersAsync(string sessionId, string answers, CancellationToken ct);
    }

    public sealed record CreateSessionResult(
        string SessionId,
        string AssessmentText
    );

    public sealed record SubmitAnswersResult(
        bool Passed,
        int Score,
        string Summary,
        IReadOnlyList<string> Issues,
        IReadOnlyList<string> Improvements,
        string? LearningPathJson,
        string? StudyPlanJson
    );
}
