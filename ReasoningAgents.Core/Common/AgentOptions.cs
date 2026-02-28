namespace ReasoningAgents.Core.Common
{
    public sealed class AgentOptions
    {
        public string? AssessmentAgentId { get; init; } = null;
        public string? CriticAgentId { get; init; } = null;

        public string ProjectEndpoint { get; init; } = "";
        public string DeploymentName { get; init; } = "";
    }
}
