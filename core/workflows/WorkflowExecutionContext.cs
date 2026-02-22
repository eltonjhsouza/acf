using AgentCore.LLM;
using AgentCore.Tools;

namespace AgentCore.Core.Workflows;

public sealed class WorkflowExecutionContext
{
    public WorkflowExecutionContext(
        string runId,
        string workflowName,
        string objective,
        ILLMClient llm,
        IEnumerable<ITool>? tools = null,
        WorkflowState? initialState = null)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("runId cannot be empty.");

        if (string.IsNullOrWhiteSpace(workflowName))
            throw new ArgumentException("workflowName cannot be empty.");

        if (string.IsNullOrWhiteSpace(objective))
            throw new ArgumentException("objective cannot be empty.");

        RunId = runId.Trim();
        WorkflowName = workflowName.Trim();
        Objective = objective.Trim();
        Llm = llm ?? throw new ArgumentNullException(nameof(llm));
        Tools = new ToolRegistry(tools ?? Array.Empty<ITool>());
        State = initialState ?? new WorkflowState();
        StartedAt = DateTimeOffset.UtcNow;

        if (!State.ContainsKey("objective"))
            State.Set("objective", Objective);
    }

    public string RunId { get; }
    public string WorkflowName { get; }
    public string Objective { get; }
    public ILLMClient Llm { get; }
    public ToolRegistry Tools { get; }
    public WorkflowState State { get; }
    public DateTimeOffset StartedAt { get; }
}
