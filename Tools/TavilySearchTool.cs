using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class TavilySearchTool : ITool
{
    private readonly string _apiKey;
    private readonly HttpClient _http;

    public TavilySearchTool(string apiKey, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Tavily apiKey cannot be empty. Set TAVILY_API_KEY env var or pass in constructor.");

        _apiKey = apiKey.Trim();
        _http = httpClient ?? new HttpClient();
    }

    public ToolSpec Spec => new ToolSpec
    {
        Name = "tavily",
        Description = "Web search via Tavily Search API. Returns structured results (title/url/content) and optional answer.",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "query":{"type":"string","description":"Search query string."},
                "search_depth":{"type":"string","enum":["basic","advanced"],"description":"Search depth. advanced is more thorough."},
                "max_results":{"type":"integer","minimum":1,"maximum":20,"description":"Number of results to return."},
                "include_answer":{"type":"boolean","description":"Whether to include Tavily's answer summary."},
                "include_raw_content":{"type":"boolean","description":"Whether to include raw page content (may be large)."},
                "include_images":{"type":"boolean","description":"Whether to include images (if supported)."},
                "topic":{"type":"string","description":"Optional topic hint (e.g., 'news')."},
                "days":{"type":"integer","minimum":1,"maximum":30,"description":"Optional recency window in days (if supported)."},
                "include_domains":{"type":"array","items":{"type":"string"},"description":"Optional allowlist of domains."},
                "exclude_domains":{"type":"array","items":{"type":"string"},"description":"Optional blocklist of domains."}
              },
              "required":["query"]
            }
            """
    };

    public async Task<string> ExecuteAsync(string inputJson)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
            return JsonSerializer.Serialize(new { ok = false, error = "empty_input" });

        TavilyRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<TavilyRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { ok = false, error = "invalid_json", message = ex.Message });
        }

        if (req == null || string.IsNullOrWhiteSpace(req.Query))
            return JsonSerializer.Serialize(new { ok = false, error = "missing_query" });

        // Build request body (Tavily expects JSON)
        var payload = new Dictionary<string, object?>
        {
            ["query"] = req.Query,
            ["search_depth"] = string.IsNullOrWhiteSpace(req.SearchDepth) ? "advanced" : req.SearchDepth,
            ["max_results"] = req.MaxResults ?? 5,
            ["include_answer"] = req.IncludeAnswer ?? true,
            ["include_raw_content"] = req.IncludeRawContent ?? false,
            ["include_images"] = req.IncludeImages ?? false
        };

        if (!string.IsNullOrWhiteSpace(req.Topic)) payload["topic"] = req.Topic;
        if (req.Days is not null) payload["days"] = req.Days;
        if (req.IncludeDomains is { Count: > 0 }) payload["include_domains"] = req.IncludeDomains;
        if (req.ExcludeDomains is { Count: > 0 }) payload["exclude_domains"] = req.ExcludeDomains;

        var json = JsonSerializer.Serialize(payload);

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search");
        httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpReq.Content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var resp = await _http.SendAsync(httpReq);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new
                {
                    ok = false,
                    status = (int)resp.StatusCode,
                    reason = resp.ReasonPhrase,
                    body
                });
            }

            // Return Tavily response as-is inside our envelope
            // (Keeps it easy for the planner/agent to use.)
            using var doc = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(new
            {
                ok = true,
                data = doc.RootElement
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { ok = false, error = "http_error", message = ex.Message });
        }
    }

    private sealed class TavilyRequest
    {
        [JsonPropertyName("query")]
        public string? Query { get; set; }

        [JsonPropertyName("search_depth")]
        public string? SearchDepth { get; set; }

        [JsonPropertyName("max_results")]
        public int? MaxResults { get; set; }

        [JsonPropertyName("include_answer")]
        public bool? IncludeAnswer { get; set; }

        [JsonPropertyName("include_raw_content")]
        public bool? IncludeRawContent { get; set; }

        [JsonPropertyName("include_images")]
        public bool? IncludeImages { get; set; }

        [JsonPropertyName("topic")]
        public string? Topic { get; set; }

        [JsonPropertyName("days")]
        public int? Days { get; set; }

        [JsonPropertyName("include_domains")]
        public List<string>? IncludeDomains { get; set; }

        [JsonPropertyName("exclude_domains")]
        public List<string>? ExcludeDomains { get; set; }
    }
}