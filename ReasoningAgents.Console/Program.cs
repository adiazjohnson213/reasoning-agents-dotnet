// See https://aka.ms/new-console-template for more information

using System.Reflection;
using Microsoft.Extensions.Configuration;
using ReasoningAgents.Console.Agents;
using ReasoningAgents.Console.Cli;
using ReasoningAgents.Console.Foundry;
using ReasoningAgents.Core.Agents;
using ReasoningAgents.Core.Common;
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

var config = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .AddEnvironmentVariables()
    .Build();

var agentOptions = config.GetSection("Agent").Get<AgentOptions>();
if (agentOptions is null ||
    string.IsNullOrWhiteSpace(agentOptions.ProjectEndpoint) ||
    string.IsNullOrWhiteSpace(agentOptions.DeploymentName))
{
    Console.WriteLine(
        "Error: Missing Agent configuration. Set User Secrets (Agent:ProjectEndpoint, Agent:DeploymentName)");
    Environment.ExitCode = 1;
    return;
}


var opt = parse.Options!;
var goal = new CertificationGoal(opt.Cert, opt.Days, opt.Minutes);

var persistenClient = new FoundryClientFactory().Create(agentOptions);

var domainAssessor = new FoundryAssessmentAgent(agentOptions, persistenClient);

IAgentStep<CertificationGoal, string> curator = new StubCuratorAgent();
IAgentStep<(CertificationGoal, string), string> planner = new StubPlannerAgent();
IAgentStep<(CertificationGoal, string), string> assessor = !opt.IsExamMode
                                                                ? domainAssessor
                                                                : new FoundryExamAssessmentAgent(domainAssessor);
IAgentStep<(CertificationGoal, string, string), (bool, string)> critic = new FoundryCriticAgent(agentOptions, persistenClient);

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
    maxIterations: 1,
    ct: CancellationToken.None
);

Console.WriteLine($"\nPASSED: {result.Passed}");
Console.WriteLine($"ITERATIONS: {result.Iterations}");
Console.WriteLine($"SUMMARY:\n{result.Summary}");
