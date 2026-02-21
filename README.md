# AgentCore

**AgentCore** é um framework modular em C# para criação de agentes de IA autônomos que executam tarefas complexas utilizando Large Language Models (LLMs) e ferramentas especializadas.

## 🚀 Características

- **API Fluente**: Interface intuitiva para configuração de agentes
- **Planejamento Automático**: LLM gera automaticamente planos de execução em múltiplos passos
- **Sistema de Ferramentas**: Arquitetura extensível para adicionar novas capacidades
- **Orquestração Inteligente**: Execução sequencial e verificada de tarefas
- **Integração OpenAI**: Suporte nativo para modelos GPT

## 📦 Requisitos

- .NET 9.0
- Chave de API da OpenAI

## 🎯 Exemplo de Uso

```csharp
using AgentCore.Core;
using AgentCore.LLM;
using AgentCore.Tools;

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var llm = new OpenAiChatClient(apiKey, "gpt-4.1");

var agent = Agent.Create("General", llm)
    .WithInstructions("You are a safe execution agent. Always follow tool contracts.")
    .WithTool(new FileSystemTool());

await agent.RunAsync("Cria pasta com o nome '_aquivo' e dentro dela um arquivo 'hello.txt' com o conteúdo 'Hello, World!'");
```

## 🏗️ Arquitetura

O projeto está organizado em módulos especializados:

### Core (`core/`)
- **Agent.cs**: Classe principal que define a API fluente
- **AgentOrchestrator.cs**: Coordena a execução dos passos planejados
- **AgentState.cs**: Mantém o estado de execução do agente
- **TaskDefinition.cs**: Define objetivos e restrições de tarefas
- **StepDefinition.cs**: Representa um passo individual do plano

### LLM (`LLM/`)
- **ILLMClient.cs**: Interface para clientes LLM
- **LlmClient.cs**: Cliente genérico LLM
- **OpenAiChatClient.cs**: Implementação específica para OpenAI
- **Planner.cs**: Gera planos de execução usando LLM

### Tools (`Tools/`)
- **ITool.cs**: Interface base para ferramentas
- **FileSystemTool.cs**: Operações de sistema de arquivos (criar diretórios, ler/escrever arquivos, listar conteúdo)
- **BrowserTool.cs**: Operações de navegação web (em desenvolvimento)
- **HttpTool.cs**: Requisições HTTP (em desenvolvimento)

### Execution (`Execution/`)
- **StepExecutor.cs**: Executa passos individuais
- **StepVerifier.cs**: Verifica a execução dos passos

## 🛠️ Ferramentas Disponíveis

### FileSystemTool

Permite operações seguras no sistema de arquivos dentro de um workspace isolado.

**Ações suportadas:**
- `create_directory`: Criar diretórios
- `write_file`: Escrever arquivos
- `read_file`: Ler conteúdo de arquivos
- `list`: Listar conteúdo de diretórios

**Exemplo de entrada:**
```json
{
  "action": "write_file",
  "path": "pasta/arquivo.txt",
  "content": "Conteúdo do arquivo"
}
```

## 🔄 Fluxo de Execução

1. **Definição da Tarefa**: O usuário define um objetivo em linguagem natural
2. **Planejamento**: O LLM analisa o objetivo e gera um plano estruturado em passos
3. **Orquestração**: O AgentOrchestrator coordena a execução sequencial dos passos
4. **Execução**: Cada passo invoca a ferramenta apropriada com os parâmetros corretos
5. **Verificação**: Resultados são capturados e validados
6. **Conclusão**: Tarefa é marcada como completa

## 📝 Configuração

### Variável de Ambiente

Configure sua chave de API da OpenAI:

```bash
# Windows PowerShell
$env:OPENAI_API_KEY="sua-chave-aqui"

# Windows CMD
set OPENAI_API_KEY=sua-chave-aqui

# Linux/Mac
export OPENAI_API_KEY=sua-chave-aqui
```

### Workspace

Por padrão, o `FileSystemTool` cria um diretório `AgentWorkspace` na raiz do projeto onde todas as operações de arquivo são executadas de forma segura e isolada.

## 🚀 Como Executar

```bash
# Clone o repositório
git clone <url-do-repositorio>

# Navegue até o diretório
cd AgentCore

# Configure a chave da API
$env:OPENAI_API_KEY="sua-chave-aqui"

# Execute o projeto
dotnet run
```

## 🔧 Extensibilidade

### Criando uma Nova Ferramenta

Implemente a interface `ITool`:

```csharp
public class MinhaFerramenta : ITool
{
    public string Name => "minha_ferramenta";

    public async Task<string> ExecuteAsync(string inputJson)
    {
        // Desserialize inputJson
        // Execute a lógica da ferramenta
        // Retorne o resultado como string
    }
}
```

### Adicionando ao Agente

```csharp
var agent = Agent.Create("General", llm)
    .WithTool(new FileSystemTool())
    .WithTool(new MinhaFerramenta());
```

## 📊 Estado do Projeto

### Implementado ✅
- Sistema de planejamento com LLM
- Orquestração de tarefas
- FileSystemTool funcional
- API fluente para agentes
- Integração OpenAI

### Em Desenvolvimento 🚧
- BrowserTool
- HttpTool
- Sistema de verificação de passos
- Tratamento de erros avançado

## 🤝 Contribuindo

Contribuições são bem-vindas! Sinta-se à vontade para abrir issues ou pull requests.

## 📄 Licença

Este projeto está sob licença MIT. Veja o arquivo LICENSE para mais detalhes.

## 👨‍💻 Autor

Desenvolvido com interesse em agentes de IA autônomos e automação inteligente.

---

**Nota**: Este é um projeto experimental focado em explorar padrões de design para agentes de IA autônomos em .NET.
