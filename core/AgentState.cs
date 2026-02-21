namespace AgentCore.Core;
public class AgentState
{
    public List<StepDefinition> Steps { get; set; } = new();
    public int CurrentStepIndex { get; set; }
    public bool IsCompleted { get; set; }
    public string WorkingDirectory { get; set; } = ".";
}