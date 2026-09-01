# Multi-Workspace Support Implementation Plan

**Status:** Historical implementation plan. Multi-Workspace lifecycle, selection and transaction ownership are implemented and validated; current behaviour is documented in [Workspaces and transactions](../../../content/workspaces-and-transactions.md). This document is not an active worklist.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow the server to keep more than one solution or project loaded at once while guaranteeing that at most one loaded workspace can own an active mutation transaction at any given time.

**Architecture:** Replace the single global `WorkspaceSnapshot` model with a host state that owns multiple per-workspace sessions. Each session keeps its own `MSBuildWorkspace`, lifecycle state, external-change manifest, and per-workspace operation gate; a separate host-level transaction owner field enforces the global invariant that only one session may be in `TransactionActive` or `TransactionConflicted` at a time.

**Tech Stack:** .NET 10, C#, Roslyn `MSBuildWorkspace`, ModelContextProtocol C# SDK, Stateless, xUnit, Moq, AwesomeAssertions.

---

## File Map

**Primary docs**

- Modify: `docs/RoslynMcpToolDesign.md`
- Modify: `docs/RoslynMcpToolContracts.md`
- Modify: `docs/RoslynMcpToolImplementationMatrix.md`

**Primary contracts**

- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Results/ToolResult.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Results/WorkspaceIdentity.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Selectors/SnapshotPrecondition.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Selectors/ResolvedLocation.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/CodeActions/CodeActionInfo.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Server/WorkspaceOpenRequest.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Server/WorkspaceOpenData.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Server/WorkspaceStatusData.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Transactions/TransactionStartRequest.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Transactions/TransactionPreviewRequest.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Transactions/TransactionHistoryRequest.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Transactions/TransactionCommitRequest.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Transactions/TransactionRollbackRequest.cs`
- Create: `src/Roslyn.Workbench.Mcp.Contracts/Selectors/WorkspaceSelector.cs`
- Create: `src/Roslyn.Workbench.Mcp.Contracts/Server/WorkspaceListData.cs`
- Create: `src/Roslyn.Workbench.Mcp.Contracts/Server/WorkspaceListRequest.cs`

**Representative request-contract batches**

- Modify all request contracts under:
  - `src/Roslyn.Workbench.Mcp.Contracts/Inspection/`
  - `src/Roslyn.Workbench.Mcp.Contracts/CodeActions/`
  - `src/Roslyn.Workbench.Mcp.Contracts/Refactorings/`

**Primary host and workspace layer**

- Modify: `src/Roslyn.Workbench.Mcp.Plugins/IToolExecutionContext.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Plugins/IToolExecutionContextFactory.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Plugins/ToolExecutor.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/IWorkspaceCoordinator.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceCoordinator.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceCoordinatorOptions.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceStateMachine.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceResolver.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceQueryContext.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceMutationContext.cs`
- Replace: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceSnapshot.cs`
- Create: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceHostSnapshot.cs`
- Create: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceSessionSnapshot.cs`
- Create: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceSelection.cs`

**Server-owned tools**

- Modify: `src/Roslyn.Workbench.Mcp/WorkspaceLifecycleToolFactory.cs`
- Modify: `src/Roslyn.Workbench.Mcp/TransactionToolFactory.cs`
- Modify: `src/Roslyn.Workbench.Mcp/Program.cs`

**Tests**

- Modify: `test/Roslyn.Workbench.Mcp.Test/WorkspaceLifecycleToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Workspace.Test/WorkspaceCoordinatorTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Workspace.Test/WorkspaceStateMachineTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Test/ToolExecutorTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Contracts.Test/Schema/SchemaGenerationTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.TestSupport/InspectionSampleFixture.cs` or create a sibling helper to produce two disposable workspaces for integration-style tests

## Design Decisions To Lock Before Coding

- Keep `workspace-open` additive. Opening a second workspace must no longer require closing the first.
- Add a new `workspace-list` tool because a multi-loaded server needs an explicit enumeration surface.
- Route every query or mutation to one selected workspace. Do not reintroduce “active workspace switching” as the main workflow.
- Allow omitted workspace selection only when exactly one workspace is loaded. If more than one is loaded, reject with a clear structured error such as `WorkspaceSelectorRequired`.
- Enforce a single global mutation owner. Exactly one workspace session may hold a transaction at a time.
- Do not let a compact “current workspace” concept leak back into snapshot semantics. Snapshot preconditions must stay bound to the actual workspace that produced them.

### Task 1: Rewrite The Public Design And Contract Docs Around Workspace Sessions

**Files:**

- Modify: `docs/RoslynMcpToolDesign.md`
- Modify: `docs/RoslynMcpToolContracts.md`
- Modify: `docs/RoslynMcpToolImplementationMatrix.md`

- [ ] **Step 1: Replace the single-workspace rule with a multi-session rule**

Change the design language from:

```md
It supports one loaded writable solution at a time.
```

to language like:

```md
It supports multiple loaded workspaces at once. Exactly one loaded workspace may own an active transaction for staged mutations.
```

- [ ] **Step 2: Introduce the new public concepts in the contract catalogue**

Document these shared shapes:

```text
WorkspaceSelector { workspaceId?: string, alias?: string, path?: string }
WorkspaceIdentity { workspaceId: string, alias?: string, workspaceEpoch: long, loadedPath: string }
ToolResult<TData> { workspaceId?: string, workspaceEpoch?: long, transactionRevision?: int, ... }
```

- [ ] **Step 3: Add the missing server-owned enumeration tool**

Add a contract row for:

```text
workspace-list -> WorkspaceListData { workspaces: WorkspaceIdentity[], transactionOwnerWorkspaceId?: string }
```

- [ ] **Step 4: Update lifecycle and transaction tool descriptions**

Explicitly document:

- `workspace-status`, `workspace-close`, and `workspace-reload` now target one selected workspace
- query and mutation tools accept an optional `workspace` selector
- `transaction-start` rejects if another workspace already owns the global transaction slot

### Task 2: Extend The Shared Contracts With Workspace Identity And Routing

**Files:**

- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Results/ToolResult.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Results/WorkspaceIdentity.cs`
- Create: `src/Roslyn.Workbench.Mcp.Contracts/Selectors/WorkspaceSelector.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Selectors/SnapshotPrecondition.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/Selectors/ResolvedLocation.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Contracts/CodeActions/CodeActionInfo.cs`

