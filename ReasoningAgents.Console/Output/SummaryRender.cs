using ReasoningAgents.Domain.Models;

namespace ReasoningAgents.Console.Output
{
    public static class SummaryRender
    {
        public static void PrettyPrintSummary(WorkflowResult result)
        {
            System.Console.WriteLine("=== RESULTADO ===");
            System.Console.WriteLine($"Passed: {result.Passed}");
            System.Console.WriteLine($"Score: {result.Score}/100");

            if (!string.IsNullOrWhiteSpace(result.Summary))
            {
                System.Console.WriteLine();
                System.Console.WriteLine(result.Summary.Trim());
            }

            System.Console.WriteLine();

            if (result.Issues is { Count: > 0 })
            {
                System.Console.WriteLine("=== ISSUES (what went wrong) ===");
                PrintList(result.Issues);
                System.Console.WriteLine();
            }

            if (result.Improvements is { Count: > 0 })
            {
                System.Console.WriteLine("=== IMPROVEMENTS (what to do now) ===");
                PrintList(result.Improvements);
                System.Console.WriteLine();
            }
        }

        private static void PrintList(IReadOnlyList<string> items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i]?.Trim().TrimEnd('.');
                if (!string.IsNullOrWhiteSpace(item))
                    System.Console.WriteLine($"{i + 1}. {item}");
            }
        }
    }
}
