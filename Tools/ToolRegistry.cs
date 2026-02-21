namespace AgentCore.Tools;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        foreach (var tool in tools)
            Add(tool);
    }

    public void Add(ITool tool)
    {
        var name = tool.Spec.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tool Spec.Name cannot be empty.");

        _tools[name] = tool;
    }

    public bool TryGet(string name, out ITool tool) => _tools.TryGetValue(name, out tool!);

    public IReadOnlyCollection<ITool> All => _tools.Values.ToList().AsReadOnly();

    public string ListNames() => string.Join(", ", _tools.Keys.OrderBy(x => x));
}