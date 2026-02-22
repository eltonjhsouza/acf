namespace AgentCore.Core.Workflows;

public sealed class CompositeWorkflowLogger : IWorkflowLogger
{
    private readonly IReadOnlyList<IWorkflowLogger> _loggers;

    public CompositeWorkflowLogger(params IWorkflowLogger[] loggers)
    {
        _loggers = loggers ?? Array.Empty<IWorkflowLogger>();
    }

    public async Task LogAsync(WorkflowLogEntry entry, CancellationToken cancellationToken = default)
    {
        foreach (var logger in _loggers)
            await logger.LogAsync(entry, cancellationToken);
    }
}
