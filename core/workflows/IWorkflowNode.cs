namespace AgentCore.Core.Workflows;

public interface IWorkflowNode
{
    string Name { get; }

    Task<WorkflowNodeResult> ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default);
}
