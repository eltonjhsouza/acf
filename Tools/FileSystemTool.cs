using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.Versioning;

namespace AgentCore.Tools;

public sealed class FileSystemTool : ITool
{
    private readonly string _root;

    public FileSystemTool(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("rootPath cannot be empty.");

        _root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_root);
    }

    public ToolSpec Spec => new ToolSpec
    {
        Name = "filesystem",
        Description = "Operations on files and directories (create/read/write/list/move/copy/delete) under a configured root path. Supports chmod on Linux/macOS.",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "action":{"type":"string","enum":[
                  "create_directory","write_file","append_file","read_file","list",
                  "delete_file","delete_directory","move","copy","exists","chmod", "cd", "pwd"
                ]},
                "path":{"type":"string","description":"Relative path under the configured root. If omitted for list, lists root."},
                "to":{"type":"string","description":"Destination relative path for move/copy."},
                "content":{"type":"string","description":"Content for write/append."},
                "recursive":{"type":"boolean","description":"For delete_directory."},
                "mode":{"type":"string","description":"Unix mode in octal string, e.g. 755 or 644, for chmod."}
              },
              "required":["action"]
            }
            """
    };

    public async Task<string> ExecuteAsync(string inputJson)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
            return "Invalid request: empty inputJson.";

        FileRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<FileRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return $"Invalid JSON: {ex.Message}";
        }

        if (req == null)
            return "Invalid request: could not parse JSON.";

        var action = NormalizeAction(req.Action);
        var path = req.Path?.Trim();

        if (string.IsNullOrWhiteSpace(action))
            return "Invalid request: missing action.";

        // ✅ regra especial: list sem path => lista raiz do workspace
        if (action == "list" && string.IsNullOrWhiteSpace(path))
            path = ".";

        // Ações que realmente precisam de path
        if (RequiresPath(action) && string.IsNullOrWhiteSpace(path))
            return $"Invalid request: missing path for action '{action}'.";

        try
        {
            // Resolve path somente quando precisa
            var fullPath = RequiresPath(action) ? ResolveSafe(path!) : _root;

            switch (action)
            {
                case "create_directory":
                    Directory.CreateDirectory(fullPath);
                    return Ok(new { action, path = Rel(fullPath) });

                case "write_file":
                    EnsureParentDir(fullPath);
                    await File.WriteAllTextAsync(fullPath, req.Content ?? "");
                    return Ok(new { action, path = Rel(fullPath), bytes = (req.Content ?? "").Length });

                case "append_file":
                    EnsureParentDir(fullPath);
                    await File.AppendAllTextAsync(fullPath, req.Content ?? "");
                    return Ok(new { action, path = Rel(fullPath), appendedBytes = (req.Content ?? "").Length });

                case "read_file":
                    if (!File.Exists(fullPath))
                        return NotFound(path!);
                    var text = await File.ReadAllTextAsync(fullPath);
                    return Ok(new { action, path = Rel(fullPath), content = text });

                case "list":
                    if (!Directory.Exists(fullPath))
                        return NotFound(path!);
                    var entries = Directory.EnumerateFileSystemEntries(fullPath)
                        .Select(Rel)
                        .OrderBy(x => x)
                        .ToArray();
                    return Ok(new { action, path = Rel(fullPath), entries });

                case "delete_file":
                    if (!File.Exists(fullPath))
                        return NotFound(path!);
                    File.Delete(fullPath);
                    return Ok(new { action, path = Rel(fullPath) });

                case "delete_directory":
                    if (!Directory.Exists(fullPath))
                        return NotFound(path!);
                    var recursive = req.Recursive ?? false;
                    Directory.Delete(fullPath, recursive);
                    return Ok(new { action, path = Rel(fullPath), recursive });

                case "move":
                    if (string.IsNullOrWhiteSpace(req.To))
                        return "Invalid request: missing 'to' for move.";

                    var destMove = ResolveSafe(req.To.Trim());
                    EnsureParentDir(destMove);

                    if (File.Exists(fullPath))
                    {
                        File.Move(fullPath, destMove, overwrite: true);
                        return Ok(new { action, from = Rel(fullPath), to = Rel(destMove), type = "file" });
                    }
                    if (Directory.Exists(fullPath))
                    {
                        if (Directory.Exists(destMove))
                            Directory.Delete(destMove, recursive: true);

                        Directory.Move(fullPath, destMove);
                        return Ok(new { action, from = Rel(fullPath), to = Rel(destMove), type = "directory" });
                    }
                    return NotFound(path!);

                case "copy":
                    if (string.IsNullOrWhiteSpace(req.To))
                        return "Invalid request: missing 'to' for copy.";

                    var destCopy = ResolveSafe(req.To.Trim());
                    EnsureParentDir(destCopy);

                    if (File.Exists(fullPath))
                    {
                        File.Copy(fullPath, destCopy, overwrite: true);
                        return Ok(new { action, from = Rel(fullPath), to = Rel(destCopy), type = "file" });
                    }
                    if (Directory.Exists(fullPath))
                    {
                        CopyDirectory(fullPath, destCopy);
                        return Ok(new { action, from = Rel(fullPath), to = Rel(destCopy), type = "directory" });
                    }
                    return NotFound(path!);

                case "exists":
                    return Ok(new
                    {
                        action,
                        path = Rel(fullPath),
                        exists = File.Exists(fullPath) || Directory.Exists(fullPath),
                        isFile = File.Exists(fullPath),
                        isDirectory = Directory.Exists(fullPath)
                    });

                case "chmod":
                    if (string.IsNullOrWhiteSpace(req.Mode))
                        return "Invalid request: missing 'mode' for chmod. Use octal like 755 or 644.";

                    if (!IsUnix())
                        return "chmod is only supported on Unix-like systems (Linux/macOS).";

                    if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                        return NotFound(path!);

                    if (!TryParseUnixMode(req.Mode.Trim(), out var unixMode, out var modeErr))
                        return $"Invalid mode: {modeErr}";

                    ApplyUnixMode(fullPath, unixMode);
                    return Ok(new { action, path = Rel(fullPath), mode = req.Mode.Trim() });

                default:
                    return $"Invalid action: '{action}'.";
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return $"Unauthorized: {ex.Message}";
        }
        catch (IOException ex)
        {
            return $"IO error: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    // ---------------- Helpers ----------------

    private static bool RequiresPath(string action) =>
        action is "create_directory" or "write_file" or "append_file" or "read_file" or "list"
            or "delete_file" or "delete_directory" or "move" or "copy" or "exists" or "chmod";

    private static string NormalizeAction(string? action)
    {
        action = (action ?? "").Trim().ToLowerInvariant();
        return action switch
        {
            "mkdir" or "create_folder" or "create_dir" => "create_directory",
            "write" or "save" => "write_file",
            "append" => "append_file",
            "read" or "cat" => "read_file",
            "ls" or "dir" => "list",
            "rm" => "delete_file",
            "rmdir" => "delete_directory",
            _ => action
        };
    }

    private string ResolveSafe(string relative)
    {
        // aceita "." para raiz
        if (string.IsNullOrWhiteSpace(relative) || relative.Trim() == ".")
            return _root;

        relative = relative.Replace('\\', Path.DirectorySeparatorChar)
                           .Replace('/', Path.DirectorySeparatorChar)
                           .Trim();

        var combined = Path.Combine(_root, relative);
        var full = Path.GetFullPath(combined);

        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Path traversal detected (outside root).");

        return full;
    }

    private string Rel(string fullPath)
    {
        if (fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            return fullPath.Substring(_root.Length).TrimStart(Path.DirectorySeparatorChar);

        return fullPath;
    }

    private static void EnsureParentDir(string filePath)
    {
        var parent = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(parent))
            Directory.CreateDirectory(parent);
    }

    private static string Ok(object obj) => JsonSerializer.Serialize(new { ok = true, data = obj });

    private static string NotFound(string path) => JsonSerializer.Serialize(new { ok = false, error = "not_found", path });

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSub = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSub);
        }
    }

    private static bool TryParseUnixMode(string octal, out UnixFileMode mode, out string error)
    {
        error = "";
        mode = default;

        octal = octal.StartsWith("0") ? octal : "0" + octal;

        if (octal.Length < 4 || octal.Length > 5 || octal.Any(c => c < '0' || c > '7'))
        {
            error = "Mode must be an octal string like 755 or 644.";
            return false;
        }

        try
        {
            var value = Convert.ToInt32(octal, 8);
            mode = (UnixFileMode)value;
            return true;
        }
        catch
        {
            error = "Failed to parse octal mode.";
            return false;
        }
    }

    private static bool IsUnix()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    private static void ApplyUnixMode(string fullPath, UnixFileMode mode)
    {
        if (!IsUnix())
            throw new PlatformNotSupportedException("chmod is only supported on Unix-like systems.");

        ApplyUnixModeUnixOnly(fullPath, mode);
    }

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("osx")]
    private static void ApplyUnixModeUnixOnly(string fullPath, UnixFileMode mode)
    {
        // No Unix, funciona para arquivo e diretório
        File.SetUnixFileMode(fullPath, mode);
    }

    private sealed class FileRequest
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("to")]
        public string? To { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("recursive")]
        public bool? Recursive { get; set; }

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }
    }
}