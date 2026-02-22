namespace AgentCore.Core.Workflows;

public sealed class HumanApprovalNode : IWorkflowNode
{
    private readonly string _promptTemplate;
    private readonly string _approvedNextNode;
    private readonly string? _rejectedNextNode;
    private readonly string _decisionKey;

    public HumanApprovalNode(
        string name,
        string promptTemplate,
        string approvedNextNode,
        string? rejectedNextNode = null,
        string decisionKey = "human.approval")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name cannot be empty.");

        if (string.IsNullOrWhiteSpace(promptTemplate))
            throw new ArgumentException("promptTemplate cannot be empty.");

        if (string.IsNullOrWhiteSpace(approvedNextNode))
            throw new ArgumentException("approvedNextNode cannot be empty.");

        Name = name.Trim();
        _promptTemplate = promptTemplate;
        _approvedNextNode = approvedNextNode.Trim();
        _rejectedNextNode = rejectedNextNode?.Trim();
        _decisionKey = decisionKey.Trim();
    }

    public string Name { get; }

    public Task<WorkflowNodeResult> ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var rendered = StateTemplateRenderer.Render(_promptTemplate, context);
        if (rendered.HasUnresolvedTokens)
        {
            var missing = string.Join(", ", rendered.UnresolvedTokens.OrderBy(x => x));
            throw new InvalidOperationException(
                $"Human approval node '{Name}' has unresolved tokens: {missing}");
        }

        Console.WriteLine();
        Console.WriteLine($"[HUMAN-IN-THE-LOOP] {rendered.RenderedText}");
        Console.Write("Approve? (yes/no): ");
        var input = Console.ReadLine()?.Trim();
        var approved = string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(input, "y", StringComparison.OrdinalIgnoreCase);

        if (!approved && string.IsNullOrWhiteSpace(_rejectedNextNode))
        {
            var complete = WorkflowNodeResult.Complete()
                .WithUpdate(_decisionKey, "rejected")
                .WithUpdate("workflow.status", "stopped_by_human");

            return Task.FromResult(complete);
        }

        var next = approved ? _approvedNextNode : _rejectedNextNode!;
        var result = WorkflowNodeResult.Next(next)
            .WithUpdate(_decisionKey, approved ? "approved" : "rejected");

        return Task.FromResult(result);
    }
}
