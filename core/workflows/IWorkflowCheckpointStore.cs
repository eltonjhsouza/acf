namespace AgentCore.Core.Workflows;

public interface IWorkflowCheckpointStore
{
    Task SaveAsync(WorkflowCheckpoint checkpoint, CancellationToken cancellationToken = default);

    Task<WorkflowCheckpoint?> GetLatestAsync(
        string runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowCheckpoint>> GetAllAsync(
        string runId,
        CancellationToken cancellationToken = default);
}
