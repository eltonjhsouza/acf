namespace AgentCore.Core.Workflows;

public sealed class LLMTaskNode : IWorkflowNode
{
    private readonly string _role;
    private readonly string _instructions;
    private readonly string _inputTemplate;
    private readonly string _outputStateKey;
    private readonly string? _nextNodeName;
    private readonly double _temperature;

    public LLMTaskNode(
        string name,
        string role,
        string instructions,
        string inputTemplate,
        string outputStateKey,
        string? nextNodeName,
        double temperature = 0.2)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name cannot be empty.");

        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("role cannot be empty.");

        if (string.IsNullOrWhiteSpace(inputTemplate))
            throw new ArgumentException("inputTemplate cannot be empty.");

        if (string.IsNullOrWhiteSpace(outputStateKey))
            throw new ArgumentException("outputStateKey cannot be empty.");

        Name = name.Trim();
        _role = role.Trim();
        _instructions = instructions?.Trim() ?? string.Empty;
        _inputTemplate = inputTemplate;
        _outputStateKey = outputStateKey.Trim();
        _nextNodeName = nextNodeName?.Trim();
        _temperature = temperature;
    }

    public string Name { get; }

    public async Task<WorkflowNodeResult> ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var rendered = StateTemplateRenderer.Render(_inputTemplate, context);
        if (rendered.HasUnresolvedTokens)
        {
            var missing = string.Join(", ", rendered.UnresolvedTokens.OrderBy(x => x));
            throw new InvalidOperationException(
                $"Node '{Name}' has unresolved template tokens: {missing}");
        }

        var system =
            $"You are {_role}.\n" +
            _instructions +
            "\nReturn concise, structured output. Avoid placeholders.";

        var response = await context.Llm.CompleteAsync(
            system,
            rendered.RenderedText,
            _temperature);

        var result = string.IsNullOrWhiteSpace(_nextNodeName)
            ? WorkflowNodeResult.Complete()
            : WorkflowNodeResult.Next(_nextNodeName);

        result.WithUpdate(_outputStateKey, response.Trim());
        result.WithUpdate($"{Name}.status", "completed");
        result.WithUpdate($"{Name}.updated_at", DateTimeOffset.UtcNow.ToString("O"));

        return result;
    }
}
