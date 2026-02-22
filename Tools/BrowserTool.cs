using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

namespace AgentCore.Tools;

public sealed class BrowserTool : ITool, IAsyncDisposable
{
    private readonly bool _headless;
    private readonly int _slowMoMs;
    private readonly int _viewportWidth;
    private readonly int _viewportHeight;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;

    public BrowserTool(
        bool headless = true,
        int slowMoMs = 0,
        int viewportWidth = 1280,
        int viewportHeight = 720)
    {
        _headless = headless;
        _slowMoMs = slowMoMs < 0 ? 0 : slowMoMs;
        _viewportWidth = viewportWidth <= 0 ? 1280 : viewportWidth;
        _viewportHeight = viewportHeight <= 0 ? 720 : viewportHeight;
    }

    public ToolSpec Spec => new ToolSpec
    {
        Name = "browser",
        Description = "Browser automation using Playwright (goto, scroll, html, screenshot, click, fill).",
        JsonSchema =
            """
            {
              "type":"object",
              "properties":{
                "action":{"type":"string","enum":[
                  "goto","html","text","screenshot",
                  "scroll_to_bottom","wait","current_url",
                  "click","fill"
                ]},
                "url":{"type":"string","description":"Target URL for goto"},
                "selector":{"type":"string","description":"CSS selector for click/fill"},
                "text":{"type":"string","description":"Text for fill"},
                "path":{"type":"string","description":"Relative path for screenshot (e.g. screenshots/page.png)"},
                "ms":{"type":"integer","description":"Milliseconds to wait (wait action)"},
                "scrollStep":{"type":"integer","description":"Pixels per scroll step (scroll_to_bottom)"},
                "scrollDelayMs":{"type":"integer","description":"Delay per step in ms (scroll_to_bottom)"},
                "maxScrolls":{"type":"integer","description":"Max scroll iterations (scroll_to_bottom)"}
              },
              "required":["action"]
            }
            """
    };

    public async Task<string> ExecuteAsync(string inputJson)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
            return Fail("invalid_request", "empty inputJson");

        BrowserRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<BrowserRequest>(inputJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            return Fail("invalid_json", ex.Message);
        }

        if (req == null)
            return Fail("invalid_request", "could not parse JSON");

        var action = (req.Action ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(action))
            return Fail("invalid_request", "missing action");

