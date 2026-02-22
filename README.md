# AgentCore

AgentCore e um framework em C# para criar agentes, equipes multiagentes e workflows stateful com checkpoint, HITL (human in the loop) e tool calling nativo.

## Status atual

O projeto ja suporta:

- Multi-provedor de LLM (OpenAI, Anthropic, Gemini, OpenRouter, Groq, Ollama local)
- Modo legado `plan -> steps -> orchestrator`
- Modo nativo de tool calling para provedores OpenAI-compatible
- Runtime de workflow com estado e checkpoint
- Blueprint de equipe `Researcher -> Writer -> Reviewer`
- Human-in-the-loop no workflow (aprovacao final humana)
- Logs estruturados em JSONL por execucao

---

## 1) Setup rapido

Requisitos:

- .NET 9 SDK
- Chave de API do provedor escolhido (ou Ollama local)

Clone e rode:

```bash
dotnet restore AgentCore.sln
dotnet build AgentCore.sln
dotnet run --project AgentCore.csproj
```

Copie `.env.sample` para `.env` e configure as variaveis.

---

## 2) Provedores de IA suportados

Selecione o provedor com `LLM_PROVIDER`.

Suportados hoje:

- `openai`
- `anthropic`
- `gemini`
- `openrouter`
- `groq`
- `ollama`

O factory esta em `LLM/LlmClientFactory.cs`.

### Exemplo de `.env` para OpenAI

```env
LLM_PROVIDER=openai
OPENAI_API_KEY=...
OPENAI_MODEL=gpt-4.1
```

### Exemplo de `.env` para Anthropic

```env
LLM_PROVIDER=anthropic
ANTHROPIC_API_KEY=...
ANTHROPIC_MODEL=claude-3-5-sonnet-latest
```

### Exemplo de `.env` para Gemini

```env
LLM_PROVIDER=gemini
GEMINI_API_KEY=...
GEMINI_MODEL=gemini-1.5-pro
```

### Exemplo de `.env` para OpenRouter

```env
LLM_PROVIDER=openrouter
OPENROUTER_API_KEY=...
OPENROUTER_MODEL=openai/gpt-4o-mini
OPENROUTER_SITE_URL=https://seu-app.com
OPENROUTER_APP_NAME=AgentCore
```

### Exemplo de `.env` para Groq

```env
LLM_PROVIDER=groq
GROQ_API_KEY=...
GROQ_MODEL=llama-3.3-70b-versatile
```

### Exemplo de `.env` para Ollama local

```env
LLM_PROVIDER=ollama
OLLAMA_BASE_URL=http://localhost:11434/v1
OLLAMA_MODEL=llama3.1
```

---

## 3) Perfis disponiveis no console

`Program.cs` expoe 3 perfis:

1. `NewsTTSWriterAgent`
2. `WebResearchAgent`
3. `TeamWorkflowResearchWriterReviewer` (novo)

O perfil 3 executa um workflow multiagente com checkpoint e aprovacao humana.

---

## 4) Workflows, equipes e estado

### Runtime de workflow

Arquivos principais:

- `core/workflows/WorkflowRunner.cs`
- `core/workflows/WorkflowState.cs`
- `core/workflows/WorkflowDefinition.cs`
- `core/workflows/IWorkflowCheckpointStore.cs`

### Checkpoint stores

- `InMemoryWorkflowCheckpointStore`
- `JsonFileWorkflowCheckpointStore` (persistencia em disco)

### Team blueprint pronto

Factory pronta:

- `ResearchWriterReviewerWorkflowFactory`

Sequencia:

`research -> writer -> reviewer -> review_router -> human_gate -> publish`

Se reviewer pedir revisao, volta para `writer` ate `maxRevisions`.

### API intuitiva para construir equipes/workflows

Voce pode montar suas equipes e workflows via builders:

- `TeamBuilder`
- `WorkflowBuilder`

Exemplo:

```csharp
using AgentCore.Core.Workflows;

var team = TeamBuilder.Create("content_team")
    .AddMember("researcher", "Research Analyst", "Find reliable evidence")
    .AddMember("writer", "Technical Writer", "Write clear markdown")
    .AddMember("reviewer", "Quality Reviewer", "Approve or request revision")
    .Build();

var workflow = WorkflowBuilder.Create("custom_flow")
    .AddLlmTask(
        name: "research",
        role: "Research Analyst",
        instructions: "Find key facts",
        inputTemplate: "Objective: {{objective}}",
        outputStateKey: "research.output",
        nextNodeName: "draft")
    .AddLlmTask(
        name: "draft",
        role: "Writer",
        instructions: "Write concise markdown",
        inputTemplate: "Research:\n{{research.output}}",
        outputStateKey: "draft.output",
        nextNodeName: null)
    .StartWith("research")
    .Build();
```

---

## 5) Human in the loop (HITL)

HITL esta disponivel em dois niveis:

1. **Workflow node**: `HumanApprovalNode`
   - interrompe fluxo e pede aprovacao no console
   - pode seguir para branch de aprovado/rejeitado

2. **Tool**: `human` (`HumanInteractionTool`)
   - acoes: `confirm`, `ask_text`, `ask_choice`

---

## 6) Logs

Workflow runner suporta logs estruturados:

- `ConsoleWorkflowLogger`
- `JsonlWorkflowLogger`
- `CompositeWorkflowLogger`

No perfil de equipe, logs sao salvos em:

- `AgentWorkspace/.logs/<runId>.jsonl`

Eventos incluem:

- `run_started`
- `checkpoint_loaded`
- `node_started`
- `node_completed`
- `checkpoint_saved`
- `run_completed`

---

## 7) Toolkits (inspirado em Agno)

Toolkits catalogados em `Tools/ToolkitCatalog.cs`.

### Implementados atualmente

**Local toolkit style**

- `filesystem`
- `shell` (opcional, exige `ALLOW_SHELL_TOOL=1`)
- `calculator`
- `sleep`
- `datetime`
- `json`
- `csv`
- `python` (opcional, exige `ALLOW_PYTHON_TOOL=1`)
- `human`

**Search/Web toolkit style**

- `http`
- `webtools`
- `wikipedia`
- `duckduckgo`
- `tavily` (se `TAVILY_API_KEY` estiver definido)

**Browser automation**

- `browser` (pode ser adicionado manualmente)

### Sobre "maximo de tools"

O Agno possui dezenas de integrações externas (Slack, Gmail, Notion, Jira, etc.).
Neste repo, foi criada a base para expansao rapida sem quebrar o runtime.
As proximas integrações podem ser adicionadas como novos `ITool` e plugadas no `ToolkitCatalog`.

---

## 8) Tool calling nativo

`ILLMClient` foi estendido com:

- `SupportsToolCalling`
- `CompleteWithToolsAsync(...)`

`OpenAiChatClient` e `OpenAiCompatibleChatClient` implementam tool calling nativo.
Hoje isso cobre OpenAI e provedores OpenAI-compatible (OpenRouter, Groq, Ollama).
Anthropic e Gemini estao integrados para completions e podem receber tool calling em iteracao futura.

No `Agent`, use:

```csharp
var finalText = await agent.RunWithNativeToolCallingAsync("Your objective");
```

---

## 9) Seguranca e boas praticas

- Nao hardcode secrets no codigo.
- Use `.env` + variaveis de ambiente.
- `shell` vem desabilitado por padrao.
- `filesystem` protege contra path traversal fora do workspace.

---

## 10) Comandos de qualidade

```bash
dotnet build AgentCore.sln
dotnet format AgentCore.sln --verify-no-changes
dotnet test AgentCore.sln
```

Observacao: ainda nao existe projeto de testes dedicado no repo.

---

## 11) Roadmap recomendado (proximos passos)

1. Adicionar store de checkpoint em SQLite/Postgres.
2. Suportar paralelismo de nodes (fan-out/fan-in).
3. Adicionar processador hierarquico (manager agent).
4. Expandir toolkits externos (Slack, Gmail, Notion, Jira, GitHub, etc.).
5. Adicionar testes E2E para garantir que nenhum `{{template}}` seja persistido.
