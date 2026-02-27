namespace ReasoningAgents.Console.Cli
{
    public static class CliOptionsParser
    {
        public static CliParseResult Parse(string[] args)
        {
            if (args.Any(a => a is "--help" or "-h" or "-?"))
            {
                return new CliParseResult(Success: false, Options: null, ShowHelp: true, Error: null);
            }

            var tokens = ArgsTokenizer.ExpandEqualsSyntax(args);

            string cert = "";
            int days = 0, minutes = 0;

            bool seenCert = false, seenDays = false, seenMinutes = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                switch (token)
                {
                    case "--cert":
                        if (seenCert) return Fail("Duplicate option --cert.");
                        if (!TryReadValue(tokens, ref i, "--cert", out cert, out var err1)) return Fail(err1);
                        seenCert = true;
                        break;

                    case "--days":
                        if (seenDays) return Fail("Duplicate option --days.");
                        if (!TryReadInt(tokens, ref i, "--days", out days, out var err2)) return Fail(err2);
                        if (days <= 0) return Fail("Invalid value for --days. Must be a positive integer.");
                        seenDays = true;
                        break;

                    case "--minutes":
                        if (seenMinutes) return Fail("Duplicate option --minutes.");
                        if (!TryReadInt(tokens, ref i, "--minutes", out minutes, out var err3)) return Fail(err3);
                        if (minutes <= 0) return Fail("Invalid value for --minutes. Must be a positive integer.");
                        seenMinutes = true;
                        break;

                    default:
                        return Fail($"Unknown option: {token}");
                }
            }

            if (!seenCert) return Fail("Missing required option --cert.");
            if (!seenDays) return Fail("Missing required option --days.");
            if (!seenMinutes) return Fail("Missing required option --minutes.");

            return new CliParseResult(
                Success: true,
                Options: new CliOptions(cert, days, minutes),
                ShowHelp: false,
                Error: null
            );
        }

        private static CliParseResult Fail(string message) =>
            new(Success: false, Options: null, ShowHelp: true, Error: message);

        private static bool TryReadValue(
            List<string> tokens,
            ref int i,
            string optionName,
            out string value,
            out string error)
        {
            value = "";
            error = "";

            if (i + 1 >= tokens.Count)
            {
                error = $"Missing value for {optionName}.";
                return false;
            }

            var candidate = tokens[++i];

            if (candidate.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Missing value for {optionName}.";
                return false;
            }

            value = candidate.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"Invalid value for {optionName}.";
                return false;
            }

            return true;
        }

        private static bool TryReadInt(
            List<string> tokens,
            ref int i,
            string optionName,
            out int value,
            out string error)
        {
            value = 0;

            if (!TryReadValue(tokens, ref i, optionName, out var raw, out error))
                return false;

            if (!int.TryParse(raw, out value))
            {
                error = $"Invalid value for {optionName}. Must be a positive integer.";
                return false;
            }

            return true;
        }
    }
}
