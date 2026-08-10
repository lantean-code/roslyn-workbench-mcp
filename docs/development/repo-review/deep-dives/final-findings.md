# Deep-dive validated findings

## Final repository assessment

All seven implementation-depth review units and all fourteen repository-wide validation passes are complete. Public contract, Workspace, plugin, Code Action, Host and test dependency direction, ordinary session/transaction gating, revision history, path containment, durable commit ordering, non-cancellable application/recovery boundaries, cross-process locking, plugin cache isolation, normal package composition, exact Code Action replay, source-only staging, startup prerequisite ordering, Host DI composition, local/external error-model separation, ordinary sequential consent handling and the automated unit/component/published-Host test layering are coherent. Twenty-nine actionable defects survived final independent current-source validation: two P1, twenty-five P2 and two P3. RWMCP-015 and RWMCP-024 remain rejected, and no new repository-wide candidate was substantiated. The older RWMCP-001 through RWMCP-007 findings are resolved against current source. No P0 issue was found.

Remediation status is tracked in the implementation table below. RWMCP-014 has been completed and revalidated for the agreed multi-target project-context scope; retaining a document from a different project file after deletion of its linked physical source is an accepted behaviour that remains visible until project inspection or reload. RWMCP-031 has also been completed and revalidated: preparation now creates the actual strongly typed Sentry event and exact reviewed JSON, SDK enrichment is reduced, and a final allow-list removes unreviewed fields before the envelope is queued. RWMCP-009 is complete and revalidated: workspace identity is now globally unique, strongly typed as `Guid` across contracts and clients, and recursively rejects an empty identifier at the Host request boundary. RWMCP-027 is complete and revalidated: recursive request validation now enforces selector invariants before resolution, and the same public validation metadata publishes equivalent input-schema constraints while respecting deserialised defaults. RWMCP-008 is complete and revalidated: open, reload and commit promotion now certify Workspace inputs across Solution creation or commit application and manifest capture, including commit-owned watcher filtering and applied-state revalidation. RWMCP-013 is complete and revalidated: successful reload now republishes the replacement session's `Ready` advisory state and clears transaction and commit metadata before returning. RWMCP-011 is complete and revalidated: batch target-framework evaluation remains synchronous but now propagates cancellation through its public contract and observes it between physical MSBuild project evaluations. RWMCP-016 is complete and revalidated: replacement and recovery preserve exact Unix permissions through the version-1 recovery manifest, Windows replacement uses the metadata-preserving platform primitive, and permission drift is treated as a commit or recovery conflict. RWMCP-018 is complete under the accepted operational design: measured representative repository commits did not justify a production aggregate cap or disk-backed planning, permanent scenario measurements retain visibility, and version-specific agent guidance directs callers towards small coherent transactions. RWMCP-017 is complete and revalidated: recovery-capacity limits are checked before persistence, reported through an explicit result and translated into a stable structured commit rejection while leaving the transaction recoverable. RWMCP-028 is complete and revalidated: Full output schemas now require nullable data for server-owned and query success envelopes while retaining non-null mutation data, matching the existing no-change serializers without introducing another schema dependency. RWMCP-021 is complete and revalidated: plugin discovery and schema preflight now run through DI-owned startup services, unsupported transport contracts disable only their owning plugin, and published plugin calls use prebuilt strongly typed adapters without request-path reflection. RWMCP-020 is complete and revalidated: one shared policy now enforces protocol-compatible final tool names in both source diagnostics and runtime plugin preparation, with suppressed-analyser published-plugin coverage proving deterministic isolation. RWMCP-022 is closed as accepted plugin-author responsibility: synchronous configuration is an explicit contract enforced by RWMCP003, while trusted in-process plugins are not partially sandboxed through runtime inspection of this single misuse. RWMCP-030 is complete and revalidated: ordinary managed console output is redirected to stderr before any extension construction while the MCP SDK retains the raw stdout stream, and a published plugin proves protocol continuity across startup and invocation output. The remaining P2/P3 set contains important correctness, compatibility, cancellation, resource and operational issues but does not invalidate the otherwise coherent architecture.

## Suggested implementation order

Treat each row as an individual implementation work step. Complete and revalidate the findings in the listed internal order before starting a dependent step. Steps 1A and 1B were independent P1 tracks and are now complete. Other steps may be parallelised only when they do not touch the same contracts or tests, but the sequence below minimises rework from foundational contract and persistence changes.

