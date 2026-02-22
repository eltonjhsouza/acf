namespace AgentCore.Core.Workflows;

public sealed class WorkflowRunResult
{
    public required string RunId { get; init; }
    public required string WorkflowName { get; init; }
    public required bool IsCompleted { get; init; }
    public required string FinalNodeName { get; init; }
    public required int StepsExecuted { get; init; }
    public required WorkflowState FinalState { get; init; }
    public required DateTimeOffset FinishedAt { get; init; }
}
