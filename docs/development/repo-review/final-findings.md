# RWMCP3 final validated findings

Date: 2026-08-16

## Repository-level assessment

The repository has a coherent acyclic architecture, clear ownership of Host, Workspace, plugin and Code Action concerns, disciplined process/resource lifetimes, a strong normal durable-commit protocol and credible component/published-Host coverage. Independent validation nevertheless retained nineteen concrete findings: two P1 correctness/concurrency defects and seventeen P2 contract, lifecycle, filesystem, tool, performance and operational-evidence defects. No candidate was rejected or merged as a duplicate.

The most urgent release risks identified by the review were non-atomic global transaction admission and reusable public snapshot identities. Both have since been remediated and independently reviewed. The filesystem directory-swap issue is real but was reduced to P2 because native exploitability and impact have not been reproduced.

## Remediation work items

The outstanding findings are grouped below by shared production boundary and validation path. In remediation discussions, the **next item** means the first row whose status is `Incomplete`. Every finding retains its own identifier, validation evidence, remediation record and completion status even when implemented and independently reviewed as part of one grouped change. If investigation shows that a grouping does not converge cleanly, update this table before splitting the work.

| Order | Findings | Work item | Status |
|---:|---|---|---|
| 1 | RWMCP3-002 | Correct target-framework project resolution | Complete |
| 2 | RWMCP3-005, RWMCP3-010 | Preserve durable file topology and explicit project membership | Incomplete |
| 3 | RWMCP3-006, RWMCP3-015 | Preserve structured lifecycle failures and authoritative Workspace context | Incomplete |
| 4 | RWMCP3-007 | Bind filesystem containment validation to atomic writes | Incomplete |
| 5 | RWMCP3-008, RWMCP3-009, RWMCP3-011 | Correct Core tool location, bounds and document-binding behaviour | Incomplete |
| 6 | RWMCP3-012, RWMCP3-013 | Make Code Action discovery and replay resilient | Incomplete |
| 7 | RWMCP3-014 | Reject undeclared request members at binding | Incomplete |
| 8 | RWMCP3-019 | Prune and deduplicate manifest traversal | Incomplete |
| 9 | RWMCP3-016, RWMCP3-017, RWMCP3-018 | Make ScenarioRunner restoration, evidence and option parsing reliable | Incomplete |

## P1 — High confidence

### RWMCP3-003 — Concurrent transaction starts can create two active transactions

**Status:** Complete — remediated and independently reviewed on 2026-08-20

