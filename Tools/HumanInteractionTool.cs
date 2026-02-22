using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class HumanInteractionTool : ITool
{
    public ToolSpec Spec => new()
    {
        Name = "human",
        Description = "Human-in-the-loop prompts for approvals and input in the console.",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "action":{"type":"string","enum":["confirm","ask_text","ask_choice"]},
                "prompt":{"type":"string"},
                "choices":{"type":"array","items":{"type":"string"}}
              },
              "required":["action","prompt"]
            }
            """
    };

    public Task<string> ExecuteAsync(string inputJson)
    {
        HumanRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<HumanRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fail("invalid_json", ex.Message));
        }

        if (req == null || string.IsNullOrWhiteSpace(req.Action) || string.IsNullOrWhiteSpace(req.Prompt))
            return Task.FromResult(Fail("invalid_request", "action and prompt are required"));

        var action = req.Action.Trim().ToLowerInvariant();

        return action switch
        {
            "confirm" => Task.FromResult(Confirm(req.Prompt)),
            "ask_text" => Task.FromResult(AskText(req.Prompt)),
            "ask_choice" => Task.FromResult(AskChoice(req.Prompt, req.Choices ?? Array.Empty<string>())),
            _ => Task.FromResult(Fail("invalid_action", $"'{action}'"))
        };
    }

    private static string Confirm(string prompt)
    {
        Console.WriteLine();
        Console.WriteLine($"[HUMAN CONFIRM] {prompt}");
        Console.Write("Type 'yes' to approve: ");
        var input = Console.ReadLine()?.Trim();

        var approved = string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(input, "y", StringComparison.OrdinalIgnoreCase);

        return Success(new
        {
            approved,
            input
        });
    }

    private static string AskText(string prompt)
    {
        Console.WriteLine();
        Console.WriteLine($"[HUMAN INPUT] {prompt}");
        Console.Write("> ");
        var input = Console.ReadLine() ?? string.Empty;

        return Success(new
        {
            text = input
        });
    }

    private static string AskChoice(string prompt, IReadOnlyList<string> choices)
    {
        if (choices.Count == 0)
            return Fail("invalid_request", "choices are required for ask_choice");

        Console.WriteLine();
        Console.WriteLine($"[HUMAN CHOICE] {prompt}");
        for (var i = 0; i < choices.Count; i++)
            Console.WriteLine($" {i + 1}) {choices[i]}");

        Console.Write("Choose option number: ");
        var raw = Console.ReadLine()?.Trim();

        if (!int.TryParse(raw, out var selected) || selected < 1 || selected > choices.Count)
            return Fail("invalid_choice", "user did not pick a valid option");

        return Success(new
        {
            selectedIndex = selected - 1,
            selectedValue = choices[selected - 1]
        });
    }

    private static string Success(object data)
        => JsonSerializer.Serialize(new { ok = true, data });

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class HumanRequest
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }

        [JsonPropertyName("choices")]
        public string[]? Choices { get; set; }
    }
}
