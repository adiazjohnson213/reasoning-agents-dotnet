using ReasoningAgents.Core.Agents;
using ReasoningAgents.Core.Models;

namespace ReasoningAgents.Core.Orchestration
{
    public sealed class WorkflowRunner
    {
        private readonly IAgentStep<(CertificationGoal Goal, string PerformanceJson), string> _curator;
        private readonly IAgentStep<(CertificationGoal Goal, string LearningPath), string> _planner;
        private readonly IAgentStep<(CertificationGoal Goal, string StudyPlan), string> _assessor;
        private readonly IAgentStep<(CertificationGoal Goal, string Assessment, string UserAnswers), (bool Passed, string Summary)> _critic;

        public WorkflowRunner(
            IAgentStep<(CertificationGoal Goal, string PerformanceJson), string> curator,
            IAgentStep<(CertificationGoal Goal, string LearningPath), string> planner,
            IAgentStep<(CertificationGoal Goal, string StudyPlan), string> assessor,
            IAgentStep<(CertificationGoal Goal, string Assessment, string UserAnswers), (bool Passed, string Summary)> critic)
        {
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

            while (true)
            {
                iterations++;

                // 1) Assess
                var assessment = await _assessor.ExecuteAsync((goal, studyPlan), ct);

                // 2) User answers
                var userAnswers = await getUserAnswersAsync(assessment, ct);

                // 3) Critic
                var (passed, summary) = await _critic.ExecuteAsync((goal, assessment, userAnswers), ct);

                if (passed)
                {
                    return new WorkflowResult(
                        Passed: true,
                        Summary: summary,
                        Iterations: iterations
                    );
                }

                // 4) Curate + Plan ALWAYS on failure (even on last iteration)
                var performanceBlob =
                    $"[PERFORMANCE_FEEDBACK]\n{summary}\n\n" +
                    $"[LAST_ASSESSMENT]\n{assessment}";

                learningPath = await _curator.ExecuteAsync((goal, performanceBlob), ct);
                studyPlan = await _planner.ExecuteAsync((goal, learningPath), ct);

                // 5) Now decide whether we can iterate again
                if (iterations >= maxIterations)
                {
                    return new WorkflowResult(
                        Passed: false,
                        Summary: $"Failed after {iterations} iteration(s). Last evaluation: {summary}",
                        Iterations: iterations
                    );
                }

                // Loop continues with improved studyPlan
            }
        }
    }
}