**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionService.cs:50-123,350-403`; `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSessionAcquirer.cs:19-62`; `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSessionStore.cs:141-159`

Exclusive operation gates are per Workspace. Two requests for different Workspaces can both read a null process-global owner, create transactions and then overwrite the owner serially. Both sessions retain active transactions; later commit/rollback can also clear ownership without verifying the owner. Host protocol dispatch is concurrent and provides no compensating serialisation. Make admission and clearing atomic and owner-aware inside the session store, and add simultaneous multi-Workspace protocol/component tests.

**Remediation:** Transaction admission and completion now execute atomically under the session-store lock. Completion returns an invariant-bearing result without changing state when ownership differs; rollback reports the structured ownership fault, while commit performs durable restoration before reporting it. Store, service and real multi-Workspace component tests cover concurrent admission and completion failure. Workspace unit tests passed 1,057/1,057, Workspace integration tests passed 105/105 and the independent staged review found no defects.

### RWMCP3-004 — Reused public revision numbers can validate stale snapshots against another solution

**Status:** Complete — remediated and independently reviewed on 2026-08-21

**Location:** `src/Roslyn.Workbench.Mcp.Abstractions/Workspace/Selectors/SnapshotPrecondition.cs:6-22`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/SnapshotGuard.cs:7-24`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceTransaction.cs:17-47`; `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSnapshotIdentity.cs:25-42`

Undo followed by a new branch reuses an integer revision while allocating a different internal snapshot ID. A new transaction can likewise reuse revision zero within the same epoch. Because public preconditions and `SnapshotGuard` compare only Workspace ID, epoch and revision, stale inputs can authorise work against a different immutable solution. Publish and require an opaque snapshot identity or make public revision identity monotonic and non-reusable; cover branch and transaction-replacement aliasing through adapters.

**Remediation:** Public snapshot preconditions now include an opaque immutable-solution snapshot ID and are propagated through Workspace, plugin, Code Action and Host contracts, success envelopes, schemas and scenario execution. Branch replacement allocates a fresh identity, while undo and redo restore the stored revision identity. Snapshot validation uses an invariant-bearing result, normative guidance requires callers to echo the complete published snapshot, and stale branch/replacement coverage verifies that matching public revision numbers cannot authorise work against another solution. Workspace unit tests passed 1,063/1,063, broad component, acceptance and representative scenario validation passed, and the final independent staged review found no defects.

## P2 — High confidence

### RWMCP3-001 — New external wildcard documents remain invisible to an open Workspace

**Status:** Complete — remediated and independently reviewed on 2026-08-21

**Location:** `src/Roslyn.Workbench.Mcp.Workspace/ChangeDetection/WorkspaceChangeDetector.cs:106-159,162-243,277-303`; `src/Roslyn.Workbench.Mcp.Workspace/ChangeDetection/WorkspaceInputChangeMonitor.cs:27-41`

Only the Workspace root is recursively watched. Existing external evaluated files are individually fingerprinted, while external directories are checked only for continued existence. A newly created file matching an external wildcard is therefore not detected, the Workspace can remain Ready and reload refuses. Represent and poll external evaluated membership roots or provide explicit recertification; add real-MSBuild external wildcard-addition coverage.

**Remediation:** Manifest construction now retains evaluated `Compile`, `AdditionalFiles` and `EditorConfigFiles` wildcard rules under the same design-time MSBuild properties as the loaded Workspace, consolidates external search roots and records their loaded membership. A dedicated external-input monitor uses recursive watchers with channel-backed event processing, performs initial and directory-event membership reconciliation, polls missing or unwatchable roots and conservatively invalidates on watcher failures. Real-MSBuild tests cover conditioned globs and populated-directory insertion; focused monitor, membership and property tests have 100% line and branch coverage. Workspace unit tests passed 1,108/1,108, Workspace integration tests passed 108/108, Host unit tests passed 514/514, latest-all analyzer builds were clean, Windows and WSL EF Core stress scenarios completed with clean restoration/shutdown, and the repeated fresh staged review found no findings after correction of its first pass.

### RWMCP3-002 — Target-framework selectors can match an unrelated output-path ancestor

**Status:** Complete — remediated and independently reviewed on 2026-08-21

**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Resolution/WorkspaceResolver.cs:297-320,499-520`

After checking a project-name suffix, target-framework matching accepts any matching segment in the absolute output path. A parent directory named for another TFM can therefore select the wrong target-specific project for plugin and Code Action consumers. Use authoritative evaluated TFM identity or a validated output-layout position and add an ancestor-collision test.

**Remediation:** Workspace loading now captures authoritative `ProjectLoadProgress.TargetFramework` metadata during Roslyn resolve operations, correlates it conservatively to project IDs using physical project paths and Roslyn's multi-target name discriminator, and retains the immutable mapping through open, reload, query, transaction and mutation-staging flows. Resolver selection no longer inspects output paths or guesses from project names; missing or contradictory metadata remains unmatched. Real-MSBuild integration tests cover direct project loading, full `.slnx` loading with interleaved single- and multi-target projects, and contradictory metadata, while unit coverage verifies ancestor collisions and lifecycle propagation. Workspace unit tests passed 1,113/1,113, Workspace integration tests passed 111/111, latest-all analyser builds were clean, and both fresh independent staged reviews found no defects.

### RWMCP3-005 — Add/delete commits do not preserve explicitly itemised project graphs

**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceMutationCandidateValidator.cs:86-123`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitPlanner.cs:92-149`

Staging permits source-document additions and removals, but commit emits only file creates/deletes and neither changes project items nor proves post-reload membership. With explicit compile items, a new file disappears on reload or a deleted item remains broken. Persist project membership transactionally or reject add/delete unless reevaluation proves equivalence; add explicit-item stage/commit/reload tests.

### RWMCP3-006 — Malformed nonblank recovery paths can crash Workspace opening

**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Recovery/CommitRecoveryStore.cs:119-130,171-182,665-676`; `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:528-539`

Invalid recovery records preserve raw nonblank identities. Admission later calls `Path.GetFullPath` on that value without safe normalisation, so an unnormalisable path escapes as an exception instead of structured recovery-pending state. Sanitise invalid identities to a global block or use non-throwing normalisation; cover malformed nonblank recovery plus Workspace open.

### RWMCP3-008 — Location-based CFG requests throw for ordinary executable locations

**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetControlFlowGraphTool.cs:41-67`; `src/Roslyn.Workbench.Mcp/ToolExecution/Plugins/PluginQueryMcpServerTool.cs:53-94`

