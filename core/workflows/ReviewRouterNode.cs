using System.Text.RegularExpressions;

namespace AgentCore.Core.Workflows;

public sealed class ReviewRouterNode : IWorkflowNode
{
    private static readonly Regex ApproveRegex = new(
        @"DECISION\s*:\s*APPROV(E|ED)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly string _reviewOutputKey;
    private readonly string _writerOutputKey;
    private readonly string? _approvedNextNodeName;
    private readonly string _revisionNodeName;
    private readonly int _maxRevisions;
    private readonly string _revisionCounterKey;

    public ReviewRouterNode(
        string name,
        string reviewOutputKey,
        string writerOutputKey,
        string? approvedNextNodeName,
        string revisionNodeName,
        int maxRevisions = 2,
        string revisionCounterKey = "workflow.revision_count")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name cannot be empty.");

        if (string.IsNullOrWhiteSpace(reviewOutputKey))
            throw new ArgumentException("reviewOutputKey cannot be empty.");

        if (string.IsNullOrWhiteSpace(writerOutputKey))
            throw new ArgumentException("writerOutputKey cannot be empty.");

        if (string.IsNullOrWhiteSpace(revisionNodeName))
            throw new ArgumentException("revisionNodeName cannot be empty.");

        Name = name.Trim();
        _reviewOutputKey = reviewOutputKey.Trim();
        _writerOutputKey = writerOutputKey.Trim();
        _approvedNextNodeName = approvedNextNodeName?.Trim();
        _revisionNodeName = revisionNodeName.Trim();
        _maxRevisions = maxRevisions < 0 ? 0 : maxRevisions;
        _revisionCounterKey = revisionCounterKey.Trim();
    }

    public string Name { get; }

    public Task<WorkflowNodeResult> ExecuteAsync(
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var reviewText = context.State.GetString(_reviewOutputKey) ?? string.Empty;
        var writerText = context.State.GetString(_writerOutputKey) ?? string.Empty;

        var approved = ApproveRegex.IsMatch(reviewText);
        if (approved)
        {
            if (!string.IsNullOrWhiteSpace(_approvedNextNodeName))
            {
                var goNext = WorkflowNodeResult.Next(_approvedNextNodeName)
                    .WithUpdate("review.decision", "APPROVE")
                    .WithUpdate("final.output", writerText)
                    .WithUpdate("workflow.status", "awaiting_human_approval");

                return Task.FromResult(goNext);
            }

            var done = WorkflowNodeResult.Complete()
                .WithUpdate("review.decision", "APPROVE")
                .WithUpdate("final.output", writerText)
                .WithUpdate("workflow.status", "completed");

            return Task.FromResult(done);
        }

        var revisionCount = context.State.GetInt32(_revisionCounterKey) + 1;
        if (revisionCount > _maxRevisions)
        {
            var maxed = WorkflowNodeResult.Complete()
                .WithUpdate(_revisionCounterKey, revisionCount)
                .WithUpdate("review.decision", "MAX_REVISIONS_REACHED")
                .WithUpdate("final.output", writerText)
                .WithUpdate("workflow.status", "completed_with_max_revisions");

            return Task.FromResult(maxed);
        }

        var loop = WorkflowNodeResult.Next(_revisionNodeName)
            .WithUpdate(_revisionCounterKey, revisionCount)
            .WithUpdate("review.decision", "REVISE")
            .WithUpdate("workflow.status", "in_revision_loop");

        return Task.FromResult(loop);
    }
}
