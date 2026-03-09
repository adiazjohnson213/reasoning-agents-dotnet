using System.Text.RegularExpressions;
using ReasoningAgents.Api.Contracts.Responses;

namespace ReasoningAgents.Api.Parsing
{
    public enum QuestionType
    {
        Single,
        Multi,
        Reorder
    }

    public static class AssessmentParser
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

        // ---- TYPE (strict line + inline anywhere) ----
        private static readonly Regex TypeStrictRegex = new(
            @"(?mi)^\s*Type:\s*(?<t>SINGLE|MULTI|REORDER)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout);

        // Accepts: "Q50) Type: REORDER" OR "... Type: MULTI" at end of a line
        private static readonly Regex TypeInlineRegex = new(
            @"(?mi)\bType:\s*(?<t>SINGLE|MULTI|REORDER)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout);

        // ---- QUESTION BLOCK ----
        // Qn) ... until next Qm) or end
        private static readonly Regex QuestionBlockRegex = new(
            @"(?ms)^\s*Q(?<num>\d+)\)\s*(?<body>.*?)(?=^\s*Q\d+\)|\z)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout);

        // ---- SELECT (strict line + inline anywhere) ----
        private static readonly Regex SelectStrictRegex = new(
            @"(?mi)^\s*Select:\s*(?<k>TWO|THREE|FOUR)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout);

        // Accepts: "Q9) ... Select: TWO" OR "... Select: THREE" at end of a line
        private static readonly Regex SelectInlineRegex = new(
            @"(?mi)\bSelect:\s*(?<k>TWO|THREE|FOUR)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout);

        // ---- OPTIONS ----
        // Options A) ... up to Z)
        private static readonly Regex OptionRegex = new(
            @"(?ms)^\s*(?<letter>[A-Z])\)\s*(?<text>.*?)(?=^\s*[A-Z]\)\s*|\z)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout);

        private static readonly Regex FirstOptionLineRegex = new(
            @"(?m)^\s*[A-Z]\)\s+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout);

        // Slightly more tolerant inference (optional fallback)
        private static readonly Regex InferSelectFromPromptRegex = new(
            @"(?i)\b(?:which|select|choose|pick)\b(?:\s+\w+){0,2}\s+(?<k>two|three|four|2|3|4)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexTimeout);

        /// <summary>
        /// Tolerant parser: skips malformed/empty question blocks instead of throwing.
        /// Use ParseStrict if you want failures.
        /// </summary>
        public static IReadOnlyList<QuestionDto> Parse(string assessmentText)
            => ParseInternal(assessmentText, strict: false);

        /// <summary>
        /// Strict parser: throws on first malformed block.
        /// </summary>
        public static IReadOnlyList<QuestionDto> ParseStrict(string assessmentText)
            => ParseInternal(assessmentText, strict: true);

        private static IReadOnlyList<QuestionDto> ParseInternal(string assessmentText, bool strict)
        {
            if (string.IsNullOrWhiteSpace(assessmentText))
                throw new InvalidOperationException("Assessment text is empty.");

            var matches = QuestionBlockRegex.Matches(assessmentText);
            if (matches.Count == 0)
                throw new InvalidOperationException("Could not parse assessment questions. Output format does not match expected layout.");

            var result = new List<QuestionDto>(matches.Count);

            foreach (Match m in matches)
            {
                var num = int.Parse(m.Groups["num"].Value);
                var body = m.Groups["body"].Value ?? "";

                // ---- Determine type (Type: ... takes precedence) ----
                var qType = QuestionType.Single;

                var tm = MatchType(body);
                if (tm.Success)
                {
                    qType = tm.Groups["t"].Value switch
                    {
                        "SINGLE" => QuestionType.Single,
                        "MULTI" => QuestionType.Multi,
                        "REORDER" => QuestionType.Reorder,
                        _ => QuestionType.Single
                    };

                    body = RemoveType(body);
                }

                // IMPORTANT FIX: only infer MULTI from Select if we are still SINGLE
                // (your current parser can overwrite REORDER -> MULTI) :contentReference[oaicite:4]{index=4}
                if (qType == QuestionType.Single && HasSelect(body))
                    qType = QuestionType.Multi;

                // ---- Select (ONLY for MULTI) ----
                int? selectCount = null;
                if (qType == QuestionType.Multi)
                {
                    var sm = MatchSelect(body);
                    if (!sm.Success)
                    {
                        if (strict) throw new InvalidOperationException($"Q{num}: MULTI missing Select line.");
                        continue; // skip malformed block
                    }

                    selectCount = sm.Groups["k"].Value switch
                    {
                        "TWO" => 2,
                        "THREE" => 3,
                        "FOUR" => 4,
                        _ => null
                    };

                    if (selectCount is null)
                    {
                        if (strict) throw new InvalidOperationException($"Q{num}: MULTI Select must be TWO|THREE|FOUR.");
                        continue;
                    }

                    body = RemoveSelect(body);
                }
                else
                {
                    // If not MULTI, remove any stray Select lines (tolerant cleanup)
                    body = RemoveSelect(body);
                }

                // If after cleanup body is empty, this is an orphan block like:
                // "Q38) Type: REORDER" with nothing else :contentReference[oaicite:5]{index=5}
                if (string.IsNullOrWhiteSpace(Normalize(body)))
                {
                    if (strict) throw new InvalidOperationException($"Q{num}: empty question block.");
                    continue;
                }

                // ---- Extract options ----
                var optionMatches = OptionRegex.Matches(body);
                if (optionMatches.Count == 0)
                {
                    if (strict) throw new InvalidOperationException($"Q{num}: no options found (expected lines like A) ...).");
                    continue; // skip malformed block
                }

                var options = new List<OptionDto>(optionMatches.Count);
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (Match om in optionMatches)
                {
                    var key = om.Groups["letter"].Value;
                    var text = Normalize(om.Groups["text"].Value);

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        if (strict) throw new InvalidOperationException($"Q{num}: option {key} is empty.");
                        options.Clear();
                        break;
                    }

                    if (!seen.Add(key))
                    {
                        if (strict) throw new InvalidOperationException($"Q{num}: duplicate option key {key}.");
                        options.Clear();
                        break;
                    }

                    options.Add(new OptionDto(key, text));
                }

                if (options.Count == 0)
                {
                    if (strict) throw new InvalidOperationException($"Q{num}: invalid options.");
                    continue;
                }

                // ---- Prompt = everything before first option line ----
                var firstOpt = FirstOptionLineRegex.Match(body);
                var promptRaw = firstOpt.Success ? body[..firstOpt.Index] : body;
                var prompt = Normalize(promptRaw);

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    if (strict) throw new InvalidOperationException($"Q{num}: prompt is empty.");
                    continue;
                }

                // Optional fallback: infer MULTI if generator forgot Select but prompt says choose/select two/three/four
                if (qType == QuestionType.Single && selectCount is null && options.Count > 4)
                {
                    var inferred = InferSelectCountFromPrompt(prompt);
                    if (inferred is not null)
                    {
                        qType = QuestionType.Multi;
                        selectCount = inferred.Value;
                    }
                }

                // Guardrail: >4 options must be MULTI or REORDER
                if (qType == QuestionType.Single && selectCount is null && options.Count > 4)
                {
                    if (strict)
                    {
                        throw new InvalidOperationException(
                            $"Q{num}: Found {options.Count} options but missing Select: (MULTI) or Type: REORDER. Generator must declare the type.");
                    }
                    continue;
                }

                // ---- Minimal validation ----
                try
                {
                    switch (qType)
                    {
                        case QuestionType.Single:
                            if (selectCount is not null)
                                throw new InvalidOperationException($"Q{num}: SINGLE must not include Select.");
                            ValidateSingle(num, options);
                            break;

                        case QuestionType.Multi:
                            if (selectCount is null)
                                throw new InvalidOperationException($"Q{num}: MULTI must include Select.");
                            ValidateMulti(num, options, selectCount.Value);
                            break;

                        case QuestionType.Reorder:
                            if (selectCount is not null)
                                throw new InvalidOperationException($"Q{num}: REORDER must not include Select.");
                            ValidateReorder(num, options);
                            break;
                    }
                }
                catch
                {
                    if (strict) throw;
                    continue; // skip invalid block
                }

                result.Add(new QuestionDto(
                    Number: num,
                    Prompt: prompt,
                    Options: options.ToArray(),
                    SelectCount: selectCount,
                    QuestionType: qType.ToString().ToLowerInvariant()
                ));
            }

            result.Sort((x, y) => x.Number.CompareTo(y.Number));

            if (result.Count == 0)
                throw new InvalidOperationException("No valid questions parsed from assessment text.");

            return result;
        }

        private static void ValidateSingle(int num, List<OptionDto> options)
        {
            if (options.Count != 4)
                throw new InvalidOperationException($"Q{num}: SINGLE must have exactly 4 options (A–D). Found {options.Count}.");

            var letters = options.Select(o => o.Key).OrderBy(x => x).ToArray();
            var expected = new[] { "A", "B", "C", "D" };

            if (!letters.SequenceEqual(expected))
                throw new InvalidOperationException($"Q{num}: SINGLE must use A–D only. Found: {string.Join(",", letters)}.");
        }

        private static void ValidateMulti(int num, List<OptionDto> options, int selectCount)
        {
            if (selectCount < 2 || selectCount > 4)
                throw new InvalidOperationException($"Q{num}: MULTI Select must be 2..4. Found {selectCount}.");

            if (options.Count < 4)
                throw new InvalidOperationException($"Q{num}: MULTI must have at least 4 options. Found {options.Count}.");

            if (selectCount == 4 && options.Count < 6)
                throw new InvalidOperationException($"Q{num}: Select FOUR requires at least 6 options. Found {options.Count}.");
        }

        private static void ValidateReorder(int num, List<OptionDto> options)
        {
            if (options.Count < 4 || options.Count > 7)
                throw new InvalidOperationException($"Q{num}: REORDER must have 4–7 items. Found {options.Count}.");

            var expected = Enumerable.Range(0, options.Count)
                .Select(i => ((char)('A' + i)).ToString())
                .ToArray();

            var letters = options.Select(o => o.Key).OrderBy(x => x).ToArray();

            if (!letters.SequenceEqual(expected))
                throw new InvalidOperationException($"Q{num}: REORDER must use consecutive keys {string.Join(",", expected)}. Found: {string.Join(",", letters)}.");
        }

        private static int? InferSelectCountFromPrompt(string prompt)
        {
            var m = InferSelectFromPromptRegex.Match(prompt ?? "");
            if (!m.Success) return null;

            return m.Groups["k"].Value.ToLowerInvariant() switch
            {
                "two" or "2" => 2,
                "three" or "3" => 3,
                "four" or "4" => 4,
                _ => null
            };
        }

        private static string Normalize(string s)
            => Regex.Replace(s ?? "", @"\s+", " ", RegexOptions.CultureInvariant, RegexTimeout).Trim();

        private static Match MatchType(string body)
        {
            var m = TypeStrictRegex.Match(body);
            return m.Success ? m : TypeInlineRegex.Match(body);
        }

        private static bool HasSelect(string body)
            => SelectStrictRegex.IsMatch(body) || SelectInlineRegex.IsMatch(body);

        private static Match MatchSelect(string body)
        {
            var m = SelectStrictRegex.Match(body);
            return m.Success ? m : SelectInlineRegex.Match(body);
        }

        private static string RemoveType(string body)
        {
            body = TypeStrictRegex.Replace(body, "");
            body = TypeInlineRegex.Replace(body, "");
            return body;
        }

        private static string RemoveSelect(string body)
        {
            body = SelectStrictRegex.Replace(body, "");
            body = SelectInlineRegex.Replace(body, "");
            return body;
        }
    }
}