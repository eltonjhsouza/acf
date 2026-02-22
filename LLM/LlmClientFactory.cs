namespace AgentCore.LLM;

public static class LlmClientFactory
{
    public static ILLMClient CreateFromEnvironment()
    {
        var provider = Environment.GetEnvironmentVariable("LLM_PROVIDER") ?? "openai";
        var model = Environment.GetEnvironmentVariable("LLM_MODEL");
        return Create(provider, model);
    }

    public static ILLMClient Create(string provider, string? model = null)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("provider cannot be empty.");

        var normalized = provider.Trim().ToLowerInvariant();

        return normalized switch
        {
            "openai" => new OpenAiChatClient(
                apiKey: GetRequiredEnv("OPENAI_API_KEY"),
                model: model ?? Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4.1"),

            "anthropic" => new AnthropicChatClient(
                apiKey: GetRequiredEnv("ANTHROPIC_API_KEY"),
                model: model ?? Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-3-5-sonnet-latest"),

            "gemini" => new GeminiChatClient(
                apiKey: GetRequiredEnv("GEMINI_API_KEY"),
                model: model ?? Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-1.5-pro"),

            "openrouter" => BuildOpenRouter(model),
            "groq" => BuildGroq(model),
            "ollama" => BuildOllama(model),

            _ => throw new InvalidOperationException(
                $"Unsupported LLM provider '{provider}'. Supported: openai, anthropic, gemini, openrouter, groq, ollama.")
        };
    }

    private static ILLMClient BuildOpenRouter(string? model)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var referer = Environment.GetEnvironmentVariable("OPENROUTER_SITE_URL");
        var appName = Environment.GetEnvironmentVariable("OPENROUTER_APP_NAME");

        if (!string.IsNullOrWhiteSpace(referer))
            headers["HTTP-Referer"] = referer;
        if (!string.IsNullOrWhiteSpace(appName))
            headers["X-Title"] = appName;

        return new OpenAiCompatibleChatClient(
            baseUrl: "https://openrouter.ai/api/v1",
            model: model ?? Environment.GetEnvironmentVariable("OPENROUTER_MODEL") ?? "openai/gpt-4o-mini",
            apiKey: GetRequiredEnv("OPENROUTER_API_KEY"),
            defaultHeaders: headers);
    }

    private static ILLMClient BuildGroq(string? model)
    {
        return new OpenAiCompatibleChatClient(
            baseUrl: "https://api.groq.com/openai/v1",
            model: model ?? Environment.GetEnvironmentVariable("GROQ_MODEL") ?? "llama-3.3-70b-versatile",
            apiKey: GetRequiredEnv("GROQ_API_KEY"));
    }

    private static ILLMClient BuildOllama(string? model)
    {
        var baseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434/v1";
        var apiKey = Environment.GetEnvironmentVariable("OLLAMA_API_KEY");

        return new OpenAiCompatibleChatClient(
            baseUrl: baseUrl,
            model: model ?? Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama3.1",
            apiKey: string.IsNullOrWhiteSpace(apiKey) ? null : apiKey);
    }

    private static string GetRequiredEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Environment variable '{name}' is required.");

        return value.Trim();
    }
}
