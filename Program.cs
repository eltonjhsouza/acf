using AgentCore.Core;
using AgentCore.LLM;
using AgentCore.Tools;

DotNetEnv.Env.Load();



var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("Please set the OPENAI_API_KEY environment variable.");
    return;
}
var llm = new OpenAiChatClient(apiKey, "gpt-4.1");

var agent = Agent.Create("General", llm)
    .WithInstructions("You are a safe execution agent. Always follow tool contracts.")
    .WithTool(new FileSystemTool(rootPath: @"C:\playground\c#\agentes\AgentWorkspace"));


string action = "corrija o arquivo index.hml por index.html e adicione a tag <title> com o texto 'Página Inicial'";
await agent.RunAsync(action);