        try
        {
            await EnsureStartedAsync();

            switch (action)
            {
                case "goto":
                    return await HandleGotoAsync(req);

                case "wait":
                    return await HandleWaitAsync(req);

                case "current_url":
                    return Ok(new { action, url = _page!.Url });

                case "scroll_to_bottom":
                    return await HandleScrollToBottomAsync(req);

                case "html":
                    return await HandleHtmlAsync(req);

                case "text":
                    return await HandleTextAsync(req);

                case "screenshot":
                    return await HandleScreenshotAsync(req);

                case "click":
                    return await HandleClickAsync(req);

                case "fill":
                    return await HandleFillAsync(req);

                default:
                    return Fail("invalid_action", $"Unknown action '{action}'");
            }
        }
        catch (PlaywrightException ex)
        {
            return Fail("playwright_error", ex.Message);
        }
        catch (Exception ex)
        {
            return Fail("error", ex.Message);
        }
    }

    private async Task<string> HandleGotoAsync(BrowserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Url))
            return Fail("invalid_request", "goto requires url");

        var url = req.Url.Trim();

        await _page!.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60000
        });

        return Ok(new { action = "goto", url = _page.Url });
    }

    private async Task<string> HandleWaitAsync(BrowserRequest req)
    {
        var ms = req.Ms is > 0 ? req.Ms.Value : 1000;
        await _page!.WaitForTimeoutAsync(ms);
        return Ok(new { action = "wait", ms });
    }

    private async Task<string> HandleScrollToBottomAsync(BrowserRequest req)
    {
        var step = req.ScrollStep is > 0 ? req.ScrollStep.Value : 1200;
        var delay = req.ScrollDelayMs is > 0 ? req.ScrollDelayMs.Value : 300;
        var max = req.MaxScrolls is > 0 ? req.MaxScrolls.Value : 40;

        var lastHeight = await _page!.EvaluateAsync<int>("() => document.body.scrollHeight");

        int iterations;
        for (iterations = 0; iterations < max; iterations++)
        {
            await _page.EvaluateAsync("([s]) => window.scrollBy(0, s)", new object[] { step });
            await _page.WaitForTimeoutAsync(delay);

            var newHeight = await _page.EvaluateAsync<int>("() => document.body.scrollHeight");
            var atBottom = await _page.EvaluateAsync<bool>(
                "() => (window.innerHeight + window.scrollY) >= (document.body.scrollHeight - 2)");

            if (newHeight == lastHeight && atBottom)
                break;

            lastHeight = newHeight;
        }

        return Ok(new
        {
            action = "scroll_to_bottom",
            iterations,
            scrollStep = step,
            scrollDelayMs = delay,
            maxScrolls = max
        });
    }

    private async Task<string> HandleHtmlAsync(BrowserRequest req)
    {
        var html = await _page!.ContentAsync();
        return Ok(new { action = "html", html });
    }

    private async Task<string> HandleTextAsync(BrowserRequest req)
    {
        var text = await _page!.InnerTextAsync("body");
        return Ok(new { action = "text", text });
    }

    private async Task<string> HandleScreenshotAsync(BrowserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Path))
            return Fail("invalid_request", "screenshot requires path");

        var rel = req.Path.Trim().Replace('\\', '/');

        var dir = Path.GetDirectoryName(rel);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        await _page!.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = rel,
            FullPage = true
        });

        return Ok(new { action = "screenshot", path = rel });
    }

    private async Task<string> HandleClickAsync(BrowserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Selector))
            return Fail("invalid_request", "click requires selector");

        var sel = req.Selector.Trim();
        await _page!.ClickAsync(sel, new PageClickOptions { Timeout = 30000 });
        return Ok(new { action = "click", selector = sel });
    }

    private async Task<string> HandleFillAsync(BrowserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Selector))
            return Fail("invalid_request", "fill requires selector");

        var sel = req.Selector.Trim();
        await _page!.FillAsync(sel, req.Text ?? "", new PageFillOptions { Timeout = 30000 });
        return Ok(new { action = "fill", selector = sel });
    }

    private async Task EnsureStartedAsync()
    {
        if (_playwright != null && _browser != null && _context != null && _page != null)
            return;

        _playwright = await Playwright.CreateAsync();

        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _headless,
            SlowMo = _slowMoMs
        });

        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = _viewportWidth, Height = _viewportHeight },
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AgentCoreBrowser/1.0"
        });

        _page = await _context.NewPageAsync();
    }

    private static string Ok(object data)
        => JsonSerializer.Serialize(new { ok = true, data });

    private static string Fail(string code, string message)
        => JsonSerializer.Serialize(new { ok = false, error = code, message });

    public async ValueTask DisposeAsync()
    {
        try { if (_page != null) await _page.CloseAsync(); } catch { }
        try { if (_context != null) await _context.CloseAsync(); } catch { }
        try { if (_browser != null) await _browser.CloseAsync(); } catch { }
        try { _playwright?.Dispose(); } catch { }
    }

    private sealed class BrowserRequest
    {
        [JsonPropertyName("action")] public string? Action { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("selector")] public string? Selector { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("path")] public string? Path { get; set; }

        [JsonPropertyName("ms")] public int? Ms { get; set; }

        [JsonPropertyName("scrollStep")] public int? ScrollStep { get; set; }
        [JsonPropertyName("scrollDelayMs")] public int? ScrollDelayMs { get; set; }
        [JsonPropertyName("maxScrolls")] public int? MaxScrolls { get; set; }
    }
}