using AgentCore.Core;
using System.Text.Json;

namespace AgentCore.LLM;

public sealed class Planner
{
    private readonly ILLMClient _llm;
    private readonly string _instructions;

    public Planner(ILLMClient llm, string instructions)
    {
        _llm = llm;
        _instructions = instructions;
    }

    public async Task<List<StepDefinition>> CreatePlanAsync(TaskDefinition task)
    {
        var system = _instructions + "\nYou are a strict planner. Return only JSON array.";

        var user = $@"
Return ONLY a JSON array of steps (no markdown).

You have ONLY these tools:
- filesystem

Each step must have:
- order (int)
- description (string)
- toolName (string) -> MUST be ""filesystem""
- toolInput (object) with:
   - action: one of [create_directory, write_file, read_file, list]
   - path: string (required)
   - content: string (required only for write_file)

Objective:
{task.Objective}
";

        var raw = await _llm.CompleteAsync(system, user, temperature: 0.2);

        var json = ExtractJsonArray(raw);

        var steps = JsonSerializer.Deserialize<List<StepDefinition>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? new();

        if (steps.Count == 0)
            throw new Exception("Planner returned no steps. Raw response:\n" + raw);

        return steps;
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