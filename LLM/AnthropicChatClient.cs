using System.Text;
using System.Text.Json;

namespace AgentCore.LLM;

public sealed class AnthropicChatClient : ILLMClient
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _messagesUrl;

    public AnthropicChatClient(
        string apiKey,
        string model = "claude-3-5-sonnet-latest",
        string baseUrl = "https://api.anthropic.com/v1")
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Anthropic apiKey cannot be empty.");

        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Anthropic model cannot be empty.");

        _model = model;
        _messagesUrl = baseUrl.TrimEnd('/') + "/messages";

        _http = new HttpClient();
        _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey.Trim());
        _http.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");
    }

    public async Task<string> CompleteAsync(string system, string user, double temperature = 0.2)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = _model,
            ["max_tokens"] = 4096,
            ["temperature"] = temperature,
            ["system"] = system,
            ["messages"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = user
                }
            }
        };

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.PostAsync(_messagesUrl, content);
        resp.EnsureSuccessStatusCode();

        var respJson = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(respJson);

        if (!doc.RootElement.TryGetProperty("content", out var contentArray) ||
            contentArray.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var item in contentArray.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                parts.Add(textEl.GetString() ?? string.Empty);
        }

        return string.Join("\n", parts).Trim();
    }
}
