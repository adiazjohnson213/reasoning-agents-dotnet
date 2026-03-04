using Microsoft.Extensions.Caching.Memory;
using ReasoningAgents.Application.Models;
using ReasoningAgents.Application.Sessions;

namespace ReasoningAgents.Api.Sessions
{
    public sealed class MemoryAssessmentSessionStore : IAssessmentSessionStore
    {
        private readonly IMemoryCache _cache;

        public MemoryAssessmentSessionStore(IMemoryCache cache) => _cache = cache;

        public void Upsert(AssessmentSession session, TimeSpan ttl)
            => _cache.Set(Key(session.SessionId), session, ttl);

        public bool TryGet(string sessionId, out AssessmentSession? session)
            => _cache.TryGetValue(Key(sessionId), out session);

        public void Remove(string sessionId) => _cache.Remove(Key(sessionId));

        private static string Key(string id) => $"assessment-session:{id}";
    }
}
