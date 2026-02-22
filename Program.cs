using AgentCore.Core;
using AgentCore.Core.Workflows;
using AgentCore.LLM;
using AgentCore.Tools;

try
{
    DotNetEnv.Env.Load(".env", DotNetEnv.Env.TraversePath());
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: failed to load .env automatically: {ex.Message}");
}

// =====================
// Config (workspace + LLM)
// =====================
var provider = Environment.GetEnvironmentVariable("LLM_PROVIDER") ?? "openai";
var model = Environment.GetEnvironmentVariable("LLM_MODEL");
var tavilyKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
var enableShell = string.Equals(Environment.GetEnvironmentVariable("ALLOW_SHELL_TOOL"), "1", StringComparison.OrdinalIgnoreCase);

// Workspace cross-platform (Linux/Windows)
var workspaceRoot =
    Environment.GetEnvironmentVariable("AGENT_WORKSPACE") ??
    Path.Combine(Directory.GetCurrentDirectory(), "AgentWorkspace");

Directory.CreateDirectory(workspaceRoot);

ILLMClient llm;
try
{
    llm = LlmClientFactory.Create(provider, model);
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to initialize LLM provider '{provider}': {ex.Message}");
    Console.WriteLine("Current directory: " + Directory.GetCurrentDirectory());
    Console.WriteLine("Required env vars for this provider: " + string.Join(", ", GetRequiredVars(provider)));
    Console.WriteLine("Tip: verify your .env is in this repo (or parent) and contains the exact variable names.");
    return;
}

// =====================
// Menu de perfis/agentes
// =====================
var profiles = new Dictionary<int, (string Name, string Instructions, string DefaultObjective)>
{
    [1] = (
    "NewsTTSWriterAgent",
    """
    Role: NewsTTSWriterAgent
    Goal: Search Google News for the informed topic, collect reliable and recent information,
    and generate a journalistic text optimized for Text-to-Speech (TTS) narration.

    Rules:
    - Prefer using browser to access Google News (https://news.google.com).
    - Always search using the provided topic.
    - Filter by most recent news when possible.
    - Open at least 3 relevant and reliable sources.
    - Extract: headline, source, date, key facts.
    - Cross-check key information between sources.
    - Do NOT invent facts.
    - If there are conflicting reports, clearly state that information differs between sources.
    - Write in a neutral, professional, broadcast-news tone.
    - Optimize text for TTS:
        * Use short and clear sentences.
        * Avoid complex punctuation.
        * Avoid abbreviations unless spelled out.
        * Write numbers in full when necessary for better pronunciation.
        * Avoid symbols that break speech synthesis.
        * Use natural pauses with line breaks.
    - Produce continuous narration text (no stage directions).
    - Format the final output in Markdown (.md).
    - Save the final content as noticia_tts.md in the root directory.
    """,
    "Busque no Google Notícias sobre 'assunto informado pelo usuário' e gere um texto jornalístico pronto para narração em TTS."
),
[2] = (
  "WebResearchAgent",
  """
Role: WebResearchAgent
Goal: Research topics on the internet using the Tavily tool and produce a structured report.

NON-NEGOTIABLE:
- The final step MUST be a filesystem.write_file saving a Markdown report to "pesquisa.md".
- The content MUST be the final report itself (no placeholders).

Process:
1) Use tavily with search_depth="advanced", max_results=5..8, include_answer=true.
2) Use the result URLs (top 3-5) and (optionally) open them with browser for confirmation.
3) Write a Markdown report with:
   - Summary
   - Key points (bullets)
   - Sources (list of URLs)
4) Do not invent facts. If sources conflict, state it.
""",
  "Pesquise sobre 'IA para automação de tarefas no WhatsApp' e salve um relatório em pesquisa.md no root."
),
[3] = (
  "TeamWorkflowResearchWriterReviewer",
  "Multi-agent team workflow with checkpoints.",
  "Pesquise e escreva um mini-relatorio sobre 'automacao de processos com IA' com revisao critica."
),
[4] = (
  "ExampleNativeToolCalling",
  "You are a practical assistant. Use tools directly to satisfy the objective with minimal text.",
  "Crie um arquivo examples/native_tool_calling.md contendo: provider atual, data/hora atual e uma lista dos arquivos do workspace."
),
[5] = (
  "ExampleCustomBuilderWorkflow",
  "Demonstrates TeamBuilder + WorkflowBuilder + HITL + checkpoints.",
  "Produza um resumo curto sobre 'como adotar agentes em operacoes internas' em markdown objetivo."
),
[6] = (
  "ExampleDirectTools",
  "Demonstrates direct toolkit usage without LLM planning.",
  "Execute um demo das tools locais (datetime, calculator e filesystem)."
)
};

