namespace ReasoningAgents.Core.Agents
{
    public interface IAgentStep<TIn, TOut>
    {
        Task<TOut> ExecuteAsync(TIn input, CancellationToken ct);
    }
}
