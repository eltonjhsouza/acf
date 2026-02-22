namespace AgentCore.Core.Workflows;

public sealed class WorkflowBuilder
{
    private readonly WorkflowDefinition _workflow;

    private WorkflowBuilder(string name)
    {
        _workflow = new WorkflowDefinition(name);
    }

    public static WorkflowBuilder Create(string name) => new(name);

    public WorkflowBuilder AddLlmTask(
        string name,
        string role,
        string instructions,
        string inputTemplate,
        string outputStateKey,
        string? nextNodeName)
    {
        _workflow.AddNode(new LLMTaskNode(
            name: name,
            role: role,
            instructions: instructions,
            inputTemplate: inputTemplate,
            outputStateKey: outputStateKey,
            nextNodeName: nextNodeName));

        return this;
    }

    public WorkflowBuilder AddReviewRouter(
        string name,
        string reviewOutputKey,
        string writerOutputKey,
        string? approvedNextNodeName,
        string revisionNodeName,
        int maxRevisions = 2,
        string revisionCounterKey = "workflow.revision_count")
    {
        _workflow.AddNode(new ReviewRouterNode(
            name: name,
            reviewOutputKey: reviewOutputKey,
            writerOutputKey: writerOutputKey,
            approvedNextNodeName: approvedNextNodeName,
            revisionNodeName: revisionNodeName,
            maxRevisions: maxRevisions,
            revisionCounterKey: revisionCounterKey));

        return this;
    }

    public WorkflowBuilder AddHumanApproval(
        string name,
        string promptTemplate,
        string approvedNextNode,
        string? rejectedNextNode = null,
        string decisionKey = "human.approval")
    {
        _workflow.AddNode(new HumanApprovalNode(
            name,
            promptTemplate,
            approvedNextNode,
            rejectedNextNode,
            decisionKey));

        return this;
    }

    public WorkflowBuilder StartWith(string nodeName)
    {
        _workflow.SetStart(nodeName);
        return this;
    }

    public WorkflowDefinition Build()
    {
        _workflow.Validate();
        return _workflow;
    }
}