| Step | Status | Group | Findings in implementation order | Dependency rationale and completion gate |
| --- | --- | --- | --- | --- |
| 1A | Completed | Linked-source transaction safety | RWMCP-014 → RWMCP-019 | Completed and revalidated. Candidate processing normalises removals across project contexts sharing the same canonical project-file path, deliberately retains documents belonging to different project files, and accepts safe adjacent linked-document changes while rejecting genuine overlaps and ambiguous insertions. |
| 1B | Completed | Sentry outbound allow-list | RWMCP-031 | Completed and revalidated. Preparation stores the actual typed Sentry event and derives `payloadJson` from it; reduced SDK enrichment and a final defensive allow-list make the queued event match the reviewed diagnostic JSON. Real-client envelope coverage proves the outbound diagnostic categories, and the affected unit, integration and acceptance suites pass. |
| 2 | Completed | Snapshot and selector contract foundations | RWMCP-009 → RWMCP-027 → RWMCP-012 → RWMCP-010 | Completed and revalidated. Workspace identity is globally unique and strongly typed, recursive request validation rejects invalid nested selectors, malformed paths produce structured failures through a shared non-throwing normaliser and workspace-bound path service, and solution-hierarchy membership uses the canonical workspace-relative identity and filesystem comparer. |
| 3 | Completed | Workspace certification and lifecycle | RWMCP-008 → RWMCP-013 → RWMCP-011 | Completed and revalidated. Open, reload and commit promotion certify the complete Solution/application-to-manifest interval; commit-owned filesystem events are filtered without hiding unrelated changes; applied commit state is revalidated; successful reload republishes `Ready` with transaction and commit metadata cleared; and synchronous batch target-framework evaluation observes cancellation between physical projects. The completion gate is satisfied: inconsistent Ready snapshots are rejected, reload clears external advisory state and cancellation releases large-solution query leases at project boundaries. |
| 4 | Completed | Recovery payload and persistence limits | RWMCP-016 → RWMCP-018 → RWMCP-017 | Completed and revalidated. Source metadata is persisted and restored, aggregate planning memory has measured operational guidance, and every supported recovery-capacity rejection is structured before persistence while the transaction remains recoverable. |
| 5 | Completed | Protocol schema pipeline | RWMCP-028 → RWMCP-021 | Completed and revalidated. Full output schemas match supported success and no-change payloads; authoritative request and configured-response schemas are preflighted during plugin admission; unsupported contracts disable only their owning plugin; and plugin execution uses prebuilt strongly typed adapters without request-path reflection. |
| 6 | Completed | Plugin catalogue admission | RWMCP-020 → RWMCP-022 | RWMCP-020 is complete and revalidated: merged tool names are checked before collision admission by the same policy used by RWMCP022 source diagnostics, and a suppressed-analyser packaged plugin is disabled without affecting the valid catalogue. RWMCP-022 is closed as accepted plugin-author responsibility because `Configure` is explicitly synchronous, RWMCP003 rejects `async void`, and trusted in-process plugin code is not runtime-sandboxed against isolated contract violations. |
| 7 | Completed | Plugin stdio containment | RWMCP-030 | Completed and revalidated. `Console.Out` is redirected to stderr before Host or extension construction, while the pinned MCP SDK opens the raw stdout stream independently for protocol traffic. A packaged plugin writes during entry-point construction, configuration, handler construction and invocation without corrupting initialisation, its result or a subsequent MCP call; all markers are captured on stderr. Direct raw-handle or native output remains outside this cooperative trusted-plugin guard. |
| 8 | Not started | Plugin metadata inspection resources | RWMCP-023 | This is independent of catalogue semantics but should follow their admission changes to avoid overlapping discovery edits. Gate: large managed and native DLLs are inspected through bounded streaming without complete-file managed allocations. |
| 9 | Not started | Code Action reference semantics | RWMCP-025 → RWMCP-029 | Make reference-cache admission failure truthful before changing retry metadata, so clients receive a stable capacity outcome even before they honour the corrected non-idempotent annotation. Gate: saturated/oversized recipes cannot produce false empty success, and repeated reference-producing queries are not advertised as idempotent. |
| 10 | Not started | Fix All diagnostic scaling | RWMCP-026 | Independent after reference-result semantics are stable. Gate: compiler and analyser diagnostics are computed once per project per Fix All execution and then partitioned for document/project callbacks, with repository-scale measurement or a representative large fixture. |
| 11 | Not started | Error-report authorisation and retention | RWMCP-033 → RWMCP-032 | Build atomic consent/acquisition after the P1 outbound shape is fixed, then add timer-driven store cleanup around the final submission state model. Gate: close/suppression invalidation cannot race into dispatch, and sensitive captured/prepared records are physically released at expiry without another request. |
| 12 | Not started | Scenario-runner evidence integrity | RWMCP-038 → RWMCP-034 → RWMCP-036 → RWMCP-035 → RWMCP-037 | Acquire a per-checkout lease before changing checkout certification; then validate preparation effects while holding it. Persist partial evidence before repairing trace and cancellation commands so later failures remain diagnosable. Finally correct EventPipe commit tracing and enforce cancellation outcomes. Gate: hermetic tests cover contention, preparation side effects, partial failure, trace lifecycle and ignored cancellation before external repositories are used for release evidence. |
| 13 | Blocked | Final repository and release revalidation | All remediated identifiers | Rerun the affected unit/component suites after every step, then run the full non-acceptance gate. Before the v1 release gate, explicitly run the authorised acceptance, Code Action audit and representative external-repository scenarios, recheck cross-project contracts and remove an identifier from the active worklist only after current-source validation. |

If a work step reveals that its prerequisite contract must change again, reopen every later completed step that consumes that contract. In particular, changes from steps 2, 4 and 5 require revalidation of Host adapters, plugin package consumers, Code Action replay and transaction recovery as applicable.

## Validated findings

### RWMCP-014 — Reconcile linked-document deletions before deleting the physical file

- Severity: P1
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitPlanner.cs:135-142`; candidate-normalisation gap at `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceMutationCandidateProcessor.cs:33-41`; inconsistent promotion at `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs:333-365`
- Concrete failure scenario: A mutation removes a linked file from one project context but leaves the same physical path in another. Commit schedules and applies a physical delete, then promotes a Ready Solution that still contains the surviving document.
- Supporting call path or evidence: mutation candidate → addition propagation/text merge, neither of which reconciles removals → planner schedules every removed document path without checking current-Solution path ownership → writer moves the target to a delete marker → committed session retains `transaction.CurrentSolution`.
- Affected projects or subsystems: Workspace mutation processing, commit planning and promotion; plugin, Code Action and Host transaction consumers.
- Remediation direction: Propagate or reject asymmetric linked deletion and make planning refuse to delete a path still referenced by any current document.
- Resolution: Completed for the agreed high-probability multi-target scenario. Removed documents are propagated across Roslyn project contexts that share the same canonical project-file path. Documents from different project files are deliberately retained because Roslyn does not expose authoritative MSBuild link ownership; agents can observe and repair a remaining explicit link through project inspection or workspace reload.

### RWMCP-031 — Enforce the reviewed allow-list after Sentry SDK enrichment

- Severity: P1
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp/Hosting/RoslynWorkbenchServiceCollectionExtensions.cs:269-285`; event hand-off at `src/Roslyn.Workbench.Mcp/ErrorReporting/Dispatch/SentryErrorReportDispatcher.cs:90-92,110-127`; pinned dependency at `src/Directory.Packages.props:23`
- Concrete failure scenario: A user approves the returned minimal Sentry preview and digest, but the real SDK adds a current stack trace, loaded assembly names/versions and runtime contexts before queueing. Source paths and private plugin/company assembly identifiers can therefore reach Sentry despite being absent from the preview and explicitly listed as excluded.
- Supporting call path or evidence: prepare/digest minimal preview → submit constructs minimal `SentryEvent` → real Sentry 6.8.0 client applies option-owned processors → default `MainSentryEventProcessor` adds stack/thread, every loaded non-dynamic assembly, culture/time-zone, memory/thread-pool and runtime/device context. Host leaves automatic stack and assembly reporting enabled and has no final outbound allow-list. Mock dispatcher tests inspect the event before these processors run.
- Affected projects or subsystems: Host error-reporting trust boundary, Sentry-enabled builds, report review/consent semantics and provider data governance.
- Remediation direction: Disable SDK diagnostic enrichment that is outside the preview and apply a final outbound allow-list/validation before enqueueing; contract-test the serialised envelope produced by a real client against the reviewed permitted content.
- Resolution: Completed. Preparation creates and retains the actual strongly typed `SentryEvent`, assigns its identity and timestamp before review, and derives `payloadJson` directly from that event. Submission passes a defensive copy through the real SDK with automatic stack and assembly enrichment disabled, then applies a final allow-list after SDK processing so threads, stacks, modules, runtime/device contexts and all other unreviewed fields are removed before queue admission. Real-client envelope tests verify that the outbound diagnostic event matches the reviewed JSON, with unit, Host integration and full acceptance coverage passing.

