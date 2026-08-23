# RWMCP3 candidate finding ledger

Date created: 2026-08-16

**Status:** Independent validation initially validated nineteen candidates without rejection or duplication; remediation subsequently rejected RWMCP3-007 as outside the product operating model.

**Next identifier:** `RWMCP3-020`

## Evidence boundary

This ledger records only candidates independently established from the current repository during the RWMCP3 review. It must not contain or reconstruct findings from Git history, removed review artefacts, external backups, historical audits, earlier reviews or prior conversation context.

## Candidate requirements

Each candidate must record its stable identifier, status, severity, confidence, exact file and line range, concrete failure scenario, supporting call path or evidence, affected projects/subsystems, concise remediation direction, originating review unit and complete validation/rejection history. Identifiers are monotonically allocated and never reused. Candidates rejected or merged during independent validation remain here with their disposition but are excluded from the final validated report; a finding rejected later during remediation remains in both ledgers with its complete history and final disposition.

## Candidates

### RWMCP3-001 — New external wildcard documents remain invisible to an open Workspace

**Status:** Complete — remediated and independently reviewed on 2026-08-21
**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Workspace/ChangeDetection/WorkspaceChangeDetector.cs:106-157,207-304`; `src/Roslyn.Workbench.Mcp.Workspace/ChangeDetection/WorkspaceInputChangeMonitor.cs:27-41`

**Scenario:** A project inside `workspaceRoot` includes `../../shared/**/*.cs`. `SharedA.cs` exists at open; later `SharedB.cs` is created beside it. The watcher covers only `workspaceRoot`, external directory polling checks existence rather than membership, and file polling knows only `SharedA.cs`. The Workspace remains `Ready` indefinitely and reload refuses because no stale state was detected.

**Evidence:** Manifest creation records loaded documents and parent existence; recursive enumeration is limited to project directories. Query acquisition and status trust `HasChanged`; reload requires out-of-date. Current integration tests cover in-root additions and pre-existing external files, not post-open external wildcard membership.

**Affected:** Workspace change detection/lifecycle, Host status/reload, plugin and Code Action consumers.  
**Remediation direction:** Safely represent/poll evaluated wildcard membership roots or provide an explicit recertifying reload; add real-MSBuild external wildcard membership coverage.  
**Origin/history:** Unit 1, 2026-08-16; current source and tests inspected, no executable reproduction; independently validated in Stage 4. Remediated on 2026-08-21 by preserving evaluated `Compile`, `AdditionalFiles` and `EditorConfigFiles` wildcard membership under the Workspace's design-time MSBuild properties, monitoring consolidated external roots, reconciling directory-level changes and polling when watchers are unavailable. Real-MSBuild, unit, integration and Windows/WSL EF Core stress coverage passed; the first independent review found three defects that were corrected, and the repeated fresh staged review found no findings.

### RWMCP3-002 — Target-framework selectors can match an unrelated output-path ancestor

**Status:** Complete — remediated and independently reviewed on 2026-08-21
**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Resolution/WorkspaceResolver.cs:297-320,499-520`

**Scenario:** A `net10.0` project under a directory named `net8.0` has an output path containing both names. A selector requesting `net8.0` matches because the resolver searches every absolute output-path segment, selecting the wrong target-specific project.

**Evidence:** Plugin and Code Action project scopes share this resolver. Tests cover project-name suffix, expected output segment and missing framework, not unrelated ancestor collisions.

**Affected:** Abstractions, Workspace, Plugins/Core, CodeActions and Host clients.  
**Remediation direction:** Use authoritative evaluated target-framework identity or restrict inference to a validated output-layout position; add unit and real-MSBuild collision tests.  
**Origin/history:** Unit 1, 2026-08-16; current implementation/consumers inspected; independently validated in Stage 4.

### RWMCP3-003 — Concurrent transaction starts can create two active transactions

**Status:** Complete — remediated and independently reviewed on 2026-08-20

