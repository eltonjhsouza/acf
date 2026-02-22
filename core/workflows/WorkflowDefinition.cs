namespace AgentCore.Core.Workflows;

public sealed class WorkflowDefinition
{
    private readonly Dictionary<string, IWorkflowNode> _nodes =
        new(StringComparer.OrdinalIgnoreCase);

    public WorkflowDefinition(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow name cannot be empty.");

        Name = name.Trim();
    }

    public string Name { get; }
    public string StartNodeName { get; private set; } = string.Empty;

    public IReadOnlyCollection<IWorkflowNode> Nodes => _nodes.Values.ToList().AsReadOnly();

    public WorkflowDefinition AddNode(IWorkflowNode node)
    {
        _nodes[node.Name] = node;
        return this;
    }

    public WorkflowDefinition SetStart(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Start node cannot be empty.");

        StartNodeName = nodeName.Trim();
        return this;
    }

    public bool TryGetNode(string nodeName, out IWorkflowNode node)
        => _nodes.TryGetValue(nodeName, out node!);

    public IWorkflowNode GetNodeOrThrow(string nodeName)
    {
        if (!_nodes.TryGetValue(nodeName, out var node))
            throw new InvalidOperationException($"Workflow node '{nodeName}' not found.");

        return node;
    }

    public void Validate()
    {
        if (_nodes.Count == 0)
            throw new InvalidOperationException($"Workflow '{Name}' has no nodes.");

        if (string.IsNullOrWhiteSpace(StartNodeName))
            throw new InvalidOperationException($"Workflow '{Name}' has no start node configured.");

        if (!_nodes.ContainsKey(StartNodeName))
            throw new InvalidOperationException(
                $"Workflow '{Name}' start node '{StartNodeName}' is not registered.");
    }
}
