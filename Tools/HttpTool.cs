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
        Description = "Performs HTTP requests (GET/POST). Returns status code and response body (truncated).",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "method":{"type":"string","enum":["GET","POST"]},
                "url":{"type":"string"},
                "headers":{"type":"object","additionalProperties":{"type":"string"}},
                "body":{"type":"string"},
                "contentType":{"type":"string","description":"e.g. application/json"}
              },
              "required":["method","url"]
            }
            """
    };

    public async Task<string> ExecuteAsync(string inputJson)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
            return "Invalid request: empty inputJson.";

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
            return $"Invalid JSON: {ex.Message}";
        }

        if (req == null)
            return "Invalid request: could not parse JSON.";

        var method = (req.Method ?? "").Trim().ToUpperInvariant();
        var url = (req.Url ?? "").Trim();

        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(url))
            return "Invalid request: method and url are required.";

        if (method is not "GET" and not "POST")
            return "Invalid request: method must be GET or POST.";

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

            // Trunca para não explodir contexto
            const int max = 4000;
            if (respBody.Length > max)
                respBody = respBody.Substring(0, max) + "...(truncated)";

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
            return $"HTTP error: {ex.Message}";
        }
    }

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
    }
}