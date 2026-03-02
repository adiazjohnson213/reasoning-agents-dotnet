namespace ReasoningAgents.Console.Output
{
    public static class SummaryRender
    {
        public static void PrettyPrintSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                System.Console.WriteLine("(No summary)");
                return;
            }

            var scorePart = summary;
            var issuesPart = "";
            var improvementsPart = "";

            var issuesIndex = summary.IndexOf("Issues:", StringComparison.OrdinalIgnoreCase);
            if (issuesIndex >= 0)
            {
                scorePart = summary[..issuesIndex].Trim();
                var rest = summary[(issuesIndex + "Issues:".Length)..].Trim();

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

            System.Console.WriteLine("=== RESULTADO ===");
            System.Console.WriteLine(scorePart);
            System.Console.WriteLine();

            if (!string.IsNullOrWhiteSpace(issuesPart))
            {
                System.Console.WriteLine("=== ISSUES (qué estuvo mal) ===");
                PrintPipeList(issuesPart);
                System.Console.WriteLine();
            }

            if (!string.IsNullOrWhiteSpace(improvementsPart))
            {
                System.Console.WriteLine("=== IMPROVEMENTS (qué hacer ahora) ===");
                PrintPipeList(improvementsPart);
                System.Console.WriteLine();
            }
        }

        private static void PrintPipeList(string text)
        {
            var items = text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i].Trim().TrimEnd('.');
                if (!string.IsNullOrWhiteSpace(item))
                    System.Console.WriteLine($"{i + 1}. {item}");
            }
        }
    }
}