- [ ] **Step 1: Add a real workspace selector contract**

Use one shared selector shape:

```csharp
public sealed record WorkspaceSelector
{
    public string? WorkspaceId { get; init; }
    public string? Alias { get; init; }
    public string? Path { get; init; }
}
```

Validation rule: at least one selector field is required; multiple fields must resolve to the same loaded workspace.

- [ ] **Step 2: Make workspace identity readable and stable**

Extend `WorkspaceIdentity` to include a short server-generated ID and optional caller-friendly alias:

```csharp
public string WorkspaceId { get; init; } = string.Empty;
public string? Alias { get; init; }
```

- [ ] **Step 3: Add workspace identity to snapshot-bound payloads**

Update these contracts so snapshot reuse is unambiguous across multiple loaded sessions:

```csharp
public string? WorkspaceId { get; init; }
```

Apply that to:

- `ToolResult<TData>`
- `SnapshotPrecondition`
- `ResolvedLocation`
- `CodeActionInfo`

- [ ] **Step 4: Update constructor helpers on `ToolResult<TData>`**

Thread the new argument through the factory methods:

```csharp
public static ToolResult<TData> Succeeded(
    TData data,
    string? workspaceId = null,
    long? workspaceEpoch = null,
    int? transactionRevision = null,
    ...)
```

Keep the existing `Outcome`, `Changes`, diagnostics, warnings, and error invariants intact.

### Task 3: Add Workspace Selection To Every Request Surface

**Files:**

- Modify all request DTOs under:
  - `src/Roslyn.Workbench.Mcp.Contracts/Inspection/`
  - `src/Roslyn.Workbench.Mcp.Contracts/CodeActions/`
  - `src/Roslyn.Workbench.Mcp.Contracts/Refactorings/`
  - `src/Roslyn.Workbench.Mcp.Contracts/Server/`
  - `src/Roslyn.Workbench.Mcp.Contracts/Transactions/`

- [ ] **Step 1: Add `workspace` to every request that executes against a workspace**

Use the same property name everywhere:

```csharp
public WorkspaceSelector? Workspace { get; init; }
```

That includes:

- read-only queries
- mutation tools
- `transaction-start`, `transaction-preview`, `transaction-history`, `transaction-commit`, `transaction-rollback`
- `workspace-status`, `workspace-close`, `workspace-reload`

- [ ] **Step 2: Allow `workspace-open` to accept an alias**

Add:

```csharp
public string? Alias { get; init; }
```

to `WorkspaceOpenRequest` so agents can refer to workspaces with concise labels instead of repeating absolute paths.

- [ ] **Step 3: Replace empty lifecycle requests where selection is now needed**