**Severity:** P1  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionService.cs:66-108,388`; `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSessionStore.cs:141-152`

**Scenario:** Simultaneous starts on different Workspaces hold different gates, both observe no global owner, both create transactions and later store writes overwrite the owner ID. Both calls can succeed while both sessions retain transactions; rollback can then clear ownership unconditionally and leave another active transaction ownerless.

**Evidence:** Owner check and set are separate operations; store replacement has no compare-and-set. Current ownership tests and scenarios start sequentially.

**Affected:** Workspace state/transactions, Host tools, all mutation admission and protocol concurrency.  
**Remediation direction:** Make owner admission/clearing atomic and owner-aware inside the store; add simultaneous multi-Workspace component/protocol tests.  
**Origin/history:** Unit 2, 2026-08-16; traced through service/store/admission and tests; focused suites passed without exercising the race. Remediated on 2026-08-20 with atomic store-owned admission, owner-aware completion results, durable commit recovery and simultaneous multi-Workspace component coverage; the staged implementation received an independent Review Agent pass with no findings.

### RWMCP3-004 — Reused public revision numbers can validate stale snapshots against another solution

**Status:** Complete — remediated and independently reviewed on 2026-08-21
**Severity:** P1  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Abstractions/Workspace/Selectors/SnapshotPrecondition.cs:6-22`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/SnapshotGuard.cs:14`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceTransaction.cs:21-41`

**Scenario:** A transaction reaches revision 2, undoes to 1 and branches to a different revision 2. The stale public tuple `{workspaceId, workspaceEpoch, transactionRevision}` matches the unrelated solution. The same aliasing can occur after commit followed by a new revision-zero transaction because the internal snapshot changes but epoch does not.

**Evidence:** Append truncates redo and reuses public revision numbers while allocating an internal snapshot ID; `SnapshotGuard` compares only public fields. Ordinary plugin/bundled mutations rely on that precondition, unlike stronger internal Code Action references.

**Affected:** Abstractions, Workspace, Plugins/Core, external plugins and Host protocol.  
**Remediation direction:** Publish/require opaque snapshot identity or make public revision identity monotonic and non-reusable; add stale-branch and old-transaction adapter tests.  
**Origin/history:** Unit 2, 2026-08-16; current contracts/history/guard/consumers inspected; existing mismatch tests did not create tuple aliasing. Remediated on 2026-08-21 by publishing and validating an opaque immutable-solution snapshot ID across all execution boundaries, adding stale branch/replacement coverage, and aligning agent and plugin guidance; the final staged implementation received an independent Review Agent pass with no findings.

### RWMCP3-005 — Add/delete commits do not preserve explicitly itemised project graphs

**Status:** Complete — intentional product boundary confirmed and documented on 2026-08-21
**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceMutationCandidateValidator.cs:93-115`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitPlanner.cs:117-147`

**Scenario:** With `EnableDefaultCompileItems=false` and explicit compile items, a plugin or Code Action adds or removes a document. Staging accepts and commit changes only the file. Reload omits the new file or leaves an explicit missing item, diverging from the temporarily promoted Roslyn solution.

**Evidence:** Candidate validation checks source/path containment, not reevaluated inclusion. Planner writes source only; documentation excludes project-file mutation. Existing add/create coverage uses default globs.

**Affected:** Workspace graph/commit, Plugins, CodeActions and lifecycle.  
**Remediation direction:** Conservatively reject add/delete unless evaluation proves post-reload inclusion/removal correctness, or transactionally persist project items; add explicit-item reload coverage.  
**Origin/history:** Unit 2, 2026-08-16; traced across all mutation adapters and current fixtures; independently validated in Stage 4. Disposition confirmed on 2026-08-21: source mutations intentionally do not edit project files, so callers own explicit membership, exclusions and linked-item declarations. The boundary is now explicit in agent and transaction guidance, and the grouped final independent review found no defects.

### RWMCP3-006 — Malformed nonblank recovery paths can crash Workspace opening

