// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Text;
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
IAgentStep<(CertificationGoal, string), string> curator = new FoundryCuratorAgent(agentOptions, persistenClient, http);
IAgentStep<(CertificationGoal, string), string> planner = new StubPlannerAgent();
IAgentStep<(CertificationGoal, string), string> assessor = !opt.IsExamMode
                                                                ? domainAssessor
                                                                : new FoundryExamAssessmentAgent(domainAssessor);
IAgentStep<(CertificationGoal, string, string), (bool, string)> critic = new FoundryCriticAgent(agentOptions, persistenClient);

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

PrettyPrintSummary(result.Summary);

static void PrettyPrintSummary(string summary)
{
    // Separaciones básicas por etiquetas conocidas.
    // Esto es un "formatter" MVP: no asume JSON, solo texto.
    var scorePart = summary;
    var issuesPart = "";
    var improvementsPart = "";

    // 1) Split Issues
    var issuesIndex = summary.IndexOf("Issues:", StringComparison.OrdinalIgnoreCase);
    if (issuesIndex >= 0)
    {
        scorePart = summary[..issuesIndex].Trim();
        var rest = summary[(issuesIndex + "Issues:".Length)..].Trim();

        // 2) Split Improvements
        var improvementsIndex = rest.IndexOf("Improvements:", StringComparison.OrdinalIgnoreCase);
        if (improvementsIndex >= 0)
        {
            issuesPart = rest[..improvementsIndex].Trim().TrimEnd('.');
            improvementsPart = rest[(improvementsIndex + "Improvements:".Length)..].Trim().TrimEnd('.');
        }
        else
        {
            issuesPart = rest.Trim().TrimEnd('.');
        }
    }

    Console.WriteLine("=== RESULTADO ===");
    Console.WriteLine(scorePart);
    Console.WriteLine();

    if (!string.IsNullOrWhiteSpace(issuesPart))
    {
        Console.WriteLine("=== ISSUES (qué estuvo mal) ===");
        foreach (var (item, i) in issuesPart.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                             .Select((x, i) => (x.Trim().TrimEnd('.'), i + 1)))
        {
            Console.WriteLine($"{i}. {item}");
        }
        Console.WriteLine();
    }

    if (!string.IsNullOrWhiteSpace(improvementsPart))
    {
        Console.WriteLine("=== IMPROVEMENTS (qué hacer ahora) ===");
        foreach (var (item, i) in improvementsPart.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                                   .Select((x, i) => (x.Trim().TrimEnd('.'), i + 1)))
        {
            Console.WriteLine($"{i}. {item}");
        }
        Console.WriteLine();
    }
}
