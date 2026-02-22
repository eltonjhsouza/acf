namespace AgentCore.Core.Workflows;

public sealed class TemplateRenderResult
{
    public TemplateRenderResult(string renderedText, IReadOnlyList<string> unresolvedTokens)
    {
        RenderedText = renderedText;
        UnresolvedTokens = unresolvedTokens;
    }

    public string RenderedText { get; }
    public IReadOnlyList<string> UnresolvedTokens { get; }
    public bool HasUnresolvedTokens => UnresolvedTokens.Count > 0;
}
