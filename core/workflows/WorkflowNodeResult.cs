using System.Text.Json;

namespace AgentCore.Core.Workflows;

public sealed class WorkflowNodeResult
{
    public string? NextNodeName { get; private init; }
    public bool IsCompleted { get; private init; }

    public Dictionary<string, JsonElement> StateUpdates { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public static WorkflowNodeResult Next(string nextNodeName)
    {
        if (string.IsNullOrWhiteSpace(nextNodeName))
            throw new ArgumentException("nextNodeName cannot be empty.");

        return new WorkflowNodeResult { NextNodeName = nextNodeName, IsCompleted = false };
    }

    public static WorkflowNodeResult Complete()
        => new WorkflowNodeResult { IsCompleted = true };

    public WorkflowNodeResult WithUpdate<T>(string key, T value)
    {
        StateUpdates[key] = JsonSerializer.SerializeToElement(value);
        return this;
    }
}
