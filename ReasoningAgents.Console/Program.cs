// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using ReasoningAgents.Application.Orchestration;
using ReasoningAgents.Console.Cli;
using ReasoningAgents.Console.Output;
using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Inputs;
using ReasoningAgents.Domain.Models;
using ReasoningAgents.Infrastructure.Configuration;
using ReasoningAgents.Infrastructure.Foundry;

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

var handler = new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2)
};

var http = new HttpClient(handler, disposeHandler: true);


var opt = parse.Options!;
var goal = new CertificationGoal(opt.Cert, opt.Days, opt.Minutes);

var persistenClient = new FoundryClientFactory().Create(agentOptions);

var domainAssessor = new FoundryAssessmentAgent(agentOptions, persistenClient);

IAgentStep<CertificationGoal, string> preflight = new FoundryAssessmentPreflightAgent(agentOptions, persistenClient);
IAgentStep<CuratorInput, string> curator = new FoundryCuratorAgent(agentOptions, persistenClient, http);
IAgentStep<PlannerInput, string> planner = new FoundryPlannerAgent(agentOptions, persistenClient);
IAgentStep<AssessmentInput, string> assessor = !opt.IsExamMode ? domainAssessor
                                                                : new FoundryExamAssessmentAgent(domainAssessor);
IAgentStep<CriticInput, CriticEvaluation> critic = new FoundryCriticAgent(agentOptions, persistenClient);

var runner = new WorkflowRunner(preflight, curator, planner, assessor, critic);

Console.WriteLine("Generating assessment...\n");

var result = await runner.RunAsync(
    goal,
    getUserAnswersAsync: async (assessment, ct) =>
    {
        Console.WriteLine("=== ASSESSMENT ===");
        Console.WriteLine(assessment);
        Console.WriteLine();
        Console.WriteLine("Enter your answers in this exact format (one per line), then press Enter on an empty line:");
        Console.WriteLine("Q1=A");
        Console.WriteLine("Q2=B");
        Console.WriteLine("Q3=C");
        Console.WriteLine("Q4=D");
        Console.WriteLine();

        var sb = new StringBuilder();
        while (true)
        {
            var line = await Task.Run(() => Console.ReadLine(), ct) ?? "";
            if (string.IsNullOrWhiteSpace(line)) break;
            sb.AppendLine(line.Trim());
        }

        return sb.ToString().Trim();
    },
    maxIterations: 1,
    ct: CancellationToken.None
);

Console.WriteLine($"\nPASSED: {result.Passed}");
Console.WriteLine($"ITERATIONS: {result.Iterations}");
Console.WriteLine();

SummaryRender.PrettyPrintSummary(result);

if (!string.IsNullOrWhiteSpace(result.StudyPlan))
{
    Console.WriteLine("\n=== STUDY PLAN ===");
    StudyPlanRender.PrintStudyPlan(result.StudyPlan);
}

if (!string.IsNullOrWhiteSpace(result.LearningPath))
{
    Console.WriteLine("\n=== LEARNING PATH (RAW) ===");
    LearningPathRender.PrintLearningPath(result.LearningPath);
}