### RWMCP-008 — Detect inputs changed between Solution creation and manifest capture

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:81-112`; reload equivalent at `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:313-329`; commit equivalent at `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs:248-263,333-340`
- Concrete failure scenario: A build input changes after MSBuild evaluates it but before manifest capture, or an applied source file changes externally before post-commit manifest capture. The session is published Ready with a Solution and manifest describing different disk states, so later validation can report no change.
- Supporting call path or evidence: open/reload load a Solution before `BuildManifest`; commit applies files before `CreateCommittedSession` calls `BuildManifest(transaction.CurrentSolution)`. No observer certifies the complete interval.
- Affected projects or subsystems: Workspace lifecycle/loading/change detection, commit promotion and every query or mutation consumer.
- Remediation direction: Detect changes across each complete Solution-creation/application and manifest-capture interval and retry or reject inconsistent publication.
- Resolution: Completed. Input certification begins before Solution loading or commit application and buffers filesystem events until the replacement manifest is available. Open and reload reject inconsistent publication, while commit promotion filters only exact commit-owned paths and atomic temporary files, revalidates the applied target state around manifest certification, and restores the transaction on conflicts. Workspace unit and integration suites, latest-all analyzer builds, the full solution build and the EF Core watcher-stress scenario pass.

### RWMCP-009 — Prevent snapshot identity reuse after server restart

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSessionStore.cs:44-64`
- Concrete failure scenario: A retained snapshot from the first workspace in one process matches a different first workspace and revision after restart because all public identity components repeat.
- Supporting call path or evidence: deterministic per-process counters → public `SnapshotPrecondition` → `WorkspaceResolver.ValidateSnapshot`/`SnapshotGuard.Validate` compare only ID, epoch and revision.
- Affected projects or subsystems: Abstractions snapshot contracts, Workspace resolution and transaction guards, plugin and Code Action mutations.
- Remediation direction: Add an unpredictable process generation or globally unique workspace identity and document restart expiry.
- Resolution: Completed. Workspace IDs are generated as globally unique GUIDs and represented as `Guid`/`Guid?` throughout Abstractions, Workspace, Host, plugin, Code Action, acceptance and scenario-runner flows. Snapshot validation now prevents identity reuse across independently composed server instances, and the recursive Host request-validation pipeline rejects `Guid.Empty` in nested selectors and snapshot preconditions. Unit, integration, the complete published-Host acceptance suite and representative scenarios from every affected runner family pass.

### RWMCP-010 — Use one canonical path identity for solution-folder membership

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Workspace/Projects/ProjectStructureService.cs:131-134`
- Concrete failure scenario: A solution below the workspace root stores hierarchy keys relative to the solution directory, while Plugins.Core looks them up relative to the workspace root, silently losing solution-folder membership. Case-only differences also fail under the ordinal dictionary.
- Supporting call path or evidence: solution serializer → `SolutionHierarchyResult.ProjectFolderPaths` → `GetSolutionStructureTool` → `IWorkspaceResolver.NormalizeProjectPath` → failed dictionary lookup.
- Affected projects or subsystems: Abstractions project contracts, Workspace project services and Plugins.Core structure projection.
- Remediation direction: Use canonical absolute paths and the Workspace path comparer internally.
- Resolution: Completed. Project hierarchy keys and Plugins.Core lookups now use the same canonical workspace-relative path identity and platform-appropriate comparer. Path normalisation is exposed independently through the workspace-bound `IWorkspacePathService`, while `IWorkspaceResolver` remains responsible for selector resolution. Unit, integration and Host composition coverage pass.

### RWMCP-011 — Observe cancellation during batch target-framework evaluation

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Workspace/Projects/ProjectTargetFrameworkResolver.cs:106-111`
- Concrete failure scenario: A cancelled cold solution query continues evaluating every uncached project, retaining its lease and making exclusive Workspace operations busy until completion.
- Supporting call path or evidence: target-framework resolver invokes one synchronous batch; `ProjectStructureService` loops without a token; cancellation is checked only after return.
- Affected projects or subsystems: Abstractions/Workspace project services, Workspace leases and Plugins.Core queries.
- Remediation direction: Check cancellation between physical project evaluations and document or improve individual MSBuild evaluation granularity.
- Resolution: Completed. The public synchronous batch target-framework contract now accepts a `CancellationToken`, which the snapshot-scoped resolver forwards unchanged. The Workspace implementation checks cancellation before each project, around each distinct physical MSBuild evaluation and before returning, while retaining request-scoped `ProjectCollection` reuse and unconditional unloading. Individual synchronous MSBuild evaluation remains the documented cancellation granularity. Workspace unit and integration suites, latest-all analyzer builds and the full solution build pass.

