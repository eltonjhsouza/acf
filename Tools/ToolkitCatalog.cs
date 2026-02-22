namespace AgentCore.Tools;

public static class ToolkitCatalog
{
    public static IReadOnlyList<ITool> BuildDefault(
        string workspaceRoot,
        string? tavilyApiKey = null,
        bool enableShell = false)
    {
        var tools = new List<ITool>
        {
            // Local toolkit style
            new FileSystemTool(rootPath: workspaceRoot),
            new ShellTool(workingDirectory: workspaceRoot, enabled: enableShell),
            new CalculatorTool(),
            new SleepTool(),
            new DateTimeTool(),
            new JsonTool(),
            new CsvTool(rootPath: workspaceRoot),
            new PythonTool(workingDirectory: workspaceRoot),
            new HumanInteractionTool(),

            // Search/Web toolkit style
            new HttpTool(),
            new WebToolsTool(),
            new WikipediaTool(),
            new DuckDuckGoTool()
        };

        if (!string.IsNullOrWhiteSpace(tavilyApiKey))
            tools.Add(new TavilySearchTool(tavilyApiKey));

        return tools;
    }
}
