namespace ReasoningAgents.Domain.Models
{
    public sealed record CriticEvaluation(
        bool Passed,
        int Score,
        string Summary,
        IReadOnlyList<string> Issues,
        IReadOnlyList<string> Improvements,
        string? Domain = null
    );
}
