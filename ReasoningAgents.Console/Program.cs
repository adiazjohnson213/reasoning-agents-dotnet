// See https://aka.ms/new-console-template for more information

using ReasoningAgents.Console.Agents;
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
