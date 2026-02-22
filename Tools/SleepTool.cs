using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class SleepTool : ITool
{
    public ToolSpec Spec => new()
    {
        Name = "sleep",
        Description = "Pauses execution for a configured duration.",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "seconds":{"type":"number","description":"Seconds to wait"},
                "milliseconds":{"type":"integer","description":"Milliseconds to wait"}
              }
            }
            """
    };

    public async Task<string> ExecuteAsync(string inputJson)
    {
        SleepRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<SleepRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Fail("invalid_json", ex.Message);
        }

        var ms = req?.Milliseconds;
        if (ms is null)
        {
            var seconds = req?.Seconds ?? 1;
            ms = (int)Math.Round(seconds * 1000);
        }

        if (ms <= 0)
            ms = 1000;

        ms = Math.Min(ms.Value, 120000);
        await Task.Delay(ms.Value);

        return JsonSerializer.Serialize(new
        {
            ok = true,
            data = new
            {
                sleptMs = ms.Value
            }
        });
    }

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class SleepRequest
    {
        [JsonPropertyName("seconds")]
        public double? Seconds { get; set; }

        [JsonPropertyName("milliseconds")]
        public int? Milliseconds { get; set; }
    }
}
