namespace AgentCore.Core.Workflows;

public static class TeamPresets
{
    public static TeamDefinition CreateResearchWriterReviewerTeam(string name = "default_team")
    {
        return new TeamDefinition
        {
            Name = name,
            ProcessType = TeamProcessType.Sequential,
            Members = new List<TeamMemberDefinition>
            {
                new()
                {
                    Name = "researcher",
                    Role = "Research Analyst",
                    Instructions =
                        "Investigate the topic, prioritize reliable sources, and call out uncertainty explicitly."
                },
                new()
                {
                    Name = "writer",
                    Role = "Technical Writer",
                    Instructions =
                        "Transform research into clear, practical Markdown output with concise structure."
                },
                new()
                {
                    Name = "reviewer",
                    Role = "Quality Reviewer",
                    Instructions =
                        "Critique factual quality, clarity, and completeness. Be strict but actionable."
                }
            }
        };
    }
}
