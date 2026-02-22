using System.Text.Json;

namespace AgentCore.Core.Workflows;

public sealed class WorkflowCheckpoint
{
    public required string RunId { get; init; }
    public required string WorkflowName { get; init; }
    public required string NodeName { get; init; }
    public string? NextNodeName { get; init; }
    public required int StepIndex { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required bool IsCompleted { get; init; }
    public required Dictionary<string, JsonElement> StateSnapshot { get; init; }
}
