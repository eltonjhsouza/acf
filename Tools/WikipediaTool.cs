using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class WikipediaTool : ITool
{
    private readonly HttpClient _http;

    public WikipediaTool(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    public ToolSpec Spec => new()
    {
        Name = "wikipedia",
        Description = "Searches Wikipedia and fetches page summaries.",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "action":{"type":"string","enum":["search","summary"]},
                "query":{"type":"string"},
                "title":{"type":"string"},
                "limit":{"type":"integer"}
              },
              "required":["action"]
            }
            """
    };

    public async Task<string> ExecuteAsync(string inputJson)
    {
        WikiRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<WikiRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Fail("invalid_json", ex.Message);
        }

        if (req == null || string.IsNullOrWhiteSpace(req.Action))
            return Fail("invalid_request", "action is required");

        try
        {
            return req.Action.Trim().ToLowerInvariant() switch
            {
                "search" => await SearchAsync(req.Query, req.Limit ?? 5),
                "summary" => await SummaryAsync(req.Title),
                _ => Fail("invalid_action", $"'{req.Action}'")
            };
        }
        catch (Exception ex)
        {
            return Fail("http_error", ex.Message);
        }
    }

    private async Task<string> SearchAsync(string? query, int limit)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Fail("invalid_request", "query is required for search");

        limit = Math.Clamp(limit, 1, 20);
        var url =
            "https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch=" +
            Uri.EscapeDataString(query.Trim()) +
            "&format=json&srlimit=" + limit;

        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);

        var results = new List<object>();
        if (doc.RootElement.TryGetProperty("query", out var queryEl) &&
            queryEl.TryGetProperty("search", out var searchEl) &&
            searchEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in searchEl.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : null;
                var snippet = item.TryGetProperty("snippet", out var snippetEl) ? snippetEl.GetString() : null;

                results.Add(new
                {
                    title,
                    snippet,
                    url = title == null ? null :
                        "https://en.wikipedia.org/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_'))
                });
            }
        }

        return Success(new
        {
            query,
            results
        });
    }

    private async Task<string> SummaryAsync(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Fail("invalid_request", "title is required for summary");

        var url = "https://en.wikipedia.org/api/rest_v1/page/summary/" + Uri.EscapeDataString(title.Trim());
        var json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);

        var extract = doc.RootElement.TryGetProperty("extract", out var extractEl) ? extractEl.GetString() : null;
        var pageTitle = doc.RootElement.TryGetProperty("title", out var titleEl) ? titleEl.GetString() : title;
        var pageUrl = doc.RootElement.TryGetProperty("content_urls", out var cuEl) &&
                      cuEl.TryGetProperty("desktop", out var deskEl) &&
                      deskEl.TryGetProperty("page", out var pageEl)
            ? pageEl.GetString()
            : null;

        return Success(new
        {
            title = pageTitle,
            summary = extract,
            url = pageUrl
        });
    }

    private static string Success(object data)
        => JsonSerializer.Serialize(new { ok = true, data });

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class WikiRequest
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("query")]
        public string? Query { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("limit")]
        public int? Limit { get; set; }
    }
}
