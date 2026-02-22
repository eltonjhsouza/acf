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
    .WithTool(new FileSystemTool(rootPath: @"C:\playground\c#\agentes\AgentWorkspace"))
    .WithTool(new BrowserTool(headless: false, slowMoMs: 100));
    // .WithTool(new HttpTool());


// await agent.RunAsync("Use http tool para buscar https://iacopi.com.br/ e salve o body em iacopi.html no filesystem.");
// await agent.RunAsync("Mostre o diretório atual (pwd).");
// await agent.RunAsync("Mostre o diretório atual (pwd) e depois liste a raiz.");
// await agent.RunAsync("Quais preços existem nesse site https://www.kabum.com.br/ e salve o resultado em kabun.txt");
await agent.RunAsync("Acesse a página https://www.globo.com/ , faça o scroll até o fim e salve em page1.html no root");
// await agent.RunAsync("Mostre o diretório atual (pwd).");
// await agent.RunAsync("Crie a pasta Research, entre nela (cd) e liste (list) o conteúdo.");
// await agent.RunAsync("Use http para buscar https://example.com e salve em page.html no diretório atual.");