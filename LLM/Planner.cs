using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using AgentCore.LLM;
using AgentCore.Tools;

namespace AgentCore.Core;

public sealed class Planner
{
    private readonly ILLMClient _llm;
    private readonly string _instructions;
    private readonly ToolRegistry _tools;

    public Planner(ILLMClient llm, string instructions, IEnumerable<ITool> tools)
    {
        _llm = llm;
        _instructions = instructions;
        _tools = new ToolRegistry(tools);
    }

    public async Task<List<StepDefinition>> CreatePlanAsync(TaskDefinition task)
    {
        var system = _instructions + "\nYou are a strict planner. Return only JSON array.";

        var toolsBlock = BuildToolsCatalog(_tools);

        var user = $@"
            Return ONLY a JSON array of steps (no markdown).

            You can ONLY use tools from this catalog:
            {toolsBlock}

            Rules:
            - toolName MUST be exactly one of the tool names in the catalog.
            - toolInput MUST follow the JSON schema of the selected tool.
            - Keep steps minimal and executable.
            - The filesystem tool operates inside its configured root (workspace).
            - When the user says 'root directory', interpret it as the tool root (workspace root).
            - For listing the tool root, use: toolInput {{ ""action"": ""list"" }} (path omitted) OR path ""."".
            - Never use absolute paths like 'C:' or '/'.
            - Each step MUST include a non-empty description field.

            - To pass data between steps, you MUST use templates:
              - For HTTP response body from the previous step, use exactly: ""{{{{last.body}}}}""
              - For Browser HTML from the previous step, use exactly: ""{{{{last.html}}}}"" (maps to last.data.html)
            - Do NOT use placeholders like ""<<body from previous step>>"" or ""{{{{last.body}}}}"" with single braces or any other placeholder format.

            - If the user asks to save the fetched page to a file, set http toolInput maxChars to 200000 (or -1 if needed).
            - Para browser.screenshot, use path relativo como ""screenshots/page.png"".

            - When using the browser tool to fetch HTML from a URL, you MUST do:
              1) browser goto ""{{{{url}}}}""
              2) browser html
            - Never call browser html without first navigating to the target URL in the same plan.

            - If the user asks to scroll to the end / scroll até o fim, you MUST do this sequence:
                1) browser goto ""{{{{url}}}}""
                2) browser scroll_to_bottom
                3) browser wait (ms: 1500)
                4) browser html
                5) filesystem write_file with content ""{{{{last.html}}}}""
            - For Browser HTML from the previous step, use exactly: ""{{{{last.html}}}}""

            Objective:
            {task.Objective}
            ";

        var raw = await _llm.CompleteAsync(system, user, temperature: 0.2);

        var json = ExtractJsonArray(raw);

        var steps = JsonSerializer.Deserialize<List<StepDefinition>>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }
        ) ?? new();

        if (steps.Count == 0)
            throw new Exception("Planner returned no steps. Raw response:\n" + raw);

        return steps;
    }

    private static string BuildToolsCatalog(ToolRegistry registry)
    {
        var sb = new StringBuilder();
        foreach (var tool in registry.All.OrderBy(t => t.Spec.Name))
        {
            sb.AppendLine($"- Name: {tool.Spec.Name}");
            sb.AppendLine($"  Description: {tool.Spec.Description}");
            sb.AppendLine($"  Input JSON Schema: {tool.Spec.JsonSchema}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string ExtractJsonArray(string text)
    {
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end < 0 || end <= start)
            throw new Exception("No JSON array found:\n" + text);

        return text.Substring(start, end - start + 1);
    }
}