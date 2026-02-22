namespace AgentCore.LLM;

public sealed class ToolCallRecord
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ArgumentsJson { get; init; }
    public required string Result { get; init; }
}

public sealed class ToolCallingResponse
{
    public required string FinalText { get; init; }
    public required IReadOnlyList<ToolCallRecord> ToolCalls { get; init; }
    public required string LastRawResponse { get; init; }
}
