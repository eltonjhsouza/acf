using AgentCore.LLM;
using AgentCore.Tools;
using System.Text.Json;

namespace AgentCore.Core;
public class AgentOrchestrator
{
    private readonly Planner _planner;
    private readonly Dictionary<string, ITool> _tools;

    public AgentOrchestrator(Planner planner, IEnumerable<ITool> tools)
    {
        _planner = planner;
        _tools = tools.ToDictionary(t => t.Name);
    }

    public async Task RunAsync(TaskDefinition task)
    {
        var state = new AgentState();
        state.Steps = await _planner.CreatePlanAsync(task);

        while (!state.IsCompleted)
        {
            if (state.CurrentStepIndex >= state.Steps.Count)
            {
                state.IsCompleted = true;
                break;
            }

            var step = state.Steps[state.CurrentStepIndex];

            if (string.IsNullOrWhiteSpace(step.ToolName))
                throw new Exception($"Step {state.CurrentStepIndex} has null/empty ToolName. Description: {step.Description}");

            Console.WriteLine($"Executing: {step.Description}");

            // 🔐 NORMALIZAÇÃO DE TOOL
            var toolName = step.ToolName.Trim().ToLowerInvariant();

            if (toolName is "write_file" or "mkdir" or "create_directory")
            {
                toolName = "filesystem";
            }

            if (!_tools.TryGetValue(toolName, out var tool))
            {
                throw new Exception(
                    $"Tool '{step.ToolName}' not found. Available tools: {string.Join(", ", _tools.Keys)}");
            }

            var inputJson = step.ToolInput.ValueKind == JsonValueKind.Undefined
                ? "{}"
                : step.ToolInput.GetRawText();

            var result = await tool.ExecuteAsync(inputJson);

            Console.WriteLine($"Result: {result}");

            state.CurrentStepIndex++;
        }

        Console.WriteLine("Task Completed.");
    }
}