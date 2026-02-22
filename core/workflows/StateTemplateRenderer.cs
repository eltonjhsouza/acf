using System.Text.RegularExpressions;

namespace AgentCore.Core.Workflows;

public static class StateTemplateRenderer
{
    private static readonly Regex TokenRegex = new(
        "{{\\s*([a-zA-Z0-9_.-]+)\\s*}}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static TemplateRenderResult Render(string template, WorkflowExecutionContext context)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        var unresolved = new List<string>();
        var rendered = TokenRegex.Replace(template, match =>
        {
            var token = match.Groups[1].Value;

            if (TryResolveToken(token, context, out var value))
                return value;

            unresolved.Add(token);
            return match.Value;
        });

        return new TemplateRenderResult(rendered, unresolved);
    }

    private static bool TryResolveToken(
        string token,
        WorkflowExecutionContext context,
        out string value)
    {
        value = string.Empty;

        if (token.Equals("objective", StringComparison.OrdinalIgnoreCase))
        {
            value = context.Objective;
            return true;
        }

        if (token.Equals("run_id", StringComparison.OrdinalIgnoreCase))
        {
            value = context.RunId;
            return true;
        }

        if (token.Equals("workflow", StringComparison.OrdinalIgnoreCase))
        {
            value = context.WorkflowName;
            return true;
        }

        var stateKey = token.StartsWith("state.", StringComparison.OrdinalIgnoreCase)
            ? token.Substring("state.".Length)
            : token;

        if (!context.State.TryGet(stateKey, out var element))
            return false;

        value = element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Number => element.GetRawText(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            System.Text.Json.JsonValueKind.Object => element.GetRawText(),
            System.Text.Json.JsonValueKind.Array => element.GetRawText(),
            _ => string.Empty
        };

        return true;
    }
}
