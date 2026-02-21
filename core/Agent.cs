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
            throw new InvalidOperationException("No tools were added. Use .WithTool(new FileSystemTool()) first.");

        var planner = new Planner(_llm, _instructions);
        var orchestrator = new AgentOrchestrator(planner, _tools);

        await orchestrator.RunAsync(new TaskDefinition
        {
            Objective = objective
        });
    }
}