### RWMCP-012 — Convert malformed public paths into structured contract failures

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Workspace/Loading/WorkspaceRootResolver.cs:26-34`
- Concrete failure scenario: A rooted caller path containing a null character throws from `Path.GetFullPath` and becomes a generic internal tool failure rather than the request's structured invalid-path or not-found result.
- Supporting call path or evidence: MCP binding → root/Workspace/project/document selector → uncaught path normalisation; selector shape validation does not validate path characters, while the adjacent open-path normaliser already catches `ArgumentException`.
- Affected projects or subsystems: Abstractions selectors, Workspace loading/selection/resolution and all consumers.
- Remediation direction: Centralise non-throwing path normalisation and map failure to the owning public error.
- Resolution: Completed. Workspace loading, root selection, selector resolution and project hierarchy code now use shared non-throwing path normalisation. Invalid public paths are converted into structured selector or workspace failures, and the public context-scoped `IWorkspacePathService` returns an explicit Try-pattern outcome without exposing arbitrary-root normalisation. Unit, integration and Host composition coverage pass.

### RWMCP-016 — Preserve source-file permissions across replacement and recovery

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Workspace/IO/AtomicFileWriter.cs:73-93`; source use at `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitWriter.cs:67-76,180-189`
- Concrete failure scenario: Replacing a Unix source file with mode `0444`, group-write permission or an executable bit moves a newly created default-mode temporary file over it, silently changing the repository mode; recovery cannot restore the original metadata because it persists bytes only.
- Supporting call path or evidence: apply/restore → default atomic write → `FileMode.CreateNew`, with explicit Unix mode only for owner-only state files → atomic move over destination. Default replacement integration tests assert content only.
- Affected projects or subsystems: Workspace atomic I/O, commit and recovery on Unix; analogous Windows metadata needs platform validation.
- Remediation direction: Preserve destination metadata on replacement, persist recovery metadata and add platform-specific integration assertions.
- Resolution: Completed. The version-1 recovery manifest records the original Unix mode for replacements, planning and applied-state validation reject permission drift, and apply or recovery sets the exact mode on the private temporary file before its final durability flush and atomic rename. Windows replacement uses `ReplaceFileW` so destination metadata is retained without serialising ACLs. Unit and real-filesystem integration coverage exercises manifest validation, commit, recovery and permission conflicts; a published-Host acceptance test commits a rename and verifies exact Unix permissions and clean recovery state. Workspace unit and integration suites, latest-all analyzer builds, the full solution build and the complete 59-test published-Host acceptance wrapper pass.

### RWMCP-017 — Translate recovery size limits at the commit boundary

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Workspace/Recovery/CommitRecoveryStore.cs:52-57,695-714`; uncaught boundary at `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs:275-290`
- Concrete failure scenario: A staged file over the 128 MiB artifact limit or a plan over the 16 MiB manifest limit throws expected `InvalidDataException` as an unexpected correlated MCP error instead of a structured commit-preparation rejection.
- Supporting call path or evidence: transaction commit → plan persistence → size validation/serialisation → `InvalidDataException`; commit translates only IO and authorisation exceptions.
- Affected projects or subsystems: Workspace commit and recovery persistence, Host error mapping.
- Remediation direction: Validate size during planning or translate the store limit to a stable structured commit error while retaining the transaction.
- Resolution: Completed. Recovery owner, manifest and artifact capacity are checked before the store creates directories or writes data. `PersistPlanAsync` returns an invariant-bearing persistence result, and the commit service maps capacity exhaustion to the stable `CommitRecoveryCapacityReached` rejection with `RollbackTransaction` guidance while clearing advisory staging state and retaining the unchanged transaction. Unit tests cover each supported limit and verify that no persistence, application, recovery or session replacement occurs; Workspace unit/integration, Host unit/integration and the complete 59-test published-Host acceptance suite pass.

### RWMCP-019 — Allow adjacent linked-document text changes to merge

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Workspace/Transactions/LinkedDocumentChangeMerger.cs:117-134`
- Concrete failure scenario: Independent linked-document replacements touch adjacent spans. Roslyn can apply both because they do not overlap, but the merger rejects them when one end equals the other start.
- Supporting call path or evidence: candidate processing → sorted linked changes → `TextSpan.IntersectsWith` returns true for coincident boundaries → `LinkedDocumentConflict`; Microsoft Learn documents that boundary-inclusive behaviour and existing tests omit adjacency.
- Affected projects or subsystems: Workspace linked reconciliation and plugin/Code Action mutation staging.
- Remediation direction: Permit adjacent non-empty spans while retaining explicit conflict handling for competing insertions and ambiguous insertion boundaries.
- Resolution: Completed. Adjacent non-empty replacements now merge, while genuine overlaps, competing insertions and ambiguous insertion boundaries remain conflicts.

### RWMCP-020 — Reject protocol-invalid plugin tool names during preparation

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Plugins/Preparation/PluginConfigurationPreparer.cs:45-65,82-87`; direct publication at `src/Roslyn.Workbench.Mcp/Protocol/McpToolProtocolFactory.cs:101-132`
- Concrete failure scenario: A plugin declares a non-blank name containing whitespace or more than 128 characters. Host reports the plugin enabled and publishes the invalid identifier, which a conforming MCP client can reject or omit.
- Supporting call path or evidence: merged metadata → whitespace-only validation → ordinal collision checks → direct `Protocol.Tool.Name` assignment. The installed SDK contract requires `^[A-Za-z0-9_.-]{1,128}$`, but the Host bypasses its validated reflective factory.
- Affected projects or subsystems: Plugins metadata and preparation, Host MCP publication and external clients.
- Remediation direction: Validate merged names against the MCP contract before collisions and add source diagnostics for statically known invalid values.
- Resolution: Completed. A shared protocol-name policy validates final merged metadata before collision admission and powers the RWMCP022 authoring diagnostic for constant attribute and fluent names. Dynamic names remain runtime-validated, invalid plugins are disabled without sanitisation, and unit plus complete published-Host acceptance coverage proves suppressed-analyser isolation while valid tools remain published.

### RWMCP-021 — Preflight plugin transport schemas before publishing DI registrations

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Plugins/Preparation/PluginConfigurationPreparer.cs:69-78`; late schema boundary at `src/Roslyn.Workbench.Mcp/Protocol/McpSdkSchemaProvider.cs:19-86`
- Concrete failure scenario: A public request contains an unsupported `System.Type` or delegate member. The plugin is enabled and registered, then authoritative schema generation throws while resolving the complete MCP tool collection, preventing all tools from being published. Full output-schema mode exposes the same failure for unsupported response contracts.
- Supporting call path or evidence: runtime preparation checks accessibility only → catalogue tool enters DI → singleton adapter construction → SDK/System.Text.Json schema generation. Microsoft Learn identifies these types as unsupported, and no per-plugin preparation boundary invokes the authoritative provider.
- Affected projects or subsystems: Plugins preparation, Host DI/protocol publication, server tool listing and external package compatibility.
- Remediation direction: Generate and cache authoritative request/configured-response schemas inside per-plugin materialisation and disable the plugin before adding any DI registration when generation fails.
- Resolution: Completed. Plugin discovery, materialisation and adapter construction now run through DI-owned startup services. Authoritative request schemas and configured Full response schemas are generated and cached at the per-plugin admission boundary, so unsupported contracts produce sanitised diagnostics and disable only their owning plugin. The runtime catalogue is published atomically, MCP listing and fallback invocation use prebuilt strongly typed adapters, and task-augmented plugin calls retain the SDK's rejection semantics. Unit, Host integration, real client/server protocol and complete published-Host acceptance coverage pass.

