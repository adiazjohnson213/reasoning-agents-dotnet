namespace ReasoningAgents.Api.Contracts.Responses
{

    public sealed record SubmitAnswersResponse(
        bool Passed,
        int Score,
        string Summary,
        string[] Issues,
        string[] Improvements,
        string? LearningPathJson,
        string? StudyPlanJson
    );
}
