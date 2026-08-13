# Review Unit 2: Transactions, Commit and Recovery

Date: 2026-08-13

**Status:** Complete

## Evidence boundary

This review used only the current checked-out source, tests, project/configuration files and current normative review programme and transaction documentation. It did not use Git history, diffs, changed-file discovery, commits, branches, tags, stashes, reflogs, deleted or renamed artefacts, external backups, historical audits or previous review findings as evidence.

## Scope completed

The review covered transaction admission, global ownership, immutable revision history, preview, undo, redo and rollback; plugin and Code Action mutation adapters; candidate validation and processing; added, removed and linked-document reconciliation; diff and staging projection; source-file commit planning; pre-application and applied-state validation; physical containment; atomic writes and permission preservation; per-workspace-root cross-process locking; recovery owner, artifact and manifest persistence; commit phases, cancellation, restoration, cleanup and startup recovery; server-owned transaction tools, status projection, DI registrations, options and current unit, integration and acceptance claims.

Direct implementation and consumer paths were followed across `Roslyn.Workbench.Mcp.Abstractions`, `Roslyn.Workbench.Mcp.Workspace`, `Roslyn.Workbench.Mcp.Plugins`, `Roslyn.Workbench.Mcp.Plugins.Core`, `Roslyn.Workbench.Mcp.CodeActions` and `Roslyn.Workbench.Mcp`. The Workspace lock fixture and real-filesystem transaction/recovery tests were included. No production code was modified.

## Transaction and persistence model

The Host permits one active transaction owner across all open Workspaces. `TransactionService` obtains the selected session gate exclusively for start, history, commit and rollback, while preview uses a shared lease. A transaction retains its committed baseline solution and snapshot ID, an ordered bounded revision list and a current revision cursor. Appending after undo discards the redo suffix and its snapshot IDs. Mutation execution holds the same exclusive session lease from context acquisition through handler execution, candidate processing and staging, preventing concurrent in-process mutation or lifecycle operations on that Workspace.

Mutation handlers receive the current immutable solution but no disk-writing service. Plugin and Code Action adapters convert successful proposals to Workspace candidates and stage them through `MutationStagingService`. The processor rejects Workspace-instance, project-structure, compilation-option, reference and unsupported-document changes; propagates added and removed documents across matching multi-target project contexts; and merges non-overlapping linked-document text changes to one physical result. Staging creates a new immutable revision and snapshot identity, replaces the authoritative session and publishes advisory instance status. No staging or rollback operation writes source files.

Commit is the sole public source-write boundary. It checks the snapshot and current transaction state, detects known input changes, starts filesystem certification, acquires a non-blocking per-root inter-process lock and constructs an exact create/replace/delete plan. The plan records target paths, original and intended SHA-256 hashes, original Unix modes, created directories, exact staged bytes and backups. Owner evidence is written before artifacts and the `Prepared` manifest; targets are revalidated; an `Applying` manifest is persisted; then target application, restoration and validation are non-cancellable. Successful application builds a new certified input manifest, writes `Committed`, promotes the transaction solution to the committed session, disposes the old manifest and removes delete markers and recovery evidence when cleanup succeeds.

Recovery state is kept under the configured state directory, separate from the Workspace. Startup initialises and validates that directory before MCP transport, cleans pre-manifest owners only while holding the matching Workspace lock, retries non-terminal manifests under that lock, and retains `RecoveryConflict` or `RecoveryIncomplete` evidence. Workspace open and full server status consume the same recovery store. Recovery manifests and paths are structurally validated, state files are owner-only on Unix, and physical containment is rechecked before source or recovery filesystem operations.

## Representative traces

### Single-file mutation and successful commit

The Host plugin adapter acquires a mutation execution lease, invokes the bundled `rename-symbol` handler, checks that the plugin did not mutate the live Roslyn Workspace, and calls the lease stager. Snapshot-aware symbol resolution rejects stale epoch/revision input before Roslyn creates a candidate solution. Workspace candidate processing and diff construction append revision 1 without touching disk. `transaction-commit` then obtains the exclusive gate, validates revision 1, takes the root lock, plans a replacement, persists owner/backup/staged/manifest records, revalidates the original hash and mode, atomically replaces the source, validates the intended hash, certifies the promoted solution and removes recovery evidence. The session keeps its Workspace epoch but receives a new committed snapshot ID and no transaction.

### No change, history and rollback

A handler no-change result is returned without calling the Workspace stager. The public execution-result contract makes the handler distinguish `NoChange` from a successful candidate; the common stager does not reclassify a successful proposal solely because its resulting change summary is empty. Current published-Host no-change coverage exercises the explicit handler outcome rather than a successful no-delta candidate. Undo and redo move only the revision cursor and current immutable solution after snapshot validation; a later append truncates redo history. Rollback selects the transaction baseline, clears global ownership and returns Ready, or returns WorkspaceOutOfDate when rolling back an already conflicted transaction. Current transaction unit, integration and published-Host acceptance tests exercise admission rejection, capacity, revision traversal, redo truncation, no-change and rollback without disk mutation.

