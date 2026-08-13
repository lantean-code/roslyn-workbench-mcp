# Review Unit 1: Public Contracts and Workspace Semantics

Date: 2026-08-13

**Status:** Complete

## Evidence boundary

This review used only the current checked-out source, tests, project/configuration files and the current normative review programme. It did not use Git history, diffs, changed-file discovery, branches, tags, stashes, reflogs, deleted or renamed artefacts, backups, historical audits or previous review findings.

## Scope completed

The review covered the complete current public selector, result, resolution and service surface in `Roslyn.Workbench.Mcp.Abstractions`; the loading, root-containment, selection, resolution, state, gate, lease, lifecycle, change-detection, cache, project, reference and hierarchy implementations in `Roslyn.Workbench.Mcp.Workspace`; and direct consumers in `Roslyn.Workbench.Mcp.Plugins`, `Roslyn.Workbench.Mcp.Plugins.Core`, `Roslyn.Workbench.Mcp.CodeActions` and `Roslyn.Workbench.Mcp`. DI registrations, option mapping and the unit, integration and relevant acceptance claims were inspected. Transaction implementation details were followed only where necessary to establish snapshot and lease semantics; transaction correctness remains review unit 2.

## Contract and state model

The public contract separates routing identity from replay identity. `WorkspaceSelector` routes by workspace ID, exact alias or absolute loaded path; `ProjectSelector` and `DocumentSelector` resolve within the selected `Solution`; `LocationSelector` represents either a document-bound UTF-16 span or copied text plus context; and location-backed symbol selectors require a snapshot precondition at their tool consumer. `SnapshotPrecondition` carries workspace ID, epoch and nullable transaction revision. `WorkspaceResolver.ValidateSnapshot` rejects a different workspace ID or epoch as `WorkspaceEpochMismatch` and a different nullable revision as `TransactionRevisionMismatch`.

The Host retains immutable `WorkspaceSessionSnapshot` records in a singleton session store. A session owns the filtered Roslyn `Solution`, the underlying `MSBuildWorkspace`, an input manifest/change monitor, a stable operation gate, a committed snapshot ID, a workspace epoch and an optional transaction. The permitted lifecycle is Ready to WorkspaceOutOfDate on external change, Ready to TransactionActive on transaction start, TransactionActive to Ready on commit/rollback, TransactionActive to TransactionConflicted on external change, TransactionConflicted to WorkspaceOutOfDate on rollback, and WorkspaceOutOfDate to Ready on reload.

Shared query leases and exclusive mutation/lifecycle leases are non-waiting. The acquirer selects from one host snapshot, acquires the selected session's gate, rereads the session, and returns the lease to the caller even when that reread observes removal. Query and mutation context factories dispose the acquired lease on construction exceptions and transfer ownership to their returned async-disposable composite leases. Reload preserves the gate while replacing the session with a new epoch, preventing an old and replacement session from admitting work concurrently.

Workspace query caches use workspace ID as the partition and `Solution` reference identity plus component identity as the scope, so a changed solution cannot reuse entries even before explicit invalidation. Plugin query caches use the complete `WorkspaceSnapshotIdentity` and are invalidated by workspace epoch, transaction ID or discarded snapshot IDs through lifecycle observers. Cache factories coalesce identical in-flight work, link computation cancellation to generation invalidation and Host shutdown, and reject late stores from invalidated generations.

## Representative traces

### Workspace open

`WorkspaceOpenTool` delegates through the server-owned tool base to `WorkspaceLifecycleService.OpenAsync`. The service normalises an absolute `.sln`, `.slnx` or `.csproj`, resolves a physically contained workspace root, checks capacity/path/alias uniqueness and pending recovery, starts filesystem certification, and calls `WorkspaceLoadWorkflow`. `WorkspaceLoader` creates `MSBuildWorkspace`, captures load diagnostics and opens the solution/project. The workflow rejects incompatible project input, removes unsupported projects and unresolved analyser references, rejects project/source paths outside the physical root, then returns the filtered solution. The change detector evaluates project imports/artifact roots, fingerprints tracked files/directories, attaches the already-running watcher, and performs a post-load change check. Registration allocates workspace/epoch/snapshot identities and a gate, then atomically repeats capacity and uniqueness validation. Failure paths dispose the partially loaded workspace, manifest monitor and advisory instance handle.

