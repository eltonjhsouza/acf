namespace AgentCore.Core.Workflows;

public sealed class ConsoleWorkflowLogger : IWorkflowLogger
{
    public Task LogAsync(WorkflowLogEntry entry, CancellationToken cancellationToken = default)
    {
        var node = string.IsNullOrWhiteSpace(entry.NodeName) ? "-" : entry.NodeName;
        Console.WriteLine($"[{entry.Timestamp:O}] [{entry.EventType}] run={entry.RunId} node={node} {entry.Message}");
        return Task.CompletedTask;
    }
}