### RWMCP-022 — Reject asynchronous plugin configuration at the runtime boundary

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp/PluginLoading/MefPluginComposer.cs:14-24`; immediate freeze at `src/Roslyn.Workbench.Mcp/PluginLoading/LoadedPluginPreparer.cs:23-35`
- Concrete failure scenario: A plugin suppresses RWMCP003 and implements `Configure` as `async void`. Host freezes and may enable a partial configuration when the method first suspends; its continuation can then throw outside startup containment and terminate the stdio server.
- Supporting call path or evidence: MEF export → ordinary `void Configure` call → immediate freeze/preparation. Runtime never checks asynchronous state-machine metadata even though documentation promises authoritative runtime validation when analysers are absent or suppressed.
- Affected projects or subsystems: Plugin analyser/runtime parity, Host composition, startup catalogue correctness and availability.
- Remediation direction: Reject asynchronous interface implementations before invocation and add a suppressed-analyser binary fixture proving runtime isolation.
- Resolution: Closed as accepted plugin-author responsibility. `IRoslynPlugin.Configure` is an explicitly synchronous trusted in-process extension contract, RWMCP003 rejects asynchronous implementations during normal authoring, and suppressing or bypassing that error makes resulting plugin instability the plugin author's responsibility. Runtime inspection of this single misuse would not provide meaningful containment for arbitrary in-process plugin behaviour, so no production guard or acceptance fixture is added.

### RWMCP-025 — Report reference-cache admission failure instead of returning a false action list

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.CodeActions/References/CodeActionReferenceState.cs:54-95`; projection boundary at `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionInfoFactory.cs:24-60` and `src/Roslyn.Workbench.Mcp.CodeActions/Tools/ListCodeActionsTool.cs:156-203`
- Concrete failure scenario: Repeated discovery fills the five-minute replay cache near its configured capacity, or one recipe exceeds the remaining capacity. An applicable leaf cannot be stored and `list-code-actions` returns a successful partial or empty collection whose `totalCount` also excludes the action, misleading the caller into concluding that no action exists.
- Supporting call path or evidence: discovery → recipe creation → size-limited `MemoryCache` refuses admission → `TryCreate` returns false → list projection silently skips the item and count. Microsoft Learn documents that an entry is not cached when aggregate sizes exceed `SizeLimit`; the focused unit test explicitly expects the false empty-success result, while Fix All preparation maps the same store failure to `ActionReferenceCapacityExceeded`.
- Affected projects or subsystems: CodeActions reference cache, discovery/list contract and Host MCP query responses.
- Remediation direction: Surface admission failure as a structured capacity rejection or bounded warning with truthful result metadata and actionable retry/configuration guidance.

### RWMCP-026 — Compute project Fix All diagnostics once per project

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.CodeActions/Execution/FixAll/WorkspaceFixAllDiagnosticProvider.cs:36-55`; repeated whole-compilation work at `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionDiagnosticService.cs:19-52,108-205`
- Concrete failure scenario: Project or solution Fix All on a project with hundreds or thousands of documents runs complete compiler diagnostics and all selected analysers once per source document plus once for project diagnostics. Preparation and staging both recreate the Fix All action, causing long request latency and sustained CPU/allocation pressure.
- Supporting call path or evidence: Fix All creation → Roslyn project-wide diagnostic callback → Workbench loops documents → every document call enters `GetCompilationDiagnosticsAsync`, calls `compilation.GetDiagnostics` and runs `CompilationWithAnalyzers`, then filters to one tree. Microsoft Learn defines this callback as one project-wide request and describes document-based Fix All as computing diagnostics before bucketing them by document.
- Affected projects or subsystems: CodeActions diagnostic collection and Fix All, Roslyn analyser execution, Workspace lease duration and Host responsiveness.
- Remediation direction: Compute diagnostics once per project, partition source diagnostics by document and serve document/project/all callbacks from that result while retaining cancellation and warnings.

### RWMCP-027 — Enforce selector invariants before choosing a target or scope

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp/Protocol/ToolRequestBinder.cs:20-60,283-301`; disconnected rules at `src/Roslyn.Workbench.Mcp.Workspace/Selectors/WorkspaceContractValidator.cs:5-40,49-120`; precedence/fallback at `src/Roslyn.Workbench.Mcp.Workspace/Resolution/WorkspaceResolver.cs:215-268` and `src/Roslyn.Workbench.Mcp.Plugins/Resolution/ToolRequestResolver.cs:59-144`
- Concrete failure scenario: A mutation supplies a document selector with a valid document ID and a different path. Both fields bind, and resolution returns the ID target while silently ignoring the path, so the mutation can be staged against a different document from the one the caller named. A contradictory Solution scope similarly ignores a supplied project, and numeric nested enum value 999 binds successfully and falls through to an empty project set.
- Supporting call path or evidence: Host binding validates top-level request members only. The Workspace validator defines selector exact-one, at-least-one and kind/member invariants but has no production caller. Resolvers then use precedence and catch-all branching. A current-assembly probe confirmed undefined nested enum admission, while tests exercise binder and validator separately.
- Affected projects or subsystems: Abstractions selectors, Host binding, Workspace resolution, plugin/Code Action adapters and server-owned lifecycle/transaction tools.
- Remediation direction: Perform recursive selector and enum validation at Host binding and reject contradictory nested contracts before target resolution; publish equivalent conditional/exclusive schema rules where representable.
- Resolution: Completed. Public validation attributes in Abstractions now define selector member-group, conditional, non-empty GUID and enum invariants. The Host recursively validates every bound request object before dispatch, so nested plugin contracts receive the same DataAnnotations and selector enforcement before any resolver can apply precedence. The authoritative input-schema transformer derives equivalent JSON Schema constraints from those attributes while leaving output/value schemas unchanged; omission semantics account for CLR and declared defaults so published schemas remain aligned with deserialised runtime validation. Focused transformer coverage is 100% line and branch, and the complete Host unit/contract, Host integration and published-Host acceptance suites pass.

