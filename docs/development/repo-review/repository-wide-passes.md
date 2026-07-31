# Repository-wide review passes

## Representative end-to-end traces

### Workspace open and query

`workspace-open` binds and validates its request in Host, calls `WorkspaceLifecycleService`, checks pending recovery, loads/evaluates MSBuild through Workspace, publishes advisory instance state, builds the input manifest and registers a session. A bundled or external query then enters its typed Host adapter, acquires a shared Workspace execution lease, resolves the exact snapshot, optionally uses snapshot-partitioned caches, invokes the handler and serialises a bounded structured result. This trace exposed RWMCP-001 between successful load and registered ownership.

### Plugin/Code Action mutation and commit

A mutation adapter acquires the mutation context for an active transaction, invokes a trusted handler or deterministic Code Action replay, detects direct live-Workspace mutation, validates the candidate solution and stages a new revision. `transaction-commit` reacquires exclusive ownership, validates snapshot/external state, takes a cross-process lock, persists exact recovery evidence, crosses the non-cancellable applying boundary, writes files atomically, reloads/registers the committed solution and removes recovery state. The server-owned/plugin-owned boundary and transaction consistency semantics match across projects.

### Unexpected failure and optional reporting

An unexpected adapter/handler exception reaches the top-level Host filter, which stores a bounded rich record and returns a correlation ID. The local details tool can return that record. Preparation looks it up, creates a separate sanitised external DTO and immutable dispatcher payload, and returns preview/digest without I/O. Submission applies consent/elicitation, atomically acquires the handle and dispatches only the prepared payload. No source/path/message field from the local record bypasses projection.

### External plugin startup

CLI/environment configuration supplies search roots; discovery enumerates immediate package directories, checks physical containment, reads metadata without loading, requires one entry point, creates a package-confined load context, composes/prepares handlers, disables duplicate IDs or colliding tool catalogues and finally registers typed MCP tools. Packages are trusted code after validation; the system does not claim sandboxing.

## Explicit cross-cutting passes

### Cross-project contract mismatches

Selector/snapshot/result contracts remain owned by Abstractions, mutation proposals terminate at Workspace, and Host schemas/binding share serializer metadata. No runtime contract mismatch survived validation. RWMCP-005 is a test expectation mismatch with a fixture catalogue rather than a production contract mismatch.

### Dependency direction and abstraction ownership

The dependency spine is acyclic: Abstractions is foundational; Workspace implements neutral state/transactions; Plugins and CodeActions adapt independently; Plugins.Core consumes the public plugin surface; Host composes all layers. CodeActions is not exposed as a third-party plugin extension and public Plugins does not depend on Host protocol types. No incorrect production dependency direction was found.

### End-to-end behaviour across boundaries

Representative open/query, mutation/commit, external plugin and error-reporting operations were followed across every project. RWMCP-001, RWMCP-002, RWMCP-003, RWMCP-006 and RWMCP-007 affect behaviour only visible when ownership, syntax scope, control flow or storage boundaries are considered across layers.

### Dependency injection and lifetime

Production Generic Host registrations are singleton-compatible and supply options, time and `IHostApplicationLifetime`; disposal ownership is held by DI or Workspace sessions. The shared raw-service integration composition drifted from production and now fails to resolve the cache lifetime dependency (RWMCP-004). No captive scoped dependency or duplicate production singleton state was found.

### Error and cancellation propagation

Cancellation normally propagates as `OperationCanceledException` and is not converted to an unexpected error. Query-cache waiter cancellation, staged operation cancellation and non-cancellable commit application are intentional. The successful-load/advisory-publication window fails to release ownership on cancellation (RWMCP-001).

### Concurrency, shared state and thread safety

Workspace operation gates separate shared queries from exclusive mutation/commit; session/cache/reference/error stores protect mutable dictionaries; plugin cache computations coalesce and use generation invalidation; error submission uses an acquired state to avoid duplicate sends. No validated race remained after checking late stores, lifecycle invalidation, disposal and concurrent submission.

### Transaction and consistency boundaries

Candidate validation, linked document merging, snapshot revision checks, external drift checks, durable plan persistence, cross-process locks, non-cancellable apply/restore and startup recovery form a coherent consistency boundary. Malformed evidence fails closed. No data-loss or partial-commit defect was substantiated.

### Serialization and compatibility

MCP input/output schema generation and runtime serialization use matching web defaults and explicit string converters on public enums. Recovery files are versioned and bounded; unknown/malformed unsafe content becomes a recovery conflict. RWMCP-002 is the validated cross-platform compatibility error in source line interpretation.

### Configuration

CLI precedence, environment fallback, bounds and effective options were traced from resolver through options registrations and consumers. Consent is deliberately command-line-only/fail-closed. State-directory shape is validated at startup, but an existing directory's file-creation capability is not checked until commit persistence (RWMCP-007).

### Security and trust boundaries

Workspaces, analyzers and plugins are explicitly trusted executable inputs. Physical path containment rejects package/recovery paths that traverse existing links, external error projection excludes sensitive fields, recovery input is bounded and Unix state permissions are strict. Configured state-directory permissions remain the operator's responsibility. No credential material, arbitrary external endpoint, unauthorised network path or state-storage confidentiality defect was found.

### Resource ownership and disposal

Loaded Workspaces, input manifests/monitors, status handles, memory caches, plugin load contexts, Sentry client and process handles were traced to owners. RWMCP-001 is the sole validated production leak. Commit/recovery streams use scoped disposal and atomic-writer cleanup.

### Performance with plausible impact

Reference discovery and plugin queries use bounded caches; caches reject late/stale entries, result sets are bounded and recovery inputs have byte caps. Watchers stop after the first relevant change. No additional performance concern with a sufficiently concrete real-world failure scenario survived review.

### Missing or misleading integration tests

RWMCP-004 prevents large portions of integration coverage from starting; RWMCP-005 independently keeps plugin discovery integration red. Core analyser tests omit cross-newline, nested executable and all-path disposal cases corresponding to RWMCP-002, RWMCP-003 and RWMCP-006. Workspace lifecycle tests cover pre-cancel and later failure cleanup but not cancellation after successful load corresponding to RWMCP-001. State-directory tests do not exercise an existing readable but unwritable directory before transaction admission (RWMCP-007).

### Duplicate, conflicting or partial behaviour

Plugin and Code Action tools intentionally have separate catalogues/adapters despite parallel query/mutation shapes. Server-owned lifecycle tools are not duplicated in plugins, and collision policy reserves every published name. No conflicting production implementation was found; the partial behaviours promoted are the analyser boundary logic and delayed state-directory writability validation already recorded.
