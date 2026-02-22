using System.Collections.Concurrent;

namespace AgentCore.Core.Workflows;

public sealed class InMemoryWorkflowCheckpointStore : IWorkflowCheckpointStore
{
    private readonly ConcurrentDictionary<string, List<WorkflowCheckpoint>> _byRun =
        new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(WorkflowCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        var copy = Clone(checkpoint);
        var list = _byRun.GetOrAdd(copy.RunId, _ => new List<WorkflowCheckpoint>());

        lock (list)
        {
            list.Add(copy);
        }

        return Task.CompletedTask;
    }

    public Task<WorkflowCheckpoint?> GetLatestAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        if (!_byRun.TryGetValue(runId, out var list))
            return Task.FromResult<WorkflowCheckpoint?>(null);

        lock (list)
        {
            if (list.Count == 0)
                return Task.FromResult<WorkflowCheckpoint?>(null);

            var latest = list[^1];
            return Task.FromResult<WorkflowCheckpoint?>(Clone(latest));
        }
    }

    public Task<IReadOnlyList<WorkflowCheckpoint>> GetAllAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        if (!_byRun.TryGetValue(runId, out var list))
            return Task.FromResult<IReadOnlyList<WorkflowCheckpoint>>(Array.Empty<WorkflowCheckpoint>());

        lock (list)
        {
            var copy = list.Select(Clone).ToList().AsReadOnly();
            return Task.FromResult<IReadOnlyList<WorkflowCheckpoint>>(copy);
        }
    }

    private static WorkflowCheckpoint Clone(WorkflowCheckpoint checkpoint)
    {
        var stateCopy = new Dictionary<string, System.Text.Json.JsonElement>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in checkpoint.StateSnapshot)
            stateCopy[key] = value.Clone();

        return new WorkflowCheckpoint
        {
            RunId = checkpoint.RunId,
            WorkflowName = checkpoint.WorkflowName,
            NodeName = checkpoint.NodeName,
            NextNodeName = checkpoint.NextNodeName,
            StepIndex = checkpoint.StepIndex,
            Timestamp = checkpoint.Timestamp,
            IsCompleted = checkpoint.IsCompleted,
            StateSnapshot = stateCopy
        };
    }
}