### RWMCP-028 — Allow null query data in Full output schemas

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp/Protocol/McpSdkSchemaProvider.cs:51-86,100-103`; `src/Roslyn.Workbench.Mcp/Protocol/ToolSchemaFactory.cs:30-54`; runtime serialization at `src/Roslyn.Workbench.Mcp/Protocol/McpPublishedResultSerializer.cs:23-32,62-71` and `src/Roslyn.Workbench.Mcp/Contracts/Results/ToolResultEnvelopeSerializer.cs:19-37`
- Concrete failure scenario: With `--tool-output-schema-mode Full`, a plugin or Code Action query returns its supported no-change outcome. Runtime publishes `{ok:true,data:null}`, but the advertised success schema requires an object, so a schema-validating MCP client rejects the successful result.
- Supporting call path or evidence: schema normalisation collapses an exported object-or-null response to object and embeds it as required `data`; both query serializers deliberately write null for absent data. Focused adapter tests require successful null data, and a real current-provider probe confirmed the non-null object schema. The installed MCP SDK states that output schema describes structured content.
- Affected projects or subsystems: Host schema/result protocol, Plugins and Code Actions query adapters and Full-schema clients.
- Remediation direction: Make query success data nullable in generated schemas or replace null no-change responses with a consistent non-null response object, then validate representative runtime payloads against every Full schema.
- Resolution: Completed. Envelope composition now makes required success `data` nullable for server-owned/direct tools and plugin or Code Action queries while retaining the declared response schema and leaving mutation data non-null. Existing no-change serializers continue to publish `{ok:true,data:null}`. Focused unit and real-provider integration tests cover nullable object and scalar data, retained response properties and non-null mutation schemas; the complete Host unit/integration and 59-test published-Host acceptance suites pass with no additional schema package.

### RWMCP-030 — Isolate extension Console output from the stdio protocol stream

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp/Program.cs:5-11`; `src/Roslyn.Workbench.Mcp/Hosting/RoslynWorkbenchHostApplicationBuilderExtensions.cs:8-17,36-51`; extension execution at `src/Roslyn.Workbench.Mcp/PluginLoading/MefPluginComposer.cs:8-24` and `src/Roslyn.Workbench.Mcp/ToolExecution/Plugins/PluginQueryMcpServerTool.cs:50-62`
- Concrete failure scenario: A valid plugin uses `Console.WriteLine` during construction/configuration or handler execution. Its text is emitted before or between JSON-RPC frames on stdout, causing client initialisation or subsequent protocol framing to fail and making the complete Host unusable.
- Supporting call path or evidence: plugin code runs in-process before transport startup and during calls. Host logger providers correctly target stderr, but no path reserves raw stdout, redirects ordinary `Console.Out`, exposes a plugin logging abstraction or documents a restriction. A clean Host run emitted zero stdout and all logs to stderr, isolating the missing containment to extension-controlled output; current fixtures never write to stdout.
- Affected projects or subsystems: Executable Host, MCP stdio transport, plugin composition/execution, authoring guidance and all clients using an output-writing extension.
- Remediation direction: Own the raw stdout transport before executing extensions, route ordinary console output to a safe stderr/logger sink, document/expose supported plugin logging and add a real external fixture that writes during startup and invocation without breaking protocol traffic.
- Resolution: Completed. Process startup redirects ordinary `Console.Out` writes to stderr before any Host or extension construction, while the MCP SDK continues to own raw stdout through `Console.OpenStandardOutput`. Plugin guidance documents the cooperative trusted-code boundary. A complete published-Host acceptance fixture writes during plugin and handler construction, configuration and execution, confirms every marker on stderr, and proves initialisation, the plugin response and a subsequent MCP call remain valid.

### RWMCP-032 — Remove sensitive temporary records when their lifetime expires

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp/ErrorReporting/Capture/CapturedErrorStore.cs:21-48,52-61`; `src/Roslyn.Workbench.Mcp/ErrorReporting/Preparation/PreparedSubmissionStore.cs:21-50,127-136`
- Concrete failure scenario: After one failure, an exception message and source path remain strongly referenced for days in an otherwise idle long-running server even though the record advertises a one-hour absolute lifetime. Prepared reports and receipts have the same behaviour after their configured expiry.
- Supporting call path or evidence: both singleton stores call cleanup only from later add/get/acquire operations. No timer, hosted cleanup or lifecycle callback removes entries when time alone advances. Tests advance time and then call `TryGet`, proving logical rejection and lazy cleanup rather than physical removal at expiry.
- Affected projects or subsystems: Host captured-error/prepared-submission stores, sensitive-memory retention and privacy/lifetime contracts.
- Remediation direction: Add `TimeProvider`-driven cleanup with DI-owned disposal and deterministic tests that prove removal without another store operation.

### RWMCP-033 — Couple consent validity to submission acquisition

- Severity: P2
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp/ErrorReporting/Tools/SubmitErrorReportTool.cs:71-95,115-121`; invalidation at `src/Roslyn.Workbench.Mcp/ErrorReporting/Consent/ErrorReportingConsentService.cs:66-81` and `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSessionStore.cs:91-114,261-268`
- Concrete failure scenario: Submission reads a valid Workspace grant and pauses; concurrent `workspace-close` invalidates that Workspace/epoch grant and returns; submission resumes, acquires the handle and queues the external report without observing revocation. Concurrent prompt flows for different handles can similarly allow one session-suppression choice to race past another call's earlier consent read.
- Supporting call path or evidence: store lookup → consent read/optional elicitation → separate store acquisition → dispatch. Consent and submission state have independent locks and no shared generation or authorisation lease. Sequential tests mock the collaborators and never interleave lifecycle invalidation with acquisition.
- Affected projects or subsystems: Host consent/submission, Workspace lifecycle observer integration and external dispatcher boundary.
- Remediation direction: Define an atomic authorisation/acquisition linearisation point using an invalidatable consent generation or lease and add deterministic close, suppression and duplicate-submission interleaving tests.

### RWMCP-034 — Revalidate the pinned checkout after repository preparation

