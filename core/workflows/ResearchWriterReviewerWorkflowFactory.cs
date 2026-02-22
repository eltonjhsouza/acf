namespace AgentCore.Core.Workflows;

public static class ResearchWriterReviewerWorkflowFactory
{
    public static WorkflowDefinition Create(TeamDefinition team, int maxRevisions = 2)
    {
        if (team == null)
            throw new ArgumentNullException(nameof(team));

        var researcher = team.GetMemberByKeyword("research");
        var writer = team.GetMemberByKeyword("writer");
        var reviewer = team.GetMemberByKeyword("review");

        var workflow = new WorkflowDefinition($"{team.Name}.research_writer_reviewer")
            .AddNode(new LLMTaskNode(
                name: "research",
                role: researcher.Role,
                instructions:
                    researcher.Instructions +
                    "\nFocus on trustworthy sources, explicit assumptions, and actionable findings.",
                inputTemplate:
                    """
                    Objective:
                    {{objective}}

                    Build a compact research brief with this structure:
                    - Summary
                    - Evidence
                    - Risks and unknowns
                    - Suggested direction
                    """,
                outputStateKey: "research.output",
                nextNodeName: "writer"))
            .AddNode(new LLMTaskNode(
                name: "writer",
                role: writer.Role,
                instructions:
                    writer.Instructions +
                    "\nProduce clean Markdown and incorporate reviewer feedback when present.",
                inputTemplate:
                    """
                    Objective:
                    {{objective}}

                    Research brief:
                    {{research.output}}

                    Reviewer feedback (can be empty on first pass):
                    {{review.output}}

                    Write the current best version of the deliverable in Markdown.
                    """,
                outputStateKey: "writer.output",
                nextNodeName: "reviewer"))
            .AddNode(new LLMTaskNode(
                name: "reviewer",
                role: reviewer.Role,
                instructions:
                    reviewer.Instructions +
                    "\nReturn STRICT header decision format: DECISION: APPROVE or DECISION: REVISE.\n" +
                    "If REVISE, include section REVISION_NOTES with concise bullets.",
                inputTemplate:
                    """
                    Objective:
                    {{objective}}

                    Research brief:
                    {{research.output}}

                    Writer draft:
                    {{writer.output}}

                    Review for factual quality, clarity, and completeness.
                    """,
                outputStateKey: "review.output",
                nextNodeName: "review_router"))
            .AddNode(new ReviewRouterNode(
                name: "review_router",
                reviewOutputKey: "review.output",
                writerOutputKey: "writer.output",
                approvedNextNodeName: "human_gate",
                revisionNodeName: "writer",
                maxRevisions: maxRevisions,
                revisionCounterKey: "workflow.revision_count"))
            .AddNode(new HumanApprovalNode(
                name: "human_gate",
                promptTemplate:
                    """
                    Reviewer approved the draft.

                    Final output preview:
                    {{final.output}}

                    Do you approve publishing this output?
                    """,
                approvedNextNode: "publish",
                rejectedNextNode: "writer",
                decisionKey: "human.final_approval"))
            .AddNode(new LLMTaskNode(
                name: "publish",
                role: "Publishing Coordinator",
                instructions:
                    "Prepare the final output exactly as approved. Do not add extra commentary.",
                inputTemplate:
                    """
                    Approved final content:
                    {{final.output}}

                    Return the same content, cleaned for final delivery.
                    """,
                outputStateKey: "final.output",
                nextNodeName: null))
            .SetStart("research");

        return workflow;
    }
}
