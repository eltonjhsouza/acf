namespace AgentCore.Core.Workflows;

public sealed class WorkflowLogEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string RunId { get; init; }
    public required string WorkflowName { get; init; }
    public required string EventType { get; init; }
    public required string Message { get; init; }
    public string? NodeName { get; init; }
    public object? Data { get; init; }
}
