namespace ReasoningAgents.Infrastructure.Configuration
{
    public sealed class AgentOptions
    {
        public string? AssessmentAgentId { get; init; } = null;
        public string? CriticAgentId { get; init; } = null;
        public string? CuratorAgentId { get; init; } = null;
        public string? PreflightAgentId { get; init; } = null;
        public string? PlannerAgentId { get; init; } = null;

        public string ProjectEndpoint { get; init; } = "";
        public string DeploymentName { get; init; } = "";
    }
}
