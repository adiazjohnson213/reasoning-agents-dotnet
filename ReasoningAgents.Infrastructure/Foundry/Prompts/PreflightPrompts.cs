namespace ReasoningAgents.Infrastructure.Foundry.Prompts
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
                                      "global": {
                                        "avoidAsCorrectAnswer": ["<string>"],
                                        "preferredNames": [
                                          { "old": "<string>", "new": "<string>", "notes": "<string>" }
                                        ],
                                        "allowedServices": ["<string>"]
                                      },
                                      "domains": [
                                        {
                                          "name": "<string>",
                                          "count": <integer>,
                                          "avoidAsCorrectAnswer": ["<string>"],
                                          "preferredNames": [
                                            { "old": "<string>", "new": "<string>", "notes": "<string>" }
                                          ],
                                          "allowedServices": ["<string>"],
                                          "notes": ["<string>"],
                                          "sources": ["<url>"]
                                        }
                                      ],
                                      "sources": ["<url>"]
                                    }

                                    You MUST use the MCP tools to search and fetch official Microsoft Learn documentation to justify the guardrails.
                                    """;

        public static string BuildPreflightRunPrompt(string certificationCode) => $$"""
                                    Build up-to-date assessment guardrails for certification {{certificationCode}}.

                                    You MUST use microsoft_docs_search + microsoft_docs_fetch (Microsoft Learn MCP).
                                    Use ONLY fetched URLs in sources.

                                    HARD LIMITS (mandatory):
                                    - Total fetched pages <= 10
                                    - Per domain: fetch 1 page (max 2 only if needed)
                                    - Keep notes concise (max 6 bullets per domain)

                                    CONSISTENCY RULES (mandatory):
                                    - No value in avoidAsCorrectAnswer may appear in allowedServices (global or any domain).
                                    - sources must contain unique URLs (no duplicates).
                                    - Include at least 1 fetched URL that supports each rename/retirement in preferredNames.

                                    DOMAINS:
                                    1) Plan and manage an Azure AI solution
                                    2) Implement generative AI solutions
                                    3) Implement an agentic solution
                                    4) Implement computer vision solutions
                                    5) Implement natural language processing solutions
                                    6) Implement knowledge mining and information extraction solutions

                                    GLOBAL TOPICS (prioritize, but do NOT exceed the fetch limits):
                                    - LUIS -> CLU migration/deprecation
                                    - QnA Maker retirement -> Question Answering
                                    - Form Recognizer -> Azure AI Document Intelligence rename
                                    - Azure Cognitive Search -> Azure AI Search rename

                                    Return ONLY JSON matching the schema exactly.

                                    CANONICAL NAMING RULES (mandatory):
                                    - Use these canonical service names in allowedServices only: "Azure Language", "Azure AI Search", "Azure AI Document Intelligence", "Azure OpenAI", "Azure AI Vision", "Azure AI Speech", "Azure AI Translator", "Azure Bot Service".
                                    - Do NOT include umbrella names like "Azure Cognitive Services" or vague groupings in allowedServices.
                                    - Consistency: no value in global.avoidAsCorrectAnswer may appear in any allowedServices (global or domain). Keep sources unique.
                                    """;
    }
}