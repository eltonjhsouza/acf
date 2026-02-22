using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class CsvTool : ITool
{
    private readonly string _root;

    public CsvTool(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("rootPath cannot be empty.");

        _root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_root);
    }

    public ToolSpec Spec => new()
    {
        Name = "csv",
        Description = "Reads and writes CSV files inside workspace root.",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "action":{"type":"string","enum":["read","write","describe"]},
                "path":{"type":"string"},
                "headers":{"type":"array","items":{"type":"string"}},
                "rows":{"type":"array","items":{"type":"array","items":{"type":"string"}}},
                "maxRows":{"type":"integer"}
              },
              "required":["action","path"]
            }
            """
    };

    public async Task<string> ExecuteAsync(string inputJson)
    {
        CsvRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<CsvRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Fail("invalid_json", ex.Message);
        }

        if (req == null || string.IsNullOrWhiteSpace(req.Action) || string.IsNullOrWhiteSpace(req.Path))
            return Fail("invalid_request", "action and path are required");

        var action = req.Action.Trim().ToLowerInvariant();
        var fullPath = ResolveSafe(req.Path.Trim());

        try
        {
            return action switch
            {
                "read" => await ReadAsync(fullPath, req.MaxRows ?? 100),
                "describe" => await DescribeAsync(fullPath),
                "write" => await WriteAsync(fullPath, req),
                _ => Fail("invalid_action", $"'{action}'")
            };
        }
        catch (Exception ex)
        {
            return Fail("error", ex.Message);
        }
    }

    private async Task<string> ReadAsync(string path, int maxRows)
    {
        if (!File.Exists(path))
            return Fail("not_found", path);

        var lines = await File.ReadAllLinesAsync(path);
        if (lines.Length == 0)
            return Success(new { headers = Array.Empty<string>(), rows = Array.Empty<string[]>() });

        var headers = SplitCsvLine(lines[0]);
        var rows = new List<string[]>();

        for (var i = 1; i < lines.Length && rows.Count < maxRows; i++)
            rows.Add(SplitCsvLine(lines[i]));

        return Success(new
        {
            path = Rel(path),
            headers,
            rows,
            totalRows = Math.Max(0, lines.Length - 1)
        });
    }

    private async Task<string> DescribeAsync(string path)
    {
        if (!File.Exists(path))
            return Fail("not_found", path);

        var lines = await File.ReadAllLinesAsync(path);
        if (lines.Length == 0)
            return Success(new { path = Rel(path), columns = 0, rowCount = 0 });

        var headers = SplitCsvLine(lines[0]);
        return Success(new
        {
            path = Rel(path),
            columns = headers.Length,
            headers,
            rowCount = Math.Max(0, lines.Length - 1)
        });
    }

    private async Task<string> WriteAsync(string path, CsvRequest req)
    {
        var headers = req.Headers ?? Array.Empty<string>();
        var rows = req.Rows ?? Array.Empty<string[]>();

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var builder = new StringBuilder();
        if (headers.Length > 0)
            builder.AppendLine(JoinCsvLine(headers));

        foreach (var row in rows)
            builder.AppendLine(JoinCsvLine(row));

        await File.WriteAllTextAsync(path, builder.ToString());

        return Success(new
        {
            path = Rel(path),
            headers = headers.Length,
            rows = rows.Length
        });
    }

    private string ResolveSafe(string relative)
    {
        var safe = relative.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar)
            .Trim();

        var full = Path.GetFullPath(Path.Combine(_root, safe));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path traversal detected.");

        return full;
    }

    private string Rel(string fullPath)
    {
        if (!fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            return fullPath;

        return fullPath.Substring(_root.Length).TrimStart(Path.DirectorySeparatorChar);
    }

    private static string[] SplitCsvLine(string line)
        => line.Split(',').Select(x => x.Trim()).ToArray();

    private static string JoinCsvLine(IEnumerable<string> values)
        => string.Join(",", values.Select(EscapeCsvValue));

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return '"' + value.Replace("\"", "\"\"") + '"';

        return value;
    }

    private static string Success(object data)
        => JsonSerializer.Serialize(new { ok = true, data });

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class CsvRequest
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("headers")]
        public string[]? Headers { get; set; }

        [JsonPropertyName("rows")]
        public string[][]? Rows { get; set; }

        [JsonPropertyName("maxRows")]
        public int? MaxRows { get; set; }
    }
}
