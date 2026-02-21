using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AgentCore.LLM;
public class LlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public LlmClient(string apiKey)
    {
        _apiKey = apiKey;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<string> AskAsync(string prompt)
    {
        var requestBody = new
        {
            model = "gpt-4.1",
            messages = new[]
            {
                new { role = "system", content = "You are a structured planning AI." },
                new { role = "user", content = prompt }
            },
            temperature = 0.2
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            content);

        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseString);

        return doc.RootElement
                  .GetProperty("choices")[0]
                  .GetProperty("message")
                  .GetProperty("content")
                  .GetString();
    }
}