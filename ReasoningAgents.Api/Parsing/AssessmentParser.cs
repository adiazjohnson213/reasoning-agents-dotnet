using System.Text.RegularExpressions;
using ReasoningAgents.Api.Contracts.Responses;

namespace ReasoningAgents.Api.Parsing
{
    public static class AssessmentParser
    {
        private static readonly Regex QuestionRegex = new(
            pattern:
                @"Q(?<num>\d+)\)\s*(?<prompt>[\s\S]*?)\s*" +
                @"A\)\s*(?<A>[\s\S]*?)\s*" +
                @"B\)\s*(?<B>[\s\S]*?)\s*" +
                @"C\)\s*(?<C>[\s\S]*?)\s*" +
                @"D\)\s*(?<D>[\s\S]*?)\s*(?=(?:Q\d+\))|\z)",
            options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static IReadOnlyList<QuestionDto> Parse(string assessmentText)
        {
            if (string.IsNullOrWhiteSpace(assessmentText))
                throw new InvalidOperationException("Assessment text is empty.");

            var matches = QuestionRegex.Matches(assessmentText);

            if (matches.Count == 0)
                throw new InvalidOperationException("Could not parse assessment questions. Output format does not match expected MCQ layout.");

            var result = new List<QuestionDto>(matches.Count);

            foreach (Match m in matches)
            {
                var num = int.Parse(m.Groups["num"].Value);

                var prompt = Normalize(m.Groups["prompt"].Value);
                var a = Normalize(m.Groups["A"].Value);
                var b = Normalize(m.Groups["B"].Value);
                var c = Normalize(m.Groups["C"].Value);
                var d = Normalize(m.Groups["D"].Value);

                // Basic sanity: ensure 4 distinct non-empty options
                if (string.IsNullOrWhiteSpace(prompt) ||
                    string.IsNullOrWhiteSpace(a) ||
                    string.IsNullOrWhiteSpace(b) ||
                    string.IsNullOrWhiteSpace(c) ||
                    string.IsNullOrWhiteSpace(d))
                    throw new InvalidOperationException($"Parsed an invalid question block for Q{num}. One or more fields are empty.");

                result.Add(new QuestionDto(
                    Number: num,
                    Prompt: prompt,
                    Options: new[]
                    {
                    new OptionDto("A", a),
                    new OptionDto("B", b),
                    new OptionDto("C", c),
                    new OptionDto("D", d),
                    }
                ));
            }

            // Ensure ordered by number
            result.Sort((x, y) => x.Number.CompareTo(y.Number));
            return result;
        }

        private static string Normalize(string s)
            => Regex.Replace(s ?? "", @"\s+", " ").Trim();
    }
}
