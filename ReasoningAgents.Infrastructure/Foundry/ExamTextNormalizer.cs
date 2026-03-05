using System.Text.RegularExpressions;

namespace ReasoningAgents.Infrastructure.Foundry
{

    public static class ExamTextNormalizer
    {
        // Match Qn) at start of line
        private static readonly Regex QLine = new(
            @"(?m)^\s*Q(?<n>\d+)\)\s*",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Match headers you add: === DOMAIN: ... ===
        private static readonly Regex DomainHeader = new(
            @"(?m)^\s*===\s*DOMAIN:.*?===\s*$\r?\n?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string NormalizeAndRenumber(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw ?? "";

            // 1) Remove DOMAIN headers
            var text = DomainHeader.Replace(raw, "");

            // 2) Renumber questions sequentially
            var i = 0;
            text = QLine.Replace(text, _ =>
            {
                i++;
                return $"Q{i}) ";
            });

            return text.Trim();
        }
    }
}
