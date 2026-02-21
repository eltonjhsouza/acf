using System.Text.Json;
using AgentCore.LLM;
using AgentCore.Tools;

namespace AgentCore.Core;

public sealed class StepRepairer
{
    private readonly ILLMClient _llm;
    private readonly string _instructions;
    private readonly ToolRegistry _tools;

    public StepRepairer(ILLMClient llm, string instructions, IEnumerable<ITool> tools)
    {
        _llm = llm;
        _instructions = instructions;
        _tools = new ToolRegistry(tools);
    }

    public async Task<StepDefinition> RepairAsync(TaskDefinition task, StepDefinition badStep, string error)
    {
        var system =
            _instructions +
            "\nYou are a strict JSON fixer. Return ONLY one JSON object of a corrected step.";

        var toolsCatalog = BuildToolsCatalog(_tools);

        var user = $@"
We have a task:
{task.Objective}

A step is invalid. Fix it while preserving its intent.

Validation error:
{error}

Tool catalog (toolName must be one of these, and toolInput must follow schema):
{toolsCatalog}

Bad step JSON:
{JsonSerializer.Serialize(badStep)}

Return ONLY a corrected JSON object with exactly these fields:
{{
  ""order"": <int>,
  ""description"": <non-empty string>,
  ""toolName"": <string>,
  ""toolInput"": <object>
}}

Rules:
- description must be non-empty
- toolName must exist in catalog
- toolInput must match the schema for that tool
- Do not add extra fields
";

        var raw = await _llm.CompleteAsync(system, user, temperature: 0.0);

        var json = ExtractJsonObject(raw);

        var fixedStep = JsonSerializer.Deserialize<StepDefinition>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (fixedStep == null)
            throw new Exception("StepRepairer failed to parse the corrected step.\nRaw:\n" + raw);

        // hard guarantees
        fixedStep.Order = badStep.Order == 0 ? fixedStep.Order : badStep.Order;

        return fixedStep;
    }

    private static string BuildToolsCatalog(ToolRegistry registry)
    {
        var lines = new List<string>();
        foreach (var tool in registry.All.OrderBy(t => t.Spec.Name))
        {
            lines.Add($"- Name: {tool.Spec.Name}");
            lines.Add($"  Description: {tool.Spec.Description}");
            lines.Add($"  Input JSON Schema: {tool.Spec.JsonSchema}");
            lines.Add("");
        }
        return string.Join("\n", lines);
    }

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < 0 || end <= start)
            throw new Exception("No JSON object found in LLM response:\n" + text);

        return text.Substring(start, end - start + 1);
    }
}