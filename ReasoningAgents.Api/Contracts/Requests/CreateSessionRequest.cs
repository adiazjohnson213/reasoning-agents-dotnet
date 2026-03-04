namespace ReasoningAgents.Api.Contracts.Requests
{
    public sealed record CreateSessionRequest(string Cert, int Days, int Minutes, bool Exam);
}