- Severity: P2
- Confidence: High
- Exact file and line range: `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Repositories/RepositoryManager.cs:57-64,67-92`
- Concrete failure scenario: EF Core's Windows restore deletes tracked sentinel files after the only cleanliness check, so the current run measures a checkout different from the pinned commit and the next cache reuse fails.
- Supporting call path or evidence: preparation validates HEAD/tracked state, executes every repository command and returns without a post-preparation diff. Existing native Windows evidence records the exact three-file deletion.
- Affected projects or subsystems: ScenarioRunner preparation, external-repository release validation, cache reuse and performance comparability.
- Remediation direction: Validate and contain declared preparation side effects before returning a certified clean checkout while continuing to reject unexplained changes.

### RWMCP-035 — Route commit trace capture through the EventPipe collector

- Severity: P2
- Confidence: High
- Exact file and line range: `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:1081-1087`; `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Diagnostics/DiagnosticCollector.cs:16-30`
- Concrete failure scenario: The documented `commit --capture-trace` path throws before committing because it passes `ProfileKind.Trace` to a method whose Trace arm explicitly rejects that profile.
- Supporting call path or evidence: durable commit trace branch → `StartDurationProfile(Trace)` → immediate `InvalidOperationException`; general trace profiling already uses the separate `TraceCollection` implementation.
- Affected projects or subsystems: ScenarioRunner durable-commit profiling and EventPipe release evidence.
- Remediation direction: Wrap the commit phase in the directly controlled EventPipe collector and test selection, stop and failure cleanup.

### RWMCP-036 — Persist completed scenario evidence when a later item fails

- Severity: P2
- Confidence: High
- Exact file and line range: `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:288-310,463-521`
- Concrete failure scenario: A later scenario or iteration fails after earlier expensive work completed; all completed results remain in memory, no result file is written and the top-level handler prints only the final message.
- Supporting call path or evidence: measurement/destructive loops accumulate results first and call `ResultWriter` only after every item succeeds. Iteration cleanup is failure-aware, but reporting has no partial-run path.
- Affected projects or subsystems: ScenarioRunner measurement, destructive scenario families, result reporting and release diagnostics.
- Remediation direction: Persist an atomic run record after each completed item, include failed-item cleanup/validation evidence and still return non-zero.

### RWMCP-037 — Fail when an in-flight query ignores protocol cancellation

- Severity: P2
- Confidence: High
- Exact file and line range: `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Scenarios/ToolInvocationRunner.cs:194-242`; `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:328-351`
- Concrete failure scenario: The cancellation notification is sent while a request is active, but a regressed Host ignores it and completes normally. The runner records `OperationCanceled = false`, writes zero cancelled invocations and exits successfully.
- Supporting call path or evidence: after the delay wins, only `OperationCanceledException` changes the flag; normal post-notification completion is accepted and no later correctness assertion inspects it.
- Affected projects or subsystems: ScenarioRunner cancellation validation, Host/Roslyn cancellation evidence and future release gates.
- Remediation direction: Reject normal completion after an awaited cancellation notification while retaining failed-run timing and lease-recovery evidence.

### RWMCP-038 — Serialise access to each shared scenario checkout

- Severity: P2
- Confidence: High
- Exact file and line range: `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:48-55`; `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Repositories/RepositoryManager.cs:30-31,46-92`
- Concrete failure scenario: Two runner processes use the same repository/commit cache; both pass the clean check, then one prepares, mutates or restores files while the other's Host is reading or committing them, invalidating both runs.
- Supporting call path or evidence: cache identity is repository ID plus commit and `PrepareAsync` returns a bare path. No lease spans preparation, Host execution and restoration; unique state/output roots do not isolate the worktree.
- Affected projects or subsystems: ScenarioRunner shared cache, preparation/restoration and concurrent manual or future matrix validation.
- Remediation direction: Hold an OS-backed exclusive per-checkout lease from preflight through final restoration and validation.

### RWMCP-023 — Inspect package DLL metadata without buffering entire binaries

- Severity: P2
- Confidence: Medium
- Exact file and line range: `src/Roslyn.Workbench.Mcp/PluginLoading/PluginAssemblyMetadataReader.cs:19-24`; package loop at `src/Roslyn.Workbench.Mcp/PluginLoading/PluginPackageDiscovery.cs:67-91`
- Concrete failure scenario: A plugin ships a several-hundred-megabyte native dependency. Discovery allocates an equally large managed array solely to determine that the DLL has no managed metadata, causing a major startup working-set spike or process exhaustion.
- Supporting call path or evidence: package enumeration of every top-level DLL → `ReadAllBytes` → `PEReader(ImmutableArray<byte>)` → `HasMetadata`. Native dependencies are supported package contents and no size or PE-kind filter runs before buffering.
- Affected projects or subsystems: Host package discovery, PE inspection, startup performance and availability.
- Remediation direction: Inspect PE headers and metadata through a bounded read-only stream rather than materialising complete binaries.

### RWMCP-018 — Bound aggregate recovery-plan memory before materialising all artifacts

- Severity: P2
- Confidence: Medium
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitPlanner.cs:45-59,168-202,252-262,391-398`
- Concrete failure scenario: A repository-wide mutation touching many large sources materialises and retains complete original and intended byte arrays for every file, exhausting process memory even though each artifact remains below 128 MiB.
- Supporting call path or evidence: planner loops all changes → full text encoding plus full backup read → aggregate artifact dictionary → only after completion does the store enforce per-artifact size. No cumulative budget or streaming boundary exists.
- Affected projects or subsystems: Workspace commit planning, recovery persistence and Host availability.
- Remediation direction: Check an aggregate budget incrementally or stream bounded provisional artifacts with safe cancellation cleanup.
- Resolution: Completed under the accepted operational design without changing production commit planning. Permanent scenario-runner sampling now records baseline, final and peak Host working-set and private-memory values during `transaction-commit`. Published-Host measurements covered 1-file GuardClauses, 27-file Serilog and 948-file EF Core commits; the EF Core plan contained 36,664,873 bytes of original and intended artifacts, with a median private-memory increase of 38,952,960 bytes and median working-set increase of 70,746,112 bytes across three fresh Hosts. The evidence placed credible pressure at multi-gigabyte aggregate plans involving tens of thousands of similarly sized files, well beyond the intended small coherent transaction workflow. Release documentation and tag-specific MCP agent guidance now direct callers to preview and promptly commit or roll back one coherent change, while the retained measurements allow this decision to be revisited if real workloads grow. The scenario runner, integration tests, latest-all analyzer builds and complete 59-test published-Host acceptance wrapper pass.

### RWMCP-013 — Republish Ready instance state after successful reload

- Severity: P3
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:344-371`
- Concrete failure scenario: Other processes continue reporting this instance as `WorkspaceOutOfDate` after its local reload succeeds because no replacement state is published.
- Supporting call path or evidence: external-change transition queues out-of-date status → reload replaces the session with `Ready` → no publisher call or lifecycle observer updates the status handle.
- Affected projects or subsystems: Workspace lifecycle and cross-process advisory coordination.
- Remediation direction: Publish `Ready` with cleared transaction/commit fields after successful replacement.
- Resolution: Completed. After installing the certified replacement session and disposing the previous Workspace, reload awaits publication of the replacement session's `Ready` state with transaction revision, commit identifier and commit phase cleared. Failed and uncertified reloads do not publish `Ready`, and a real two-instance integration test proves that an observer sees the target transition from `WorkspaceOutOfDate` back to `Ready`. Workspace unit and integration suites, latest-all analyzer builds and the full solution build pass.

