using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Core;
public class StepDefinition
{
    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = "";

    [JsonPropertyName("toolInput")]
    public JsonElement ToolInput { get; set; }
}