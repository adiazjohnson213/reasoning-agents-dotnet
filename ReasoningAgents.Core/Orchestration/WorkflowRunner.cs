using ReasoningAgents.Core.Agents;
using ReasoningAgents.Core.Models;

namespace ReasoningAgents.Core.Orchestration
{
    public sealed class WorkflowRunner
    {
        private readonly IAgentStep<CertificationGoal, string> _curator;
        private readonly IAgentStep<(CertificationGoal Goal, string LearningPath), string> _planner;
        private readonly IAgentStep<(CertificationGoal Goal, string StudyPlan), string> _assessor;
        private readonly IAgentStep<(CertificationGoal Goal, string Assessment, string UserAnswers), (bool Passed, string Summary)> _critic;

        public WorkflowRunner(IAgentStep<CertificationGoal, string> curator,
                              IAgentStep<(CertificationGoal, string), string> planner,
                              IAgentStep<(CertificationGoal, string), string> assessor,
                              IAgentStep<(CertificationGoal, string, string), (bool, string)> critic)
        {
            _curator = curator;
            _planner = planner;
            _assessor = assessor;
            _critic = critic;
        }

        public async Task<WorkflowResult> RunAsync(CertificationGoal goal,
                                                   Func<string, CancellationToken, Task<string>> getUserAnswersAsync,
                                                   int maxIterations,
                                                   CancellationToken ct)
        {
            var iterations = 0;

            // 1) Curate
            var learningPath = await _curator.ExecuteAsync(goal, ct);

            // 2) Plan
            var studyPlan = await _planner.ExecuteAsync((goal, learningPath), ct);

            while (true)
            {
                iterations++;

                // 3) Assess
                var assessment = await _assessor.ExecuteAsync((goal, studyPlan), ct);

                // 4) Human-in-the-loop: user answers
                var userAnswers = await getUserAnswersAsync(assessment, ct);

                // 5) Critic (PASS/RETRY)
                var (passed, summary) = await _critic.ExecuteAsync((goal, assessment, userAnswers), ct);

                if (passed)
                {
                    return new WorkflowResult(
                        Passed: true,
                        Summary: summary,
                        Iterations: iterations
                    );
                }

                if (iterations >= maxIterations)
                {
                    return new WorkflowResult(
                        Passed: false,
                        Summary: $"Failed after {iterations} iteration(s). Last evaluation: {summary}",
                        Iterations: iterations
                    );
                }

                // 6) Iterate: refine the plan based on critique (simple MVP behavior)
                // Later: replace this with a dedicated "PlanRefinerAgent".
                studyPlan = $"{studyPlan}\n\n[REFINEMENT]\nFocus more on the weak areas identified:\n{summary}";
            }
        }
    }
}
