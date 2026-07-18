# Architecture Audit Checklist

This document captures the current audit findings across the source projects and turns them into a working checklist.

Scope:
- `Roslyn.Workbench.Mcp`
- `Roslyn.Workbench.Mcp.CodeActions`
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

Current production dependency direction:

```text
Host -> CodeActions -> Workspace
Host -> Plugins.Core -> Plugins -> Workspace
Host -> Plugins -> Workspace
```

The former production Contracts project has been removed. Contracts now live with their owner: Code Actions in CodeActions, inspection DTOs in Plugins.Core, Workspace domain models in Workspace, plugin metadata/results in Plugins, and MCP protocol/lifecycle contracts in Host.

The CodeActions follow-up is complete in [CodeActionsArchitectureValidation.md](CodeActionsArchitectureValidation.md). The remaining Host work is maintained, in implementation order, in [HostArchitectureValidation.md](HostArchitectureValidation.md).

## P1: High Priority

### Workspace

- [x] Replace sequential source writes with a durable multi-file write-ahead commit protocol.
  Notes:
  - A complete canonical create/replace/delete plan and exact binary artifacts are durable before `Applying`.
  - Per-workspace-root inter-process locking and the existing in-process operation gate are both authoritative.
  - Hash-aware, idempotent restoration preserves externally divergent files and records conflicts.
  - Atomic replacement uses write-through publication on Windows and parent-directory synchronisation on Unix.
  - The authoritative lock is an actual OS lock, with real contention and crash-release integration coverage; file-share flags alone are not treated as ownership.
  - A durable pre-manifest owner record allows startup to clean interrupted preparation only while holding the correct workspace-root lock.
  - Successful source-only commits promote the staged Roslyn `Solution`; `MSBuildWorkspace.TryApplyChanges` and reload are not used.
  - `.vs` instance/status files are warning-only hints, are excluded from workspace input tracking, and are queryable through `workspace-status.instances` after live-handle validation.
  - Workspace coordination resolves one persisted repository-level `WorkspaceRoot`; project-only loads therefore do not create `.vs` directories beneath each project.
  - A single CLR `FileStream.Lock` provider owns the cross-process byte-range lock; only genuine contention maps to `WorkspaceBusy`.

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

- [x] Remove runtime-typed execution from the plugin invocation path.
  Files:
  - `src/Roslyn.Workbench.Mcp.Plugins/IPluginToolInvoker.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins/PluginToolInvoker.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins/ToolExecutor.cs`
  Notes:
  - Closed generic registrations dispatch through typed visitors without reflection, `dynamic`, or `object` invocation.

- [x] Reduce heuristic/reflection-driven response and schema publication.
  Files:
  - `src/Roslyn.Workbench.Mcp.Plugins/ToolResponseDescriptorResolver.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins/ToolSchemaFactory.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins/Tooling/ToolSchemaBuilder.cs`
  - `src/Roslyn.Workbench.Mcp.Plugins/ToolResponseShaper.cs`
  Notes:
  - Host now owns schema generation, response shaping and MCP publication for both plugin and internal Code Action catalogues.

## P2: Medium Priority

### Workspace

- [x] Remove remaining plugin result leakage from workspace internals.
  Files:
  - `src/Roslyn.Workbench.Mcp.Workspace/IMutationStagingService.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/MutationStagingService.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceExecutionContextFactory.cs`
  Notes:
  - Workspace exposes only workspace-owned contexts, failures, mutation candidates and staging results.

- [x] Rework Code Action provider composition into a container-validatable catalogue boundary.
  Files:
  - `src/Roslyn.Workbench.Mcp.CodeActions/Composition/MefCodeActionProviderCatalog.cs`
  - `src/Roslyn.Workbench.Mcp/HostConfiguredMsBuildWorkspaceFactory.cs`
  Notes:
  - Code Actions owns an immutable MEF provider catalogue; Host bridges its optional Roslyn host services into Workspace creation through constructor-injected services.
  - Code Action tool registrations retain closed handler, request and response generic types without constructing handlers during catalogue creation. Host registers the handler and closed MCP adapter with DI, allowing constructor validation.
  - Query and mutation execution contexts contain only invocation-specific Workspace state. List and describe handlers own their orchestration and receive focused collaborators; mutation handlers receive replay, fix-all, scoped-fix or location-fix services directly through constructor injection. There are no aggregated query- or mutation-workflow façades.