**Status:** Complete — remediated and independently reviewed on 2026-08-21

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Recovery/CommitRecoveryStore.cs:126-129`; `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:533-539`

**Scenario:** An invalid recovery manifest preserves a nonblank loaded path that `Path.GetFullPath` rejects. Startup reports a recovery conflict, but later Workspace admission normalises the raw path without safe handling and throws instead of returning structured `RecoveryPending`.

**Evidence:** Invalid-manifest creation preserves raw `LoadedPath`; blank identities block globally, but malformed nonblank paths bypass that branch. Current tests cover malformed/empty identities separately, not their combination with open.

**Affected:** Recovery, Workspace lifecycle, Host startup/status/protocol.  
**Remediation direction:** Sanitise invalid identities to empty/global or use non-throwing normalisation and globally block unnormalisable evidence; add malformed nonblank admission coverage.  
**Origin/history:** Unit 2, 2026-08-16; focused suites passed without this composition; independently validated in Stage 4. Remediated on 2026-08-21 with non-throwing canonical identity handling, explicit malformed-evidence state, solution-only matching, legacy payload validation and pre-recovery orphan-owner validation. Workspace unit tests passed 1,148/1,148, Workspace integration tests passed 112/112, affected `latest-all` analyser builds were clean and the final independent staged review found no defects.

### RWMCP3-007 — Directory-swap races can invalidate containment before atomic writes

**Status:** Complete — rejected as outside the product operating model on 2026-08-22
**Severity:** P2  
**Confidence:** Medium  
**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitWriter.cs:43-100`; `src/Roslyn.Workbench.Mcp.Workspace/IO/AtomicFileWriter.cs:105-143`; `src/Roslyn.Workbench.Mcp.Workspace/IO/PhysicalPathContainment.cs:51-64`; `src/Roslyn.Workbench.Mcp.Workspace/IO/NativeAtomicFileCommitter.cs:18-60`

**Scenario:** After revalidation confirms containment, another process replaces a parent directory with a symlink/reparse point before the atomic writer opens its pathname. The write can target outside the Workspace root; later recovery can detect divergence but may reject the escaping path and cannot undo the external write.

**Evidence:** The Workbench lock is advisory to this Host. Containment is path-based and does not bind validation to a stable directory handle; atomic IO later uses the original string path. Tests cover pre-existing links, not directory replacement in the interval.

**Affected:** Workspace containment, commit writer, atomic IO, recovery and cross-process security.  
**Remediation direction:** Mutate relative to verified directory handles with no-follow/reparse protections or otherwise bind validation/rename to stable filesystem identities; add deterministic directory-swap tests.  
**Origin/history:** Unit 2, 2026-08-16; static call path substantiated, executable race not reproduced. Remediation review confirmed that normal agent mutations are coordinated through the transaction pipeline and ordinary user or IDE changes are covered by change detection, snapshot semantics and commit revalidation. The non-cancellable application phases of `transaction-commit` and automatic startup recovery are short coordinated boundaries during which the user and other tools must not switch branches, check out or reset paths, move directory trees, or replace directories with links. After starting or restarting the Host, callers must wait for initialisation and recovery status before structural repository work. Supporting concurrent structural changes during those application phases would require substantial Windows and POSIX handle-relative native mutation rather than another racy pathname check. The scenario was therefore rejected as a product defect and recorded as an accepted residual risk in the [product operating model](../ProductOperatingModel.md); existing pre-write containment remains required for ordinary and pre-existing link escapes.

### RWMCP3-008 — Location-based CFG requests can throw for ordinary executable locations

**Status:** Validated  
**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetControlFlowGraphTool.cs:41-67`; `src/Roslyn.Workbench.Mcp/ToolExecution/Plugins/PluginQueryMcpServerTool.cs:53-94`

**Scenario:** A client supplies a valid location inside a method expression or statement. Resolution returns the innermost syntax node, and `ControlFlowGraph.Create` receives an operation with a non-null parent and throws instead of returning a graph or structured invalid request.

**Evidence:** A current published-Host request reproduced `ArgumentException: Given operation has a non-null parent` with correlation `b4e0e56f-77aa-40d0-8684-0d258e17e40e`. Existing CFG tests use method declarations or symbol selectors rather than nested executable locations.

**Affected:** Plugins.Core CFG tool, Host error projection and MCP clients.  
**Remediation direction:** Resolve a supported enclosing executable root or reject unsupported locations; add handler and protocol coverage for nested and unsupported locations.  
**Origin/history:** Unit 4, 2026-08-16; reproduced read-only against current source and independently reproduced in Stage 4.

### RWMCP3-009 — Maximum permitted `afterLines` overflows the code-context window

**Status:** Validated  
**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/GetCodeContextRequest.cs:17-28`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetCodeContextTool.cs:24-33`

**Scenario:** A schema-valid `afterLines = int.MaxValue` request selects a line after the first. Window arithmetic wraps negative, and `Enumerable.Range` throws for a negative count.

**Evidence:** A current published-Host request reproduced the overflow and generic correlated failure with correlation `721e23b7-be34-4ed7-99b7-ae9954ba0137`. Tests do not exercise schema-permitted extreme line counts.

**Affected:** Plugins.Core contract/tool, Host schema/binder and MCP clients.  
**Remediation direction:** Use overflow-safe remaining-line arithmetic and impose a meaningful output bound; add maximum, near-maximum and end-of-file protocol tests.  
**Origin/history:** Unit 4, 2026-08-16; reproduced read-only against current source and independently reproduced in Stage 4.

### RWMCP3-010 — `renameFile=true` cannot complete a durable same-document path transition

**Status:** Complete — remediated and independently reviewed on 2026-08-21
**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Refactorings/RenameSymbolTool.cs:30-48`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceMutationCandidateValidator.cs:61-83`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitPlanner.cs:99-109,152-209`

