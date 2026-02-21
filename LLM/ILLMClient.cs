namespace AgentCore.LLM;
public interface ILLMClient
{
    Task<string> CompleteAsync(string system, string user, double temperature = 0.2);
}