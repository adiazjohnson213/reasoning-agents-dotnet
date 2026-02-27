// See https://aka.ms/new-console-template for more information

using ReasoningAgents.Console.Cli;
using ReasoningAgents.Core.Agents;
using ReasoningAgents.Core.Models;
using ReasoningAgents.Core.Orchestration;

var parse = CliOptionsParser.Parse(args);

if (!parse.Success)
{
    if (!string.IsNullOrWhiteSpace(parse.Error))
        Console.WriteLine($"Error: {parse.Error}\n");

    if (parse.ShowHelp)
        CliHelpPrinter.Print();

    Environment.ExitCode = string.IsNullOrWhiteSpace(parse.Error) ? 0 : 1;
    return;
}

var opt = parse.Options!;
var goal = new CertificationGoal(opt.Cert, opt.Days, opt.Minutes);

// Temporary in-memory agent implementations (MVP).
IAgentStep<CertificationGoal, string> curator = new StubCuratorAgent();
IAgentStep<(CertificationGoal, string), string> planner = new StubPlannerAgent();
IAgentStep<(CertificationGoal, string), string> assessor = new StubAssessmentAgent();
IAgentStep<(CertificationGoal, string, string), (bool, string)> critic = new StubCriticAgent();

var runner = new WorkflowRunner(curator, planner, assessor, critic);

Console.WriteLine("Generating assessment...\n");

var result = await runner.RunAsync(
    goal,
    getUserAnswersAsync: async (assessment, ct) =>
    {
        Console.WriteLine("=== ASSESSMENT ===");
        Console.WriteLine(assessment);
        Console.WriteLine("\nPaste your answers and press Enter:");
        return await Task.Run(() => Console.ReadLine() ?? string.Empty, ct);
    },
    maxIterations: 2,
    ct: CancellationToken.None
);

Console.WriteLine($"\nPASSED: {result.Passed}");
Console.WriteLine($"ITERATIONS: {result.Iterations}");
Console.WriteLine($"SUMMARY:\n{result.Summary}");

internal sealed class StubCuratorAgent : IAgentStep<CertificationGoal, string>
{
    public Task<string> ExecuteAsync(CertificationGoal input, CancellationToken ct) =>
        Task.FromResult($"Learning path for {input.CertificationCode}: [docs placeholders]");
}

internal sealed class StubPlannerAgent : IAgentStep<(CertificationGoal Goal, string LearningPath), string>
{
    public Task<string> ExecuteAsync((CertificationGoal Goal, string LearningPath) input, CancellationToken ct) =>
        Task.FromResult($"Study plan: {input.Goal.DaysAvailable} days x {input.Goal.DailyMinutes} min/day.");
}

internal sealed class StubAssessmentAgent : IAgentStep<(CertificationGoal Goal, string StudyPlan), string>
{
    public Task<string> ExecuteAsync((CertificationGoal Goal, string StudyPlan) input, CancellationToken ct) =>
        Task.FromResult("Q1) Explain retrieval vs grounding.\nQ2) When to use a Critic agent?\n");
}

internal sealed class StubCriticAgent : IAgentStep<(CertificationGoal Goal, string Assessment, string UserAnswers), (bool Passed, string Summary)>
{
    public Task<(bool Passed, string Summary)> ExecuteAsync((CertificationGoal Goal, string Assessment, string UserAnswers) input, CancellationToken ct)
    {
        var passed = !string.IsNullOrWhiteSpace(input.UserAnswers) && input.UserAnswers.Length > 20;
        var summary = passed ? "Good enough for MVP. Next: real rubric + real model calls."
                             : "Answers too short. Provide more detail and examples.";
        return Task.FromResult((passed, summary));
    }
}
