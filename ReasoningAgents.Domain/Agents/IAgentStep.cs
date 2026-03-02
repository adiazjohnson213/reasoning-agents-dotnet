namespace ReasoningAgents.Domain.Agents
{
    public interface IAgentStep<TIn, TOut>
    {
        Task<TOut> ExecuteAsync(TIn input, CancellationToken ct);
    }
}
