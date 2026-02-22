using System.Text.Json;

namespace AgentCore.Core.Workflows;

public sealed class JsonFileWorkflowCheckpointStore : IWorkflowCheckpointStore
{
    private readonly string _directoryPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonFileWorkflowCheckpointStore(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("directoryPath cannot be empty.");

        _directoryPath = Path.GetFullPath(directoryPath);
        Directory.CreateDirectory(_directoryPath);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task SaveAsync(WorkflowCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(checkpoint.RunId);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var checkpoints = await ReadAllInternalAsync(filePath, cancellationToken);
            checkpoints.Add(checkpoint);

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, checkpoints, _jsonOptions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WorkflowCheckpoint?> GetLatestAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(runId, cancellationToken);
        return all.Count == 0 ? null : all[^1];
    }

    public async Task<IReadOnlyList<WorkflowCheckpoint>> GetAllAsync(
        string runId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(runId);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var checkpoints = await ReadAllInternalAsync(filePath, cancellationToken);
            return checkpoints.AsReadOnly();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<WorkflowCheckpoint>> ReadAllInternalAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            return new List<WorkflowCheckpoint>();

        await using var stream = File.OpenRead(filePath);
        var checkpoints = await JsonSerializer.DeserializeAsync<List<WorkflowCheckpoint>>(
            stream,
            _jsonOptions,
            cancellationToken);

        return checkpoints ?? new List<WorkflowCheckpoint>();
    }

    private string GetFilePath(string runId)
    {
        var safe = new string(runId.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(safe))
            safe = "default_run";

        return Path.Combine(_directoryPath, safe + ".checkpoints.json");
    }
}