A valid location inside a statement or expression resolves to a non-root operation. Passing it to `ControlFlowGraph.Create` throws `ArgumentException` and becomes a generic correlated failure. Independent validation reproduced this through the current Host. Resolve a supported enclosing executable root or reject the location, with handler and protocol tests for nested/unsupported positions.

### RWMCP3-009 — Maximum permitted `afterLines` overflows the code-context window

**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/GetCodeContextRequest.cs:17-28`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetCodeContextTool.cs:24-33`

The schema permits `afterLines = int.MaxValue`; adding it to the selected end line overflows and produces a negative `Enumerable.Range` count. Independent validation reproduced the failure through the current Host. Use overflow-safe remaining-line arithmetic and a meaningful output bound; cover maximum, near-maximum and end-of-file requests.

### RWMCP3-010 — `renameFile=true` cannot complete a durable same-document path transition

**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Refactorings/RenameSymbolTool.cs:30-48`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceMutationCandidateValidator.cs:61-83`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitPlanner.cs:99-109,152-209`

Roslyn produces a same-document-ID text/path change. Staging accepts it because text also changed, but planning treats the new path as an existing replacement and creates no deletion or move for the old path. Model a validated durable move/delete-plus-create or reject file rename until supported; cover full stage/commit/reload, including explicit items.

### RWMCP3-011 — Format range silently ignores its document binding

**Location:** `src/Roslyn.Workbench.Mcp.Abstractions/Workspace/Selectors/TextSpanSelector.cs:8-25`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/FormatDocumentRequest.cs:6-16`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Refactorings/FormatDocumentTool.cs:11-45`

A request can bind its top-level selector to document A and nested range selector to B. Both validate, but the handler resolves only A and applies B's numeric range to A. Use a documentless nested span or require both selectors to resolve identically; add handler and protocol mismatch tests.

### RWMCP3-012 — Sibling Code Fix roots can become permanently ambiguous during replay

**Location:** `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionDiscoveryService.cs:212-249`; `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Replay/CodeActionResolver.cs:186-229`

Every registered Code Fix root is flattened with path `[0]`, unlike correctly indexed refactoring roots. Siblings with otherwise matching identity receive colliding recipes and replay returns `ActionAmbiguous` for an action just published. Include the actual root index and add a controlled sibling-root discovery/replay test.

### RWMCP3-014 — Undeclared top-level request members are silently discarded before optional defaults

**Location:** `src/Roslyn.Workbench.Mcp/Protocol/ToolRequestBinder.cs:20-81,98-125,361-370`; `src/Roslyn.Workbench.Mcp.Workspace/Selection/WorkspaceSelectorService.cs:20-31`

Published input schemas are closed, but runtime deserialization uses default unmapped-member handling. A misspelled `workspace` property disappears before validation; with one loaded Workspace, omission targets it, including for destructive lifecycle/transaction tools. Reject undeclared members except in explicitly extensible contracts and add raw protocol tests around one-Workspace fallback.

### RWMCP3-015 — Server-owned lifecycle failures can lose authoritative Workspace attribution

**Location:** `src/Roslyn.Workbench.Mcp/Tools/ServerOwnedToolBase.cs:41-64`; `src/Roslyn.Workbench.Mcp/ToolExecution/UnhandledToolExceptionFilter.cs:64-81,99-120`; `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:225-265,434-452`; `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceSessionCleanup.cs:19-61`; `src/Roslyn.Workbench.Mcp/ErrorReporting/Capture/ErrorCaptureService.cs:50-116`

Close removes a session before cleanup; reload replaces the epoch before old-resource disposal. If either then fails, server-owned tools retain no immutable resolved context, so fallback capture sees no session or the replacement. Diagnostics and Workspace-scoped consent are consequently lost or misattributed. Attach authoritative context after target resolution and cover real post-transition failures through the protocol.

### RWMCP3-016 — Baseline untracked files can be mutated without restoration

**Location:** `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Repositories/RepositoryRestorer.cs:19-61,93-163,234-265`; `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Repositories/RepositoryManager.cs:168-204`; `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Validation/RunStateValidator.cs:67-97`

Cache validation ignores untracked files and restoration records only their baseline pathnames. A wildcard-loaded baseline untracked file can be changed or deleted without capture or verification, contaminating later exact-commit runs. Preserve baseline content/identity, reject mutable baseline untracked inputs or use disposable per-run worktrees.

### RWMCP3-017 — Scenario failures can discard the evidence needed to diagnose them

**Location:** `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:271-278,450-529`

Family writers run only after all iterations. Earlier workload, Host, EventPipe, restoration or validation failure reaches an outer catch that records only `exception.Message`, after which transient state can be recursively deleted. Persist structured failure evidence—invocation, completed measurements, exception, stderr, cleanup/validation and artifact paths—before cleanup.

### RWMCP3-018 — Unknown ScenarioRunner options silently run a different workload

**Location:** `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioOptions.cs:45-97,185-228`

Unknown `--name value` pairs are retained but never checked for consumption. `--iteratons 20` therefore succeeds with the default iteration count, and other misspellings silently alter release evidence. Validate names and command applicability, reject ambiguous duplicates and add parser tests.

## P2 — Medium-high confidence

### RWMCP3-013 — One throwing Code Action provider aborts discovery for all providers

**Location:** `src/Roslyn.Workbench.Mcp.CodeActions/Tools/ListCodeActionsTool.cs:110-166,192-215,405-430`; `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionDiscoveryService.cs:145-170,173-249,349-365`

Provider calls for refactoring computation, fixable diagnostic IDs and fix registration execute without a per-provider fault boundary. One exception aborts aggregate discovery and hides unaffected providers. Add cancellation-preserving provider isolation, discard only failed-provider partial state and surface bounded diagnostics; cover mixed throwing/successful providers.

### RWMCP3-019 — Manifest construction recursively traverses excluded and overlapping directory trees

**Location:** `src/Roslyn.Workbench.Mcp.Workspace/ChangeDetection/WorkspaceChangeDetector.cs:162-215,277-303`; `src/Roslyn.Workbench.Mcp.Workspace/ChangeDetection/WorkspaceInputPathPolicy.cs:29-47`

Each project recursively enumerates all descendant directories before applying exclusions to yielded paths. Excluded roots are not pruned, overlapping project roots repeat traversal and enumeration is uncancellable during open, reload and post-commit rebuilding. Prune while traversing, deduplicate roots and propagate cancellation; measure large/overlapping trees.

## P2 — Medium confidence

### RWMCP3-007 — Directory-swap races can invalidate containment before atomic writes

**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitWriter.cs:43-100`; `src/Roslyn.Workbench.Mcp.Workspace/IO/AtomicFileWriter.cs:105-143`; `src/Roslyn.Workbench.Mcp.Workspace/IO/PhysicalPathContainment.cs:51-64`; `src/Roslyn.Workbench.Mcp.Workspace/IO/NativeAtomicFileCommitter.cs:18-60`

