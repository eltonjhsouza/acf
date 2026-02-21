namespace AgentCore.Tools;

public interface ITool
{
    ToolSpec Spec { get; }
    Task<string> ExecuteAsync(string inputJson);
}