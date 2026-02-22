using AgentCore.LLM;
using AgentCore.Tools;

namespace AgentCore.Core.Workflows;

public sealed class TeamWorkflowRuntime
{
    private readonly WorkflowRunner _workflowRunner;

    public TeamWorkflowRuntime(WorkflowRunner? workflowRunner = null)
    {
        _workflowRunner = workflowRunner ?? new WorkflowRunner();
    }

    public Task<WorkflowRunResult> RunAsync(
        WorkflowDefinition workflow,
        ILLMClient llm,
        string objective,
        string runId,
        IEnumerable<ITool>? tools = null,
        WorkflowState? initialState = null,
        bool resumeFromCheckpoint = true,
        CancellationToken cancellationToken = default)
    {
        var context = new WorkflowExecutionContext(
            runId: runId,
            workflowName: workflow.Name,
            objective: objective,
            llm: llm,
            tools: tools,
            initialState: initialState);

        return _workflowRunner.RunAsync(
            workflow,
            context,
            resumeFromCheckpoint,
            cancellationToken);
    }

    public Task<WorkflowRunResult> RunResearchWriterReviewerAsync(
        TeamDefinition team,
        ILLMClient llm,
        string objective,
        string runId,
        IEnumerable<ITool>? tools = null,
        int maxRevisions = 2,
        bool resumeFromCheckpoint = true,
        CancellationToken cancellationToken = default)
    {
        var initialState = new WorkflowState();
        initialState.Set("review.output", string.Empty);
        initialState.Set("workflow.revision_count", 0);

        var workflow = ResearchWriterReviewerWorkflowFactory.Create(team, maxRevisions);
        return RunAsync(
            workflow,
            llm,
            objective,
            runId,
            tools,
            initialState,
            resumeFromCheckpoint,
            cancellationToken);
    }
}