### RWMCP-029 — Do not mark reference-producing Code Action queries as idempotent

- Severity: P3
- Confidence: High
- Exact file and line range: `src/Roslyn.Workbench.Mcp/Protocol/McpToolProtocolFactory.cs:84-98,101-132`; reference creation at `src/Roslyn.Workbench.Mcp.CodeActions/References/CodeActionReferenceState.cs:62-92`, `src/Roslyn.Workbench.Mcp.CodeActions/Tools/ListCodeActionsTool.cs:156-203` and `src/Roslyn.Workbench.Mcp.CodeActions/Tools/PrepareFixAllTool.cs:138-168`
- Concrete failure scenario: A client safely retries either Code Action query because `idempotentHint` is true. Every retry returns new random action IDs and consumes additional bounded cache entries, accelerating cache pressure and potentially changing later action-list results under RWMCP-025.
- Supporting call path or evidence: the protocol factory maps every query mechanically to idempotent. Both Code Action queries create new GUID-backed replay references; the installed SDK defines idempotence as no additional environmental effect on repetition. The analogous handle-producing error-report preparation tool is explicitly non-idempotent.
- Affected projects or subsystems: Host Code Action metadata, reference cache and retrying MCP clients.
- Remediation direction: Add explicit idempotence to Code Action metadata and publish false for both reference-producing queries.

## Notable test gaps

- No mutation/commit test removes one linked physical path from only one project context.
- Linked-change tests do not exercise adjacent replacements or insertion-boundary conflict rules.
- Atomic replacement and recovery tests verify exact bytes but not preservation of source modes, attributes or ACLs.
- Commit tests do not cover per-artifact/manifest limit translation; permanent scenarios measure aggregate commit memory, but no synthetic multi-gigabyte recovery plan is exercised.
- No test mutates a project/imported input between evaluation and manifest capture or a source between commit application and promotion capture.
- Snapshot tests do not model a process restart and deterministic identity reuse.
- Solution hierarchy fixtures keep the solution at the workspace root with matching path casing.
- Target-framework tests do not cancel during real multi-project evaluation.
- Root and selector tests do not exercise malformed rooted strings across every public normaliser.
- Reload tests do not assert that advisory cross-instance state returns to `Ready`.
- Plugin preparation tests do not reject protocol-invalid merged tool names.
- No external fixture uses an unsupported JSON request or response member and proves per-plugin schema-failure isolation.
- Runtime composition tests do not suppress RWMCP003 or reject an `async void Configure` binary; this is accepted because synchronous configuration is an analyser-enforced contract for trusted in-process plugins rather than a runtime sandbox boundary.
- Package discovery tests inspect only small DLLs and do not bound allocations for large native dependencies.
- Code Action list tests expect a failed reference admission to disappear and do not assert a capacity rejection, warning or truthful applicable-action count.
- Fix All diagnostic-provider tests mock the diagnostic service and therefore do not reveal that every per-document call performs whole-compilation diagnostics and analyser execution; realistic project/solution scaling is unmeasured.
- Binder tests cover top-level enums and attributes but never pass contradictory or undefined nested Workspace selectors through a real tool adapter.
- Full-schema tests inspect keywords and shapes but do not validate actual success, no-change, failure and unexpected-error payloads as schema instances.
- Code Action protocol tests do not repeat reference-producing queries or compare their advertised idempotence with the resulting IDs/cache state.
- The published console-output fixture covers plugin and handler construction, configuration and invocation; converter output and deliberate raw-handle or native stdout writes remain unexercised, with the latter explicitly outside cooperative trusted-plugin containment.
- Sentry tests mock `ISentryClient` and never compare the reviewed preview with the final envelope after the pinned SDK's default event processors run.
- Error-store expiry tests require a later lookup to trigger cleanup and do not prove that sensitive records are released when time alone advances.
- Consent and submission tests do not interleave Workspace close/reload or session suppression between consent evaluation, handle acquisition and dispatch.
- No integration test drives a failed Sentry transport or proves queued-event draining during real Host disposal; remote delivery remains best-effort by documented design.
- The ScenarioRunner has no hermetic tests for repository preparation side effects, repeat cache reuse, concurrent checkout access, partial result persistence, ignored cancellation, commit trace selection, child-process cancellation or injected restoration failures.

## Review limitations

- Unit 7 validated the complete test/CI topology, passed 1,943 solution-wide Unit/Contract cases with integration/audit/acceptance correctly excluded, successfully built the ScenarioRunner with .NET SDK 10.0.102 and deserialised/listed its checked-in suite. The repository-wide pass additionally passed 30 focused current-source Plugins.Core tests while reconciling the resolved original tool findings. Focused unit-6 evidence remains 52 Host error-reporting/exception-filter tests plus 21 Host composition/tool/schema integration tests; unit-5 evidence remains 245 Host unit/contract tests, 34 Host composition/hosting/protocol integration tests, a clean executable run and two read-only protocol probes.
- Acceptance, Code Action audit and external-repository scenarios were not run because repository policy requires separate authorisation or release-stage execution.
- Windows source metadata preservation was not dynamically exercised in the Linux environment.
- Roslyn MCP tooling was unavailable; local source inspection was used for symbol and call-site navigation.
