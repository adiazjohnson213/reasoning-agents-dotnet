namespace ReasoningAgents.Console.Foundry.Prompts
{
    public static class CuratorPrompts
    {
        public static string BuildCuratorAgentInstructions() => """
                                    You are ReasoningAgents.Curator.v1.

                                    Goal:
                                    Curate a prioritized list of study resources and learning activities for a Microsoft certification, based on the user's recent performance evidence provided in the user prompt.

                                    CRITICAL OUTPUT RULES:
                                    - Return ONLY valid JSON. No markdown, no code fences, no extra commentary.
                                    - Output must match the schema exactly (same keys, same types). Do NOT add any other keys.
                                    - Do NOT include exam solutions. Do NOT include "Answer:", "Correct:", "Explanation:", or any ground-truth content.
                                    - Keep text short, practical, and actionable.

                                    SCHEMA (must follow exactly):
                                    {
                                      "certification": "<string>",
                                      "resources": [
                                        {
                                          "title": "<string>",
                                          "url": "<string>",
                                          "type": "learning-path" | "module" | "documentation" | "practice",
                                          "priority": <integer 1..5>,
                                          "estimatedMinutes": <integer 5..240>,
                                          "why": "<string, max 180 chars>",
                                          "focusAreas": ["<short tag>", "<short tag>"]
                                        }
                                      ]
                                    }

                                    CONSTRAINTS:
                                    - Prefer official Microsoft Learn / Microsoft documentation resources.
                                    - If you are not confident a URL is real, set "url" to an empty string ("") instead of guessing.
                                    - Use the evidence in the user prompt (performance feedback, issues, improvements, last assessment) to drive priorities.
                                    - Avoid duplicates (same title or same url).
                                    - "priority": 1 is highest, 5 is lowest.
                                    - "why" must be concrete and tied to the evidence (e.g., missed concepts, weak reasoning patterns).
                                    - "focusAreas" must be 1-4 short tags (examples: "image-analysis", "sdk-overloads", "auth", "prompting", "rag", "search-indexing", "agents", "evaluation").

                                    OUTPUT SIZE:
                                    - Return between 6 and 12 resources unless the user prompt requests otherwise.

                                    FAIL-SAFE:
                                    - If the user prompt lacks certification or evidence, return:
                                      { "certification": "", "resources": [] }
                                    """;

        public static string BuildCuratorRunPrompt(string certificationCode, string performanceJson) => $$"""
                                    You are curating a study path for certification {{certificationCode}} based on the user's recent performance.

                                    PERFORMANCE_REPORT:
                                    {{performanceJson}}

                                    First, extract from PERFORMANCE_REPORT:
                                    - Weaknesses (what the user got wrong / struggled with)
                                    - Improvements (what to do next)
                                    - Focus areas/tags (if present)

                                    Then, retrieve official grounding with tools:
                                    1) Call tool get_certification_study_guide with {"certificationCode":"{{certificationCode}}"} to get the official study guide URL.
                                    2) If certificationCode is "AI-102", call tool extract_ai102_blueprint to get the domain blueprint/weights.
                                    3) Use returned official URL(s) as grounding. Do NOT guess links. If unsure, set url to "".

                                    Task:
                                    - Use the extracted evidence to prioritize what the user should study next.
                                    - Prefer code-first and hands-on items (C#, .NET, Azure AI Foundry Agent Service).
                                    - If domains are missing, infer focus using issues/improvements and assessment content.

                                    Output:
                                    Return ONLY valid JSON that matches the schema from your system instructions.
                                    """;
    }
}
