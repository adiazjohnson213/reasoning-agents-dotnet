using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Inputs;
using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Application.Orchestration
{
    public sealed class WorkflowRunner
    {
        private readonly IAgentStep<CertificationGoal, string> _preflight;
        private readonly IAgentStep<CuratorInput, string> _curator;
        private readonly IAgentStep<PlannerInput, string> _planner;
        private readonly IAgentStep<AssessmentInput, string> _assessor;
        private readonly IAgentStep<CriticInput, (bool Passed, string Summary)> _critic;

        public WorkflowRunner(
            IAgentStep<CertificationGoal, string> preflight,
            IAgentStep<CuratorInput, string> curator,
            IAgentStep<PlannerInput, string> planner,
            IAgentStep<AssessmentInput, string> assessor,
            IAgentStep<CriticInput, (bool Passed, string Summary)> critic)
        {
            _preflight = preflight;
            _curator = curator;
            _planner = planner;
            _assessor = assessor;
            _critic = critic;
        }

        public async Task<WorkflowResult> RunAsync(
            CertificationGoal goal,
            Func<string, CancellationToken, Task<string>> getUserAnswersAsync,
            int maxIterations,
            CancellationToken ct)
        {
            var iterations = 0;

            string learningPath = "";
            string studyPlan = "";

            var guardrailsJson = await _preflight.ExecuteAsync(goal, ct);

            while (true)
            {
                iterations++;

                var studyPlanWithGuardrails =
                            $"[ASSESSMENT_GUARDRAILS_JSON]\n{guardrailsJson}\n\n{studyPlan}";
                // 1) Assess
                var assessment = await _assessor.ExecuteAsync(new(goal, studyPlanWithGuardrails), ct);

                // 2) User answers
                var userAnswers = await getUserAnswersAsync(assessment, ct);

                // 3) Critic
                var (passed, summary) = await _critic.ExecuteAsync(new(goal, assessment, userAnswers), ct);

                if (passed)
                {
                    return new WorkflowResult(
                        Passed: true,
                        Summary: summary,
                        Iterations: iterations,
                        LearningPath: learningPath,
                        StudyPlan: studyPlan
                    );
                }

                // 4) Curate + Plan ALWAYS on failure (even on last iteration)
                var performanceBlob =
                    $"[PERFORMANCE_FEEDBACK]\n{summary}\n\n" +
                    $"[LAST_ASSESSMENT]\n{assessment}";

                learningPath = await _curator.ExecuteAsync(new(goal, performanceBlob), ct);
                studyPlan = await _planner.ExecuteAsync(new(goal, learningPath), ct);

                // 5) Now decide whether we can iterate again
                if (iterations >= maxIterations)
                {
                    return new WorkflowResult(
                        Passed: false,
                        Summary: $"Failed after {iterations} iteration(s). Last evaluation: {summary}",
                        Iterations: iterations,
                        LearningPath: learningPath,
                        StudyPlan: studyPlan
                    );
                }

                // Loop continues with improved studyPlan
            }
        }
    }
}