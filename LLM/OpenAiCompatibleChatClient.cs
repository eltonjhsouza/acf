using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AgentCore.Tools;

namespace AgentCore.LLM;

public sealed class OpenAiCompatibleChatClient : ILLMClient
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly string _chatCompletionsUrl;

    public OpenAiCompatibleChatClient(
        string baseUrl,
        string model,
        string? apiKey = null,
        IReadOnlyDictionary<string, string>? defaultHeaders = null)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("baseUrl cannot be empty.");

        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("model cannot be empty.");

        _model = model;
        _chatCompletionsUrl = baseUrl.TrimEnd('/') + "/chat/completions";

        _http = new HttpClient();

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }

        if (defaultHeaders != null)
        {
            foreach (var (name, value) in defaultHeaders)
            {
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
                    _http.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
            }
        }
    }

    public bool SupportsToolCalling => true;

    public async Task<string> CompleteAsync(string system, string user, double temperature = 0.2)
    {
        var body = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature
        };

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.PostAsync(_chatCompletionsUrl, content);
        resp.EnsureSuccessStatusCode();

        var respJson = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(respJson);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    public async Task<ToolCallingResponse> CompleteWithToolsAsync(
        string system,
        string user,
        IReadOnlyCollection<ToolSpec> tools,
        Func<string, string, Task<string>> toolExecutor,
        double temperature = 0.2,
        int maxToolRounds = 8)
    {
        if (tools == null || tools.Count == 0)
            throw new ArgumentException("At least one tool is required for native tool calling.");

        if (toolExecutor == null)
            throw new ArgumentNullException(nameof(toolExecutor));

        var messages = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["role"] = "system",
                ["content"] = system
            },
            new()
            {
                ["role"] = "user",
                ["content"] = user
            }
        };

        var toolDefinitions = BuildToolDefinitions(tools);
        var calls = new List<ToolCallRecord>();
        var rounds = Math.Max(1, maxToolRounds);
        var lastRawResponse = string.Empty;

        for (var i = 0; i < rounds; i++)
        {
            var requestBody = new Dictionary<string, object?>
            {
                ["model"] = _model,
                ["messages"] = messages,
                ["tools"] = toolDefinitions,
                ["tool_choice"] = "auto",
                ["temperature"] = temperature
            };

            var requestJson = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using var resp = await _http.PostAsync(_chatCompletionsUrl, content);
            resp.EnsureSuccessStatusCode();

            lastRawResponse = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(lastRawResponse);

            var message = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message");

            var contentText = ExtractContent(message);
            var hasToolCalls = message.TryGetProperty("tool_calls", out var toolCallsElement) &&
                               toolCallsElement.ValueKind == JsonValueKind.Array &&
                               toolCallsElement.GetArrayLength() > 0;

            if (!hasToolCalls)
            {
                return new ToolCallingResponse
                {
                    FinalText = contentText,
                    ToolCalls = calls,
                    LastRawResponse = lastRawResponse
                };
            }

            var parsedToolCalls = new List<(string Id, string Name, string ArgsJson)>();
            var toolCallMessages = new List<object>();
            foreach (var toolCall in toolCallsElement.EnumerateArray())
            {
                var toolCallId = toolCall.GetProperty("id").GetString() ?? Guid.NewGuid().ToString("N");
                var function = toolCall.GetProperty("function");
                var toolName = function.GetProperty("name").GetString() ?? string.Empty;
                var arguments = function.TryGetProperty("arguments", out var argsElement) &&
                                argsElement.ValueKind == JsonValueKind.String
                    ? argsElement.GetString() ?? "{}"
                    : "{}";

                parsedToolCalls.Add((toolCallId, toolName, NormalizeArguments(arguments)));
                toolCallMessages.Add(new Dictionary<string, object?>
                {
                    ["id"] = toolCallId,
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object?>
                    {
                        ["name"] = toolName,
                        ["arguments"] = arguments
                    }
                });
            }

            messages.Add(new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = string.IsNullOrWhiteSpace(contentText) ? null : contentText,
                ["tool_calls"] = toolCallMessages
            });

            foreach (var toolCall in parsedToolCalls)
            {
                string toolResult;
                try
                {
                    toolResult = await toolExecutor(toolCall.Name, toolCall.ArgsJson);
                }
                catch (Exception ex)
                {
                    toolResult = JsonSerializer.Serialize(new
                    {
                        ok = false,
                        error = "tool_executor_exception",
                        message = ex.Message
                    });
                }

                calls.Add(new ToolCallRecord
                {
                    Id = toolCall.Id,
                    Name = toolCall.Name,
                    ArgumentsJson = toolCall.ArgsJson,
                    Result = toolResult
                });

                messages.Add(new Dictionary<string, object?>
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = toolCall.Id,
                    ["name"] = toolCall.Name,
                    ["content"] = toolResult
                });
            }
        }

        throw new InvalidOperationException(
            $"Tool calling exceeded max rounds ({rounds}) without a final assistant response.");
    }

    private static string ExtractContent(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var contentElement))
            return string.Empty;

        return contentElement.ValueKind switch
        {
            JsonValueKind.String => contentElement.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => contentElement.GetRawText()
        };
    }

    private static string NormalizeArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return "{}";

        try
        {
            using var _ = JsonDocument.Parse(arguments);
            return arguments;
        }
        catch
        {
            return "{}";
        }
    }

    private static IReadOnlyList<object> BuildToolDefinitions(IReadOnlyCollection<ToolSpec> tools)
    {
        var result = new List<object>();

        foreach (var tool in tools)
        {
            var parameters = ParseSchema(tool.JsonSchema);
            var definition = new Dictionary<string, object?>
            {
                ["type"] = "function",
                ["function"] = new Dictionary<string, object?>
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = parameters
                }
            };

            result.Add(definition);
        }

        return result;
    }

    private static JsonElement ParseSchema(string jsonSchema)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonSchema);
            return doc.RootElement.Clone();
        }
        catch
        {
            return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>()
            });
        }
    }
}
