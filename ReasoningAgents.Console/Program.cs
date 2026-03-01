// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
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
IAgentStep<(CertificationGoal, string), string> planner = new FoundryPlannerAgent(agentOptions, persistenClient);
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
Console.WriteLine();

if (!string.IsNullOrWhiteSpace(result.StudyPlan))
{
    Console.WriteLine("\n=== STUDY PLAN ===");
    PrintStudyPlan(result.StudyPlan);
}

if (!string.IsNullOrWhiteSpace(result.LearningPath))
{
    Console.WriteLine("\n=== LEARNING PATH (RAW) ===");
    PrintLearningPath(result.LearningPath);
}

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

static void PrintStudyPlan(string studyPlanJson)
{
    try
    {
        using var doc = JsonDocument.Parse(studyPlanJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("days", out var days) || days.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine(studyPlanJson);
            return;
        }

        foreach (var day in days.EnumerateArray())
        {
            var dayNumber = GetInt(day, "day");
            var total = GetInt(day, "totalMinutes");

            Console.WriteLine($"\n--- Day {dayNumber} ({total} min) ---");

            if (!day.TryGetProperty("sessions", out var sessions) || sessions.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine("  (No sessions)");
                continue;
            }

            foreach (var s in sessions.EnumerateArray())
            {
                var title = GetString(s, "title");
                var minutes = GetInt(s, "minutes");
                var type = GetString(s, "type");
                var why = GetString(s, "why");
                var output = GetString(s, "output");

                Console.WriteLine($"- [{type}] {minutes} min — {title}");
                if (!string.IsNullOrWhiteSpace(why))
                    Console.WriteLine($"  Why: {why}");
                if (!string.IsNullOrWhiteSpace(output))
                    Console.WriteLine($"  Output: {output}");
            }
        }

        if (root.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Array)
        {
            var noteList = notes.EnumerateArray()
                                .Select(n => n.ValueKind == JsonValueKind.String ? n.GetString() : null)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .ToList();

            if (noteList.Count > 0)
            {
                Console.WriteLine("\nNotes:");
                foreach (var n in noteList)
                    Console.WriteLine($"- {n}");
            }
        }
    }
    catch
    {
        // Fallback: if JSON is invalid, print raw
        Console.WriteLine(studyPlanJson);
    }
}

static int GetInt(JsonElement obj, string name)
{
    if (obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
        return v;
    return 0;
}

static string GetString(JsonElement obj, string name)
{
    if (obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
        return el.GetString() ?? "";
    return "";
}

static void PrintLearningPath(string learningPathJson)
{
    try
    {
        using var doc = JsonDocument.Parse(learningPathJson);
        var root = doc.RootElement;

        var certification = GetString(root, "certification");
        if (!string.IsNullOrWhiteSpace(certification))
            Console.WriteLine($"Certification: {certification}");

        if (!root.TryGetProperty("resources", out var resources) || resources.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine(learningPathJson);
            return;
        }

        // Order by priority asc (1 is highest), then estimatedMinutes desc
        var list = resources.EnumerateArray()
            .Select(r => new
            {
                Title = GetString(r, "title"),
                Url = GetString(r, "url"),
                Type = GetString(r, "type"),
                Priority = GetInt(r, "priority"),
                Minutes = GetInt(r, "estimatedMinutes"),
                Why = GetString(r, "why"),
                FocusAreas = GetStringArray(r, "focusAreas")
            })
            .OrderBy(x => x.Priority == 0 ? int.MaxValue : x.Priority)
            .ThenByDescending(x => x.Minutes)
            .ToList();

        foreach (var r in list)
        {
            var priorityLabel = r.Priority == 0 ? "?" : r.Priority.ToString();

            Console.WriteLine();
            Console.WriteLine($"- (P{priorityLabel}) [{r.Type}] {r.Title}");

            if (r.Minutes > 0)
                Console.WriteLine($"  Est.: {r.Minutes} min");

            if (!string.IsNullOrWhiteSpace(r.Url))
                Console.WriteLine($"  URL: {r.Url}");
            else
                Console.WriteLine("  URL: (none)");

            if (!string.IsNullOrWhiteSpace(r.Why))
                Console.WriteLine($"  Why: {r.Why}");

            if (r.FocusAreas.Count > 0)
                Console.WriteLine($"  Focus: {string.Join(", ", r.FocusAreas)}");
        }
    }
    catch
    {
        // Fallback: print raw
        Console.WriteLine(learningPathJson);
    }
}

static List<string> GetStringArray(JsonElement obj, string name)
{
    var result = new List<string>();

    if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
        return result;

    foreach (var item in el.EnumerateArray())
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            var s = item.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                result.Add(s.Trim());
        }
    }

    return result;
}