`EmptyRequest` is no longer sufficient for selected lifecycle tools. Introduce typed requests such as:

```csharp
public sealed record WorkspaceStatusRequest
{
    public WorkspaceSelector? Workspace { get; init; }
}
```

Use the same approach for close, reload, and list.

- [ ] **Step 4: Update schema tests after the request-contract sweep**

Ensure representative schema assertions now look for the new property:

```csharp
requestProperties.TryGetProperty("workspace", out var workspaceProperty).Should().BeTrue();
```

### Task 4: Replace The Single Snapshot With Host State Plus Per-Workspace Sessions

**Files:**

- Replace: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceSnapshot.cs`
- Create: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceHostSnapshot.cs`
- Create: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceSessionSnapshot.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceCoordinator.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceStateMachine.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceCoordinatorOptions.cs`

- [ ] **Step 1: Split host-wide state from per-workspace state**

Use a host snapshot like:

```csharp
internal sealed record WorkspaceHostSnapshot
{
    public IReadOnlyDictionary<string, WorkspaceSessionSnapshot> Workspaces { get; init; } = new Dictionary<string, WorkspaceSessionSnapshot>(StringComparer.Ordinal);
    public string? TransactionOwnerWorkspaceId { get; init; }
}
```

and a session snapshot like:

```csharp
internal sealed record WorkspaceSessionSnapshot
{
    public WorkspaceLifecycleState State { get; init; }
    public WorkspaceIdentity Workspace { get; init; } = new();
    public MSBuildWorkspace? LoadedWorkspace { get; init; }
    public Solution? CurrentSolution { get; init; }
    public WorkspaceTransaction? Transaction { get; init; }
    public WorkspaceInputManifest? InputManifest { get; init; }
    public WorkspaceOperationGate OperationGate { get; init; } = new(2);
    public int ProjectCount { get; init; }
    public int DocumentCount { get; init; }
    public IReadOnlyList<DiagnosticInfo> LoadDiagnostics { get; init; } = [];
}
```

- [ ] **Step 2: Add an optional memory-safety cap on loaded workspaces**

Add one host option:

```csharp
public int MaxLoadedWorkspaces { get; init; } = 4;
```

Reject `workspace-open` beyond that cap with a clear structured error rather than allowing unbounded memory growth.

- [ ] **Step 3: Make the state machine per session, not global**

Keep the existing lifecycle states, but apply them to one `WorkspaceSessionSnapshot` at a time. The host-level `TransactionOwnerWorkspaceId` is a separate invariant and must not be folded into `WorkspaceLifecycleState`.

- [ ] **Step 4: Keep per-workspace operation gates**

Each session should own its own `WorkspaceOperationGate`. Queries on workspace `B` must still run while workspace `A` has an active transaction, provided no operation on `B` itself is blocked.

### Task 5: Thread Workspace Routing Through The Tool Executor And Coordinator

**Files:**

- Modify: `src/Roslyn.Workbench.Mcp.Plugins/IToolExecutionContext.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Plugins/IToolExecutionContextFactory.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Plugins/ToolExecutor.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/IWorkspaceCoordinator.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceQueryContext.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceMutationContext.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceResolver.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceCoordinator.cs`

- [ ] **Step 1: Resolve workspace selection before acquiring a context**

Change the context-factory surface so the coordinator can see the deserialised request, not just the tool:

```csharp
ValueTask<ToolExecutionContextLease<IQueryContext>> CreateQueryContextAsync(
    RegisteredTool tool,
    object request,
    CancellationToken cancellationToken);
```

Do the same for mutation contexts.

- [ ] **Step 2: Extract `WorkspaceSelector` once in the executor**

After deserialising the request in `ToolExecutor`, pass it to the context factory. Avoid re-parsing the raw JSON argument dictionary in the coordinator.

- [ ] **Step 3: Store selected workspace identity on the execution context**

`IToolExecutionContext.WorkspaceIdentity` remains the selected session identity. `ToolExecutor.CreateToolResult(...)` must now populate both `WorkspaceId` and `WorkspaceEpoch` on the result envelope.

- [ ] **Step 4: Reject ambiguous routing early**

If multiple workspaces are loaded and a request omits `workspace`, reject before invoking plugin code:

```csharp
ToolResult.Rejected<TData>(
    new ToolError
    {
        Code = "WorkspaceSelectorRequired",
        Message = "Select a workspace when more than one workspace is loaded.",
    },
    RequiredAction.ResolveTargetAgain);