**Scenario:** Renaming a type with `renameFile=true` produces a same-document-ID text and path change. Staging accepts it, but commit planning treats the new path as an existing replacement and creates no old-path deletion or move, so durable commit cannot realise the advertised rename.

**Evidence:** Unit tests establish the candidate name change, while integration, acceptance and scenario mutation paths use `renameFile=false`. Static tracing finds no same-ID move entry.

**Affected:** Plugins.Core rename, Host/plugin mutation adaptation and Workspace commit/reload.  
**Remediation direction:** Represent and validate a durable move/delete-plus-create, or reject file rename until supported; cover complete stage/commit/reload including explicit compile items.  
**Origin/history:** Unit 4, 2026-08-16; complete static cross-boundary trace. Remediated on 2026-08-21 with validated same-directory path relocation, sibling target-framework propagation, durable delete-plus-create planning, separate original/intended Unix permission state and conflict-preserving recovery. Workspace unit tests passed 1,138/1,138, Plugins.Core unit tests passed 317/317, Workspace integration tests passed 111/111 and Plugins.Core integration tests passed 12/12; affected latest-all analyser builds were clean and the final fresh independent review found no defects.

### RWMCP3-011 — Format range silently ignores its document binding

**Status:** Validated  
**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Abstractions/Workspace/Selectors/TextSpanSelector.cs:8-25`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/FormatDocumentRequest.cs:8-16`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Refactorings/FormatDocumentTool.cs:13-45`

**Scenario:** A request binds its top-level selector to document A and its range selector to document B. Both validate, but the handler resolves only A and applies the numeric range to A, silently reinterpreting a caller-supplied selector.

**Evidence:** Direct binder-to-handler trace; current tests cover missing and invalid numeric ranges but not mismatched document bindings.

**Affected:** Abstractions selector contract, Plugins.Core format mutation, Host schema/binder and clients.  
**Remediation direction:** Use a documentless nested span contract or require both selectors to resolve to the same document; add handler and protocol mismatch tests.  
**Origin/history:** Unit 4, 2026-08-16; current contract/consumer trace; independently validated in Stage 4.

### RWMCP3-012 — Sibling Code Fix roots can become permanently ambiguous during replay

**Status:** Complete — remediated and independently reviewed on 2026-08-23

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionDiscoveryService.cs:212-249`; `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Replay/CodeActionResolver.cs:186-214`

**Scenario:** A provider registers two sibling Code Fix roots with the same title, equivalence key and diagnostic identity but different operations. Both are flattened with root path `[0]`, so their replay recipes collide and a just-published action later resolves as ambiguous.

**Evidence:** Code Fix roots use a constant path while refactoring roots use their registration index. Existing controlled-provider coverage does not create colliding sibling Code Fix roots.

**Affected:** Code Action discovery, opaque replay and Host staging.  
**Remediation direction:** Include the actual root registration index in the recipe and add controlled provider discovery-to-replay coverage.  
**Origin/history:** Unit 5, 2026-08-16; current discovery/replay trace; independently validated in Stage 4. Remediated on 2026-08-23 by preserving context-local Code Fix root registration indices through discovery, replay and the built-in compatibility audit. Controlled sibling-root integration coverage proves independently replayable recipes. Grouped unit tests passed 310/310, integration tests passed 25/25, the compatibility audit passed 120/120 and the final fresh independent review found no defects after prior review corrections.

### RWMCP3-013 — One throwing Code Action provider aborts discovery for all providers

**Status:** Complete — remediated and independently reviewed on 2026-08-23

**Severity:** P2  
**Confidence:** Medium-high  
**Location:** `src/Roslyn.Workbench.Mcp.CodeActions/Tools/ListCodeActionsTool.cs:120-130,192-213`; `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionDiscoveryService.cs:145-160,173-224,349-364`

