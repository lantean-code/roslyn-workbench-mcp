# Tool ExecuteAsync Unit Coverage Implementation Plan

**Status:** Historical implementation plan. Its recorded follow-on list was closed by the later coverage reassessment in [Tool Test Inventory](../../Tool%20Test%20Inventory.md), which found no known untested supported tool behaviour. This document is not an active worklist; [Future Tasks](../../FutureTasks.md) is the sole active engineering backlog.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add branch-focused unit tests so host and bundled-core tool `ExecuteAsync` paths are covered by xUnit + Moq, with Cobertura coverage used to verify the unit-only loop.

**Architecture:** Reuse `Roslyn.Workbench.Mcp.TestSupport` to build in-memory Roslyn workspaces, mocked request resolvers, and mocked query or mutation contexts. Group tools by execution shape: host server-owned tools, direct delegation tools, replay-code-action tools, and branch-heavy inspection or refactoring tools. Use Cobertura output from the unit-only test filter to confirm coverage after each batch.

**Tech Stack:** .NET 10 SDK, xUnit v3, Moq, AwesomeAssertions, coverlet.collector, Cobertura XML

---

### Task 1: Build Reusable Tool Test Harnesses

**Files:**

- Modify: `test/Roslyn.Workbench.Mcp.TestSupport/QueryContextBuilder.cs`
- Modify: `test/Roslyn.Workbench.Mcp.TestSupport/MutationContextBuilder.cs`
- Modify: `test/Roslyn.Workbench.Mcp.TestSupport/ToolExecutionServicesBuilder.cs`
- Create: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/ToolExecuteAsyncTestSupport.cs`
- Create: `test/Roslyn.Workbench.Mcp.Test/ServerOwnedToolTestSupport.cs`

- [ ] Add lightweight helpers for creating in-memory documents, symbols, locations, and request-resolver setups without temp-file workspaces.
- [ ] Add host-side `McpServer` request helpers so server-owned tool invocation tests stay small and consistent.
- [ ] Add assertion helpers for rejected, no-change, and success plugin results so the branch tests read directly off the tool flow.

### Task 2: Complete Host Tool ExecuteAsync Coverage

**Files:**

- Modify: `test/Roslyn.Workbench.Mcp.Test/ServerStatusToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Test/WorkspaceListToolTests.cs`
- Create: `test/Roslyn.Workbench.Mcp.Test/WorkspaceLifecycleToolUnitTests.cs`
- Create: `test/Roslyn.Workbench.Mcp.Test/TransactionToolUnitTests.cs`

- [ ] Add direct unit coverage for `WorkspaceOpenTool`, `WorkspaceCloseTool`, `WorkspaceReloadTool`, `WorkspaceStatusTool`, `TransactionStartTool`, `TransactionPreviewTool`, `TransactionHistoryTool`, `TransactionRollbackTool`, and `TransactionCommitTool`.
- [ ] Keep host test names in the required `GIVEN_[State]_WHEN_CallingExecuteAsync_THEN_Should[BeExpectedOutcome]` form.
- [ ] Cover service-result mapping branches through mocked `IWorkspaceLifecycleService` and `ITransactionService` return values.

### Task 3: Cover Simple Code Action And Replay Refactoring Tools

**Files:**

- Create: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/CodeActions/CodeActionDelegationToolTests.cs`
- Create: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Refactorings/SimpleReplayRefactoringToolTests.cs`

- [ ] Add direct delegation tests for `DescribeCodeActionTool`, `ListCodeActionsTool`, `StageCodeActionTool`, `StageCodeFixTool`, and `StageFixAllTool`.
- [ ] Add grouped replay-forwarding tests for one-line refactoring tools that only forward provider IDs, titles, and snapshots into `StageReplaySelectionAsync`.
- [ ] Verify the exact provider ID, title or title-prefix filters, and request selector forwarding for each tool.

### Task 4: Cover Branching Refactoring Tools

**Files:**

- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Refactorings/ConvertPropertyToolTests.cs`
- Create: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Refactorings/BranchingRefactoringToolTests.cs`

- [ ] Add branch-complete tests for `AddMissingUsingsTool`, `ConvertExpressionBodyTool`, `ExtractMethodTool`, `IntroduceVariableTool`, `IntroduceParameterTool`, `ConvertToInterpolatedStringTool`, `ConvertForeachLinqTool`, `InlineVariableTool`, `MoveTypeToFileTool`, `EncapsulateFieldTool`, and `SortUsingsTool`.
- [ ] Use `MiniWorkspaceFactory` and mocked request resolvers so symbol, location, and syntax-root branches are exercised as unit tests.
- [ ] Cover every rejection branch and each input-driven switch arm that changes the staged replay request.

### Task 5: Cover Branching Inspection Tools

**Files:**

- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/GetProjectDetailsToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/AnalyzeNullabilityToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/FindCallersToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/FindReferencesToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/FindUnusedSymbolsToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/GetApiSurfaceToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/GetChangeImpactToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/GetCodeContextToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/GetDocumentOutlineToolTests.cs`
- Modify: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/GetSolutionStructureToolTests.cs`
- Create: `test/Roslyn.Workbench.Mcp.Plugins.Core.Test/Inspection/InspectionBranchToolTests.cs`

- [ ] Add explicit branch tests for `GetDiagnosticsTool` following the top-to-bottom flow, including resolver rejections, scope-kind behaviour, null compilation, analyzer presence, document filtering, id filtering, severity filtering, and max-result bounding.
- [ ] Add branch tests for `GetProjectDetailsTool`, `GetDocumentOptionsTool`, `GetSymbolInfoTool`, `GoToDefinitionTool`, `ResolveSymbolTool`, `SearchSymbolsTool`, `FindDerivedTypesTool`, `FindOverloadsTool`, and `GetTypeHierarchyTool`.
- [ ] Extend existing unit tests to cover every remaining request-driven branch before relying on any integration tests for those tools.

### Task 6: Run Cobertura And Close Gaps

**Files:**

- Modify: `docs/test-project-audit-2026-07-07.md`
- Modify: `docs/superpowers/plans/2026-07-08-tool-executeasync-unit-coverage.md`

- [ ] Run the unit-only loop with `--collect:"XPlat Code Coverage"` and capture Cobertura XML under `/tmp/artifacts/roslyn-workbench-mcp`.
- [ ] Inspect remaining uncovered host or core tool branches and add missing tests until the intended `ExecuteAsync` paths are covered.
- [ ] Record any tool paths that still cannot be unit-tested without production refactoring, with the exact blocking seam documented in the audit.

### Task 7: Format, Normalise, And Verify

**Files:**

- Modify: all changed files from this task

- [ ] Run `dotnet format --include <changed files> --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`
- [ ] Run `unix2dos` on changed CRLF-governed files
- [ ] Run `dotnet test Roslyn.Workbench.Mcp.slnx --filter "Category!=Integration&Category!=Audit" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`
- [ ] Run the same filtered command with `--collect:"XPlat Code Coverage"` and inspect Cobertura output

---

## Execution Status (2026-07-08)

Completed in this pass:

- Added reusable host-side and plugin-core `ExecuteAsync` test support and expanded host tool unit coverage.
- Added grouped code-action, replay-refactoring, dependency-analysis, diagnostics, async-analysis, document/symbol inspection, and symbol-search unit suites.
- Added Cobertura-driven verification runs for `Roslyn.Workbench.Mcp.Plugins.Core.Test` and for the repo fast loop.
- Ran `dotnet format` on the touched test files, normalised CRLF with `unix2dos`, and reran the filtered fast loop successfully.

Current verified commands:

- `dotnet test --filter "Category!=Integration&Category!=Audit" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`
- `dotnet test --filter "Category!=Integration&Category!=Audit" --collect:"XPlat Code Coverage" --results-directory /tmp/artifacts/roslyn-workbench-mcp/coverage-fast-loop --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`

Current notable remaining low-coverage bundled-core tool files from the latest plugin-core Cobertura pass:

- `GetCodeMetricsTool.cs` at about `4.4%`
- `FindCalleesTool.cs` at about `4.6%`
- `GetControlFlowGraphTool.cs` at about `6.8%`
- `AnalyzeControlFlowTool.cs` at about `7.8%`
- `FindDuplicateCodeTool.cs` at about `8.0%`
- `GetSymbolDependenciesTool.cs` at about `8.0%`
- `AnalyzeDataFlowTool.cs` at about `8.2%`
- `GetOperationTreeTool.cs` at about `9.3%`
- `AnalyzeDisposablesTool.cs` at about `10.2%`
- `GetTypeHierarchyTool.cs` at about `12.0%`

Follow-on work identified by this historical pass:

- Add dedicated unit suites for the remaining heavy Roslyn graph, flow, duplicate-code, dependency, and type-hierarchy tools listed above.
- Expand the symbol inspection coverage further for `SearchSymbolsTool`, `ResolveSymbolTool`, `GetSymbolMembersTool`, `FindOverloadsTool`, `GoToDefinitionTool`, `FindImplementationsTool`, and neighbouring tools until their full request-driven branches are exercised.
- Add the remaining branch-rich refactoring coverage for `ConvertToInterpolatedStringTool`, `RenameSymbolTool`, `SortUsingsTool`, `InlineVariableTool`, `EncapsulateFieldTool`, `MoveTypeToFileTool`, and `FormatDocumentTool`.
