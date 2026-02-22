namespace AgentCore.Core.Workflows;

public interface IWorkflowLogger
{
    Task LogAsync(WorkflowLogEntry entry, CancellationToken cancellationToken = default);
}
