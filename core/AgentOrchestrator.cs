using System.Text.Json;
using AgentCore.Tools;

namespace AgentCore.Core;

public class AgentOrchestrator
{
    private readonly Planner _planner;
    private readonly ToolRegistry _tools;
    private readonly StepRepairer _repairer;

    private const int MaxRepairsPerStep = 2;

    public AgentOrchestrator(Planner planner, IEnumerable<ITool> tools, StepRepairer repairer)
    {
        _planner = planner;
        _tools = new ToolRegistry(tools);
        _repairer = repairer;
    }

    public async Task RunAsync(TaskDefinition task)
    {
        var state = new AgentState();
        state.Steps = await _planner.CreatePlanAsync(task);

        Console.WriteLine($"Plan steps: {state.Steps.Count}");
        for (var i = 0; i < state.Steps.Count; i++)
            Console.WriteLine($"Step[{i}] tool={state.Steps[i].ToolName} desc='{state.Steps[i].Description}'");

        while (state.CurrentStepIndex < state.Steps.Count)
        {
            var repairs = 0;

            while (true)
            {
                var step = state.Steps[state.CurrentStepIndex];

                // --- validations / repairs ---
                if (string.IsNullOrWhiteSpace(step.Description))
                {
                    if (repairs++ >= MaxRepairsPerStep)
                        step.Description = $"Step {state.CurrentStepIndex + 1}";
                    else
                    {
                        step = await _repairer.RepairAsync(task, step, "description is empty");
                        state.Steps[state.CurrentStepIndex] = step;
                        continue;
                    }
                }

                if (string.IsNullOrWhiteSpace(step.ToolName))
                {
                    if (repairs++ >= MaxRepairsPerStep)
                        throw new Exception($"Step {state.CurrentStepIndex} has null/empty ToolName after repairs.");

                    step = await _repairer.RepairAsync(task, step, "toolName is empty");
                    state.Steps[state.CurrentStepIndex] = step;
                    continue;
                }

                Console.WriteLine($"Executing: {step.Description}");

                var toolName = step.ToolName.Trim();

                // Normalização defensiva (temporária)
                if (toolName.Equals("write_file", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Equals("mkdir", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Equals("create_directory", StringComparison.OrdinalIgnoreCase))
                {
                    toolName = "filesystem";
                }

                if (!_tools.TryGet(toolName, out var tool))
                {
                    if (repairs++ >= MaxRepairsPerStep)
                        throw new Exception($"Tool '{step.ToolName}' not found. Available: {_tools.ListNames()}");

                    step = await _repairer.RepairAsync(task, step,
                        $"tool '{step.ToolName}' not found. Available: {_tools.ListNames()}");
                    state.Steps[state.CurrentStepIndex] = step;
                    continue;
                }

                var inputJson = step.ToolInput.ValueKind == JsonValueKind.Undefined
                    ? "{}"
                    : step.ToolInput.GetRawText();

                if (!IsValidJson(inputJson))
                {
                    if (repairs++ >= MaxRepairsPerStep)
                        throw new Exception($"Invalid toolInput JSON at step {state.CurrentStepIndex}: {inputJson}");

                    step = await _repairer.RepairAsync(task, step, "toolInput is not valid JSON");
                    state.Steps[state.CurrentStepIndex] = step;
                    continue;
                }

                // ✅ aplica templates ({{last.body}} etc.)
                inputJson = TemplateEngine.ApplyJson(inputJson, state);

                // ✅ cwd para filesystem
                if (toolName.Equals("filesystem", StringComparison.OrdinalIgnoreCase))
                {
                    using var doc = JsonDocument.Parse(inputJson);
                    var root = doc.RootElement;

                    var action = root.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.String
                        ? a.GetString()!.Trim().ToLowerInvariant()
                        : "";

                    if (action == "pwd")
                    {
                        var resultPwd = JsonSerializer.Serialize(new { ok = true, cwd = state.WorkingDirectory });
                        Console.WriteLine($"Result: {resultPwd}");

                        // salva last result também (pra consistência)
                        state.LastResultRaw = resultPwd;
                        state.LastResultJson?.Dispose();
                        state.LastResultJson = JsonDocument.Parse(resultPwd);

                        state.CurrentStepIndex++;
                        break; // ✅ sai só do repair-loop e volta pro loop principal
                    }

                    if (action == "cd")
                    {
                        var target = root.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String
                            ? p.GetString()!.Trim()
                            : "";

                        if (string.IsNullOrWhiteSpace(target))
                        {
                            if (repairs++ >= MaxRepairsPerStep)
                                throw new Exception("filesystem cd requires 'path'.");

                            step = await _repairer.RepairAsync(task, step, "filesystem cd requires non-empty path");
                            state.Steps[state.CurrentStepIndex] = step;
                            continue;
                        }

                        var combined = CombineCwd(state.WorkingDirectory, target);

                        var existsReq = JsonSerializer.Serialize(new { action = "exists", path = combined });
                        var existsRespJson = await tool.ExecuteAsync(existsReq);

                        using var existsDoc = JsonDocument.Parse(existsRespJson);
                        var ok = existsDoc.RootElement.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;

                        if (!ok)
                        {
                            if (repairs++ >= MaxRepairsPerStep)
                                throw new Exception($"cd failed: target not found: {combined}");

                            step = await _repairer.RepairAsync(task, step, $"cd target not found: {combined}");
                            state.Steps[state.CurrentStepIndex] = step;
                            continue;
                        }

                        var isDir = existsDoc.RootElement.GetProperty("data")
                            .TryGetProperty("isDirectory", out var isDirEl) && isDirEl.ValueKind == JsonValueKind.True;

                        if (!isDir)
                        {
                            if (repairs++ >= MaxRepairsPerStep)
                                throw new Exception($"cd failed: target is not a directory: {combined}");

                            step = await _repairer.RepairAsync(task, step, $"cd target is not a directory: {combined}");
                            state.Steps[state.CurrentStepIndex] = step;
                            continue;
                        }

                        state.WorkingDirectory = NormalizeRel(combined);

                        var resultCd = JsonSerializer.Serialize(new { ok = true, cwd = state.WorkingDirectory });
                        Console.WriteLine($"Result: {resultCd}");

                        // salva last result também
                        state.LastResultRaw = resultCd;
                        state.LastResultJson?.Dispose();
                        state.LastResultJson = JsonDocument.Parse(resultCd);

                        state.CurrentStepIndex++;
                        break;
                    }

                    inputJson = RewriteFilesystemPathWithCwd(inputJson, state.WorkingDirectory);
                }

                // --- execute tool ---
                inputJson = ReplaceTokens(inputJson, state);

                if (!IsValidJson(inputJson))
                {
                    if (repairs++ >= MaxRepairsPerStep)
                        throw new Exception($"toolInput became invalid JSON after templating: {inputJson}");

                    step = await _repairer.RepairAsync(task, step, "toolInput became invalid JSON after templating");
                    state.Steps[state.CurrentStepIndex] = step;
                    continue;
                }

                if (ContainsUnresolvedLastToken(inputJson))
                {
                    if (repairs++ >= MaxRepairsPerStep)
                        throw new Exception($"toolInput still contains unresolved template token: {inputJson}");

                    step = await _repairer.RepairAsync(task, step,
                        "toolInput contains unresolved template token (last/last.body/last.html/last.answer)");
                    state.Steps[state.CurrentStepIndex] = step;
                    continue;
                }

                var result = await tool.ExecuteAsync(inputJson);
                Console.WriteLine($"Result: {result}");

                if (!IsSuccessfulToolResult(result, out var toolError))
                {
                    if (repairs++ >= MaxRepairsPerStep)
                        throw new Exception($"tool execution failed at step {state.CurrentStepIndex}: {toolError}");

                    step = await _repairer.RepairAsync(task, step, $"tool execution failed: {toolError}");
                    state.Steps[state.CurrentStepIndex] = step;
                    continue;
                }

                // ✅ guarda last result
                state.LastResultRaw = result;
                try
                {
                    state.LastResultJson?.Dispose();
                    state.LastResultJson = JsonDocument.Parse(result);
                }
                catch
                {
                    state.LastResultJson = null;
                }

                // ✅ avança step
                state.CurrentStepIndex++;
                break; // ✅ sai do repair-loop e volta pro loop principal
            }
        }

        Console.WriteLine("Task Completed.");
    }

    private static string ReplaceTokens(string s, AgentState state)
    {
        if (string.IsNullOrEmpty(s))
            return s;

        // Substituição básica do resultado bruto
        s = s.Replace("{{last}}", state.LastResultRaw ?? "");

        if (string.IsNullOrWhiteSpace(state.LastResultRaw))
            return s;

        try
        {
            using var doc = JsonDocument.Parse(state.LastResultRaw);

            // HTTP → { body: "..." }
            if (doc.RootElement.TryGetProperty("body", out var bodyEl) &&
                bodyEl.ValueKind == JsonValueKind.String)
            {
                s = s.Replace("{{last.body}}", bodyEl.GetString());
            }

            // Browser / Tavily → { data: { html: "...", answer: "..." } }
            if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                dataEl.ValueKind == JsonValueKind.Object)
            {
                if (dataEl.TryGetProperty("html", out var htmlEl) &&
                    htmlEl.ValueKind == JsonValueKind.String)
                {
                    s = s.Replace("{{last.html}}", htmlEl.GetString());
                }

                if (dataEl.TryGetProperty("answer", out var answerEl) &&
                    answerEl.ValueKind == JsonValueKind.String)
                {
                    s = s.Replace("{{last.answer}}", answerEl.GetString());
                }
            }
        }
        catch
        {
            // ignora erro de parse
        }

        return s;
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsUnresolvedLastToken(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("{{last", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("{last", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuccessfulToolResult(string result, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(result))
        {
            error = "empty tool result";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(result);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return true;

            var root = doc.RootElement;
            if (root.TryGetProperty("ok", out var okEl))
            {
                if (okEl.ValueKind == JsonValueKind.False)
                {
                    error = ExtractErrorMessage(root);
                    return false;
                }

                if (okEl.ValueKind == JsonValueKind.True)
                    return true;
            }

            if (root.TryGetProperty("error", out _))
            {
                error = ExtractErrorMessage(root);
                return false;
            }

            return true;
        }
        catch
        {
            if (LooksLikeErrorString(result))
            {
                error = result;
                return false;
            }

            return true;
        }
    }

    private static string ExtractErrorMessage(JsonElement root)
    {
        if (root.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
            return msgEl.GetString() ?? "unknown error";

        if (root.TryGetProperty("error", out var errEl))
        {
            if (errEl.ValueKind == JsonValueKind.String)
                return errEl.GetString() ?? "unknown error";

            if (errEl.ValueKind == JsonValueKind.Object &&
                errEl.TryGetProperty("message", out var nestedMsg) &&
                nestedMsg.ValueKind == JsonValueKind.String)
            {
                return nestedMsg.GetString() ?? "unknown error";
            }

            return errEl.GetRawText();
        }

        return "tool returned ok=false";
    }

    private static bool LooksLikeErrorString(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith("Invalid", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("HTTP error", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("IO error", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("playwright_error", StringComparison.OrdinalIgnoreCase);
    }

    private static string CombineCwd(string cwd, string path)
    {
        if (string.IsNullOrWhiteSpace(cwd) || cwd == ".")
            return NormalizeRel(path);

        return NormalizeRel($"{cwd.TrimEnd('/', '\\')}/{path.TrimStart('/', '\\')}");
    }

    private static string NormalizeRel(string path)
    {
        path = (path ?? "").Trim().Replace('\\', '/');

        while (path.StartsWith("./"))
            path = path.Substring(2);

        if (string.IsNullOrWhiteSpace(path))
            return ".";

        return path;
    }

    private static string RewriteFilesystemPathWithCwd(string inputJson, string cwd)
    {
        using var doc = JsonDocument.Parse(inputJson);
        var root = doc.RootElement;

        var action = root.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.String
            ? a.GetString()!.Trim()
            : "";

        var hasPath = root.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String;
        var pathValue = hasPath ? p.GetString()!.Trim() : "";

        string? newPath = null;

        // list sem path => list no cwd
        if (string.Equals(action, "list", StringComparison.OrdinalIgnoreCase) && !hasPath)
        {
            newPath = NormalizeRel(cwd);
        }
        else if (hasPath && !string.IsNullOrWhiteSpace(pathValue))
        {
            var isAbs = pathValue.StartsWith("/") || pathValue.Contains(":\\") || pathValue.Contains(":/");
            if (!isAbs)
                newPath = CombineCwd(cwd, pathValue);
        }

        if (newPath == null)
            return inputJson;

        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(inputJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        dict["path"] = newPath;

        return JsonSerializer.Serialize(dict);
    }
}
