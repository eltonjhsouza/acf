using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class DuckDuckGoTool : ITool
{
    private readonly HttpClient _http;

    public DuckDuckGoTool(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    public ToolSpec Spec => new()
    {
        Name = "duckduckgo",
        Description = "Searches DuckDuckGo instant answer API.",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "query":{"type":"string"}
              },
              "required":["query"]
            }
            """
    };

    public async Task<string> ExecuteAsync(string inputJson)
    {
        Request? req;
        try
        {
            req = JsonSerializer.Deserialize<Request>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Fail("invalid_json", ex.Message);
        }

        if (req == null || string.IsNullOrWhiteSpace(req.Query))
            return Fail("invalid_request", "query is required");

        try
        {
            var url =
                "https://api.duckduckgo.com/?format=json&no_html=1&skip_disambig=1&q=" +
                Uri.EscapeDataString(req.Query.Trim());

            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var abstractText = doc.RootElement.TryGetProperty("AbstractText", out var absEl)
                ? absEl.GetString()
                : null;
            var heading = doc.RootElement.TryGetProperty("Heading", out var headingEl)
                ? headingEl.GetString()
                : null;
            var answer = doc.RootElement.TryGetProperty("Answer", out var answerEl)
                ? answerEl.GetString()
                : null;

            var related = new List<object>();
            if (doc.RootElement.TryGetProperty("RelatedTopics", out var relatedEl) &&
                relatedEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var topic in relatedEl.EnumerateArray().Take(5))
                {
                    if (topic.TryGetProperty("Text", out var textEl))
                    {
                        related.Add(new
                        {
                            text = textEl.GetString(),
                            firstUrl = topic.TryGetProperty("FirstURL", out var urlEl) ? urlEl.GetString() : null
                        });
                    }
                }
            }

            return JsonSerializer.Serialize(new
            {
                ok = true,
                data = new
                {
                    query = req.Query,
                    heading,
                    answer,
                    abstractText,
                    related
                }
            });
        }
        catch (Exception ex)
        {
            return Fail("http_error", ex.Message);
        }
    }

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class Request
    {
        [JsonPropertyName("query")]
        public string? Query { get; set; }
    }
}
