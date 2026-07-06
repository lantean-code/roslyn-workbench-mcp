# Architecture Audit Checklist

This document captures the current audit findings across the source projects and turns them into a working checklist.

Scope:
- `Roslyn.Workbench.Mcp`
- `Roslyn.Workbench.Mcp.Contracts`
- `Roslyn.Workbench.Mcp.Workspace`
- `Roslyn.Workbench.Mcp.Plugins`
- `Roslyn.Workbench.Mcp.Plugins.Core`

Principles being enforced:
- explicit boundaries
- constructor-injected collaborators
- minimal runtime reflection and type guessing
- strong nullability and invariant design
- internal workspace services using workspace-owned models
- tool-owned contracts where practical
- smaller, focused services over god classes

## P1: High Priority

### Workspace

- [x] Break up `MefCodeActionService` into focused collaborators.
  Files:
  - `src/Roslyn.Workbench.Mcp.Workspace/MefCodeActionService.cs`
  Notes:
  - The class is still acting as a god service.
  - Current responsibilities include discovery, filtering, snapshot checks, action resolution, token creation/validation, analyzer activation, result construction, and JSON handling.

- [x] Remove invalid-state ambiguity from workspace selection.
  Files:
  - `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceSelectorService.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceSelectionResult.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceExecutionContextFactory.cs`
  Notes:
  - `WorkspaceSelectionResult` can still represent neither a selection nor an error.
  - Callers currently defend against that with throws, which weakens the API boundary.

### Plugins

- [ ] Remove runtime-typed execution from the plugin invocation path.
  Files:
  - `src/Roslyn.Workbench.Mcp.Plugins/IPluginToolInvoker.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins/PluginToolInvoker.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins/ToolExecutor.cs`
  Notes:
  - The core execution path still relies on `object` requests and casts.
  - This is weaker than the newer server-owned tool path.

- [ ] Reduce heuristic/reflection-driven response and schema publication.
  Files:
  - `src/Roslyn.Workbench.Mcp.Plugins/ToolResponseDescriptorResolver.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins/ToolSchemaFactory.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins/Tooling/ToolSchemaBuilder.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins/ToolResponseShaper.cs`
  Notes:
  - The current model infers behaviour from property names and response shape.
  - This is harder to reason about and test than tool-owned contracts.

## P2: Medium Priority

### Workspace

- [ ] Remove remaining plugin result leakage from workspace internals.
  Files:
  - `src/Roslyn.Workbench.Mcp.Workspace/IMutationStagingService.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/MutationStagingService.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceExecutionContextFactory.cs`
  Notes:
  - Workspace services still produce `PluginExecutionResult`, `PluginExecutionResultBox`, and `ToolError`.
  - That cuts across the boundary you wanted between workspace workflows and tool transport concerns.

- [ ] Rework code action runtime composition into a cleaner service boundary.
  Files:
  - `src/Roslyn.Workbench.Mcp.Workspace/CodeActionRuntime.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/CodeActionRuntimeComposer.cs`
  Notes:
  - The public runtime bag and nullable members still feel like composition artefacts.
  - The factory also reflects over non-public MEF APIs.

### Host

- [ ] Review plugin loading so it is less dependent on manual activation.
  Files:
  - `src/Roslyn.Workbench.Mcp/PluginCatalogLoader.cs`
  Notes:
  - `Activator.CreateInstance` bypasses normal DI and lifetime management.
  - This may be acceptable by design, but it is a deliberate trade-off that stands out.

## P3: Lower Priority

### Plugins.Core

- [ ] Reduce null-forgiving and weak invariants in first-party plugin tools.
  Files:
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeControlFlowTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeDataFlowTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/FindCallersTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/FindCalleesTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetCodeContextTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetControlFlowGraphTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetOperationTreeTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetPartialDeclarationsTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetProjectDetailsTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetSolutionStructureTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetSymbolInfoTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/ResolveSymbolTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/SearchSymbolsTool.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins.Core/ReplayCodeActionExecutor.cs`
  Notes:
  - Several tools still rely on null-forgiving or `ValueTask.FromResult` short-circuit patterns.
  - That usually points to contracts or resolver APIs not expressing invariants strongly enough.

### Workspace

- [ ] Reduce null-forgiving and invariant-by-convention in workspace support code.
  Files:
  - `src/Roslyn.Workbench.Mcp.Workspace/MefCodeActionService.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/MutationStagingService.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/TransactionCommitService.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceResolver.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceTransaction.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceTransactionRevision.cs`
  Notes:
  - Some of these are harmless mechanically, but they still suggest type invariants are not fully modelled.

### Host

- [ ] Tighten remaining invariant bridges in server-host mapping code.
  Files:
  - `src/Roslyn.Workbench.Mcp/WorkspaceToolResultMapper.cs`
  Notes:
  - The mapper still relies on null-forgiving based on outcome conventions.

## Project Summary

- [ ] `Roslyn.Workbench.Mcp.Contracts`
  Notes:
  - No major structural issues stood out in this pass.
  - The main watchpoint is keeping contract invariants explicit rather than convention-based.

- [ ] `Roslyn.Workbench.Mcp`
  Notes:
  - Much cleaner after the server-owned tool refactor.
  - Main remaining concerns are plugin loading and invariant bridges.

- [ ] `Roslyn.Workbench.Mcp.Workspace`
  Notes:
  - Still the main architecture hotspot.
  - The code action stack is where most of the remaining complexity sits.

- [ ] `Roslyn.Workbench.Mcp.Plugins`
  Notes:
  - The main concern is static, reflection-heavy infrastructure and runtime-typed execution plumbing.

- [ ] `Roslyn.Workbench.Mcp.Plugins.Core`
  Notes:
  - Mostly serviceable, but there are still a number of weaker nullability and short-circuit patterns.

## Suggested Working Order

The recommended order is now explicitly workspace-first.
The plugin and host work should follow after the central workspace boundaries are in a better state.

### Phase 1: Workspace Core

- [x] 1. Split `MefCodeActionService`.
- [x] 2. Tighten `WorkspaceSelectionResult` into a stricter result shape.
- [ ] 3. Remove remaining plugin result leakage from workspace internals.
- [ ] 4. Rework `CodeActionRuntime` composition into a cleaner service boundary.
- [ ] 5. Sweep remaining workspace null-forgiving and invariant-by-convention cases.

### Phase 2: Plugin Boundary

- [ ] 6. Remove runtime `object` execution from the plugin invocation path.
- [ ] 7. Revisit plugin response/schema publication so more of the contract is explicit and tool-owned.

### Phase 3: Host And Follow-On Cleanup

- [ ] 8. Review plugin loading so it is less dependent on manual activation.
- [ ] 9. Tighten remaining invariant bridges in server-host mapping code.
- [ ] 10. Sweep remaining `Plugins.Core` null-forgiving and `ValueTask.FromResult` cases where the API can be strengthened.
