# AGENTS.md
Repository guidance for agentic coding assistants working in `AgentCore`.

## 1) Project Snapshot
- Stack: C# + .NET 9 (`TargetFramework: net9.0`).
- App type: console app (`AgentCore.csproj`, `OutputType=Exe`).
- Solution: `AgentCore.sln` (single project currently).
- Main folders:
  - `core/`: planner, orchestrator, state, template, repair.
  - `LLM/`: model client abstraction + OpenAI chat client.
  - `Tools/`: tool contracts and implementations (`filesystem`, `http`, `browser`, `tavily`).
  - `Execution/`: placeholder files currently.
- Project options: nullable enabled, implicit usings enabled.

## 2) Build/Lint/Test Commands
Run from repo root: `C:\playground\c#\agentes\AgentCore`.

### Restore
```bash
dotnet restore AgentCore.sln
```
### Build
```bash
dotnet build AgentCore.sln
dotnet build AgentCore.sln -c Release
```
### Run
```bash
dotnet run --project AgentCore.csproj
```
### Format and lint
```bash
# Apply formatting fixes
dotnet format AgentCore.sln

# CI-style verification (fails on formatting/analyzer issues)
dotnet format AgentCore.sln --verify-no-changes

# Optional stricter compile gate
dotnet build AgentCore.sln -warnaserror
```
Notes:
- `dotnet format --verify-no-changes` currently reports whitespace issues in existing files.
- No repo-level `.editorconfig` or custom `.ruleset` exists today.

### Test
```bash
dotnet test AgentCore.sln
```
Current state:
- No `*.Tests.csproj` exists yet.
- `dotnet test` mostly restores/builds and does not run a real suite today.

### Run a single test (important)
Use these once a test project exists.
```bash
# Run one test project
dotnet test <path-to-test-csproj>

# List tests first
dotnet test <path-to-test-csproj> --list-tests

# Run one test by fully qualified name
dotnet test <path-to-test-csproj> --filter "FullyQualifiedName~Namespace.ClassName.MethodName"

# Run one test by method name
dotnet test <path-to-test-csproj> --filter "Name=MethodName"
```
Suggested pattern for this repo:
```bash
dotnet test tests/AgentCore.Tests/AgentCore.Tests.csproj --filter "FullyQualifiedName~AgentCore.Tests.SomeType.SomeTest"
```

## 3) Code Style Guide
Follow existing repository patterns first; use these defaults for new code.

### Imports and file structure
- Keep `using System.*` before project usings.
- Keep usings at file top, outside namespace.
- Prefer file-scoped namespaces (`namespace AgentCore.Core;`).
- Keep one top-level public type per file.
- Match namespace to folder (`AgentCore.Core`, `AgentCore.LLM`, `AgentCore.Tools`).
- Remove unused usings.

### Formatting
- Use 4 spaces for indentation; do not use tabs.
- Use braces for multi-line blocks; single-line blocks only when clear.
- Split long calls and initializers across lines for readability.
- Keep blank lines between logical blocks.
- Run `dotnet format AgentCore.sln` after non-trivial edits.

### Types and nullability
- Respect nullable annotations (`string?` vs `string`).
- Initialize non-nullable members.
- Validate inputs with guard clauses (`string.IsNullOrWhiteSpace`).
- Prefer `var` when type is obvious; otherwise use explicit types.
- Use `required` properties for required metadata contracts (as in `ToolSpec`).
- Avoid null-forgiving (`!`) unless an invariant is proven.

### Naming conventions
- Types/methods/properties/constants: `PascalCase`.
- Local variables and parameters: `camelCase`.
- Private fields: `_camelCase`.
- Interfaces: `I` prefix (`ITool`, `ILLMClient`).
- Async methods: suffix `Async`.
- Tool JSON fields: stable, explicit, backward-compatible.

### Async and networking
- Prefer async/await end-to-end.
- Return `Task` / `Task<T>` from async APIs.
- Avoid blocking async calls (`.Wait()`, `.Result`).
- Keep explicit timeouts in HTTP/browser code.
- Return actionable network error messages.

### Error handling
- Validate early and fail fast.
- Catch specific exceptions before `Exception`.
- Throw for unrecoverable orchestration failures.
- Return structured contract errors at tool boundaries.
- Include context (tool/action/path) in errors, never secrets.

### JSON and tool contracts
- Keep `ToolSpec.JsonSchema` aligned with behavior.
- Use `JsonPropertyName` where field names are contract-bound.
- Parse tool input with case-insensitive JSON options.
- Prefer response envelopes like `{ ok, data, error }`.
- If contract fields change, update planner/repair prompts.

### Security and secrets
- Never hardcode tokens, API keys, or credentials.
- Load secrets from environment variables (`OPENAI_API_KEY`, `TAVILY_API_KEY`).
- `.env` is gitignored; do not commit real secret values.
- Existing hardcoded local keys are technical debt; do not replicate.

### Filesystem safety
- Keep file operations inside the configured workspace root.
- Defend against path traversal (`..`, absolute path escapes).
- Prefer relative paths in tool payloads unless absolute paths are required.

## 4) Testing Strategy (when adding tests)
- Preferred test project name: `AgentCore.Tests`.
- Suggested location: `tests/AgentCore.Tests/`.
- Keep tests deterministic; avoid live network dependencies by default.
- Mock/stub HTTP and LLM interactions in unit tests.
- Add focused unit tests for new behavior.
- Add at least one high-value integration path test when relevant.

## 5) Cursor/Copilot Rules
Checked and not found in this repository:
- `.cursor/rules/`
- `.cursorrules`
- `.github/copilot-instructions.md`
If these files are added later, include and honor them.

## 6) Pre-Completion Checklist
Before concluding substantial work, run:
```bash
dotnet build AgentCore.sln
dotnet format AgentCore.sln --verify-no-changes
dotnet test AgentCore.sln
```
If tests are still absent, report that explicitly with command output.
