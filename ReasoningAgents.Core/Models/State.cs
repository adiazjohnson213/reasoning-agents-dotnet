namespace ReasoningAgents.Core.Models
{
    public sealed record State(
        CertificationGoal Goal,
        string? LearningPath,
        string? StudyPlan,
        string? Assessment,
        string? UserAnswers,
        string? EvaluationSummary,
        bool Passed
    );
}
