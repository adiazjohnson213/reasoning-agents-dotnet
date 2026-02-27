
namespace ReasoningAgents.Console.Cli
{
    public static class CliHelpPrinter
    {
        public static void Print()
        {
            System.Console.WriteLine("""
                Usage:
                  ReasoningAgents.Console --cert <CODE> --days <N> --minutes <N>

                Options:
                  --cert           Certification code (e.g., AI-102, AZ-900)
                  --days           Days available (positive integer)
                  --minutes        Minutes per day (positive integer)
                  -h|--help        Show help

                Examples:
                  dotnet run --project ReasoningAgents.Console -- --cert AI-102 --days 14 --minutes 90
                  dotnet run --project ReasoningAgents.Console -- --cert=AZ-900 --days=10 --minutes=60
                """);
        }
    }
}