Console.WriteLine("========================================");
Console.WriteLine(" AgentCore - Agent Profiles (NET 9)");
Console.WriteLine(" Workspace: " + workspaceRoot);
Console.WriteLine(" Provider: " + provider);
Console.WriteLine(" Model: " + (model ?? "<provider default>"));
Console.WriteLine("========================================");
foreach (var kv in profiles.OrderBy(k => k.Key))
    Console.WriteLine($"{kv.Key,2}) {kv.Value.Name}");
Console.WriteLine(" 0) Exit");
Console.WriteLine("----------------------------------------");

Console.Write("Choose profile: ");
var choiceRaw = Console.ReadLine()?.Trim();

if (choiceRaw == "0" || string.Equals(choiceRaw, "exit", StringComparison.OrdinalIgnoreCase))
    return;

if (!int.TryParse(choiceRaw, out var choice) || !profiles.ContainsKey(choice))
{
    Console.WriteLine("Invalid option.");
    return;
}

var selected = profiles[choice];

Console.WriteLine();
Console.WriteLine($"Selected: {selected.Name}");
Console.WriteLine("Type the objective (ENTER to use default):");
var objectiveInput = Console.ReadLine();
var objective = string.IsNullOrWhiteSpace(objectiveInput) ? selected.DefaultObjective : objectiveInput.Trim();

Console.WriteLine();
Console.WriteLine("Objective:");
Console.WriteLine(objective);
Console.WriteLine();

if (selected.Name == "WebResearchAgent" && string.IsNullOrWhiteSpace(tavilyKey))
{
    Console.WriteLine("This profile expects Tavily. Set TAVILY_API_KEY in environment or .env.");
    return;
}

if (selected.Name == "TeamWorkflowResearchWriterReviewer")
{
    var team = TeamPresets.CreateResearchWriterReviewerTeam("research_writer_reviewer");
    var runId = $"rwv-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    var checkpointDir = Path.Combine(workspaceRoot, ".checkpoints");
    var logPath = Path.Combine(workspaceRoot, ".logs", runId + ".jsonl");

    var checkpointStore = new JsonFileWorkflowCheckpointStore(checkpointDir);
    var logger = new CompositeWorkflowLogger(
        new ConsoleWorkflowLogger(),
        new JsonlWorkflowLogger(logPath));

    var runtime = new TeamWorkflowRuntime(new WorkflowRunner(
        checkpointStore: checkpointStore,
        logger: logger));

    var runResult = await runtime.RunResearchWriterReviewerAsync(
        team: team,
        llm: llm,
        objective: objective,
        runId: runId,
        tools: Array.Empty<ITool>(),
        maxRevisions: 2,
        resumeFromCheckpoint: true);

    var finalOutput = runResult.FinalState.GetString("final.output")
                      ?? runResult.FinalState.GetString("writer.output")
                      ?? "No final output was produced.";

    var outFileTeam = Path.Combine(workspaceRoot, "team_output.md");
    await File.WriteAllTextAsync(outFileTeam, finalOutput);

    Console.WriteLine($"✅ Team workflow completed. RunId: {runId}");
    Console.WriteLine($"✅ Output: {outFileTeam}");
    Console.WriteLine($"✅ Logs: {logPath}");
    return;
}

if (selected.Name == "ExampleNativeToolCalling")
{
    var agentNative = Agent.Create(selected.Name, llm)
        .WithInstructions("You are a safe execution agent. Always follow tool contracts.")
        .WithInstructions(selected.Instructions);

    foreach (var tool in ToolkitCatalog.BuildDefault(workspaceRoot, tavilyKey, enableShell))
        agentNative.WithTool(tool);

    var finalText = await agentNative.RunWithNativeToolCallingAsync(objective);

    Console.WriteLine("✅ Native tool calling example executed.");
    Console.WriteLine("Assistant final message:");
    Console.WriteLine(finalText);
    return;
}