**Scenario:** One Code Fix or refactoring provider throws while computing actions, retrieving fixable diagnostic IDs or registering fixes. The exception escapes the provider loop, aborts the whole request and hides actions from unaffected providers.

**Evidence:** Direct provider-to-tool trace. Unit 6 independently confirmed the Host maps the exception to one correlated whole-call failure and provides no partial preservation. Existing tests do not prove isolation with a second successful provider.

**Affected:** Code Action discovery, Host adapter/error boundary and MCP clients.  
**Remediation direction:** Add a cancellation-preserving per-provider fault boundary, discard failed-provider partial results, continue unaffected providers and surface bounded diagnostics; add controlled throwing-provider coverage.  
**Origin/history:** Unit 5, 2026-08-16; corroborated independently by Unit 6 consumer trace and validated in Stage 4. Remediated on 2026-08-23 with typed, cancellation-preserving provider invocation boundaries around metadata, registration, Fix All and action projection. Aggregate discovery retains healthy-provider results and emits bounded structured warnings; replay retains references and returns retry guidance for transient provider failures while invalidating references for missing providers. The grouped validation and independent review evidence is recorded under RWMCP3-012.

### RWMCP3-014 — Undeclared top-level request members are silently discarded before optional defaults

**Status:** Validated  
**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp/Protocol/ToolRequestBinder.cs:20-81,361-370`; `src/Roslyn.Workbench.Mcp.Workspace/Selection/WorkspaceSelectorService.cs:20-31`

**Scenario:** With one Workspace open, a caller misspells a top-level `workspace` member on a destructive lifecycle or transaction tool. Default System.Text.Json handling drops it, binding a null selector that the Workspace selector interprets as the sole loaded Workspace.

**Evidence:** Binder serializer options do not disallow unmapped members; validation sees only the materialised object. Only nested `WorkspaceMsBuildProperties` explicitly disallows unknown members. Current tests cover unknown nested properties but not unknown top-level arguments.

**Affected:** Host protocol and every published request; Workspace lifecycle and transaction operations make the mismatch materially unsafe.  
**Remediation direction:** Reject undeclared request members during binding except for intentionally extensible contracts; add one-Workspace protocol tests for misspelled selectors and unknown options.  
**Origin/history:** Unit 6, 2026-08-16; current binder/schema/selector trace; independently validated in Stage 4.

### RWMCP3-015 — Server-owned lifecycle failures can lose authoritative Workspace attribution

**Status:** Complete — remediated and independently reviewed on 2026-08-21

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp/Tools/ServerOwnedToolBase.cs:41-64`; `src/Roslyn.Workbench.Mcp/ToolExecution/UnhandledToolExceptionFilter.cs:64-81,99-120`; `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:225-265`; `src/Roslyn.Workbench.Mcp/ErrorReporting/Capture/ErrorCaptureService.cs:50-116`

**Scenario:** `workspace-close` removes a resolved session, then cleanup throws. The server-owned path carries no authoritative context, and fallback rebinding cannot find the removed session, so captured diagnostics and Workspace-scoped consent lose the affected Workspace identity.

**Evidence:** Direct lifecycle-to-filter-to-capture trace. Plugin and Code Action adapters explicitly wrap post-acquisition exceptions; server-owned tools do not. Existing attribution tests do not exercise a real server-owned failure after state removal.

**Affected:** Host tools, Workspace lifecycle, correlated diagnostics and reporting consent/availability.  
**Remediation direction:** Retain immutable Workspace context after server-owned target resolution and attach it to unexpected failures; cover cleanup failure through the protocol boundary.  
**Origin/history:** Unit 6, 2026-08-16; independently corroborated by Unit 7 at the reporting/consent boundary and validated in Stage 4. Remediated on 2026-08-21 by capturing immutable Workspace failure context before close/reload state transitions and translating the original failure into the shared Host attribution boundary without coupling operation-result metadata to error-reporting data. Host unit tests passed 516/516, Host integration tests passed 90/90, affected `latest-all` analyser builds were clean and the final independent staged review found no defects.

### RWMCP3-016 — Baseline untracked files can be mutated without restoration

**Status:** Validated  
**Severity:** P2  
**Confidence:** High  
**Location:** `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Repositories/RepositoryRestorer.cs:33-61`; repository checkout validation using `git status --untracked-files=no`

