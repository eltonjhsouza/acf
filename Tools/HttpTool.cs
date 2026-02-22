using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class HttpTool : ITool
{
    private readonly HttpClient _http;

    public HttpTool(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public ToolSpec Spec => new ToolSpec
    {
        Name = "http",
        Description = "Performs HTTP requests (GET/POST). Returns status code and response body (optionally truncated).",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "method":{"type":"string","enum":["GET","POST"]},
                "url":{"type":"string"},
                "headers":{"type":"object","additionalProperties":{"type":"string"}},
                "body":{"type":"string"},
                "contentType":{"type":"string","description":"e.g. application/json"},
                "maxChars":{"type":"integer","description":"Max characters to return in body. 0 or omitted = default 4000. -1 = no truncation (use carefully)."}
              },
              "required":["method","url"]
            }
            """
    };

    public async Task<string> ExecuteAsync(string inputJson)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
            return Fail("invalid_request", "empty inputJson");

        HttpRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<HttpRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Fail("invalid_json", ex.Message);
        }

        if (req == null)
            return Fail("invalid_request", "could not parse JSON");

        var method = (req.Method ?? "").Trim().ToUpperInvariant();
        var url = (req.Url ?? "").Trim();

        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(url))
            return Fail("invalid_request", "method and url are required");

        if (method is not "GET" and not "POST")
            return Fail("invalid_request", "method must be GET or POST");

        // truncamento configurável
        var maxChars = req.MaxChars ?? 0;   // 0 = default
        if (maxChars == 0) maxChars = 4000; // default antigo

        try
        {
            using var message = new HttpRequestMessage(
                method == "GET" ? HttpMethod.Get : HttpMethod.Post,
                url);

            if (req.Headers != null)
            {
                foreach (var kv in req.Headers)
                    message.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }

            if (method == "POST")
            {
                var contentType = string.IsNullOrWhiteSpace(req.ContentType)
                    ? "application/json"
                    : req.ContentType.Trim();

                message.Content = new StringContent(req.Body ?? "", Encoding.UTF8, contentType);
            }

            using var resp = await _http.SendAsync(message);
            var respBody = await resp.Content.ReadAsStringAsync();

            // truncar se maxChars > 0
            if (maxChars > 0 && respBody.Length > maxChars)
                respBody = respBody.Substring(0, maxChars) + "...(truncated)";

            var result = new
            {
                ok = resp.IsSuccessStatusCode,
                status = (int)resp.StatusCode,
                reason = resp.ReasonPhrase,
                body = respBody
            };

            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            return Fail("http_error", ex.Message);
        }
    }

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class HttpRequest
    {
        [JsonPropertyName("method")]
        public string? Method { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("headers")]
        public Dictionary<string, string>? Headers { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; }

        [JsonPropertyName("maxChars")]
        public int? MaxChars { get; set; }
    }
}
