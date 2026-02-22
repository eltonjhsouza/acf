using AgentCore.Tools;

namespace AgentCore.LLM;

public interface ILLMClient
{
    Task<string> CompleteAsync(string system, string user, double temperature = 0.2);

    bool SupportsToolCalling => false;

    Task<ToolCallingResponse> CompleteWithToolsAsync(
        string system,
        string user,
        IReadOnlyCollection<ToolSpec> tools,
        Func<string, string, Task<string>> toolExecutor,
        double temperature = 0.2,
        int maxToolRounds = 8)
        => throw new NotSupportedException(
            $"{GetType().Name} does not support native tool calling.");
}
