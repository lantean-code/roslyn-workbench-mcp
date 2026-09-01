# Tool Metadata Slimming Implementation Plan

**Status:** Historical implementation plan. Configurable output-schema publication and concise tool metadata are implemented; current behaviour is documented in [Tool discovery and results](../../../content/tool-discovery.md). This document is not an active worklist.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce `tools/list` payload size enough that agents can discover tools without burning large amounts of context on verbose `outputSchema` publication.

**Architecture:** Keep runtime structured tool results exactly as they are today, keep generated `inputSchema`, and stop treating published `outputSchema` as mandatory metadata. The preferred implementation is to omit `outputSchema` by default, keep the full contract in repository docs, and add only short high-signal result hints where a caller needs them before the first tool invocation.

**Tech Stack:** .NET 10, C#, ModelContextProtocol C# SDK, System.Text.Json, xUnit, Moq, AwesomeAssertions.

---

## File Map

**Primary production files**

- Modify: `docs/RoslynMcpToolDesign.md`
- Modify: `docs/RoslynMcpToolContracts.md`
- Modify: `src/Roslyn.Workbench.Mcp.Plugins/RegisteredTool.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Plugins/ToolRegistrationMetadata.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Plugins/PluginRegistry.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Plugins/ToolSchemaFactory.cs`
- Modify: `src/Roslyn.Workbench.Mcp/PluginMcpServerTool.cs`
- Modify: `src/Roslyn.Workbench.Mcp/ServerToolMcpServerTool.cs`
- Modify: `src/Roslyn.Workbench.Mcp/StartupOptions.cs`
- Modify: `src/Roslyn.Workbench.Mcp/StartupOptionsParser.cs`
- Modify: `src/Roslyn.Workbench.Mcp/Program.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Server/ServerConfiguration.cs`
- Create: `src/Roslyn.Workbench.Mcp.Plugins/ToolOutputSchemaMode.cs`

**Likely supporting production files**

- Modify: `src/Roslyn.Workbench.Mcp/WorkspaceLifecycleToolFactory.cs`
- Modify: `src/Roslyn.Workbench.Mcp/TransactionToolFactory.cs`
- Modify: selected files under `src/Roslyn.Workbench.Mcp.Plugins.Core/` only if a short result hint is genuinely needed for specific tools such as `list-code-actions`

**Tests**

- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Test/ToolSchemaFactoryTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Contracts.Test/Schema/SchemaGenerationTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Test/PluginDiscoveryAndMcpToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Test/WorkspaceLifecycleToolTests.cs` if server-owned tool metadata assertions need updating

## Design Rules For This Change

- Do not publish a fake or lossy `outputSchema` that claims a smaller shape than the JSON actually returned.
- Keep `inputSchema` generation intact. The tool request shape is still the most useful discovery aid before the first call.
- Keep runtime response DTOs and `ToolResult<TData>` unchanged for this plan.
- Assume enum readability is already solved at runtime: contract enums already use `JsonStringEnumConverter<TEnum>`, so omitting `outputSchema` does not force callers back to numeric enum values.
- Prefer one explicit server setting over ad-hoc per-tool exceptions.

### Task 1: Update The Contract And Design Docs First

**Files:**

- Modify: `docs/RoslynMcpToolDesign.md`
- Modify: `docs/RoslynMcpToolContracts.md`

- [ ] **Step 1: Replace the current “publish full output schema” rule with an explicit publication policy**

Revise the metadata sections so they say:

```md
| `outputSchema` | Optional. In the default agent-optimised mode the server omits it to reduce `tools/list` size. Full output contracts remain authoritative in this document and the runtime still returns structured `ToolResult<TData>` JSON. |
```

- [ ] **Step 2: Document the chosen default and reject the misleading compact-schema alternative**

Add wording close to this:

```md
The server does not publish a smaller “summary” schema that diverges from the actual payload. It either publishes the real schema or omits `outputSchema` entirely.
```

- [ ] **Step 3: Record why enum readability still holds without published output schemas**

Add a short note that the response contracts serialise enums as strings via `JsonStringEnumConverter<TEnum>`, so agents reading actual tool responses still see values such as `Ready`, `Conflict`, `ReloadWorkspace`, and `Undo` rather than integer codes.

- [ ] **Step 4: Add a short implementation note for tool descriptions**

Document that only high-signal result hints belong in descriptions, for example:

```md
Returns `data.state`, load diagnostics, and transaction capabilities.
```

Do not restate the full DTO in every description.

### Task 2: Introduce An Explicit Output-Schema Publication Mode

**Files:**

- Create: `src/Roslyn.Workbench.Mcp.Plugins/ToolOutputSchemaMode.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Plugins/RegisteredTool.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Plugins/ToolRegistrationMetadata.cs`
- Modify: `src/Roslyn.Workbench.Mcp/StartupOptions.cs`
- Modify: `src/Roslyn.Workbench.Mcp/StartupOptionsParser.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Server/ServerConfiguration.cs`

- [ ] **Step 1: Add a small shared publication-mode enum**

Use a dedicated type rather than a loose boolean:

```csharp
namespace Roslyn.Workbench.Mcp.Plugins;

