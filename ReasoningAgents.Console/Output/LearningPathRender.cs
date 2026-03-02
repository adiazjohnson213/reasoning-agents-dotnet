using System.Text.Json;

namespace ReasoningAgents.Console.Output
{
    public static class LearningPathRender
    {
        public static void PrintLearningPath(string learningPathJson)
        {
            if (string.IsNullOrWhiteSpace(learningPathJson))
            {
                System.Console.WriteLine("(No learning path)");
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(learningPathJson);
                var root = doc.RootElement;

                var certification = GetString(root, "certification");
                if (!string.IsNullOrWhiteSpace(certification))
                    System.Console.WriteLine($"Certification: {certification}");

                if (!root.TryGetProperty("resources", out var resources) || resources.ValueKind != JsonValueKind.Array)
                {
                    System.Console.WriteLine(learningPathJson);
                    return;
                }

                // Order: priority asc (1 best), then estimatedMinutes desc
                var list = resources.EnumerateArray()
                    .Select(r => new ResourceRow(Title: GetString(r, "title"),
                                                 Url: GetString(r, "url"),
                                                 Type: GetString(r, "type"),
                                                 Priority: GetInt(r, "priority"),
                                                 Minutes: GetInt(r, "estimatedMinutes"),
                                                 Why: GetString(r, "why"),
                                                 FocusAreas: GetStringArray(r, "focusAreas")))
                    .OrderBy(x => x.Priority == 0 ? int.MaxValue : x.Priority)
                    .ThenByDescending(x => x.Minutes)
                    .ToList();

                foreach (var r in list)
                {
                    var priorityLabel = r.Priority == 0 ? "?" : r.Priority.ToString();

                    System.Console.WriteLine();
                    System.Console.WriteLine($"- (P{priorityLabel}) [{r.Type}] {r.Title}");

                    if (r.Minutes > 0)
                        System.Console.WriteLine($"  Est.: {r.Minutes} min");

                    System.Console.WriteLine(!string.IsNullOrWhiteSpace(r.Url)
                        ? $"  URL: {r.Url}"
                        : "  URL: (none)");

                    if (!string.IsNullOrWhiteSpace(r.Why))
                        System.Console.WriteLine($"  Why: {r.Why}");

                    if (r.FocusAreas.Count > 0)
                        System.Console.WriteLine($"  Focus: {string.Join(", ", r.FocusAreas)}");
                }
            }
            catch
            {
                System.Console.WriteLine(learningPathJson);
            }
        }

        private sealed record ResourceRow(string Title,
                                          string Url,
                                          string Type,
                                          int Priority,
                                          int Minutes,
                                          string Why,
                                          List<string> FocusAreas);

        private static int GetInt(JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
                return v;
            return 0;
        }

        private static string GetString(JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
                return el.GetString() ?? "";
            return "";
        }

        private static List<string> GetStringArray(JsonElement obj, string name)
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
    }
}
