namespace AgentCore.Core.Workflows;

public sealed class WorkflowRunner
{
    private readonly IWorkflowCheckpointStore _checkpointStore;
    private readonly IWorkflowLogger? _logger;
    private readonly int _maxSteps;

    public WorkflowRunner(
        IWorkflowCheckpointStore? checkpointStore = null,
        int maxSteps = 64,
        IWorkflowLogger? logger = null)
    {
        _checkpointStore = checkpointStore ?? new InMemoryWorkflowCheckpointStore();
        _maxSteps = maxSteps <= 0 ? 64 : maxSteps;
        _logger = logger;
    }

    public async Task<WorkflowRunResult> RunAsync(
        WorkflowDefinition workflow,
        WorkflowExecutionContext context,
        bool resumeFromCheckpoint = true,
        CancellationToken cancellationToken = default)
    {
        workflow.Validate();

        await LogAsync(context, "run_started", "Workflow execution started.", workflow.StartNodeName, new
        {
            resumeFromCheckpoint
        }, cancellationToken);

        var currentNodeName = workflow.StartNodeName;
        var stepIndex = 0;

        if (resumeFromCheckpoint)
        {
            var latest = await _checkpointStore.GetLatestAsync(context.RunId, cancellationToken);
            if (latest != null &&
                latest.WorkflowName.Equals(workflow.Name, StringComparison.OrdinalIgnoreCase))
            {
                context.State.LoadSnapshot(latest.StateSnapshot);

                await LogAsync(context, "checkpoint_loaded", "Loaded latest checkpoint.", latest.NodeName, new
                {
                    latest.StepIndex,
                    latest.IsCompleted,
                    latest.NextNodeName
                }, cancellationToken);

                if (latest.IsCompleted)
                {
                    await LogAsync(context, "run_skipped", "Run already completed in checkpoint.", latest.NodeName, null, cancellationToken);

                    return new WorkflowRunResult
                    {
                        RunId = context.RunId,
                        WorkflowName = workflow.Name,
                        IsCompleted = true,
                        FinalNodeName = latest.NodeName,
                        StepsExecuted = latest.StepIndex,
                        FinalState = context.State,
                        FinishedAt = latest.Timestamp
                    };
                }

                stepIndex = latest.StepIndex;
                currentNodeName = latest.NextNodeName ?? latest.NodeName;
            }
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (stepIndex >= _maxSteps)
                throw new InvalidOperationException(
                    $"Workflow '{workflow.Name}' exceeded max steps ({_maxSteps}).");

            var node = workflow.GetNodeOrThrow(currentNodeName);

            await LogAsync(context, "node_started", "Node execution started.", node.Name, new
            {
                stepIndex
            }, cancellationToken);

            var nodeResult = await node.ExecuteAsync(context, cancellationToken);

            context.State.Merge(nodeResult.StateUpdates);
            stepIndex++;

            await LogAsync(context, "node_completed", "Node execution completed.", node.Name, new
            {
                stepIndex,
                next = nodeResult.NextNodeName,
                nodeResult.IsCompleted
            }, cancellationToken);

            var nextNodeName = nodeResult.IsCompleted ? null : nodeResult.NextNodeName;
            var checkpoint = new WorkflowCheckpoint
            {
                RunId = context.RunId,
                WorkflowName = workflow.Name,
                NodeName = node.Name,
                NextNodeName = nextNodeName,
                StepIndex = stepIndex,
                Timestamp = DateTimeOffset.UtcNow,
                IsCompleted = nodeResult.IsCompleted || string.IsNullOrWhiteSpace(nextNodeName),
                StateSnapshot = context.State.Snapshot()
            };

            await _checkpointStore.SaveAsync(checkpoint, cancellationToken);

            await LogAsync(context, "checkpoint_saved", "Checkpoint saved.", node.Name, new
            {
                checkpoint.StepIndex,
                checkpoint.IsCompleted,
                checkpoint.NextNodeName
            }, cancellationToken);

            if (checkpoint.IsCompleted)
            {
                await LogAsync(context, "run_completed", "Workflow execution completed.", node.Name, new
                {
                    stepIndex
                }, cancellationToken);

                return new WorkflowRunResult
                {
                    RunId = context.RunId,
                    WorkflowName = workflow.Name,
                    IsCompleted = true,
                    FinalNodeName = node.Name,
                    StepsExecuted = stepIndex,
                    FinalState = context.State,
                    FinishedAt = checkpoint.Timestamp
                };
            }

            currentNodeName = nextNodeName!;
        }
    }

    private Task LogAsync(
        WorkflowExecutionContext context,
        string eventType,
        string message,
        string? nodeName,
        object? data,
        CancellationToken cancellationToken)
    {
        if (_logger == null)
            return Task.CompletedTask;

        return _logger.LogAsync(new WorkflowLogEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            RunId = context.RunId,
            WorkflowName = context.WorkflowName,
            EventType = eventType,
            Message = message,
            NodeName = nodeName,
            Data = data
        }, cancellationToken);
    }
}
