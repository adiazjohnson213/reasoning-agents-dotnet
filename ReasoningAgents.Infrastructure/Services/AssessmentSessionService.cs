using System.Security.Cryptography;
using System.Text.Json;
using ReasoningAgents.Application.Models;
using ReasoningAgents.Application.Services;
using ReasoningAgents.Application.Sessions;
using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Inputs;
using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Infrastructure.Services
{
    public sealed class AssessmentSessionService : IAssessmentSessionService
    {
        private readonly IAssessmentSessionStore _store;

        private readonly IAgentStep<CertificationGoal, string> _preflight;
        private readonly IAgentStep<AssessmentInput, string> _assessor;
        private readonly IAgentStep<CriticInput, CriticEvaluation> _critic;
        private readonly IAgentStep<CuratorInput, string> _curator;
        private readonly IAgentStep<PlannerInput, string> _planner;

        public AssessmentSessionService(
            IAssessmentSessionStore store,
            IAgentStep<CertificationGoal, string> preflight,
            IAgentStep<AssessmentInput, string> assessor,
            IAgentStep<CriticInput, CriticEvaluation> critic,
            IAgentStep<CuratorInput, string> curator,
            IAgentStep<PlannerInput, string> planner)
        {
            _store = store;
            _preflight = preflight;
            _assessor = assessor;
            _critic = critic;
            _curator = curator;
            _planner = planner;
        }

        public async Task<CreateSessionResult> CreateSessionAsync(CertificationGoal goal, bool examMode, CancellationToken ct)
        {
            // 1) Preflight -> guardrails JSON
            var preflightJson = await _preflight.ExecuteAsync(goal, ct);
            var guardrailsSlim = SlimGuardrails(preflightJson);

            // 2) Assessment (baseline plan = guardrails)
            var studyPlan = $"[ASSESSMENT_GUARDRAILS_JSON]\n{guardrailsSlim}";
            var assessment = await _assessor.ExecuteAsync(new(goal, studyPlan), ct);

            // 3) Persist session
            var sessionId = NewSessionId();
            var session = new AssessmentSession(
                SessionId: sessionId,
                Goal: goal,
                ExamMode: examMode,
                GuardrailsJsonSlim: guardrailsSlim,
                AssessmentText: assessment,
                CreatedUtc: DateTimeOffset.UtcNow
            );

            _store.Upsert(session, ttl: TimeSpan.FromHours(2));

            return new CreateSessionResult(sessionId, assessment);
        }

        public async Task<SubmitAnswersResult> SubmitAnswersAsync(string sessionId, string answers, CancellationToken ct)
        {
            if (!_store.TryGet(sessionId, out var session) || session is null)
                throw new InvalidOperationException("Session not found or expired.");

            // 1) Critic
            var evaluation = await _critic.ExecuteAsync(new(session.Goal, session.AssessmentText, answers), ct);

            // 2) Curate + Plan (always produce next steps)
            var performanceBlob =
                $"[PERFORMANCE_FEEDBACK]\n{evaluation.Summary}\n\n" +
                $"[LAST_ASSESSMENT]\n{session.AssessmentText}";

            var learningPathJson = await _curator.ExecuteAsync(new(session.Goal, performanceBlob), ct);
            var studyPlanJson = await _planner.ExecuteAsync(new(session.Goal, learningPathJson), ct);

            // Optional: keep session for next iteration or remove it once answered
            // _store.Remove(sessionId);

            return new SubmitAnswersResult(
                    Passed: evaluation.Passed,
                    Score: evaluation.Score,
                    Summary: evaluation.Summary,
                    Issues: evaluation.Issues,
                    Improvements: evaluation.Improvements,
                    LearningPathJson: learningPathJson,
                    StudyPlanJson: studyPlanJson
                );
        }

        private static string NewSessionId()
        {
            Span<byte> bytes = stackalloc byte[16];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string SlimGuardrails(string preflightJson)
        {
            // Keep only naming/avoid lists (no dates/notes) to prevent migration-trivia leakage.
            using var doc = JsonDocument.Parse(preflightJson);
            var root = doc.RootElement;

            object? preferredNames = null;
            if (root.TryGetProperty("preferredNames", out var pref)) preferredNames = JsonSerializer.Deserialize<object>(pref);

            var slim = new
            {
                certification = root.TryGetProperty("certification", out var cert) ? cert.GetString() : "",
                avoidAsCorrectAnswer = root.TryGetProperty("avoidAsCorrectAnswer", out var avoid)
                    ? avoid.Deserialize<string[]>() : Array.Empty<string>(),
                preferredNames,
                allowedServices = root.TryGetProperty("allowedServices", out var allowed)
                    ? allowed.Deserialize<string[]>() : Array.Empty<string>()
            };

            return JsonSerializer.Serialize(slim);
        }
    }
}
