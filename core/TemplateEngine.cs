using System.Text.Json;

namespace AgentCore.Core;

public static class TemplateEngine
{
    // Aplica templates SOMENTE em valores string do JSON.
    // Assim a saída continua JSON válido (escapes corretos).
    public static string ApplyJson(string inputJson, AgentState state)
    {
        using var doc = JsonDocument.Parse(inputJson);
        var rewritten = RewriteElement(doc.RootElement, state);
        return JsonSerializer.Serialize(rewritten);
    }

    private static object? RewriteElement(JsonElement el, AgentState state)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Object => RewriteObject(el, state),
            JsonValueKind.Array => RewriteArray(el, state),
            JsonValueKind.String => ReplaceTokens(el.GetString() ?? "", state),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.ToString()
        };
    }

    private static Dictionary<string, object?> RewriteObject(JsonElement obj, AgentState state)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in obj.EnumerateObject())
            dict[p.Name] = RewriteElement(p.Value, state);
        return dict;
    }

    private static List<object?> RewriteArray(JsonElement arr, AgentState state)
    {
        var list = new List<object?>();
        foreach (var item in arr.EnumerateArray())
            list.Add(RewriteElement(item, state));
        return list;
    }

    private static string ReplaceTokens(string s, AgentState state)
    {
        // last raw
        s = s.Replace("{{last}}", state.LastResultRaw)
            .Replace("{last}", state.LastResultRaw);

        // last.body (HttpTool)
        var body = TryGetLastBody(state);
        if (body != null)
        {
            s = s.Replace("{{last.body}}", body)
                .Replace("{last.body}", body)
                .Replace("<<body from previous step>>", body);
        }

        // last.html (BrowserTool -> data.html)
        var html = TryGetLastHtml(state);
        if (html != null)
        {
            s = s.Replace("{{last.html}}", html)
                .Replace("{last.html}", html)
                .Replace("{{last.data.html}}", html)
                .Replace("{last.data.html}", html);
        }

        return s;
    }

  private static string? TryGetLastHtml(AgentState state)
  {
      if (state.LastResultJson == null) return null;

      var root = state.LastResultJson.RootElement;

      // BrowserTool retorna: {"ok":true,"data":{"action":"html","html":"..."}}
      if (root.ValueKind == JsonValueKind.Object &&
          root.TryGetProperty("data", out var data) &&
          data.ValueKind == JsonValueKind.Object &&
          data.TryGetProperty("html", out var html) &&
          html.ValueKind == JsonValueKind.String)
      {
          return html.GetString();
      }

      return null;
  }

    private static string? TryGetLastBody(AgentState state)
    {
        if (state.LastResultJson == null) return null;

        var root = state.LastResultJson.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("body", out var b) &&
            b.ValueKind == JsonValueKind.String)
        {
            return b.GetString();
        }

        return null;
    }

    private static string? TryGetLastDataHtml(AgentState state)
    {
        if (state.LastResultJson == null) return null;
        var root = state.LastResultJson.RootElement;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("html", out var html) &&
            html.ValueKind == JsonValueKind.String)
        {
            return html.GetString();
        }
        return null;
    }
}