```

### Task 6: Rework Lifecycle And Transaction Operations Around The Global Mutation Owner

**Files:**

- Modify: `src/Roslyn.Workbench.Mcp.Workspace/IWorkspaceCoordinator.cs`
- Modify: `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceCoordinator.cs`
- Modify: `src/Roslyn.Workbench.Mcp/WorkspaceLifecycleToolFactory.cs`
- Modify: `src/Roslyn.Workbench.Mcp/TransactionToolFactory.cs`
- Modify: `src/Roslyn.Workbench.Mcp/Program.cs`
- Create: `src/Roslyn.Workbench.Mcp.Contracts/Server/WorkspaceListData.cs`
- Create: `src/Roslyn.Workbench.Mcp.Contracts/Server/WorkspaceListRequest.cs`

- [ ] **Step 1: Make `workspace-open` additive**

`OpenAsync` should:

- validate uniqueness of `path`
- validate uniqueness of `alias` when provided
- allocate `workspaceId`
- load the workspace
- insert a new session into the host snapshot instead of rejecting because one already exists

- [ ] **Step 2: Add `workspace-list` and selected status/close/reload operations**

Expose one list tool and route status/close/reload to a selected session:

```csharp
new ServerToolMcpServerTool<WorkspaceListRequest, WorkspaceListData>(...)
```

- [ ] **Step 3: Guard transaction start with the global owner invariant**

Before starting a transaction:

```csharp
if (hostSnapshot.TransactionOwnerWorkspaceId is not null
    && !StringComparer.Ordinal.Equals(hostSnapshot.TransactionOwnerWorkspaceId, selectedWorkspaceId))
{
    return ToolResult.Rejected<TransactionStartData>(...);
}
```

Return an error that identifies the owning workspace so the caller can make a clear decision.

- [ ] **Step 4: Clear or preserve the owner in the right places**

Rules:

- start transaction -> set `TransactionOwnerWorkspaceId`
- commit success -> clear it
- rollback success -> clear it
- transaction conflict -> keep owner until rollback completes
- close/reload selected workspace -> reject if that workspace still owns the transaction

### Task 7: Add The Multi-Workspace Test Matrix

**Files:**

- Modify: `test/Roslyn.Workbench.Mcp.Test/WorkspaceLifecycleToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Workspace.Test/WorkspaceCoordinatorTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Workspace.Test/WorkspaceStateMachineTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Test/ToolExecutorTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Contracts.Test/Schema/SchemaGenerationTests.cs`
- Modify or create helpers in `test/Roslyn.Workbench.Mcp.TestSupport/`

- [ ] **Step 1: Add lifecycle tests for two loaded workspaces**

Cover:

- open `A`
- open `B`
- list returns both
- status with selector returns the correct session
- status without selector fails when both are loaded

- [ ] **Step 2: Add transaction-owner exclusivity tests**

Cover:

- start transaction on `A`
- attempt start on `B`
- expect rejection naming `A`
- rollback on `A`
- start on `B`

- [ ] **Step 3: Add concurrency-behaviour tests**

At minimum verify:

- query on `B` succeeds while `A` has an active transaction
- mutation on `B` is rejected while `A` owns the transaction slot
- query on `A` is rejected only when `A` itself is out of date or conflicted

- [ ] **Step 4: Add schema coverage for workspace selection**

Representative assertions:

```csharp
requestProperties.TryGetProperty("workspace", out var workspaceProperty).Should().BeTrue();
workspaceProperty.GetRawText().Should().Contain("workspaceId");
workspaceProperty.GetRawText().Should().Contain("alias");
```

### Task 8: Format, Normalize, And Verify The Change

**Files:**

- Modify only the files changed by this implementation

- [ ] **Step 1: Run targeted formatting on changed C# files**

Run `dotnet format --include <changed files> --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp` with the exact changed file list from this implementation.

- [ ] **Step 2: Normalize CRLF line endings for changed CRLF-governed files**

Run `unix2dos <changed files>` for the changed `.cs` and `.md` files from this plan.

- [ ] **Step 3: Run the required verification**

Run:

```bash
dotnet restore --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet build --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```

### Acceptance Checklist

- The server can keep more than one workspace loaded at once.
- Query and lifecycle operations can target a selected workspace without unloading another one.
- Omitting workspace selection is allowed only when exactly one workspace is loaded.
- Exactly one workspace may own an active or conflicted transaction at any time.
- Query traffic for non-owning workspaces is not blocked just because another workspace owns the mutation slot.
- Snapshot-bound results carry enough workspace identity to be reused safely without cross-workspace ambiguity.
