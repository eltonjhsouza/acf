namespace AgentCore.Core.Workflows;

public sealed class TeamMemberDefinition
{
    public required string Name { get; init; }
    public required string Role { get; init; }
    public required string Instructions { get; init; }
}
