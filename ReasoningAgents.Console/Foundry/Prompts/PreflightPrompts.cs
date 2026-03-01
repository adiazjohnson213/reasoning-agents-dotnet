namespace ReasoningAgents.Console.Foundry.Prompts
{
    public static class PreflightPrompts
    {
        public static string BuildPreflightAgentInstructions() => """
                                    You are a preflight grounding agent.

                                    CRITICAL OUTPUT RULES:
                                    - Return ONLY valid JSON. No markdown. No extra text.
                                    - Do not include exam answers or solutions.
                                    - The JSON must match this schema exactly (no extra keys):

                                    {
                                      "certification": "<string>",
                                      "avoidAsCorrectAnswer": ["<string>"],
                                      "preferredNames": [
                                        { "old": "<string>", "new": "<string>", "notes": "<string>" }
                                      ],
                                      "allowedServices": ["<string>"],
                                      "notes": ["<string>"],
                                      "sources": ["<url>"]
                                    }

                                    You MUST use the MCP tools to search and fetch official Microsoft Learn documentation to justify the guardrails.
                                    """;

        public static string BuildPreflightRunPrompt(string certificationCode) => $$"""
                                    Build up-to-date assessment guardrails for certification {{certificationCode}}.

                                    You MUST:
                                    1) Use microsoft_docs_search to find official docs about:
                                       - LUIS deprecation / migration guidance
                                       - QnA Maker retirement and replacement
                                       - Form Recognizer rename to Azure AI Document Intelligence
                                    2) Use microsoft_docs_fetch to fetch the most relevant pages and extract the facts.

                                    Then output ONLY JSON with:
                                    - avoidAsCorrectAnswer: include deprecated/retired names that should not be correct
                                    - preferredNames: mapping old->new
                                    - allowedServices: current Azure AI services that should appear as correct answers in assessment
                                    - sources: include the fetched URLs used
                                    """;
    }
}