### Host

- [x] Replace manual plugin activation and loose-DLL discovery with validated MEF package composition.
  Files:
  - `src/Roslyn.Workbench.Mcp/PluginCatalogLoader.cs`
  Notes:
  - Host reads `RoslynPluginAttribute` and informational-version PE metadata before loading external code.
  - Each immediate package directory has one marked entry assembly and one non-collectible `AssemblyLoadContext` with package-local dependency resolution.
  - Plugins.Core uses the same MEF configuration and materialisation pipeline in the default load context.
  - Handler contracts and lifecycle rules are inspected before constructors run; closed generic registrations retain reflection-free typed invocation.
  - Reserved, bundled and external collision outcomes are deterministic and do not depend on filesystem order.

## P3: Lower Priority

### Plugins.Core

- [x] Reduce null-forgiving and weak invariants in first-party plugin tools.
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
  - The null-forgiving audit and subsequent plugin architecture work removed the identified invariant suppressions.
  - The former `ValueTask.FromResult` note was a mechanical style observation rather than evidence of a boundary defect.

### Workspace

- [x] Reduce null-forgiving and invariant-by-convention in workspace support code.
  Files:
  - `src/Roslyn.Workbench.Mcp.Workspace/MefCodeActionService.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/MutationStagingService.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/TransactionCommitService.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceResolver.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceTransaction.cs`
  - `src/Roslyn.Workbench.Mcp.Workspace/WorkspaceTransactionRevision.cs`
  Notes:
  - The dedicated null-forgiving remediation and the subsequent Workspace service refactors removed the listed suppressions and strengthened result and lease invariants.

### Host

- [x] Tighten remaining invariant bridges in server-host mapping code.
  Files:
  - `src/Roslyn.Workbench.Mcp/WorkspaceToolResultMapper.cs`
  Notes:
  - The mapper no longer uses null-forgiving. Its remaining default throw reports a violated internal result invariant rather than implementing ordinary flow control.
  - Broader Host composition and publication findings are tracked in [HostArchitectureValidation.md](HostArchitectureValidation.md).

## Project Summary

- [ ] `Roslyn.Workbench.Mcp`
  Notes:
  - Project ownership is correct and plugin loading is now split into focused MEF/package collaborators.
  - Remaining composition, schema, registration, publication and lifecycle work is tracked in the Host validation document.

- [x] `Roslyn.Workbench.Mcp.Workspace`
  Notes:
  - Workspace no longer references Plugins or CodeActions and its architecture and focused unit programme are complete.

- [x] `Roslyn.Workbench.Mcp.Plugins`
  Notes:
  - Typed registrations and visitors have replaced MCP-aware, runtime-typed execution plumbing; MEF configuration and materialisation are complete.

- [x] `Roslyn.Workbench.Mcp.Plugins.Core`
  Notes:
  - Bundled tools use the shared MEF plugin path and the earlier nullability concerns have been remediated.

## Suggested Working Order

The recommended order is now explicitly workspace-first.
The plugin and host work should follow after the central workspace boundaries are in a better state.

### Phase 1: Workspace Core

- [x] 1. Split `MefCodeActionService`.
- [x] 2. Tighten `WorkspaceSelectionResult` into a stricter result shape.
- [x] 3. Remove remaining plugin result leakage from workspace internals.
- [x] 4. Replace `CodeActionRuntime` composition with a directly registered provider catalogue.
- [x] 5. Sweep remaining workspace null-forgiving and invariant-by-convention cases.

### Phase 2: Plugin Boundary

- [x] 6. Remove runtime `object` execution from the plugin invocation path.
- [x] 7. Move plugin response/schema publication into Host with typed registration visitors.

### Phase 3: Host And Follow-On Cleanup

- [x] 8. Replace loose-DLL/manual activation with validated MEF package composition.
- [x] 9. Tighten remaining invariant bridges in server-host mapping code.
- [x] 10. Sweep remaining `Plugins.Core` null-forgiving cases and reassess the mechanical `ValueTask.FromResult` observation.

The next Host phases are defined in [HostArchitectureValidation.md](HostArchitectureValidation.md); that document supersedes this older phase list for Host implementation work.
