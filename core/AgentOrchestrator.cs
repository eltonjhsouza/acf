using AgentCore.Tools;

namespace AgentCore.Core;

public class AgentOrchestrator
{
    private readonly Planner _planner;
    private readonly ToolRegistry _tools;

    public AgentOrchestrator(Planner planner, IEnumerable<ITool> tools)
    {
        _planner = planner;
        _tools = new ToolRegistry(tools);
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

            var toolName = step.ToolName.Trim();

            // Normalização defensiva (temporária)
            if (toolName.Equals("write_file", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("mkdir", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("create_directory", StringComparison.OrdinalIgnoreCase))
            {
                toolName = "filesystem";
            }

            if (!_tools.TryGet(toolName, out var tool))
                throw new Exception($"Tool '{step.ToolName}' not found. Available: {_tools.ListNames()}");

            var inputJson = step.ToolInput.ValueKind == System.Text.Json.JsonValueKind.Undefined
                ? "{}"
                : step.ToolInput.GetRawText();

            var result = await tool.ExecuteAsync(inputJson);
            Console.WriteLine($"Result: {result}");

            state.CurrentStepIndex++;
        }

        Console.WriteLine("Task Completed.");
    }
}