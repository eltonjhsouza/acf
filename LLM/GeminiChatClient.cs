using System.Text;
using System.Text.Json;

namespace AgentCore.LLM;

public sealed class GeminiChatClient : ILLMClient
{
    private readonly HttpClient _http;
    private readonly string _url;

    public GeminiChatClient(
        string apiKey,
        string model = "gemini-1.5-pro",
        string baseUrl = "https://generativelanguage.googleapis.com/v1beta")
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Gemini apiKey cannot be empty.");

        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Gemini model cannot be empty.");

        _http = new HttpClient();
        _url = $"{baseUrl.TrimEnd('/')}/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey.Trim())}";
    }

    public async Task<string> CompleteAsync(string system, string user, double temperature = 0.2)
    {
        var body = new Dictionary<string, object?>
        {
            ["system_instruction"] = new Dictionary<string, object?>
            {
                ["parts"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["text"] = system
                    }
                }
            },
            ["contents"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["text"] = user
                        }
                    }
                }
            },
            ["generationConfig"] = new Dictionary<string, object?>
            {
                ["temperature"] = temperature
            }
        };

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.PostAsync(_url, content);
        resp.EnsureSuccessStatusCode();

        var respJson = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(respJson);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var contentEl) ||
            contentEl.ValueKind != JsonValueKind.Object ||
            !contentEl.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var textParts = new List<string>();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                textParts.Add(textEl.GetString() ?? string.Empty);
        }

        return string.Join("\n", textParts).Trim();
    }
}
