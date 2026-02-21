namespace AgentCore.Tools;
public interface ITool
{
    string Name { get; }
    Task<string> ExecuteAsync(string inputJson);
}