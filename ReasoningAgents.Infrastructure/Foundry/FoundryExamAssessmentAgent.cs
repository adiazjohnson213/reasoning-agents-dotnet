using System.Text;
using ReasoningAgents.Domain.Agents;
using ReasoningAgents.Domain.Inputs;

namespace ReasoningAgents.Infrastructure.Foundry
{
    public sealed class FoundryExamAssessmentAgent : IAgentStep<AssessmentInput, string>
    {
        private readonly FoundryAssessmentAgent _domainGenerator;
        public FoundryExamAssessmentAgent(FoundryAssessmentAgent domainGenerator)
            => _domainGenerator = domainGenerator;

        public async Task<string> ExecuteAsync(AssessmentInput input, CancellationToken ct)
        {
            var blueprint = new (string Domain, int Count)[]
            {
                ("Plan and manage an Azure AI solution", 13),
                ("Implement generative AI solutions", 10),
                ("Implement an agentic solution", 5),
                ("Implement computer vision solutions", 8),
                ("Implement natural language processing solutions", 7),
                ("Implement knowledge mining and information extraction solutions", 7),
            };

            var sb = new StringBuilder();

            foreach (var (domain, count) in blueprint)
            {
                var block = await _domainGenerator.GenerateDomainAsync(input.Goal, input.StudyPlan, domain, count, ct);

                sb.AppendLine($"=== DOMAIN: {domain} ({count}) ===");
                sb.AppendLine(block);
                sb.AppendLine();
            }

            return sb.ToString();

        }
    }
}
