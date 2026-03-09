namespace ReasoningAgents.Api.Contracts.Responses
{
    public sealed record CreateSessionResponse(
        string SessionId,
        DateTimeOffset CreatedUtc,
        IReadOnlyList<QuestionDto> Questions
    );

    public sealed record QuestionDto(
        int Number,
        string Prompt,
        IReadOnlyList<OptionDto> Options,
        int? SelectCount,
        string QuestionType
    );

    public sealed record OptionDto(
        string Key,
        string Text
    );
}
