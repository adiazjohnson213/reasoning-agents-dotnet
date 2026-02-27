namespace ReasoningAgents.Console.Cli
{
    public sealed record CliParseResult(
        bool Success,
        CliOptions? Options,
        bool ShowHelp,
        string? Error
    );
}
