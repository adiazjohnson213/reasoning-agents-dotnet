using ReasoningAgents.Application.Models;

namespace ReasoningAgents.Application.Sessions
{
    public interface IAssessmentSessionStore
    {
        void Upsert(AssessmentSession session, TimeSpan ttl);
        bool TryGet(string sessionId, out AssessmentSession? session);
        void Remove(string sessionId);
    }
}