Containment is revalidated by pathname, then asynchronous artifact work occurs before temporary-file creation and rename. Another local process can swap a parent directory for a link/reparse point in that interval; no stable directory handle/no-follow mutation binds validation to the write. Use handle-relative/no-follow operations or otherwise bind verified filesystem identity to mutation. Native reproduction and platform-specific impact remain unproved, which limits severity and confidence.

## Notable test gaps

Simultaneous multi-Workspace transaction admission is now covered at the store and component-service boundaries; a published MCP protocol concurrency test remains a lower-level residual gap. The most consequential remaining missing evidence is stale public snapshot aliasing, explicit-item add/delete reload, malformed recovery admission, deterministic directory swaps and large overlapping manifest traversal. Tool coverage lacks the validated CFG, code-context, rename-file, format-range and Code Action provider cases. ScenarioRunner has no owning tests despite destructive restoration, process/EventPipe and evidence responsibilities, and it is not executed in CI.

## Areas not reviewed confidently

Arbitrary third-party plugin, project analyser and MSBuild logic remains trusted and open-ended. Native filesystem race and durability semantics were not reproduced across every supported platform. Remote Sentry delivery after SDK queue acceptance is provider-owned. External-repository scenario suites were inspected but not executed during this review, and the read-only dogfood Workspace reported missing package/analyser inputs that limited broad semantic querying. These limits do not undermine the validated source-level call paths above but should inform remediation validation.

