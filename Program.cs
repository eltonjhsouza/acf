using AgentCore.Core;
using AgentCore.LLM;
using AgentCore.Tools;

DotNetEnv.Env.Load();



var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

var llm = new OpenAiChatClient(apiKey, "gpt-4.1");

var agent = Agent.Create("General", llm)
    .WithInstructions("You are a safe execution agent. Always follow tool contracts.")
    .WithTool(new FileSystemTool());

await agent.RunAsync("Cria pasta com o nome '_aquivo' e dentro dela um arquivo 'hello.txt' com o conteúdo 'Hello, World!'");