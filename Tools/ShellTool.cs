using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class ShellTool : ITool
{
    private readonly string _workingDirectory;
    private readonly bool _enabled;

    public ShellTool(string workingDirectory, bool? enabled = null)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
            throw new ArgumentException("workingDirectory cannot be empty.");

        _workingDirectory = Path.GetFullPath(workingDirectory);
        Directory.CreateDirectory(_workingDirectory);

        _enabled = enabled ??
            string.Equals(Environment.GetEnvironmentVariable("ALLOW_SHELL_TOOL"), "1", StringComparison.OrdinalIgnoreCase);
    }

    public ToolSpec Spec => new()
    {
        Name = "shell",
        Description = "Runs shell commands in the configured workspace (disabled unless ALLOW_SHELL_TOOL=1).",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "command":{"type":"string"},
                "timeoutMs":{"type":"integer","description":"Optional timeout in ms (default 20000)"}
              },
              "required":["command"]
            }
            """
    };

    public async Task<string> ExecuteAsync(string inputJson)
    {
        if (!_enabled)
            return Fail("disabled", "Shell tool is disabled. Set ALLOW_SHELL_TOOL=1 to enable it.");

        ShellRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<ShellRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Fail("invalid_json", ex.Message);
        }

        if (req == null || string.IsNullOrWhiteSpace(req.Command))
            return Fail("invalid_request", "command is required");

        var timeoutMs = req.TimeoutMs is > 0 ? req.TimeoutMs.Value : 20000;
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
                Arguments = OperatingSystem.IsWindows()
                    ? $"/c {req.Command}"
                    : $"-lc \"{req.Command.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _workingDirectory
            };

            process.Start();

            using var cts = new CancellationTokenSource(timeoutMs);
            await process.WaitForExitAsync(cts.Token);

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();

            return JsonSerializer.Serialize(new
            {
                ok = process.ExitCode == 0,
                data = new
                {
                    exitCode = process.ExitCode,
                    stdout,
                    stderr,
                    timeoutMs
                }
            });
        }
        catch (OperationCanceledException)
        {
            return Fail("timeout", $"command exceeded timeout of {timeoutMs} ms");
        }
        catch (Exception ex)
        {
            return Fail("error", ex.Message);
        }
    }

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class ShellRequest
    {
        [JsonPropertyName("command")]
        public string? Command { get; set; }

        [JsonPropertyName("timeoutMs")]
        public int? TimeoutMs { get; set; }
    }
}
