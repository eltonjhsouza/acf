namespace AgentCore.Core.Workflows;

public sealed class TeamBuilder
{
    private string _name = "team";
    private TeamProcessType _processType = TeamProcessType.Sequential;
    private readonly List<TeamMemberDefinition> _members = new();

    public static TeamBuilder Create(string name)
        => new TeamBuilder().Named(name);

    public TeamBuilder Named(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name cannot be empty.");

        _name = name.Trim();
        return this;
    }

    public TeamBuilder WithProcess(TeamProcessType processType)
    {
        _processType = processType;
        return this;
    }

    public TeamBuilder AddMember(string name, string role, string instructions)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("member name cannot be empty.");
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("member role cannot be empty.");

        _members.Add(new TeamMemberDefinition
        {
            Name = name.Trim(),
            Role = role.Trim(),
            Instructions = instructions?.Trim() ?? string.Empty
        });

        return this;
    }

    public TeamDefinition Build()
    {
        if (_members.Count == 0)
            throw new InvalidOperationException("Team must have at least one member.");

        return new TeamDefinition
        {
            Name = _name,
            ProcessType = _processType,
            Members = _members.ToList().AsReadOnly()
        };
    }
}
