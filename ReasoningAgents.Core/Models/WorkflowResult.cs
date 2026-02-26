namespace ReasoningAgents.Core.Models
{
    public sealed record WorkflowResult(
        bool Passed,
        string Summary,
        int Iterations
    );
}
