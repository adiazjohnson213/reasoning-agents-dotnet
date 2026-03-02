using System.Text.Json;

namespace ReasoningAgents.Console.Output
{
    public static class StudyPlanRender
    {
        public static void PrintStudyPlan(string studyPlanJson)
        {
            if (string.IsNullOrWhiteSpace(studyPlanJson))
            {
                System.Console.WriteLine("(No study plan)");
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(studyPlanJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("days", out var days) || days.ValueKind != JsonValueKind.Array)
                {
                    System.Console.WriteLine(studyPlanJson);
                    return;
                }

                foreach (var day in days.EnumerateArray())
                {
                    var dayNumber = GetInt(day, "day");
                    var total = GetInt(day, "totalMinutes");

                    System.Console.WriteLine($"\n--- Day {dayNumber} ({total} min) ---");

                    if (!day.TryGetProperty("sessions", out var sessions) || sessions.ValueKind != JsonValueKind.Array)
                    {
                        System.Console.WriteLine("  (No sessions)");
                        continue;
                    }

                    foreach (var s in sessions.EnumerateArray())
                    {
                        var title = GetString(s, "title");
                        var minutes = GetInt(s, "minutes");
                        var type = GetString(s, "type");
                        var why = GetString(s, "why");
                        var output = GetString(s, "output");

                        System.Console.WriteLine($"- [{type}] {minutes} min — {title}");
                        if (!string.IsNullOrWhiteSpace(why))
                            System.Console.WriteLine($"  Why: {why}");
                        if (!string.IsNullOrWhiteSpace(output))
                            System.Console.WriteLine($"  Output: {output}");
                    }
                }

                if (root.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Array)
                {
                    var hadAny = false;
                    foreach (var n in notes.EnumerateArray())
                    {
                        if (n.ValueKind != JsonValueKind.String) continue;
                        var text = n.GetString();
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        if (!hadAny)
                        {
                            System.Console.WriteLine("\nNotes:");
                            hadAny = true;
                        }

                        System.Console.WriteLine($"- {text}");
                    }
                }
            }
            catch
            {
                // Fallback: if JSON is invalid, print raw
                System.Console.WriteLine(studyPlanJson);
            }
        }

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
    }
}
