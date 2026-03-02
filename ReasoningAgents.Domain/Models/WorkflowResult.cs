namespace ReasoningAgents.Domain.Models
{
    public sealed record WorkflowResult(
        bool Passed,
        string Summary,
        int Iterations,
        string? LearningPath = null,
        string? StudyPlan = null
    );
}
