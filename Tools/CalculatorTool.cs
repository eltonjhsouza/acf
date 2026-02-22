using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class CalculatorTool : ITool
{
    public ToolSpec Spec => new()
    {
        Name = "calculator",
        Description = "Evaluates arithmetic expressions.",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "expression":{"type":"string","description":"Arithmetic expression, e.g. (12+4)*3/2"}
              },
              "required":["expression"]
            }
            """
    };

    public Task<string> ExecuteAsync(string inputJson)
    {
        CalcRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<CalcRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail("invalid_json", ex.Message));
        }

        if (req == null || string.IsNullOrWhiteSpace(req.Expression))
            return Task.FromResult(Fail("invalid_request", "expression is required"));

        try
        {
            var table = new DataTable();
            var value = table.Compute(req.Expression, string.Empty);
            var numeric = Convert.ToDouble(value);

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                ok = true,
                data = new
                {
                    expression = req.Expression,
                    result = numeric
                }
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail("evaluation_error", ex.Message));
        }
    }

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class CalcRequest
    {
        [JsonPropertyName("expression")]
        public string? Expression { get; set; }
    }
}