public enum ToolOutputSchemaMode
{
    Omit = 0,
    Full = 1,
}
```

- [ ] **Step 2: Make published output schema nullable in the registered tool model**

Adjust `RegisteredTool` to allow this:

```csharp
public JsonElement? OutputSchema { get; init; }
```

Do not change `RequestType` or `ResponseType`; runtime execution still needs the real CLR contracts.

- [ ] **Step 3: Add optional short result-summary text to metadata**

Extend `ToolRegistrationMetadata` with one optional field only:

```csharp
public string? ResultSummary { get; init; }
```

This is for concise, manually curated hints. Do not use it to duplicate whole DTOs.

- [ ] **Step 4: Add one startup option that controls schema publication**

Add an option on `StartupOptions` and surface it through `server-status` configuration:

```csharp
public ToolOutputSchemaMode ToolOutputSchemaMode { get; init; } = ToolOutputSchemaMode.Omit;
```

Parse one CLI/env surface only, for example:

```text
--tool-output-schema-mode=omit|full
ROSLYN_WORKBENCH_MCP_TOOL_OUTPUT_SCHEMA_MODE
```

### Task 3: Change Tool Registration To Respect The Publication Mode

**Files:**

- Modify: `src/Roslyn.Workbench.Mcp.Plugins/PluginRegistry.cs`
- Modify: `src/Roslyn.Workbench.Mcp/ServerToolMcpServerTool.cs`
- Modify: `src/Roslyn.Workbench.Mcp/PluginMcpServerTool.cs`
- Modify: `src/Roslyn.Workbench.Mcp/Program.cs`

- [ ] **Step 1: Gate output schema generation in plugin registration**

When building `RegisteredTool`, switch from unconditional creation:

```csharp
OutputSchema = ToolSchemaFactory.CreateToolResultSchema<TResponse>(),
```

to conditional publication:

```csharp
OutputSchema = outputSchemaMode == ToolOutputSchemaMode.Full
    ? ToolSchemaFactory.CreateToolResultSchema<TResponse>()
    : null,
```

- [ ] **Step 2: Gate output schema publication for server-owned tools as well**

Thread the same mode into `ServerToolMcpServerTool<TRequest, TResponse>` so host tools and plugin tools behave consistently:

```csharp
OutputSchema = outputSchemaMode == ToolOutputSchemaMode.Full
    ? ToolSchemaFactory.CreateToolResultSchema<TResponse>()
    : null,
```

- [ ] **Step 3: Append result-summary hints only when they add real value**

For descriptions that need a short hint, append one sentence:

```csharp
var description = metadata.ResultSummary is null
    ? metadata.Description
    : $"{metadata.Description} Result: {metadata.ResultSummary}";
```

Good candidates:

- `workspace-status`
- `transaction-preview`
- `transaction-history`
- `list-code-actions`
- `describe-code-action`

Poor candidates:

- simple selector or navigation tools whose responses are already obvious from the name

- [ ] **Step 4: Verify that runtime structured responses are untouched**

Do not alter:

- `ToolExecutor`
- `ToolResult<TData>`
- response DTOs in `Roslyn.Workbench.Mcp.Contracts`

This plan is about metadata publication, not about changing the runtime payload contract.

### Task 4: Tighten The Schema Factory Tests Around The New Behaviour

**Files:**

- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Test/ToolSchemaFactoryTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Contracts.Test/Schema/SchemaGenerationTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Test/PluginDiscoveryAndMcpToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Test/WorkspaceLifecycleToolTests.cs` as needed

- [ ] **Step 1: Keep one direct unit test for full-schema generation**

Retain a focused unit test that proves `ToolSchemaFactory.CreateToolResultSchema<T>()` still generates the real discriminated schema:

```csharp
var schema = ToolSchemaFactory.CreateToolResultSchema<TestResponse>();
schema.GetProperty("oneOf").EnumerateArray().Should().HaveCount(5);
```

- [ ] **Step 2: Add MCP-surface tests for the default omit mode**

Assert that published tools do not expose `OutputSchema` when the default mode is active:

```csharp
tool.ProtocolTool.OutputSchema.Should().BeNull();
tool.ProtocolTool.InputSchema.ValueKind.Should().NotBe(JsonValueKind.Undefined);
```

- [ ] **Step 3: Add one override-path test for full mode**

Start the server or build tools with `ToolOutputSchemaMode.Full` and assert that one representative server-owned tool and one representative plugin tool publish a non-null `OutputSchema`.

- [ ] **Step 4: Add one description-summary assertion**

For a tool that genuinely needs it, assert the description includes the concise result hint and does not dump a full DTO or JSON schema fragment.

### Task 5: Format, Verify, And Document The Behaviour Change

**Files:**

- Modify only the changed files from this task

- [ ] **Step 1: Format only the touched C# files**

Run:

```bash
dotnet format --include src/Roslyn.Workbench.Mcp.Plugins/RegisteredTool.cs --include src/Roslyn.Workbench.Mcp.Plugins/ToolRegistrationMetadata.cs --include src/Roslyn.Workbench.Mcp.Plugins/PluginRegistry.cs --include src/Roslyn.Workbench.Mcp.Plugins/ToolSchemaFactory.cs --include src/Roslyn.Workbench.Mcp/PluginMcpServerTool.cs --include src/Roslyn.Workbench.Mcp/ServerToolMcpServerTool.cs --include src/Roslyn.Workbench.Mcp/StartupOptions.cs --include src/Roslyn.Workbench.Mcp/StartupOptionsParser.cs --include src/Roslyn.Workbench.Mcp/Program.cs --include src/Roslyn.Workbench.Mcp.Contracts/Server/ServerConfiguration.cs --include src/Roslyn.Workbench.Mcp.Plugins/ToolOutputSchemaMode.cs --include test/Roslyn.Workbench.Mcp.Plugins.Test/ToolSchemaFactoryTests.cs --include test/Roslyn.Workbench.Mcp.Contracts.Test/Schema/SchemaGenerationTests.cs --include test/Roslyn.Workbench.Mcp.Test/PluginDiscoveryAndMcpToolTests.cs --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

- [ ] **Step 2: Normalize CRLF line endings for changed CRLF-governed files**

Run:

```bash
unix2dos docs/RoslynMcpToolDesign.md docs/RoslynMcpToolContracts.md src/Roslyn.Workbench.Mcp.Plugins/RegisteredTool.cs src/Roslyn.Workbench.Mcp.Plugins/ToolRegistrationMetadata.cs src/Roslyn.Workbench.Mcp.Plugins/PluginRegistry.cs src/Roslyn.Workbench.Mcp.Plugins/ToolSchemaFactory.cs src/Roslyn.Workbench.Mcp/PluginMcpServerTool.cs src/Roslyn.Workbench.Mcp/ServerToolMcpServerTool.cs src/Roslyn.Workbench.Mcp/StartupOptions.cs src/Roslyn.Workbench.Mcp/StartupOptionsParser.cs src/Roslyn.Workbench.Mcp/Program.cs src/Roslyn.Workbench.Mcp.Contracts/Server/ServerConfiguration.cs src/Roslyn.Workbench.Mcp.Plugins/ToolOutputSchemaMode.cs test/Roslyn.Workbench.Mcp.Plugins.Test/ToolSchemaFactoryTests.cs test/Roslyn.Workbench.Mcp.Contracts.Test/Schema/SchemaGenerationTests.cs test/Roslyn.Workbench.Mcp.Test/PluginDiscoveryAndMcpToolTests.cs
```

- [ ] **Step 3: Run the required verification**

Run:

```bash
dotnet restore --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet build --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

### Acceptance Checklist

- `tools/list` no longer publishes full response schemas in the default configuration.
- `inputSchema` publication is unchanged.
- Actual tool responses remain the existing `ToolResult<TData>` JSON payloads.
- Full contract detail still exists in `docs/RoslynMcpToolContracts.md`.
- Any result-summary text added to tool descriptions is short, curated, and high-signal.
- There is one explicit opt-in path to publish full output schemas for debugging or strict clients.
