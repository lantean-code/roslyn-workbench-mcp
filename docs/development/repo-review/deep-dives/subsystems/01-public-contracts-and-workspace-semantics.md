# Deep dive 1 — Public contracts and Workspace semantics

Date: 2026-07-31

Status: Complete

## Scope and dependency map

The review covered public selectors, result and snapshot contracts in `Roslyn.Workbench.Mcp.Abstractions`; Workspace loading, compatibility filtering, root containment, selection, resolver services, session storage, lifecycle state transitions, operation gates, execution leases, query caches, project services and external-change detection in `Roslyn.Workbench.Mcp.Workspace`; and their direct consumers in Plugins, Plugins.Core, CodeActions and the Host. Registrations in the Host composition root were checked for lifetime and ownership consistency, and the unit/integration tests claiming these behaviours were compared with the implementation paths.

Dependency direction remains acyclic: Abstractions owns public neutral contracts; Workspace implements them and owns mutable state; Plugins and CodeActions adapt their handlers to Workspace execution contexts; Plugins.Core consumes only the public query surface; Host owns MCP binding, composition and lifecycle tools. No incorrect project reference or captive DI lifetime was substantiated in this unit.

## Representative traces

### Workspace open

`WorkspaceOpenTool` binds an absolute load path and optional root/alias, then `WorkspaceLifecycleService.OpenAsync` normalises the request, checks capacity/uniqueness and pending recovery, loads MSBuild through `WorkspaceLoadWorkflow`, publishes an advisory instance handle, constructs the external-input manifest and registers a `WorkspaceSessionSnapshot`. Failure ownership after successful loading is guarded and the loaded Workspace, manifest and status handle are released unless registration succeeds. The trace exposed RWMCP-008 because MSBuild evaluation and manifest certification are separate filesystem observations with no consistency check spanning them.

### Query context acquisition and selector resolution

Plugin and Code Action adapters acquire a shared `IWorkspaceOperationLease`, refresh external-change state, construct a snapshot-scoped resolver and invoke their handler. Workspace selection consistently requires all supplied ID/alias/path fields to identify the same session, and resolver results distinguish resolved, ambiguous and not-found outcomes. Cache scopes include Workspace partition and Roslyn `Solution` reference, and session replacement invalidates affected partitions. No lease-release, ambiguous-selection or cross-snapshot cache reuse defect survived validation. RWMCP-009 remains because the public identity tuple can repeat after process restart, and RWMCP-012 covers malformed paths that escape structured selection/resolution errors.

### Project and query services

Snapshot-scoped reference discovery, type-hierarchy analysis and target-framework caching retain neutral interfaces in Abstractions with Workspace implementations. The target-framework cache is keyed by the physical project path within a Workspace/Solution/component scope and is invalidated by session replacement. RWMCP-010 identifies incompatible solution-directory and workspace-root path identities in solution-folder projection. RWMCP-011 identifies cancellation that is accepted by the public resolver but not observed inside a multi-project evaluation batch.

### External-change detection

Ready and transaction-active sessions validate both unexpected `MSBuildWorkspace.CurrentSolution` replacement and a manifest containing source, project, imported build, analyser, metadata and configuration inputs. A detected change transitions to `WorkspaceOutOfDate` or `TransactionConflicted`, invalidates caches and queues advisory status. Generated artifact trees are deliberately excluded using evaluated output roots, with exact project/import inputs retained. Once a manifest is installed, watcher and metadata polling provide coherent invalidation; the unresolved consistency gap is the pre-install evaluation-to-capture interval in RWMCP-008.

### Reload and close

Reload takes an exclusive lease, rejects active/conflicted transactions, loads a replacement Workspace, rebuilds the manifest, allocates a new epoch, replaces the session and disposes old resources. Close takes an exclusive lease, rejects an active transaction, removes and invalidates the session, closes advisory status and disposes the manifest and loaded Workspace. No ordinary close/reload lease race was substantiated. RWMCP-013 records the missing cross-instance `Ready` publication after reload.

## Contracts, configuration and tests

Public contract validation correctly separates required member presence from selector-domain consistency, but path well-formedness is not consistently converted into public failures. Workspace option registrations and cache lifetimes are singleton-compatible. Configuration consumed by this unit matches the declared options, and explicit workspace-root containment uses physical-path checks.

Unit coverage is broad for lifecycle branches, selection, resolution, state transitions, gates, cache coalescing/invalidation and manifest polling. Integration coverage exercises real MSBuild loading, solution hierarchy and external changes. Material omissions are concurrent project/import changes during open/reload, replayed snapshots after restart, a solution below a broader workspace root, path-casing divergence, cancellation during real batch target-framework evaluation, malformed rooted selector/root paths and advisory state reset after reload.

## Findings and limitations

Independent source validation retained RWMCP-008 through RWMCP-013. No P0 or P1 issue was substantiated. No tests were run because this execution changed only review artefacts and the reviewed defects require new targeted fixtures to prove dynamically. Acceptance, Code Action audit and external-repository scenarios were not run under repository policy. Roslyn MCP tooling was unavailable in the session; source navigation used local repository inspection, while Microsoft Learn was used for current `Path.GetFullPath` and MSBuild `ProjectCollection.LoadProject` API behaviour.
