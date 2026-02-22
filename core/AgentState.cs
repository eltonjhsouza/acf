using System.Text.Json;

namespace AgentCore.Core;
public class AgentState
{
    public List<StepDefinition> Steps { get; set; } = new();
    public int CurrentStepIndex { get; set; }
    public bool IsCompleted { get; set; }
    public string WorkingDirectory { get; set; } = ".";
    public string LastResultRaw { get; set; } = "";
    public JsonDocument? LastResultJson { get; set; }
}