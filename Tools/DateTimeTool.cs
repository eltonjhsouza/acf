using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class DateTimeTool : ITool
{
    public ToolSpec Spec => new()
    {
        Name = "datetime",
        Description = "Returns or manipulates date/time values.",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "action":{"type":"string","enum":["now","format","add_days"]},
                "value":{"type":"string","description":"Date/time value (ISO)"},
                "format":{"type":"string","description":"Date format pattern"},
                "days":{"type":"integer","description":"Days to add"}
              },
              "required":["action"]
            }
            """
    };

    public Task<string> ExecuteAsync(string inputJson)
    {
        DateTimeRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<DateTimeRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail("invalid_json", ex.Message));
        }

        var action = req?.Action?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(action))
            return Task.FromResult(Fail("invalid_request", "action is required"));

        try
        {
            return action switch
            {
                "now" => Task.FromResult(Success(new
                {
                    utc = DateTimeOffset.UtcNow.ToString("O"),
                    local = DateTimeOffset.Now.ToString("O")
                })),

                "format" => Task.FromResult(HandleFormat(req!)),
                "add_days" => Task.FromResult(HandleAddDays(req!)),
                _ => Task.FromResult(Fail("invalid_action", $"'{action}'"))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail("error", ex.Message));
        }
    }

    private static string HandleFormat(DateTimeRequest req)
    {
        if (!TryParseDate(req.Value, out var dt))
            return Fail("invalid_request", "value must be a valid date/time");

        var format = string.IsNullOrWhiteSpace(req.Format) ? "O" : req.Format;
        return Success(new
        {
            input = req.Value,
            formatted = dt.ToString(format, CultureInfo.InvariantCulture)
        });
    }

    private static string HandleAddDays(DateTimeRequest req)
    {
        if (!TryParseDate(req.Value, out var dt))
            return Fail("invalid_request", "value must be a valid date/time");

        var days = req.Days ?? 0;
        var updated = dt.AddDays(days);
        return Success(new
        {
            input = dt.ToString("O"),
            days,
            result = updated.ToString("O")
        });
    }

    private static bool TryParseDate(string? value, out DateTimeOffset date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = default;
            return false;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out date);
    }

    private static string Success(object data)
        => JsonSerializer.Serialize(new { ok = true, data });

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class DateTimeRequest
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("format")]
        public string? Format { get; set; }

        [JsonPropertyName("days")]
        public int? Days { get; set; }
    }
}
