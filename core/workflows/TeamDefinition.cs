namespace AgentCore.Core.Workflows;

public sealed class TeamDefinition
{
    public required string Name { get; init; }
    public TeamProcessType ProcessType { get; init; } = TeamProcessType.Sequential;
    public required IReadOnlyList<TeamMemberDefinition> Members { get; init; }

    public TeamMemberDefinition GetMemberByKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            throw new ArgumentException("keyword cannot be empty.");

        var member = Members.FirstOrDefault(m =>
            m.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            m.Role.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        if (member == null)
            throw new InvalidOperationException(
                $"Team member matching '{keyword}' was not found in team '{Name}'.");

        return member;
    }
}
