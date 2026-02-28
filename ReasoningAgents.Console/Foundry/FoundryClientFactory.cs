using Azure.AI.Agents.Persistent;
using Azure.Identity;
using ReasoningAgents.Core.Common;

namespace ReasoningAgents.Console.Foundry
{
    public sealed class FoundryClientFactory
    {
        public PersistentAgentsClient Create(AgentOptions options)
        => new PersistentAgentsClient(options.ProjectEndpoint, new DefaultAzureCredential());
    }
}