if (selected.Name == "ExampleCustomBuilderWorkflow")
{
    var team = TeamBuilder.Create("builder_demo_team")
        .WithProcess(TeamProcessType.Sequential)
        .AddMember("researcher", "Research Analyst", "Gather concise and reliable evidence.")
        .AddMember("writer", "Technical Writer", "Write clear, compact markdown for business audience.")
        .Build();

    var researcher = team.GetMemberByKeyword("research");
    var writer = team.GetMemberByKeyword("writer");

    var workflow = WorkflowBuilder.Create("builder_demo_workflow")
        .AddLlmTask(
            name: "research",
            role: researcher.Role,
            instructions: researcher.Instructions,
            inputTemplate:
                """
                Objective:
                {{objective}}

                Return:
                - Key findings
                - Practical recommendations
                """,
            outputStateKey: "research.output",
            nextNodeName: "draft")
        .AddLlmTask(
            name: "draft",
            role: writer.Role,
            instructions: writer.Instructions,
            inputTemplate:
                """
                Objective:
                {{objective}}

                Research:
                {{research.output}}

                Write a concise markdown draft.
                """,
            outputStateKey: "draft.output",
            nextNodeName: "human_gate")
        .AddHumanApproval(
            name: "human_gate",
            promptTemplate:
                """
                Review this draft and approve final delivery:

                {{draft.output}}
                """,
            approvedNextNode: "publish",
            rejectedNextNode: "draft",
            decisionKey: "human.builder_approval")
        .AddLlmTask(
            name: "publish",
            role: "Publishing Coordinator",
            instructions: "Return only the final approved markdown content.",
            inputTemplate: "{{draft.output}}",
            outputStateKey: "final.output",
            nextNodeName: null)
        .StartWith("research")
        .Build();

    var runId = $"builder-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    var checkpointDir = Path.Combine(workspaceRoot, ".checkpoints");
    var logPath = Path.Combine(workspaceRoot, ".logs", runId + ".jsonl");

    var runner = new WorkflowRunner(
        checkpointStore: new JsonFileWorkflowCheckpointStore(checkpointDir),
        logger: new CompositeWorkflowLogger(
            new ConsoleWorkflowLogger(),
            new JsonlWorkflowLogger(logPath)));

    var runtime = new TeamWorkflowRuntime(runner);
    var runResult = await runtime.RunAsync(
        workflow: workflow,
        llm: llm,
        objective: objective,
        runId: runId,
        tools: Array.Empty<ITool>(),
        resumeFromCheckpoint: true);

    var finalOutput = runResult.FinalState.GetString("final.output")
                      ?? runResult.FinalState.GetString("draft.output")
                      ?? "No final output was produced.";

    var outputPath = Path.Combine(workspaceRoot, "builder_workflow_output.md");
    await File.WriteAllTextAsync(outputPath, finalOutput);

    Console.WriteLine($"✅ Builder workflow example completed. RunId: {runId}");
    Console.WriteLine($"✅ Output: {outputPath}");
    Console.WriteLine($"✅ Logs: {logPath}");
    return;
}

if (selected.Name == "ExampleDirectTools")
{
    var tools = ToolkitCatalog.BuildDefault(workspaceRoot, tavilyKey, enableShell)
        .ToDictionary(t => t.Spec.Name, StringComparer.OrdinalIgnoreCase);

    Console.WriteLine("Available tools:");
    foreach (var name in tools.Keys.OrderBy(x => x))
        Console.WriteLine($"- {name}");

    if (tools.TryGetValue("datetime", out var dateTool))
    {
        var now = await dateTool.ExecuteAsync("{\"action\":\"now\"}");
        Console.WriteLine("datetime.now => " + now);
    }

    if (tools.TryGetValue("calculator", out var calcTool))
    {
        var calc = await calcTool.ExecuteAsync("{\"expression\":\"(12+8)*3/2\"}");
        Console.WriteLine("calculator => " + calc);
    }

    if (tools.TryGetValue("filesystem", out var fsTool))
    {
        var writeResult = await fsTool.ExecuteAsync(
            "{\"action\":\"write_file\",\"path\":\"examples/direct_tools_demo.md\",\"content\":\"# Direct Tools Demo\\nGenerated by ExampleDirectTools profile.\"}");
        Console.WriteLine("filesystem.write_file => " + writeResult);
    }

    Console.WriteLine("✅ Direct tools example completed.");
    return;
}

// =====================
// Build agent
// =====================
var agent = Agent.Create(selected.Name, llm)
    .WithInstructions("You are a safe execution agent. Always follow tool contracts.")
    .WithInstructions(selected.Instructions);

foreach (var tool in ToolkitCatalog.BuildDefault(workspaceRoot, tavilyKey, enableShell))
    agent.WithTool(tool);

// Optional heavy tool; uncomment for browser automation during manual runs.
// agent.WithTool(new BrowserTool(headless: false, slowMoMs: 100));

await agent.RunAsync(objective);

var outFile = Path.Combine(workspaceRoot, "roteiro_telejornal.md");
if (File.Exists(outFile))
    Console.WriteLine($"✅ Gerado: {outFile}");
else
    Console.WriteLine("⚠️ O agente terminou, mas não gerou roteiro_telejornal.md. O Planner provavelmente não incluiu o write_file no plano.");

static IReadOnlyList<string> GetRequiredVars(string provider)
{
    var normalized = (provider ?? "").Trim().ToLowerInvariant();

    return normalized switch
    {
        "openai" => new[] { "OPENAI_API_KEY" },
        "anthropic" => new[] { "ANTHROPIC_API_KEY" },
        "gemini" => new[] { "GEMINI_API_KEY" },
        "openrouter" => new[] { "OPENROUTER_API_KEY" },
        "groq" => new[] { "GROQ_API_KEY" },
        "ollama" => Array.Empty<string>(),
        _ => new[] { "LLM_PROVIDER" }
    };
}
