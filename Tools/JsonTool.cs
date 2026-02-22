using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class JsonTool : ITool
{
    public ToolSpec Spec => new()
    {
        Name = "json",
        Description = "Validates, formats, minifies, and gets values from JSON.",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "action":{"type":"string","enum":["validate","pretty","minify","get"]},
                "json":{"type":"string"},
                "path":{"type":"string","description":"Dot path for get, e.g. data.items.0.name"}
              },
              "required":["action","json"]
            }
            """
    };

    public Task<string> ExecuteAsync(string inputJson)
    {
        JsonRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<JsonRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail("invalid_json", ex.Message));
        }

        if (req == null || string.IsNullOrWhiteSpace(req.Action) || string.IsNullOrWhiteSpace(req.Json))
            return Task.FromResult(Fail("invalid_request", "action and json are required"));

        var action = req.Action.Trim().ToLowerInvariant();
        try
        {
            return action switch
            {
                "validate" => Task.FromResult(Validate(req.Json)),
                "pretty" => Task.FromResult(Pretty(req.Json)),
                "minify" => Task.FromResult(Minify(req.Json)),
                "get" => Task.FromResult(GetValue(req.Json, req.Path)),
                _ => Task.FromResult(Fail("invalid_action", $"'{action}'"))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail("error", ex.Message));
        }
    }

    private static string Validate(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return Success(new { valid = true });
        }
        catch (Exception ex)
        {
            return Success(new { valid = false, message = ex.Message });
        }
    }

    private static string Pretty(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Success(new
        {
            json = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true })
        });
    }

    private static string Minify(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Success(new { json = doc.RootElement.GetRawText() });
    }

    private static string GetValue(string json, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Fail("invalid_request", "path is required for action=get");

        var node = JsonNode.Parse(json);
        if (node == null)
            return Fail("invalid_json", "could not parse json");

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (node is JsonObject obj)
            {
                node = obj[segment];
            }
            else if (node is JsonArray arr && int.TryParse(segment, out var index))
            {
                node = index >= 0 && index < arr.Count ? arr[index] : null;
            }
            else
            {
                node = null;
            }

            if (node == null)
                return Fail("not_found", $"path segment '{segment}' not found");
        }

        return Success(new
        {
            path,
            value = node.ToJsonString()
        });
    }

    private static string Success(object data)
        => JsonSerializer.Serialize(new { ok = true, data });

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class JsonRequest
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("json")]
        public string? Json { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }
    }
}
