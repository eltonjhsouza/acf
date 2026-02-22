using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentCore.Tools;

public sealed class PythonTool : ITool
{
    private readonly string _workingDirectory;
    private readonly bool _enabled;

    public PythonTool(string workingDirectory, bool? enabled = null)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
            throw new ArgumentException("workingDirectory cannot be empty.");

        _workingDirectory = Path.GetFullPath(workingDirectory);
        Directory.CreateDirectory(_workingDirectory);

        _enabled = enabled ??
            string.Equals(Environment.GetEnvironmentVariable("ALLOW_PYTHON_TOOL"), "1", StringComparison.OrdinalIgnoreCase);
    }

    public ToolSpec Spec => new()
    {
        Name = "python",
        Description = "Runs Python code in a temporary file (disabled unless ALLOW_PYTHON_TOOL=1).",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "code":{"type":"string"},
                "timeoutMs":{"type":"integer"}
              },
              "required":["code"]
            }
            """
    };

    public async Task<string> ExecuteAsync(string inputJson)
    {
        if (!_enabled)
            return Fail("disabled", "Python tool is disabled. Set ALLOW_PYTHON_TOOL=1 to enable it.");

        PythonRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<PythonRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Fail("invalid_json", ex.Message);
        }

        if (req == null || string.IsNullOrWhiteSpace(req.Code))
            return Fail("invalid_request", "code is required");

        var timeoutMs = req.TimeoutMs is > 0 ? req.TimeoutMs.Value : 20000;
        var tempFile = Path.Combine(_workingDirectory, $".tmp_py_{Guid.NewGuid():N}.py");

        try
        {
            await File.WriteAllTextAsync(tempFile, req.Code);

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{tempFile}\"",
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
                    stderr
                }
            });
        }
        catch (OperationCanceledException)
        {
            return Fail("timeout", $"python code exceeded timeout of {timeoutMs} ms");
        }
        catch (Exception ex)
        {
            return Fail("error", ex.Message);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static string Fail(string error, string message)
        => JsonSerializer.Serialize(new { ok = false, error, message });

    private sealed class PythonRequest
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("timeoutMs")]
        public int? TimeoutMs { get; set; }
    }
}
