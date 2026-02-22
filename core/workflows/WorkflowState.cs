using System.Text.Json;

namespace AgentCore.Core.Workflows;

public sealed class WorkflowState
{
    private readonly Dictionary<string, JsonElement> _values;

    public WorkflowState()
        : this(new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase))
    {
    }

    private WorkflowState(Dictionary<string, JsonElement> values)
    {
        _values = values;
    }

    public IReadOnlyDictionary<string, JsonElement> Values => _values;

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public bool TryGet(string key, out JsonElement value) => _values.TryGetValue(key, out value);

    public void Set<T>(string key, T value)
    {
        _values[key] = JsonSerializer.SerializeToElement(value);
    }

    public void SetRaw(string key, JsonElement value)
    {
        _values[key] = value.Clone();
    }

    public T? Get<T>(string key)
    {
        if (!_values.TryGetValue(key, out var value))
            return default;

        return value.Deserialize<T>();
    }

    public string? GetString(string key)
    {
        if (!_values.TryGetValue(key, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object => value.GetRawText(),
            JsonValueKind.Array => value.GetRawText(),
            _ => null
        };
    }

    public int GetInt32(string key, int defaultValue = 0)
    {
        if (!_values.TryGetValue(key, out var value))
            return defaultValue;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
            return parsed;

        return defaultValue;
    }

    public void Merge(IReadOnlyDictionary<string, JsonElement> updates)
    {
        foreach (var (key, value) in updates)
            _values[key] = value.Clone();
    }

    public Dictionary<string, JsonElement> Snapshot()
    {
        var copy = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in _values)
            copy[key] = value.Clone();
        return copy;
    }

    public void LoadSnapshot(IReadOnlyDictionary<string, JsonElement> snapshot)
    {
        _values.Clear();
        foreach (var (key, value) in snapshot)
            _values[key] = value.Clone();
    }

    public static WorkflowState FromSnapshot(IReadOnlyDictionary<string, JsonElement> snapshot)
    {
        var state = new WorkflowState();
        state.LoadSnapshot(snapshot);
        return state;
    }
}
