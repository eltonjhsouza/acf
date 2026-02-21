using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AgentCore.LLM;
public sealed class OpenAiChatClient : ILLMClient
{
    private readonly HttpClient _http;
    private readonly string _model;

    public OpenAiChatClient(string apiKey, string model = "gpt-4.1")
    {
        _model = model;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> CompleteAsync(string system, string user, double temperature = 0.2)
    {
        var body = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature
        };

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.PostAsync("https://api.openai.com/v1/chat/completions", content);
        resp.EnsureSuccessStatusCode();

        var respJson = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(respJson);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}