### Linked and multi-target source

For an added or removed source document, the candidate processor finds sibling project contexts representing the same physical project and reproduces the change with matching folders, parse options and source kind. For existing linked documents, it computes text changes for every document sharing a physical path, rejects overlapping incompatible edits and applies the merged text to each linked identity. The diff and planner deduplicate the resulting canonical physical target only when operation and intended hash agree. Current acceptance coverage commits a rename in a multi-target linked document and asserts one physical target; unit coverage exercises propagation, compatible merge and overlap rejection.

### Add, replace, delete and partial failure

The planner derives changed, added and removed regular source documents from the current solution relative to the transaction baseline. Creates require an absent target and record missing directories; replacements require an existing target and retain exact bytes and Unix mode; deletes require an existing target and use a commit-specific same-directory marker. Every target must be physically inside the Workspace and an existing baseline document or inside an applicable project directory. The writer revalidates immediately before each entry. If later application or promotion fails, restoration walks entries in reverse, preserves externally divergent targets, removes only unchanged commit-created files, restores replacements from backups, returns delete markers, removes empty created directories and retains a conflict/incomplete manifest when exact restoration cannot be established.

### Cancellation boundary

Cancellation is observed during validation, planning, recovery-plan persistence and pre-application revalidation. A cancellation before `Applying` removes prepared evidence where possible and propagates cancellation without source application. After the durable `Applying` transition, application, validation, restoration, terminal manifest writes and cleanup intentionally use `CancellationToken.None`; the client cancellation cannot interrupt a partially applied source set. Unit tests cover cancellation during planning and preparation and assert that restoration is not spuriously invoked. No current process-level test injects client cancellation after durable application begins, but the current call path contains no cancellable source operation after that boundary.

### External drift before and during application

Drift already visible in the session input manifest is rejected before lock acquisition and transitions the transaction to conflicted. Target existence, hash and Unix-mode changes detected after planning are rejected by writer revalidation, and target drift after an entry is applied is detected by applied-state validation and restored or retained as a recovery conflict. Unrelated tracked input events captured during application or promotion also force restoration.

Candidate `RWMCP2-004` identifies an uncovered interval: the only comparison to the session's baseline input occurs before certification and commit planning. If an external process changes a source target after that check but before the planner reads it, the planner accepts the external bytes as the plan's original state. Commit certification then ignores every target path as commit-owned, so both the buffered watcher event and polling for that target are suppressed. Revalidation consequently confirms the newly captured external hash and the commit can overwrite the external edit while reporting success.

### Interrupted commit and startup recovery

An owner record without a manifest represents interruption before a recoverable plan existed and is cleaned only under the Workspace lock. `Prepared`, `Applying` and `RecoveryIncomplete` manifests are restored under the same lock; `Committed` manifests finish delete-marker cleanup; terminal restored records are removed; conflicts remain visible and block matching Workspace open. Real-filesystem integration tests instantiate a fresh recovery service over persisted records and exact files, including partially applied create/replace/delete sets, external divergence, malformed paths and deterministic second-target failure. The published-Host startup test restarts a process with a pre-existing terminal conflict and verifies status and blocked open, but it does not kill a Host during application and prove automatic restoration in a newly started process.

Candidate `RWMCP2-005` identifies an integrity hole in this path. The manifest requires syntactically valid original and intended hashes, but `ReadArtifactAsync` validates only artifact path, permissions and maximum size. Replacement restoration writes backup bytes without comparing them to `OriginalHash`; delete restoration moves the marker without hashing it; and successful traversal returns `Restored`, after which startup deletes the evidence. Corrupted or independently altered recovery content can therefore replace a source target and be declared successfully restored.

### Cross-process contention and containment

The lock manager places `commit.lock` under the physically contained `.vs/roslyn-workbench-mcp/locks` directory, rechecks containment after creating the directory and obtains an OS byte-range lock on Windows/Linux or non-blocking `flock` on macOS. Contention returns retryable `WorkspaceBusy`; an acquisition I/O/access failure returns a retryable fault. The lock fixture proves a separate Windows/Linux process blocks the same root, releases after normal exit and releases after process termination; different roots do not contend. Unit tests cover the macOS provider branch, but the checked-in external process fixture does not execute macOS `flock` inter-process behaviour.

Atomic writes use a same-directory temporary file, durable flush and native atomic replacement/move. POSIX commits fsync the containing directory; Windows uses extended-path-aware native replacement/move. Commit targets, delete markers, recovery roots and artifacts pass physical containment checks, and Unix recovery directories/files enforce owner-only modes. Current integration and acceptance tests exercise traversal/reparse rejection, exact binary content, source permissions, multi-file application and lock release.

## DI and configuration

