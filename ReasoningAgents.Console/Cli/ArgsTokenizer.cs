namespace ReasoningAgents.Console.Cli
{
    public static class ArgsTokenizer
    {
        public static List<string> ExpandEqualsSyntax(string[] args)
        {
            var tokens = new List<string>(args.Length);

            foreach (var a in args)
            {
                var idx = a.IndexOf('=');

                if (idx > 0 && a.StartsWith("--", StringComparison.Ordinal))
                {
                    tokens.Add(a[..idx]);
                    tokens.Add(a[(idx + 1)..]);
                }
                else
                {
                    tokens.Add(a);
                }
            }

            return tokens;
        }
    }
}
