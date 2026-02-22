using System.Text.Json;

namespace AgentCore.Core.Workflows;

public sealed class JsonlWorkflowLogger : IWorkflowLogger
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonlWorkflowLogger(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath cannot be empty.");

        _filePath = Path.GetFullPath(filePath);
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    public async Task LogAsync(WorkflowLogEntry entry, CancellationToken cancellationToken = default)
    {
        var line = JsonSerializer.Serialize(entry);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
