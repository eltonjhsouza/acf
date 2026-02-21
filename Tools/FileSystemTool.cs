using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;
public class FileSystemTool : ITool
{
    public string Name => "filesystem";

    private readonly string _workspaceRoot;

    public FileSystemTool(string? workspaceRoot = null)
    {
        _workspaceRoot = workspaceRoot ?? Path.Combine(Directory.GetCurrentDirectory(), "AgentWorkspace");
        Directory.CreateDirectory(_workspaceRoot);
    }

    public async Task<string> ExecuteAsync(string inputJson)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
            return "Invalid request: inputJson is empty.";

        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        FileRequest? request;

        // 1) tenta desserializar direto
        try
        {
            request = JsonSerializer.Deserialize<FileRequest>(inputJson, opts);
        }
        catch
        {
            request = TryParseFlexible(inputJson);
        }

        if (request == null)
            return $"Invalid request: could not parse JSON. Input: {inputJson}";

        // 2) normaliza action e path
        request.Action = NormalizeAction(request.Action);
        request.Path = NormalizePathFromAliases(request);

        if (string.IsNullOrWhiteSpace(request.Action))
            return $"Invalid request: missing 'action'. Input: {inputJson}";

        // 3) validações por ação
        if (NeedsPath(request.Action) && string.IsNullOrWhiteSpace(request.Path))
            return $"Invalid request: missing 'path' for action '{request.Action}'. Input: {inputJson}";

        if (request.Action == "write_file" && request.Content == null)
            request.Content = ""; // não quebra, escreve vazio

        // 4) resolve path seguro
        var fullPath = NeedsPath(request.Action) ? GetSafePath(request.Path!) : _workspaceRoot;

        // 5) executa ação
        switch (request.Action)
        {
            case "create_directory":
                Directory.CreateDirectory(fullPath);
                return $"Directory created: {MakeRelative(fullPath)}";

            case "write_file":
                // garante diretório pai
                var parent = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                await File.WriteAllTextAsync(fullPath, request.Content ?? "");
                return $"File written: {MakeRelative(fullPath)}";

            case "read_file":
                if (!File.Exists(fullPath))
                    return $"File not found: {MakeRelative(fullPath)}";

                return await File.ReadAllTextAsync(fullPath);

            case "list":
                if (!Directory.Exists(fullPath))
                    return $"Directory not found: {MakeRelative(fullPath)}";

                var entries = Directory.EnumerateFileSystemEntries(fullPath)
                    .Select(MakeRelative)
                    .ToArray();

                return JsonSerializer.Serialize(new { entries });

            default:
                return $"Invalid action: '{request.Action}'. Allowed: create_directory, write_file, read_file, list.";
        }
    }

    // ----------------------------
    // Helpers
    // ----------------------------

    private static bool NeedsPath(string action) =>
        action is "create_directory" or "write_file" or "read_file" or "list";

    private static string NormalizeAction(string? action)
    {
        action = (action ?? "").Trim().ToLowerInvariant();

        return action switch
        {
            // create dir aliases
            "mkdir" or "create_dir" or "create_folder" or "create_directory" => "create_directory",

            // write file aliases
            "write" or "writefile" or "save" or "save_file" or "write_file" => "write_file",

            // read file aliases
            "read" or "readfile" or "open" or "open_file" or "read_file" => "read_file",

            // list dir aliases
            "ls" or "dir" or "list_dir" or "list_files" or "list" => "list",

            _ => action
        };
    }

    private static string NormalizePathFromAliases(FileRequest request)
    {
        // prefer path; fallback para aliases que LLM costuma inventar
        var candidates = new[]
        {
            request.Path,
            request.FilePath,
            request.Filepath,
            request.Dir,
            request.Directory,
            request.Filename
        };

        return candidates.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))?.Trim() ?? "";
    }

    private string GetSafePath(string relativePath)
    {
        // normaliza separadores
        relativePath = relativePath
            .Trim()
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        var combined = Path.Combine(_workspaceRoot, relativePath);
        var fullPath = Path.GetFullPath(combined);

        // impede traversal fora do workspace
        if (!fullPath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path traversal detected.");

        return fullPath;
    }

    private string MakeRelative(string fullPath)
    {
        if (fullPath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
            return fullPath.Substring(_workspaceRoot.Length).TrimStart(Path.DirectorySeparatorChar);

        return fullPath;
    }

    private static FileRequest? TryParseFlexible(string inputJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(inputJson);
            var root = doc.RootElement;

            string? GetString(params string[] names)
            {
                foreach (var n in names)
                {
                    if (root.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
                        return p.GetString();
                }
                return null;
            }

            // aceita content tanto string quanto número/bool convertendo pra string
            string? GetAnyAsString(params string[] names)
            {
                foreach (var n in names)
                {
                    if (!root.TryGetProperty(n, out var p)) continue;

                    return p.ValueKind switch
                    {
                        JsonValueKind.String => p.GetString(),
                        JsonValueKind.Number => p.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => null,
                        _ => p.GetRawText()
                    };
                }
                return null;
            }

            return new FileRequest
            {
                Action = GetString("action", "Action"),
                Path = GetString("path", "Path", "filePath", "filepath", "dir", "directory", "filename"),
                Content = GetAnyAsString("content", "Content", "text", "Text", "body")
            };
        }
        catch
        {
            return null;
        }
    }

    // ----------------------------
    // Request DTO
    // ----------------------------
    private class FileRequest
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        // aliases comuns
        [JsonPropertyName("filePath")]
        public string? FilePath { get; set; }

        [JsonPropertyName("filepath")]
        public string? Filepath { get; set; }

        [JsonPropertyName("dir")]
        public string? Dir { get; set; }

        [JsonPropertyName("directory")]
        public string? Directory { get; set; }

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}