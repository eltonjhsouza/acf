using System.Text.Json;
using AgentCore.LLM;
using AgentCore.Tools;

namespace AgentCore.Core;

public sealed class Agent
{
    private readonly string _name;
    private readonly string _instructions;
    private readonly ILLMClient _llm;
    private readonly List<ITool> _tools;

    private Agent(string name, string instructions, ILLMClient llm, List<ITool>? tools = null)
    {
        _name = name;
        _instructions = instructions;
        _llm = llm;
        _tools = tools ?? new List<ITool>();
    }

    // API principal (estilo framework)
    public static Agent Create(string name, ILLMClient llm)
        => new Agent(name, "You are a helpful agent.", llm);

    public Agent WithInstructions(string instructions)
    {
        // Cria um novo Agent, mantendo as tools já adicionadas
        return new Agent(_name, instructions, _llm, new List<ITool>(_tools));
    }

    public Agent WithTool(ITool tool)
    {
        _tools.Add(tool);
        return this;
    }

    public async Task RunAsync(string objective)
    {
        if (_tools.Count == 0)
            throw new InvalidOperationException("No tools were added. Use .WithTool(...) first.");

        var task = new TaskDefinition { Objective = objective };

        var planner = new Planner(_llm, _instructions, _tools);
        var repairer = new StepRepairer(_llm, _instructions, _tools);

        var orchestrator = new AgentOrchestrator(planner, _tools, repairer);

        await orchestrator.RunAsync(task);
    }

    public async Task<string> RunWithNativeToolCallingAsync(string objective, int maxToolRounds = 8)
    {
        if (!_llm.SupportsToolCalling)
            throw new InvalidOperationException(
                $"LLM client '{_llm.GetType().Name}' does not support native tool calling.");

        if (_tools.Count == 0)
            throw new InvalidOperationException("No tools were added. Use .WithTool(...) first.");

        var registry = new ToolRegistry(_tools);
        var specs = _tools.Select(t => t.Spec).ToArray();

        var response = await _llm.CompleteWithToolsAsync(
            system: _instructions,
            user: objective,
            tools: specs,
            toolExecutor: async (toolName, argsJson) =>
            {
                if (!registry.TryGet(toolName, out var tool))
                {
                    return JsonSerializer.Serialize(new
                    {
                        ok = false,
                        error = "tool_not_found",
                        name = toolName,
                        available = registry.ListNames()
                    });
                }

                return await tool.ExecuteAsync(argsJson);
            },
            temperature: 0.2,
            maxToolRounds: maxToolRounds);

        return response.FinalText;
    }
}