The state directory/security, recovery store, planner, lock manager, writer, recovery service, mutation staging service, transaction commit service and transaction service are singleton registrations. Interface aliases point to the same singleton implementation where an internal service has multiple roles. `StartupPrerequisiteLifecycleService` initialises durable state and completes recovery before Host startup proceeds. `MaxTransactionRevisions` and `StateDirectory` are resolved from command line/environment/default configuration, validated, mapped into `WorkspaceOptions` and consumed by transaction construction and durable-state services respectively. Recovery size limits are fixed internal safety limits and are enforced before plan persistence and before artifact allocation. No unused unit-2 option or inconsistent lifetime registration was identified.

## Direct-consumer validation

- `Roslyn.Workbench.Mcp.Plugins` publishes the required mutation snapshot contract, adapts the host-owned Workspace context, contains direct live-Workspace mutation and stages only normalized successful candidates. Public authoring guidance explicitly requires handlers to use snapshot-aware resolver methods or `ValidateSnapshot`; this responsibility is not duplicated by the adapter.
- `Roslyn.Workbench.Mcp.Plugins.Core` follows that rule for bundled rename, formatting and using mutations and returns immutable candidates only. Its integration project exercises the real mutation pipeline.
- `Roslyn.Workbench.Mcp.CodeActions` resolves opaque action references against expected snapshot identity, evaluates actions against the current solution and hands only the candidate to the common stager. Its adapter consumes a reference only after successful staging.
- `Roslyn.Workbench.Mcp` owns the five transaction MCP tools, request binding/result mapping, startup recovery prerequisite, recovery/status projection and all singleton registrations. Commit and history inherit the required mutation snapshot; start, preview and rollback intentionally do not interpret caller-provided source coordinates.

## Test evidence and gaps

Executed during this unit under the pinned .NET 10 SDK with WSL artifacts routed to `/tmp/artifacts/roslyn-workbench-mcp`: `Roslyn.Workbench.Mcp.Workspace.Test` passed 975/975, `Roslyn.Workbench.Mcp.Workspace.IntegrationTest` passed 88/88, `Roslyn.Workbench.Mcp.Test` passed 480/480, and `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` passed 8/8.

The Workspace unit suite covers transaction state and results, candidate constraints, added/removed/linked processing, diff projection, plan structure, containment, revalidation, per-entry application, cancellation, restoration states, cleanup failures, recovery-store validation and startup recovery orchestration. The Workspace integration suite adds real Roslyn/MSBuild staging, exact create/replace/delete bytes, encoding and Unix modes, atomic I/O, persisted recovery and a separate lock-owner process. Host unit tests cover each server-owned transaction tool and both mutation adapters. Bundled integration proves actual handlers stage through Workspace.

Current published-Host acceptance source claims external plugin staging/rollback, no-change, history and ownership; Code Action create/replace, multi-file and linked commits; Unix permission preservation; pre-write external drift; physical containment; restart status and recovery blocking. Acceptance tests were inspected but not executed because no acceptance artefact changed and repository policy does not authorise an automatic acceptance run for this review.

The passing tests do not disprove the candidates. External-drift tests modify the file before commit invocation or inject drift after the writer has begun applying; none changes a commit target between the initial manifest check and planner capture while exercising certification's ignored paths. Recovery tests use correct backup and delete-marker bytes; artifact tests validate path, permissions and size but not the manifest hash. Process-level coverage restarts with a constructed terminal conflict rather than terminating a Host during `Applying` and restoring the exact partial set in the replacement process.

## Candidate findings

| ID | Severity | Confidence | Summary |
| --- | --- | --- | --- |
| RWMCP2-004 | P1 | High | A source edit made after initial input validation but before commit planning is captured as the commit's original state, ignored by certification and can be overwritten by a successful commit. |
| RWMCP2-005 | P1 | High | Recovery writes backup or delete-marker content without validating it against the manifest's original hash and can declare corrupted restoration successful. |

Full evidence and remediation directions are retained in `../findings.md`.

## Unit 1 revisit, conclusions and limitations

Transaction consumers were revisited against unit 1's snapshot, lease and input-certification conclusions. Mutation adapters retain the exclusive lease through staging, required mutation snapshot semantics are honoured by current direct consumers, and the architecture map remains accurate. `RWMCP2-004` is not a correction to the unit-1 change detector itself: it is caused by the commit service assigning all target paths to certification's ignored set after the only baseline comparison has passed. No unit-1 report change was required.

No additional substantiated defect was found in global in-process transaction ownership, revision traversal, stale-revision guards, candidate structure validation, linked-document reconciliation, cancellation after `Applying`, immediate per-entry hash validation, physical containment, lock release, atomic operation selection, Unix permission preservation, divergence-safe restoration or DI/configuration wiring. Native Windows and macOS filesystem semantics were established from current platform branches and test claims but were not independently executed in this WSL review. Review stops here with unit 2 complete; units 3–8, repository-wide passes, independent candidate validation and production remediation have not begun.