**Scenario:** Preparation leaves an untracked source loaded by MSBuild. A scenario mutates or deletes it, but restoration records only baseline path membership, so no change is restored and later exact-commit runs reuse contaminated cache state.

**Evidence:** Cache validation ignores untracked files; restorer creation/capture/verification compare untracked pathname sets rather than content or identity. ScenarioRunner has no tests for this boundary.

**Affected:** ScenarioRunner preparation/restoration and trustworthiness of repeated external-repository evidence.  
**Remediation direction:** Preserve baseline untracked content/identity, reject mutable untracked inputs or use disposable per-run worktrees.  
**Origin/history:** Unit 8, 2026-08-16; current operational call-path trace; independently validated in Stage 4.

### RWMCP3-017 — Scenario failures can discard the evidence needed to diagnose them

**Status:** Validated  
**Severity:** P2  
**Confidence:** High  
**Location:** `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:267-278`

**Scenario:** A warm-up, measured workload, Host, EventPipe, restoration or validation step fails before the family writer. The outer catch records only an exception message and transient execution evidence can be deleted.

**Evidence:** Family result production occurs only after successful iteration completion; the outer failure path does not persist structured partial results, stderr, validation outcomes or artifact paths.

**Affected:** ScenarioRunner release evidence and diagnosis of failed crash/cancellation/conflict/profile runs.  
**Remediation direction:** Persist a structured failure report before cleanup, including invocation, completed measurements, full exception, Host stderr, cleanup/validation and artifact paths.  
**Origin/history:** Unit 8, 2026-08-16; current application-flow trace; independently validated in Stage 4.

### RWMCP3-018 — Unknown ScenarioRunner options silently run a different workload

**Status:** Validated  
**Severity:** P2  
**Confidence:** High  
**Location:** `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioOptions.cs:56-97`

**Scenario:** A release command uses `--iteratons 20` or another misspelled option. Parsing accepts and ignores it, so the run succeeds using defaults and produces evidence for a different workload.

**Evidence:** Unknown name/value pairs are stored without validation and unused keys are never rejected. The runner has no parser test project.

**Affected:** ScenarioRunner configuration and credibility of manual release evidence.  
**Remediation direction:** Validate option names and command applicability, reject ambiguous duplicates and add parser coverage.  
**Origin/history:** Unit 8, 2026-08-16; current parser/consumer trace; independently validated in Stage 4.

### RWMCP3-019 — Manifest construction recursively traverses excluded and overlapping directory trees

**Status:** Validated  
**Severity:** P2  
**Confidence:** Medium-high  
**Location:** `src/Roslyn.Workbench.Mcp.Workspace/ChangeDetection/WorkspaceChangeDetector.cs:180-215,277-303`

**Scenario:** A project rooted high in a large repository triggers recursive enumeration of `.git`, `node_modules`, generated/artifact and other excluded trees before each directory is filtered. Nested or overlapping project roots repeat the traversal during open, reload and post-commit manifest rebuilding, causing substantial latency and allocation.

**Evidence:** Each loaded project calls `Directory.EnumerateDirectories(projectDirectory, "*", SearchOption.AllDirectories)` before applying `WorkspaceInputPathPolicy`; traversal is neither pruned nor deduplicated and accepts no cancellation. Current tests use small mocked directory sets.

**Affected:** Workspace open/reload/commit change-manifest construction and repository-scale responsiveness.  
**Remediation direction:** Prune excluded roots during traversal, deduplicate overlapping roots and propagate cancellation; add large/overlapping-tree performance evidence.  
**Origin/history:** Repository-wide pass 12, 2026-08-16; static current-source trace; independently validated in Stage 4.

## Stage 4 validation history

On 2026-08-16 a fresh context-free reviewer independently retraced all nineteen candidates against current source, consumers and tests. No candidate was rejected or merged as a duplicate. `RWMCP3-001`–`006`, `008`–`009` and `011`–`019` validated at their recorded severity and confidence. `RWMCP3-007` validated but was revised from P1 to P2 at Medium confidence because the pathname TOCTOU is real while native reproduction and a higher-impact threat model remain unavailable. `RWMCP3-010` validated at P2 and was revised from Medium to High confidence because the commit planner decisively cannot represent a same-document-ID path move. Narrow current-Host calls independently reproduced `RWMCP3-008` and `RWMCP3-009`; all other verdicts used complete current-source and test-boundary traces without broad suite execution.