### Query context acquisition and selector resolution

Plugin and Code Action adapters call `WorkspaceExecutionContextFactory.CreateQueryContext`. The session acquirer resolves zero/one/multiple-workspace routing, obtains a shared lease and rereads the selected session. The factory checks the underlying Roslyn workspace and certified inputs, transitions changed Ready/TransactionActive sessions, constructs a path service and resolver over the effective immutable solution, and returns a context tied to the current snapshot identity. Plugin adapters add a plugin/tool/snapshot cache scope. Project, document, location, symbol and scope resolution preserve not-found, ambiguous and invalid outcomes through the Plugins/Core and Code Action result mappers. Location-backed bundled symbol requests validate the expected snapshot before resolving the span. Candidate `RWMCP2-001` is the exception: `list-code-actions` accepts a raw range without any snapshot precondition.

### External-change detection

Certification starts a recursive watcher before MSBuild loading. Manifest completion supplies tracked files, directories, ignored commit paths and artifact exclusions to the monitor, replaying buffered events. Before queries/mutations and during status, `HasChanged` first observes a recorded watcher change, then incomplete evaluation, deleted directories and file timestamp/length changes. The underlying `MSBuildWorkspace.CurrentSolution` reference is also checked for unexpected Roslyn-side mutation. A changed Ready session becomes WorkspaceOutOfDate; a changed transaction becomes TransactionConflicted. Session replacement invalidates Workspace caches when the solution changes or state becomes unavailable and invalidates snapshot-scoped plugin/Code Action/error-reporting observers.

### Reload

`workspace_reload` obtains the existing gate exclusively and accepts only WorkspaceOutOfDate without an active/conflicted transaction. It repeats certified loading, compatibility, containment and manifest validation. Success creates a replacement session with the same workspace ID/alias/gate, a new epoch and committed snapshot ID, replaces the store entry, invalidates the old epoch, disposes the old manifest/workspace and republishes Ready status. Failed reload leaves the old out-of-date session intact and disposes new resources.

### Close and shutdown

`workspace_close` selects and exclusively leases a session, blocks active/conflicted transactions, removes the session (triggering cache/snapshot invalidation), closes its advisory instance handle and then disposes its input manifest and loaded Roslyn workspace. Candidate `RWMCP2-003` records that an exception while closing the advisory handle occurs after removal but before both owned disposals. Generic Host shutdown disposes registered disposable singletons, including caches and the advisory publisher, but the session store is not disposable and no hosted shutdown service drains its open sessions; candidate `RWMCP2-002` records the resulting undisposed Roslyn workspaces and watcher monitors.

## DI and configuration

All participating Workspace services are Host singletons. Mutable state is intentionally centralised in the session store, cache states, instance-status publisher and bounded Code Action/plugin stores; execution contexts and leases are constructed per invocation but are not DI scopes. `StartupOptions` maps `DefaultMaxResults`, `MaxConcurrentQueries`, `MaxTransactionRevisions`, state directory, cache size/entry limits and sliding expirations into validated Workspace/cache options. `MaxConcurrentQueries` is consumed when each new session gate is created; cache limits/expirations are consumed by their singleton state cores; result and transaction defaults flow into execution/transaction services. No unused unit-1 option was identified.

## Direct-consumer validation

