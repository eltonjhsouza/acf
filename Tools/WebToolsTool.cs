using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class WebToolsTool : ITool
{
    private readonly HttpClient _http;

    public WebToolsTool(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All
        });

        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    public ToolSpec Spec => new()
    {
        Name = "webtools",
        Description = "Web utility toolkit: expand_url, get_title.",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "action":{"type":"string","enum":["expand_url","get_title"]},
                "url":{"type":"string"}
              },
              "required":["action","url"]
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

        if (req == null || string.IsNullOrWhiteSpace(req.Action) || string.IsNullOrWhiteSpace(req.Url))
            return Fail("invalid_request", "action and url are required");

        var action = req.Action.Trim().ToLowerInvariant();
        var url = NormalizeUrl(req.Url.Trim());

        try
        {
            return action switch
            {
                "expand_url" => await ExpandUrlAsync(url),
                "get_title" => await GetTitleAsync(url),
                _ => Fail("invalid_action", $"'{action}'")
            };
        }
        catch (Exception ex)
        {
            return Fail("http_error", ex.Message);
        }
    }

    private async Task<string> ExpandUrlAsync(string url)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;

        return Success(new
        {
            original = url,
            expanded = finalUrl,
            status = (int)response.StatusCode
        });
    }

    private async Task<string> GetTitleAsync(string url)
    {
        using var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var title = ExtractTitle(html);

        return Success(new
        {
            url = response.RequestMessage?.RequestUri?.ToString() ?? url,
            title
        });
    }

    private static string ExtractTitle(string html)
    {
        const string open = "<title>";
        const string close = "</title>";

        var start = html.IndexOf(open, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return string.Empty;

        start += open.Length;
        var end = html.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            return string.Empty;

        return WebUtility.HtmlDecode(html[start..end]).Trim();
    }

    private static string NormalizeUrl(string raw)
    {
        if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return raw;

        return "https://" + raw;
    }

    private static string Success(object data)
        => JsonSerializer.Serialize(new { ok = true, data });

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class Request
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