- `Roslyn.Workbench.Mcp.Plugins` narrows Workspace contexts into public query/mutation contexts, maps operation errors, attaches plugin query-cache scopes and preserves composite lease disposal.
- `Roslyn.Workbench.Mcp.Plugins.Core` validates snapshot preconditions for location-backed symbol/span requests, uses host-owned resolver/project/reference/hierarchy services and projects replayable references containing workspace epoch/revision.
- `Roslyn.Workbench.Mcp.CodeActions` adapts the same Workspace contexts and resolver, stores references under complete snapshot identity and invalidates them through lifecycle observers. Its list request's unguarded numeric range produced `RWMCP2-001`.
- `Roslyn.Workbench.Mcp` owns lifecycle protocol tools, result mapping, option composition and singleton lifetimes. Its normal close and generic Host shutdown paths produced `RWMCP2-002` and `RWMCP2-003`.

## Test evidence and gaps

The Workspace unit project exercises selector outcomes, path comparison and physical containment, resolver ambiguity/snapshot matching, gates/acquisition races, state transitions, lifecycle success/failure, loading/compatibility, watcher certification and events, cache coalescing/invalidation/limits, project/reference/hierarchy services and cancellation. The Workspace integration project exercises real MSBuild loads, project compatibility, unresolved analysers, import/artifact manifests, same-metadata watcher changes, selector resolution, external-change transitions and reload. Published-Host acceptance projects claim `.sln`/`.slnx`/`.csproj` workflow, compatibility, containment, selection, query selectors and reload coverage.

Executed during this unit: `Roslyn.Workbench.Mcp.Workspace.Test` passed 975/975, `Roslyn.Workbench.Mcp.Workspace.IntegrationTest` passed 88/88, and `Roslyn.Workbench.Mcp.CodeActions.Test` passed 282/282 under the pinned .NET 10 SDK with WSL artifacts routed to `/tmp/artifacts/roslyn-workbench-mcp`.

The passing tests do not disprove the candidates. `ListCodeActionsToolTests` proves that a supplied range is forwarded exactly but has no stale epoch/revision case because the request cannot carry a precondition. `WorkspaceLifecycleServiceTests` proves disposal after a successful instance-status close but does not make that close fail. Host lifecycle coverage proves application-stopping token propagation and process termination, not disposal of `ILoadedWorkspace` and `WorkspaceInputManifest` instances still retained by the session store. Acceptance tests were inspected as claimed boundary evidence but were not executed because no acceptance artefact changed and the repository policy does not authorise an automatic acceptance run for this review.

## Candidate findings

| ID | Severity | Confidence | Summary |
| --- | --- | --- | --- |
| RWMCP2-001 | P1 | High | `list-code-actions` silently applies a caller's numeric range to the current document without an epoch/revision precondition, so a stale range can select and subsequently stage an action for different code. |
| RWMCP2-002 | P2 | High | Generic Host shutdown does not dispose the loaded Roslyn workspaces or input-change monitors retained in open Workspace sessions. |
| RWMCP2-003 | P2 | High | A failure closing the advisory instance-status handle removes the Workspace but skips disposal of its manifest monitor and Roslyn workspace. |

Full evidence and remediation directions are retained in `../findings.md`.

## Conclusions and limitations

No additional substantiated defect was found in selector ambiguity handling, snapshot comparison, gate/lease transfer, reload epoch replacement, cache generation/invalidation, physical root containment, compatibility filtering or watched/polled change detection. Permitting structurally incomplete nested span/selection selector objects was treated as a lower-quality validation choice rather than a candidate because the resolver safely returns not found and current tests explicitly claim that behaviour; the final cross-contract/schema pass may revisit whether the public schema should reject those values.

The architecture map did not require correction: current-source analysis confirmed its project graph, entry points, composition roots, contracts, external boundaries, extension mechanisms and test-project inventory. Platform-specific watcher timing and Windows path/reparse behaviour are supported by current tests but were not independently exercised on native Windows in this unit. Review stops here; transaction correctness and later units have not begun.
