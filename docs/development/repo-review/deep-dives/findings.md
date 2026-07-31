# Deep-dive finding ledger

This ledger retains candidates, validation status and validation history for the staged deep-dive programme. Stable identifiers continue the repository review sequence and are never reused.

## RWMCP-008 — Detect inputs changed between Workspace evaluation and manifest capture

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:81-112`, `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:313-329` and `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs:248-263,333-340`
- Concrete failure scenario: MSBuild evaluates a project or imported `Directory.Build.props`, then that input changes before `BuildManifest` fingerprints it. The manifest records the new disk metadata while the Roslyn `Solution` retains the earlier evaluation. The session is registered as `Ready`, and later change detection sees the manifest's current metadata, so queries and mutations continue against a configuration state that no longer exists on disk. The same mismatch can occur after commit when an external writer changes an applied source file before commit promotion fingerprints disk: the manifest captures the external bytes while `CurrentSolution` retains the transaction bytes.
- Supporting call path/evidence: Host `workspace-open` → `WorkspaceLifecycleService.OpenAsync` → `_workspaceLoadWorkflow.LoadAsync` completes MSBuild evaluation → `_instanceStatusPublisher.OpenAsync` → `_workspaceChangeDetector.BuildManifest`. Reload repeats the same load-then-manifest sequence. Commit performs `_commitWriter.ApplyAsync` → `CreateCommittedSession` → `BuildManifest(transaction.CurrentSolution)` as separate filesystem observations. `WorkspaceInputChangeMonitor.Track` enables watcher events only after manifest construction, and commit replaces the old manifest after the new one is built, so neither path certifies that the Solution and captured fingerprints describe one disk state.
- Affected projects or subsystems: Workspace loading, lifecycle, change detection, snapshot semantics, Plugins, CodeActions and Host consumers.
- Remediation direction: Establish a consistency protocol spanning MSBuild evaluation and manifest capture. Detect any tracked input change across that interval and retry or reject the load before publishing a `Ready` session.
- Validation history: Candidate retained after tracing open and reload, confirming that immediate post-registration `HasChanged` cannot distinguish this state, and checking that no lifecycle observer or later manifest comparison reconciles the Roslyn evaluation with newly fingerprinted inputs. Revalidated during deep dive 2 and expanded to commit promotion after confirming that file application and new-manifest capture are not one consistency boundary.

## RWMCP-009 — Prevent snapshot identity reuse after server restart

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSessionStore.cs:44-64`, `src/Roslyn.Workbench.Mcp.Workspace/Resolution/WorkspaceResolver.cs:80-108` and `src/Roslyn.Workbench.Mcp.Workspace/Transactions/SnapshotGuard.cs:7-26`
- Concrete failure scenario: The first process opens a workspace as `workspace-1`, epoch `1`, and returns a revision-zero snapshot precondition. After restart, a different first workspace receives the same workspace ID and epoch and starts a revision-zero transaction. A retained or retried mutation precondition validates against the unrelated workspace instead of being rejected as stale.
- Supporting call path/evidence: `WorkspaceSessionStore` initialises all identity counters to zero per process and emits deterministic sequential values. Public `SnapshotPrecondition` carries only workspace ID, epoch and transaction revision; both resolver and transaction guards compare only those fields. Unlike Code Action references and error-reporting handles, the public Workspace/snapshot contract is not documented as process-local.
- Affected projects or subsystems: Abstractions snapshot contracts, Workspace state and resolution, transaction guards, plugin and Code Action mutation consumers.
- Remediation direction: Include an unpredictable process/session generation in public workspace identity or allocate globally unique workspace IDs, and document that selectors and snapshot preconditions expire at restart.
- Validation history: Candidate retained after confirming in-process IDs remain unique across close/reload and narrowing the failure to cross-process replay; the exact restart collision remains deterministic.

## RWMCP-010 — Use one canonical path identity for solution-folder membership

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Projects/ProjectStructureService.cs:131-134` and `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetSolutionStructureTool.cs:82-85`
- Concrete failure scenario: `/repo/src/App.sln` is opened with `/repo` as the explicit workspace root. Solution persistence returns `App/App.csproj`, which is stored as the hierarchy key, while the consumer normalises the Roslyn project path to `src/App/App.csproj`. Lookup fails and `get-solution-structure` silently omits the project's `solutionFolderPath`. A casing difference between the solution entry and physical path produces the same failure on a case-insensitive filesystem because the hierarchy dictionary is ordinal.
- Supporting call path/evidence: `GetSolutionStructureTool` → `IProjectStructureService.GetSolutionHierarchyAsync` creates keys relative to the solution directory → tool normalises each project through `IWorkspaceResolver.NormalizeProjectPath`, which is relative to `WorkspaceIdentity.WorkspaceRoot` → ordinal dictionary lookup. Existing integration fixtures place the solution at the workspace root and preserve matching casing.
- Affected projects or subsystems: Abstractions project-structure contract, Workspace project services and Plugins.Core solution projection.
- Remediation direction: Use canonical absolute project paths and the Workspace path comparer internally, then project to workspace-relative public paths only at the output boundary.
- Validation history: Candidate retained after checking both `.sln` and `.slnx` serializers and confirming no adapter rebases hierarchy keys from the solution directory to the workspace root.

## RWMCP-011 — Observe cancellation during batch target-framework evaluation

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Projects/ProjectTargetFrameworkResolver.cs:106-111` and `src/Roslyn.Workbench.Mcp.Workspace/Projects/ProjectStructureService.cs:23-54`
- Concrete failure scenario: A cold `get-solution-structure` request starts target-framework evaluation for hundreds of projects and is cancelled after evaluation begins. The service continues loading and evaluating every cache miss before the resolver observes cancellation. Its shared Workspace lease remains active, so close, reload or mutation acquisition reports `WorkspaceBusy` until the entire batch completes.
- Supporting call path/evidence: Plugins.Core solution query → `IProjectTargetFrameworkResolver.Resolve` → synchronous `_projectStructureService.GetTargetFrameworks(projects)` → `ProjectStructureService` loops every project without a token → resolver checks the token only after the call returns. Microsoft Learn confirms that the used `ProjectCollection.LoadProject(string)` API has no cancellation-token overload. Existing tests cover caching and result projection but not cancellation during real evaluation.
- Affected projects or subsystems: Abstractions project services, Workspace query services and leases, Plugins.Core solution/project tools.
- Remediation direction: Propagate cancellation into batch evaluation and check it between physical project evaluations; document the unavoidable granularity of an individual synchronous MSBuild evaluation or adopt a cancellable/isolation boundary if individual evaluations prove operationally material.
- Validation history: Candidate retained after separating unavoidable in-call MSBuild granularity from the avoidable failure to stop between projects.

## RWMCP-012 — Convert malformed public paths into structured contract failures

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Loading/WorkspaceRootResolver.cs:26-34`, `src/Roslyn.Workbench.Mcp.Workspace/Selection/WorkspaceSelectorService.cs:91-100,145-148` and `src/Roslyn.Workbench.Mcp.Workspace/Resolution/WorkspaceResolver.cs:462-476`
- Concrete failure scenario: An MCP caller supplies a rooted workspace-root, workspace-selector or project/document-selector path containing a null character. `Path.GetFullPath` throws `ArgumentException`, which escapes the owning resolver and becomes a generic correlated tool failure instead of `WorkspaceRootInvalid`, selector-not-found or a request-validation result.
- Supporting call path/evidence: Workspace open and selection tools bind caller-controlled strings → lifecycle/selection/resolution services call `Path.GetFullPath` without a non-throwing boundary. `WorkspaceLoader.NormalizeOpenPath` already catches `ArgumentException`, proving the adjacent open-path contract expects structured rejection. Microsoft Learn documents `ArgumentException` for invalid path characters, including null. Contract validation checks selector shape but not path well-formedness.
- Affected projects or subsystems: Abstractions selectors, Workspace loading/selection/resolution and all Host/plugin/Code Action consumers of those contracts.
- Remediation direction: Centralise non-throwing path normalisation and translate malformed paths into the structured error owned by each request boundary.
- Validation history: Candidate retained after checking request binding and `WorkspaceContractValidator`; neither rejects the malformed rooted strings before these calls.

## RWMCP-013 — Republish Ready instance state after successful reload

- Severity: P3
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:344-371`
- Concrete failure scenario: External-change detection publishes `WorkspaceOutOfDate`; the user reloads successfully and the local session becomes `Ready`, but other Roslyn Workbench processes continue reading the stale out-of-date status until a later transaction event happens to publish another update.
- Supporting call path/evidence: Query/status change detection → `_instanceStatusPublisher.QueueUpdate(WorkspaceOutOfDate)` → `workspace-reload` creates and installs a `Ready` session → returns success without `UpdateAsync` or `QueueUpdate`. Session lifecycle observers cover caches, Code Action references and error-reporting consent, not instance-status publication. Reload tests assert session replacement but do not verify status reset.
- Affected projects or subsystems: Workspace lifecycle and cross-process advisory coordination, Host workspace status consumers.
- Remediation direction: Publish `Ready` with cleared transaction and commit fields after installing the reloaded session, using the established advisory-update failure policy.
- Validation history: Candidate retained after confirming no indirect publisher observer runs during `ReplaceSession`.

## RWMCP-014 — Reconcile linked-document deletions before deleting the physical file

- Severity: P1
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceMutationCandidateProcessor.cs:33-41`, `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitPlanner.cs:135-142` and `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs:333-365`
- Concrete failure scenario: A mutation removes a linked source document from one project context but leaves the same physical path in another project, as can happen in a multi-target project or two projects linking one file. Candidate processing propagates additions and merges text changes but does not propagate or reject the asymmetric removal. Commit planning sees the removed `DocumentId` and schedules a physical delete. Commit then deletes the shared file and promotes a `CurrentSolution` that still contains the surviving document, leaving both the repository and the Ready session inconsistent with the requested candidate.
- Supporting call path/evidence: Plugin or Code Action candidate → `WorkspaceMutationCandidateProcessor.ProcessAsync` → added-document propagation → `LinkedDocumentChangeMerger`, whose changed-document enumeration excludes removals → `WorkspaceCommitPlanner.AddProjectChangesAsync` schedules every removed document path without checking whether `currentSolution` still contains that path → `WorkspaceCommitWriter.ApplyAsync` moves the physical target to a delete marker → `TransactionCommitService.CreateCommittedSession` retains `transaction.CurrentSolution`. Tests cover added multi-target propagation and duplicate linked text writes, but no asymmetric linked removal.
- Affected projects or subsystems: Workspace mutation processing, commit planning and session promotion; Plugins, CodeActions and Host transaction consumers.
- Remediation direction: Normalise deletion across every document/project context for the same canonical physical path or reject asymmetric deletion, and make the planner refuse a delete while any document in the current Solution still references the target.
- Validation history: Candidate retained after independently checking the candidate validator, added-document propagator, linked merger, planner duplicate-target logic and committed-session promotion. No later call-path guard prevents the physical delete or repairs the surviving Solution document.

## RWMCP-015 — Recheck orphan evidence after acquiring the Workspace commit lock

- Severity: P1 candidate
- Confidence: High
- Validation status: Rejected on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Recovery/WorkspaceCommitRecoveryService.cs:21-29`, `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitLockManager.cs:52-58` and `src/Roslyn.Workbench.Mcp.Workspace/Transactions/FileStreamWorkspaceFileLockProvider.cs:25-40`
- Concrete failure scenario considered: Startup recovery could classify owner-only evidence as an orphan before the active writer publishes its manifest, wait for the writer's lock, then delete the newly completed recovery record using the stale classification.
- Supporting call path/evidence: `RecoverAsync` reads orphan owners before calling `Acquire`, but `WorkspaceCommitLockManager.Acquire` delegates to `TryAcquire` and returns `Contended` immediately when OS locking fails. Recovery deletes only when acquisition succeeds and otherwise skips the owner. The active commit holds this lock for planning, owner/artifact/manifest persistence, source application and cleanup.
- Affected projects or subsystems: Workspace startup recovery and cross-process commit coordination.
- Remediation direction: None; retain the non-blocking acquisition contract and its cross-process contention coverage.
- Validation history: Rejected after tracing the real lock provider and confirming with the durable integration fixture that acquisition is non-blocking during live ownership and succeeds only after crash/release. The hypothesised stale decision cannot be retained across a wait because no wait occurs.

## RWMCP-016 — Preserve source-file permissions across replacement and recovery

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/IO/AtomicFileWriter.cs:73-93` and `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitWriter.cs:67-76,180-189`
- Concrete failure scenario: On Unix, a tracked source file with mode `0444`, group-write permission or an executable bit is replaced by a mutation. `AtomicFileWriter` creates a new temporary file with the process default mode and renames that inode over the target, so the successful commit silently changes the repository file's mode. Recovery also reconstructs a replacement from bytes using a fresh default-mode file and cannot restore the original mode.
- Supporting call path/evidence: Transaction commit or recovery → `WorkspaceCommitWriter.ApplyAsync`/`RestoreAsync` → `IAtomicFileWriter.WriteAllBytesAsync(..., AtomicFileAccess.Default)` → `FileMode.CreateNew` with `UnixCreateMode` set only for `OwnerOnly` → `NativeAtomicFileCommitter.Commit` moves the temporary file over the destination. Recovery entries retain content hashes and byte artifacts but no source metadata. The release-readiness record states repository source-file permissions should remain unchanged; integration coverage for default replacement asserts bytes and temporary-file cleanup only.
- Affected projects or subsystems: Workspace atomic I/O, commit and crash recovery on Unix; Windows ACLs/attributes require separate platform validation.
- Remediation direction: Capture and apply the destination's relevant metadata to the temporary replacement before the atomic move, persist metadata needed for recovery, and add Unix-mode plus representative Windows metadata integration coverage.
- Validation history: Candidate retained after separating owner-only recovery-file creation from default source replacement and confirming that neither the plan nor manifest records source-file mode. The current Linux integration fixture has no preservation assertion.

## RWMCP-017 — Translate recovery size limits at the commit boundary

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Recovery/CommitRecoveryStore.cs:52-57,695-714` and `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs:275-290`
- Concrete failure scenario: A valid staged mutation replaces or deletes a source file whose recovery artifact exceeds 128 MiB, or produces a manifest over 16 MiB. `PersistPlanAsync` deliberately throws `InvalidDataException`, but commit catches only `IOException` and `UnauthorizedAccessException`. The expected bounded-input rejection escapes as an unexpected correlated MCP failure instead of a structured, actionable commit-preparation result.
- Supporting call path/evidence: Host `transaction-commit` → `TransactionCommitService.StageCommitAsync` → `CommitRecoveryStore.PersistPlanAsync` → `ValidateArtifactSizes`/`SerializeJson` → `InvalidDataException`; `IsRecoverableFileSystemException` excludes that exception. Validation occurs before source application, so data remains safe, but the transaction protocol does not explain the supported limit or required action. Store tests cover oversized reads and direct invalid-data exceptions; commit tests cover only IO/authorisation preparation failures.
- Affected projects or subsystems: Workspace transaction commit, recovery persistence and Host MCP error mapping.
- Remediation direction: Enforce the limits as a structured planning/preparation validation before persistence or explicitly translate the store exception to a stable commit error while retaining the active transaction.
- Validation history: Candidate retained after confirming there is no earlier mutation-size validation, no Host adapter mapping for this exception and no broad catch in the commit service that converts it into `CommitPreparationFailed`.

## RWMCP-018 — Bound aggregate recovery-plan memory before materialising all artifacts

- Severity: P2
- Confidence: Medium
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitPlanner.cs:45-59,168-202,252-262,391-398` and `src/Roslyn.Workbench.Mcp.Workspace/Recovery/CommitRecoveryStore.cs:695-704`
- Concrete failure scenario: A solution-wide rename or generated-source mutation touches hundreds or thousands of large source files. Planning encodes every intended document into a new byte array, reads every original file into another byte array and retains all arrays in one dictionary before persistence. Although each artifact may be below 128 MiB, their aggregate can exhaust process memory and terminate the server before a structured limit is reached.
- Supporting call path/evidence: `TransactionCommitService` → `WorkspaceCommitPlanner.CreateAsync` loops every project change → `GetDocumentBytesAsync` materialises complete encoded text and `ReadAllBytesAsync` materialises the backup → both are retained in `WorkspaceCommitPlanningContext.Artifacts` → only after the entire plan exists does `CommitRecoveryStore.ValidateArtifactSizes` check each artifact independently. No total byte/count option or streaming persistence boundary exists.
- Affected projects or subsystems: Workspace commit planning and recovery persistence; Host process availability for repository-scale mutations.
- Remediation direction: Introduce an aggregate plan budget checked incrementally before retaining each artifact, return a structured preparation failure when exceeded, or stream bounded artifacts to a provisional recovery plan while preserving cancellation cleanup and atomic manifest publication.
- Validation history: Candidate retained at medium confidence after checking Workspace options, transaction revision limits and recovery-store bounds. Ordinary small edits remain cheap, but the supported mutation contract permits repository-wide document changes and the existing per-artifact limit does not constrain cumulative allocation.

## RWMCP-019 — Allow adjacent linked-document text changes to merge

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Workspace/Transactions/LinkedDocumentChangeMerger.cs:117-134`
- Concrete failure scenario: Two project contexts for one linked file produce independent replacements of adjacent spans, such as one changing the last character of a token and another changing the immediately following character. The spans do not overlap and Roslyn can apply both, but `IntersectsWith` returns true when one span's end coincides with the other's start, so staging rejects the mutation as `LinkedDocumentConflict`.
- Supporting call path/evidence: Plugin or Code Action candidate → `WorkspaceMutationCandidateProcessor` → `LinkedDocumentChangeMerger.MergeGroupAsync` sorts changes → identical edits are deduplicated → `previous.Span.IntersectsWith(change.Span)` rejects coincident boundaries → `baselineText.WithChanges` is never reached. Microsoft Learn documents that `IntersectsWith` includes coincident end/start positions, while `SourceText.WithChanges` rejects overlapping changes; the existing merger test uses replacements of the same span and has no adjacent-span case.
- Affected projects or subsystems: Workspace linked-document reconciliation and plugin/Code Action mutation staging.
- Remediation direction: Use overlap semantics for non-empty adjacent spans while retaining explicit conflict rules for competing insertions or insertion/replacement boundary ambiguity, and add boundary-focused merger tests.
- Validation history: Candidate retained after checking current Roslyn API semantics against Microsoft Learn and distinguishing safe adjacency from same-position insertions that still require an explicit conflict rule.

## RWMCP-020 — Reject protocol-invalid plugin tool names during preparation

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Plugins/Preparation/PluginConfigurationPreparer.cs:45-65,82-87` and `src/Roslyn.Workbench.Mcp/Protocol/McpToolProtocolFactory.cs:101-132`
- Concrete failure scenario: An otherwise valid external plugin declares `RoslynTool("find symbol", ...)` or a name longer than 128 characters. Runtime preparation accepts the non-blank, unique value and reports the plugin enabled. Host later publishes that value directly as `Protocol.Tool.Name`, even though the installed C# MCP SDK defines valid tool names as one to 128 ASCII letters, digits, underscores, dots or hyphens. A conforming client can reject or omit the invalid tool, leaving `server-status` and Host registration claiming an enabled tool that agents cannot call.
- Supporting call path/evidence: plugin `Configure` → `PluginToolMetadataFactory.Create` → `PluginConfigurationPreparer.HasRequiredMetadata`, which checks only whitespace → collision policy, which checks only ordinal identity → `PluginMcpToolRegistrationVisitor` → `PluginQueryMcpServerTool`/`PluginMutationMcpServerTool` → `McpToolProtocolFactory.CreateCatalogueTool` assigns `Name` directly. The SDK 1.4.1 contract contains `ValidateToolNameRegex` with `^[A-Za-z0-9_.-]{1,128}\z`, but this Host-owned direct `Tool` construction does not call the SDK factory that applies it. No runtime or analyser test covers invalid syntax or length.
- Affected projects or subsystems: Plugins public metadata/builders, plugin runtime preparation, Host MCP publication and external MCP clients.
- Remediation direction: Validate the authoritative merged tool name against the MCP name contract before collision handling and disable the plugin with `PluginToolName`; add an authoring diagnostic for statically known attribute and fluent values.
- Validation history: Candidate retained after confirming that non-blank validation is the only semantic name check, collision handling does not normalise or validate, and the Host bypasses the SDK's validated reflective tool factory by constructing the protocol model directly.

## RWMCP-021 — Preflight plugin transport schemas before publishing DI registrations

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Plugins/Preparation/PluginConfigurationPreparer.cs:69-78`, `src/Roslyn.Workbench.Mcp/Hosting/RoslynWorkbenchServiceCollectionExtensions.cs:217-221` and `src/Roslyn.Workbench.Mcp/Protocol/McpSdkSchemaProvider.cs:19-86`
- Concrete failure scenario: A public query request exposes a public `System.Type` or delegate property. The plugin builds with the packaged analyser and passes runtime accessibility checks, but System.Text.Json does not support those member types. The plugin is marked enabled and registered in DI. When the Host or SDK resolves the complete `IEnumerable<McpServerTool>`, the plugin tool constructor requests its input schema and schema generation throws, preventing publication of every tool rather than disabling only the invalid plugin. With full output schemas, an unsupported response contract causes the same catalogue-wide failure.
- Supporting call path/evidence: plugin configuration → public contract validation → preparation result → catalogue materialisation → Host registers every prepared tool → singleton tool construction → `McpToolProtocolFactory.CreatePluginTool` → `ToolSchemaFactory.CreateInputSchema` and optionally `CreateOutputSchema` → `McpSdkSchemaProvider` asks the authoritative SDK/System.Text.Json exporter for metadata. No preparation step invokes this boundary or converts its expected contract exception into plugin status. Microsoft Learn lists `System.Type`, delegates and other member shapes as unsupported by System.Text.Json. Existing integration coverage exercises only known-compatible contracts.
- Affected projects or subsystems: Plugins contract preparation, Host DI and protocol/schema publication, server startup/tool listing and external plugin compatibility.
- Remediation direction: During per-plugin materialisation, generate the authoritative input schema and configured output schema for every prepared tool inside the existing plugin-failure boundary; cache validated schemas or registrations and disable the whole plugin with a specific diagnostic before adding any DI services.
- Validation history: Candidate retained after comparing the analyser audit's explicit statement that the real serializer/schema provider must remain authoritative with the actual call path, where that provider first runs only after enabled tools have entered the global DI collection.

## RWMCP-022 — Reject asynchronous plugin configuration at the runtime boundary

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp/PluginLoading/MefPluginComposer.cs:14-24` and `src/Roslyn.Workbench.Mcp/PluginLoading/LoadedPluginPreparer.cs:23-35`
- Concrete failure scenario: A plugin suppresses RWMCP003 or was built without the packaged C# analyser and implements `IRoslynPlugin.Configure` as `async void`. `Configure` returns at its first incomplete await, Host freezes the partially populated configuration and can enable it, then the continuation attempts another registration and throws `InvalidOperationException` outside the startup `try`/`catch`. On a console Host without a synchronisation context, an unhandled `async void` continuation exception can terminate the server; even a non-failing continuation silently loses registrations.
- Supporting call path/evidence: external assembly load → MEF resolves one `IRoslynPlugin` → `MefPluginComposer` invokes `Configure` as an ordinary `void` call → `LoadedPluginPreparer` immediately freezes and prepares the configuration. Runtime never inspects the interface implementation for `AsyncStateMachineAttribute`, even though public authoring documentation states runtime validation remains authoritative when diagnostics are suppressed or the plugin was built without the analyser. Tests prove RWMCP003 source analysis only and have no suppressed/binary runtime fixture.
- Affected projects or subsystems: Plugin authoring/runtime validation parity, Host MEF composition, startup catalogue correctness and process availability.
- Remediation direction: Resolve the concrete interface implementation before invocation and disable a plugin whose `Configure` method carries asynchronous state-machine metadata; retain RWMCP003 for source-local feedback and add a suppressed-analyser external fixture proving runtime rejection.
- Validation history: Candidate retained after confirming there is no later containment point for work scheduled by `async void`, configuration freeze cannot await it, and the general plugin-load catch covers only the synchronous portion of the call.

## RWMCP-023 — Inspect package DLL metadata without buffering entire binaries

- Severity: P2
- Confidence: Medium
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp/PluginLoading/PluginAssemblyMetadataReader.cs:19-24` and `src/Roslyn.Workbench.Mcp/PluginLoading/PluginPackageDiscovery.cs:67-91`
- Concrete failure scenario: A valid plugin package carries a large top-level native dependency, such as an inference or compiler runtime DLL. Discovery enumerates that dependency along with managed assemblies and allocates a managed byte array equal to the complete binary merely to determine that it has no managed metadata. A several-hundred-megabyte dependency produces an equivalent transient large-object allocation and working-set spike at every Host start; sufficiently constrained developer environments can terminate with `OutOfMemoryException` before the plugin is considered for loading.
- Supporting call path/evidence: Host startup → `PluginPackageDiscovery.DiscoverPackage` enumerates every top-level `*.dll` → `PluginAssemblyMetadataReader.Inspect` → `IFileSystem.File.ReadAllBytes` → `PEReader(ImmutableArray<byte>)` → `HasMetadata`. The package model explicitly supports private native dependencies, the loop does not filter by size or PE kind before buffering, and the caught exception set cannot safely recover from process memory exhaustion. Existing metadata tests use small managed or malformed fixtures and make no allocation assertion.
- Affected projects or subsystems: Host plugin discovery, filesystem and PE inspection, startup performance and process availability for dependency-heavy plugins.
- Remediation direction: Open a bounded read-only stream and use streaming/prefetched-metadata PE inspection so headers and metadata are read without materialising complete managed or native binaries; retain per-file error isolation and containment checks.
- Validation history: Candidate retained at medium confidence after separating trusted-code execution from pre-execution package scanning and confirming that large native DLLs are legitimate package contents rather than an adversarial-only scenario.

## RWMCP-024 — Prevent retained plugin contexts from escaping the Workspace lease

- Severity: P2 candidate
- Confidence: High
- Validation status: Rejected on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.Plugins/Execution/PluginQueryContext.cs:5-31`, `src/Roslyn.Workbench.Mcp.Plugins/Execution/PluginMutationContext.cs:5-26` and `src/Roslyn.Workbench.Mcp.Plugins/Execution/QueryResultCacheScope.cs:48-59`
- Concrete failure scenario considered: A singleton handler could retain an invocation context, use its resolver or Solution after the lease ends and bypass Workspace gates or mutate the Host through an escaped capability.
- Supporting call path/evidence: Contexts contain an immutable Solution snapshot, identity, resolver and stateless read-only services. Mutation staging is retained only on the Host-owned lease and is never exposed. The one reusable stateful capability, `QueryResultCache`, is deactivated when the query lease is disposed and rejects later calls before reaching its store. A trusted in-process plugin can retain the Solution reference itself regardless of a context wrapper, while the next Host acquisition rechecks underlying Workspace divergence.
- Affected projects or subsystems: Plugins execution contexts, Workspace leases and the documented in-process trust boundary.
- Remediation direction: None within the current trusted in-process model; retain the inactive cache scope and runtime Workspace containment checks. Use an out-of-process sandbox if adversarial reference retention must be prevented.
- Validation history: Rejected after distinguishing ordinary immutable CLR reference retention from an escaped Host mutation/staging capability. Enforcing non-retention is impossible in-process without removing the supported Solution contract, and the concrete stateful cache escape is already closed.

## RWMCP-025 — Report reference-cache admission failure instead of returning a false action list

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.CodeActions/References/CodeActionReferenceState.cs:54-95`, `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionInfoFactory.cs:24-60` and `src/Roslyn.Workbench.Mcp.CodeActions/Tools/ListCodeActionsTool.cs:156-203`
- Concrete failure scenario: Repeated Code Action discovery retains enough five-minute replay recipes to approach the configured 75,000-unit default, or one action carries a recipe larger than the remaining capacity. `MemoryCache` refuses the new entry. `list-code-actions` silently removes that applicable action from both `items` and `totalCount` and can return a successful empty list, causing an agent to conclude that no Code Fix or refactoring exists. A later retry may produce a different list after asynchronous cache compaction or expiry, but the first response contains no warning or recovery action.
- Supporting call path/evidence: `list-code-actions` discovers and sorts applicable leaves → `CodeActionInfoFactory.TryCreate` builds a replay recipe → `CodeActionReferenceState.TryCreate` calls the size-limited cache and returns `false` when immediate read-back fails → the info factory returns `false` → `CreateBoundedActionsAsync` neither adds the item nor increments `totalCount` and returns success. Microsoft Learn documents that an entry is not cached when aggregate entry sizes exceed `SizeLimit`. The focused unit test `GIVEN_ActionReferenceCannotBeCreated_WHEN_Executing_THEN_ShouldExcludeActionFromBoundedMetadata` codifies the silent empty-success outcome rather than a structured capacity result; preparation already returns `ActionReferenceCapacityExceeded` for the same store failure.
- Affected projects or subsystems: CodeActions reference storage, discovery projection and list contract; Host MCP Code Action query responses and agent selection workflows.
- Remediation direction: Distinguish an inapplicable projection from reference admission failure, stop or complete projection deterministically, and return a structured capacity rejection or bounded warning that tells the caller to retry or increase the configured cache capacity without misrepresenting the applicable action count.
- Validation history: Candidate retained after tracing both ordinary saturation and an individually oversized recipe through the complete Host query path, confirming no later adapter or serializer adds a warning, and comparing the silent list behaviour with the explicit prepared-reference capacity rejection.

## RWMCP-026 — Compute project Fix All diagnostics once per project

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp.CodeActions/Execution/FixAll/WorkspaceFixAllDiagnosticProvider.cs:36-55` and `src/Roslyn.Workbench.Mcp.CodeActions/Discovery/CodeActionDiagnosticService.cs:19-52,108-205`
- Concrete failure scenario: A project or solution Fix All targets a project containing hundreds or thousands of source documents and one or more relevant project or built-in analysers. Roslyn requests all project diagnostics. The Workbench provider loops every document, and each document call obtains the project compilation, runs complete compiler diagnostics and runs the selected analysers across that whole compilation before filtering to one syntax tree. It then performs one additional whole-compilation pass for project diagnostics. Preparation and later staging recreate the Fix All action, so this quadratic-shaped diagnostic work is paid twice, causing long-running requests and sustained CPU/allocation pressure on realistic repositories.
- Supporting call path/evidence: `prepare-fix-all` or prepared `stage-code-action` → `FixAllActionFactory` → Roslyn document-based Fix All provider → `WorkspaceFixAllDiagnosticProvider.GetAllDiagnosticsAsync(project)` → one `GetDocumentDiagnosticsAsync` call per `project.Documents` plus `GetProjectDiagnosticsAsync` → every call enters `GetCompilationDiagnosticsAsync`, calls `compilation.GetDiagnostics` and creates/runs `CompilationWithAnalyzers`. Microsoft Learn defines `GetAllDiagnosticsAsync` as one project-wide document-plus-project diagnostic request and describes `DocumentBasedFixAllProvider` as computing project diagnostics before bucketing them by document. Existing integration coverage uses a two-document fixture, while the retained scenario evidence measures Fix All only on the small GuardClauses repository.
- Affected projects or subsystems: CodeActions diagnostic collection and Fix All preparation/replay; Roslyn analyser execution; Workspace query/mutation lease duration and Host responsiveness.
- Remediation direction: Add one project-wide diagnostic collection operation that computes compiler and selected analyser diagnostics once, partitions source diagnostics by document, retains project diagnostics separately and serves all three Fix All callbacks from that result, with cancellation and bounded warnings preserved.
- Validation history: Candidate retained after verifying the controlled integration provider is created through Roslyn's document-based `FixAllProvider.Create` path, checking the official Fix All diagnostic-provider contract, and confirming that compilation reuse does not cache `GetDiagnostics` or `CompilationWithAnalyzers` execution performed by each service call.

## RWMCP-027 — Enforce selector invariants before choosing a target or scope

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp/Protocol/ToolRequestBinder.cs:20-60,283-301`, disconnected rules at `src/Roslyn.Workbench.Mcp.Workspace/Selectors/WorkspaceContractValidator.cs:5-40,49-120`, target precedence at `src/Roslyn.Workbench.Mcp.Workspace/Resolution/WorkspaceResolver.cs:215-268` and scope fallback at `src/Roslyn.Workbench.Mcp.Plugins/Resolution/ToolRequestResolver.cs:59-144`
- Concrete failure scenario: A mutation request supplies a `DocumentSelector` containing both a valid document ID and a different path. Both fields satisfy the published object schema, Host binding accepts the nested selector, and Workspace resolution returns the ID match without checking the contradictory path. The mutation is staged against a different document from the one named by the path. Scoped queries have the analogous broadening failure: `kind: Solution` silently ignores a supplied project selector, while an undefined numeric nested `ScopeKind` is accepted and falls through to an empty project set.
- Supporting call path/evidence: `McpServerToolBase` → `ToolRequestBinder.TryBind` inspects required members, enums and validation attributes only on the top-level request type → nested Workspace selectors deserialize without their declared semantic rules → adapters and handlers call resolver methods directly. `WorkspaceContractValidator` defines exact-one, at-least-one and kind/member invariants, but current-source reference search finds no production caller. `WorkspaceResolver.ResolveDocument` gives a valid ID precedence over path; `ToolRequestResolver.ResolveDocuments` treats Solution as all documents and `ResolveProjects` treats every unrecognised kind as `Projects`. A closed probe against the current Host assembly confirmed that `(ScopeKind)999` binds successfully with no error. Tests exercise the validator in isolation and ordinary binder fields, but never connect the two boundaries.
- Affected projects or subsystems: Abstractions selector contracts, Host MCP binding, Workspace target resolution, Plugins/Plugins.Core query and mutation handlers, Code Actions and server-owned lifecycle/transaction tools.
- Remediation direction: Add recursive request-contract validation at the authoritative Host binding boundary, including nested selector shapes and enum values, and reject contradictions as `InvalidRequest` before any resolver chooses a precedence or fallback. Publish equivalent conditional/exclusive schema constraints where the schema exporter can represent them.
- Validation history: Candidate retained after revisiting unit-1 selector semantics from their Host consumers, proving the validator has no production call site, tracing contradictory document and scope selectors through mutation/query consumers, and dynamically confirming undefined nested enum admission with the installed serializer and current assembly.

## RWMCP-028 — Allow null query data in Full output schemas

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp/Protocol/McpSdkSchemaProvider.cs:51-86,100-103`, `src/Roslyn.Workbench.Mcp/Protocol/ToolSchemaFactory.cs:30-54`, `src/Roslyn.Workbench.Mcp/Protocol/McpPublishedResultSerializer.cs:23-32,62-71` and `src/Roslyn.Workbench.Mcp/Contracts/Results/ToolResultEnvelopeSerializer.cs:19-37`
- Concrete failure scenario: The server starts with `--tool-output-schema-mode Full`, and a plugin or Code Action query legitimately returns `NoChange<TResponse>()`. The adapter publishes `{ "ok": true, "data": null }`, but the advertised success schema requires `data` to be an object of `TResponse`. An MCP client that validates structured content against `outputSchema` rejects a successful tool result as wire-incompatible.
- Supporting call path/evidence: `McpToolProtocolFactory` → `ToolSchemaFactory.CreateOutputSchema(Query, responseType)` → `McpSdkSchemaProvider.GetValueSchema(responseType)` → `NormalizeExportedSchema` collapses an exported `object|null` type to `object` → success schema embeds that non-null object under required `data`. Both query serializers pass nullable `result.Data` to `CreateSuccess`, which deliberately writes JSON null. Focused adapter tests explicitly require successful null data for plugin and Code Action no-change outcomes. A closed current-assembly probe generated `{"type":"object",...}` for query data, confirming that null is excluded rather than merely omitted by a mock schema provider. The MCP SDK contract states that `Tool.OutputSchema` describes `CallToolResult.StructuredContent`.
- Affected projects or subsystems: Host schema publication and result serialization, Plugins and Code Actions query adapters, Full-schema MCP clients and third-party plugin compatibility.
- Remediation direction: Compose query success `data` from a nullable version of the response schema, or change the no-change wire contract to a non-null response object consistently across every query family; add instance-validation tests that validate actual success, no-change and failure payloads against every generated Full schema.
- Validation history: Candidate retained after comparing authoritative generated schemas with the intentional no-change serialization tests and reproducing the non-null data schema through a friend-assembly probe on .NET 10 and MCP SDK 1.4.1.

## RWMCP-029 — Do not mark reference-producing Code Action queries as idempotent

- Severity: P3
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp/Protocol/McpToolProtocolFactory.cs:84-98,101-132`, reference creation at `src/Roslyn.Workbench.Mcp.CodeActions/References/CodeActionReferenceState.cs:62-92`, `src/Roslyn.Workbench.Mcp.CodeActions/Tools/ListCodeActionsTool.cs:156-203` and `src/Roslyn.Workbench.Mcp.CodeActions/Tools/PrepareFixAllTool.cs:138-168`
- Concrete failure scenario: A client retries `list-code-actions` or `prepare-fix-all` because `idempotentHint` is true. Every retry creates new random replay references and returns different action IDs; abandoned entries remain until expiry and consume the bounded reference cache. Repeated safe-retry behaviour can therefore accelerate RWMCP-025 and eventually change a later successful action list into a partial or empty result.
- Supporting call path/evidence: all catalogue queries are mapped mechanically to `ReadOnlyHint = true` and `IdempotentHint = true`. `list-code-actions` calls the reference store for each projected action, whose `TryCreate` generates a new `Guid`; `prepare-fix-all` similarly creates a fresh prepared reference. The installed MCP SDK defines idempotence as repeated calls with the same arguments having no additional effect on the environment. The Host already models the analogous `prepare-error-report` handle-producing query as read-only but explicitly non-idempotent, demonstrating that read-only and idempotent are intentionally separable metadata in this repository.
- Affected projects or subsystems: Host Code Action protocol metadata, Code Action reference cache and MCP clients that use behavioural hints for retry policy.
- Remediation direction: Add explicit idempotence to Code Action tool behaviour metadata and publish false for both reference-producing queries; keep pure plugin queries independently configurable or conservatively classified if their contract cannot guarantee idempotence.
- Validation history: Candidate retained after tracing both Code Action query outputs to random durable-in-process handles, checking the installed SDK annotation semantics and comparing the existing error-report preparation override. Retained as P3 because annotations are advisory, but the bounded-cache side effect and changing identifiers are concrete.

## RWMCP-030 — Isolate extension Console output from the stdio protocol stream

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp/Program.cs:5-11`, `src/Roslyn.Workbench.Mcp/Hosting/RoslynWorkbenchHostApplicationBuilderExtensions.cs:8-17,36-51`, plugin execution at `src/Roslyn.Workbench.Mcp/PluginLoading/MefPluginComposer.cs:8-24` and `src/Roslyn.Workbench.Mcp/ToolExecution/Plugins/PluginQueryMcpServerTool.cs:50-62`
- Concrete failure scenario: An otherwise valid third-party plugin uses `Console.WriteLine` for a startup banner in its constructor or `Configure`, or for diagnostic output in a handler. Those bytes are written to stdout before or between JSON-RPC frames. A stdio MCP client then fails initialisation or loses framing after the tool call, making the complete server unusable even though the plugin's logical operation could have succeeded.
- Supporting call path/evidence: `Program` composes plugins before the MCP stdio transport is registered or started. MEF constructs the export and calls plugin code synchronously; invocation adapters later call singleton handlers in-process. Host logging is correctly cleared and routed to stderr, but no code reserves the raw stdout stream, redirects `Console.Out`, supplies a plugin logging abstraction or rejects console-output use. The public plugin documentation does not state a console-output restriction. A clean current Host EOF run produced zero stdout bytes and all framework logs on stderr, isolating the defect to extension-controlled writes rather than Host logging. Existing external fixtures never write to `Console.Out`, so acceptance coverage does not exercise the boundary.
- Affected projects or subsystems: Executable Host, MCP stdio transport, plugin composition/execution, plugin authoring guidance and every client session using an output-writing extension.
- Remediation direction: Establish an owned raw stdout stream for MCP transport before any extension code executes, route ordinary `Console.Out` writes to stderr or a safe diagnostic sink, expose/document an invocation logging path, and add a packaged plugin fixture that writes during construction/configuration/invocation while protocol initialisation and a subsequent call remain valid.
- Validation history: Candidate retained after tracing extension execution both before and during transport lifetime, confirming Host logger separation does not affect direct console writes, checking all current production uses for any stdout containment and validating the clean-host baseline dynamically.

## RWMCP-031 — Enforce the reviewed allow-list after Sentry SDK enrichment

- Severity: P1
- Confidence: High
- Validation status: Validated against current source and the pinned Sentry 6.8.0 primary source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp/Hosting/RoslynWorkbenchServiceCollectionExtensions.cs:269-285` and `src/Roslyn.Workbench.Mcp/ErrorReporting/Dispatch/SentryErrorReportDispatcher.cs:90-92,110-127`; dependency version at `src/Directory.Packages.props:23`
- Concrete failure scenario: A user reviews a Sentry preview that contains only coarse allow-listed fields and approves its digest. The real Sentry client then adds the current submission stack trace, loaded assembly names and versions, culture/time-zone, memory/thread-pool and runtime/device contexts before queueing the event. Stack frames can contain source paths and modules can identify private plugins or company assemblies, so the provider receives diagnostic data explicitly absent from the preview and listed as excluded from external submission.
- Supporting call path/evidence: `prepare-error-report` → `SentryErrorReportDispatcher.CreatePayload` returns/digests the minimal preview → `submit-error-report` → `SentryErrorReportDispatcher.CreateSentryEvent` creates a minimal event → real `SentryClient.CaptureEvent`. Host options leave `AttachStacktrace` at Sentry's true default, `ReportAssembliesMode` at `Version` and the default event processors installed. The pinned SDK's `SentryClient.DoSendEvent` creates a scope and runs its processors; `MainSentryEventProcessor` adds the current stack/thread, every loaded non-dynamic assembly, culture, time-zone, memory, thread-pool and enriched runtime/device contexts. Existing dispatcher tests inject a mocked `ISentryClient` and inspect the event before this processing, so they prove only the pre-SDK shape.
- Affected projects or subsystems: Host error-reporting trust boundary, Sentry-enabled published builds, report preparation/consent semantics, privacy documentation and provider data governance.
- Remediation direction: Make the final SDK envelope obey the reviewed allow-list by disabling automatic stack and assembly enrichment and applying a final `BeforeSend`/transport validation that strips every unreviewed field, then contract-test the serialised envelope produced by a real `SentryClient` against the preview's permitted diagnostic content.
- Validation history: Candidate retained after comparing the exact Sentry 6.8.0 event-processing source with Host options, the representative preview, excluded-category claims and mock-based tests. `SendDefaultPii = false` prevents machine/user defaults but does not disable the stack, module and runtime enrichments implicated here.

## RWMCP-032 — Remove sensitive temporary records when their lifetime expires

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp/ErrorReporting/Capture/CapturedErrorStore.cs:21-48,52-61` and `src/Roslyn.Workbench.Mcp/ErrorReporting/Preparation/PreparedSubmissionStore.cs:21-50,127-136`
- Concrete failure scenario: One unexpected failure records an exception message and absolute source path, then the server remains running but receives no further error-detail or capture calls. Although the record advertises a one-hour absolute lifetime, it remains strongly referenced in `_records` for days or until process exit. A prepared external payload or successful receipt similarly remains in `_submissions` after its configured expiry while that store is idle.
- Supporting call path/evidence: both singleton stores remove expired entries only from ordinary `Add`/`TryAdd`, `TryGet` or `TryBeginSubmission` calls; neither store owns a `TimeProvider` timer, hosted cleanup loop or lifecycle disposal path. Capacity is bounded, and a later access removes/rejects expired entries, but elapsed time alone never releases the sensitive object. Current expiry tests advance mocked time and then deliberately call `TryGet`, thereby exercising lazy cleanup rather than absolute removal. Public privacy and security documents state that records remain in memory only until absolute expiry and that expiry removes index entries.
- Affected projects or subsystems: Host captured-error and prepared-submission stores, sensitive-memory retention, privacy/lifetime documentation and deterministic time-based tests.
- Remediation direction: Schedule timer-driven cleanup using the injected `TimeProvider`, update the next due time as entries change, dispose the timer with the DI-owned singleton, and add deterministic tests proving physical removal after time advances without a read or write operation.
- Validation history: Candidate retained after distinguishing logical expiry (which is correctly enforced on lookup) from actual retention and confirming no external cleanup caller, lifecycle observer or hosted service touches either store.

## RWMCP-033 — Couple consent validity to submission acquisition

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `src/Roslyn.Workbench.Mcp/ErrorReporting/Tools/SubmitErrorReportTool.cs:71-95,115-121`, consent invalidation at `src/Roslyn.Workbench.Mcp/ErrorReporting/Consent/ErrorReportingConsentService.cs:66-81`, and Workspace notification at `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSessionStore.cs:91-114,261-268`
- Concrete failure scenario: A report has Workspace-scoped approval. `submit-error-report` reads `AllowedForWorkspace` and is descheduled. A concurrent `workspace-close` removes that exact Workspace/epoch grant and returns. The submit call resumes, acquires the prepared handle and queues the report without rechecking consent, so external submission starts after the documented close-time invalidation. Concurrent prompts for different handles can analogously let one accepted “No, and don't ask again” suppress the session while another already-returned approval proceeds to acquisition and dispatch.
- Supporting call path/evidence: submission performs `_store.TryGet` → `_consentService.GetState` → optional asynchronous elicitation → `_store.TryBeginSubmission` → dispatcher. Consent and submission state use independent locks and no consent version, authorisation token or combined operation spans the transition. Workspace close synchronously calls the consent observer under the session-store lifecycle path, but it cannot invalidate an already-read state. Unit tests cover sequential invalidation and each submission decision with mocked collaborators, not the cross-service interleaving.
- Affected projects or subsystems: Host consent service and submission tool, Workspace lifecycle observer integration, external dispatcher boundary and MCP clients issuing concurrent calls.
- Remediation direction: Introduce an authorisation generation/lease that is atomically validated with submission acquisition and invalidated by Workspace close or session suppression, defining the dispatch-start linearisation point explicitly; add deterministic interleaving tests for Workspace invalidation, suppression and duplicate handles.
- Validation history: Candidate retained after tracing close/reload observer notification and both service locks. Same-handle acquisition prevents duplicate sends but does not make consent authorisation atomic with the external-effect transition.

## RWMCP-034 — Revalidate the pinned checkout after repository preparation

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Repositories/RepositoryManager.cs:57-64,67-92`
- Concrete failure scenario: EF Core's repository-owned Windows restore removes three tracked zero-byte sentinel files. The runner checks cleanliness before invoking that restore and never checks again, so the same invocation proceeds to measure a checkout that no longer matches the pinned commit. The next cache reuse fails the earlier cleanliness guard and requires manual repair.
- Supporting call path/evidence: scenario wrapper → `ScenarioApplication.RunAsync` → `RepositoryManager.PrepareAsync` → HEAD/tracked-state validation → repository `Preparation` command loop → unconditionally return the shared checkout. [Code Action batch 7 validation](../../CodeActionBatch7Validation-2026-07-30.md) records the exact native Windows failure, and [Future tasks](../../FutureTasks.md) retains the not-started remediation. No post-preparation diff is captured or restored.
- Affected projects or subsystems: ScenarioRunner repository preparation, external-repository release validation, cache reuse and performance comparability.
- Remediation direction: Capture and validate preparation side effects at the preparation boundary, isolate generated outputs where possible, and explicitly restore only declared pinned-content effects before returning a certified clean checkout; retain rejection of unexplained changes.
- Validation history: Candidate retained after reconciling current control flow with the existing native Windows EF Core evidence. The pre-preparation guard is sound but cannot certify the checkout actually passed to the Host.

## RWMCP-035 — Route commit trace capture through the EventPipe collector

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:1081-1087` and `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Diagnostics/DiagnosticCollector.cs:16-30`
- Concrete failure scenario: A release engineer runs the documented `commit ... --capture-trace` command. After mutation preparation, the runner calls `StartDurationProfile(ProfileKind.Trace, ...)`; that method immediately throws `InvalidOperationException`, so no commit, trace or result report is produced.
- Supporting call path/evidence: `MeasureDurableCommitsAsync` → `RunDurableCommitIterationAsync` trace branch → `DiagnosticCollector.StartDurationProfile` → explicit Trace arm stating that traces use a directly controlled EventPipe session. The general `profile --profile trace` path already uses `TraceCollection.StartAsync`; the commit branch does not. The project builds because the contradiction is runtime enum dispatch.
- Affected projects or subsystems: ScenarioRunner durable-commit profiling, EventPipe diagnostics and release performance evidence.
- Remediation direction: Use `TraceCollection` around `DurableCommitRunner.CommitAsync`, stop/finalise it in the same failure-safe pattern as general trace profiling and add a hermetic selection/lifecycle test.
- Validation history: Candidate retained after checking every `StartDurationProfile` caller. Counter profiling is valid; only the documented commit trace caller passes the deliberately unsupported Trace value.

## RWMCP-036 — Persist completed scenario evidence when a later item fails

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:288-310,463-521`
- Concrete failure scenario: `measure --scenario all` completes several expensive scenarios and a later scenario fails, or a durable commit run completes two iterations and the third fails during shutdown or restoration. Completed results remain only in a local list; `ResultWriter` is never called, and the top-level handler emits only the final exception message. The completed timing, response, validation and cleanup evidence is lost.
- Supporting call path/evidence: each command loop awaits an entire warm-up/measurement batch and appends successful results in memory; result construction and `ResultWriter` calls occur only after the loops. Iteration methods preserve and aggregate cleanup failures but throw before returning a result, and the outer catch has no partial-result writer. Output directories are otherwise unique and already suitable for incremental evidence.
- Affected projects or subsystems: ScenarioRunner measurement, destructive scenario families, result reporting and release diagnostics.
- Remediation direction: Persist an append-safe or atomically replaced run record after every completed scenario/iteration, record the failing item and cleanup/validation status, then return a non-zero exit while retaining all prior evidence.
- Validation history: Candidate retained after tracing ordinary measurement plus durable, cancellation, conflict, crash-recovery and state-sequence result writers. Cleanup survives many failures; report production does not.

## RWMCP-037 — Fail when an in-flight query ignores protocol cancellation

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Scenarios/ToolInvocationRunner.cs:194-242` and `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:328-351`
- Concrete failure scenario: The cancellation delay wins, the runner sends and awaits `notifications/cancelled`, but a regressed Host ignores it and returns normally. The runner sets `OperationCanceled` to false, successfully acquires a transaction lease, writes a report showing zero cancelled invocations and exits with code zero, allowing release automation to treat broken active-operation cancellation as success.
- Supporting call path/evidence: `MeasureCancellationAsync` distinguishes only completion before the delay from completion after notification. After notification, normal invocation completion is accepted without result validation; only `OperationCanceledException` flips the evidence flag. `ScenarioApplication.MeasureCancellationAsync` writes whatever measurements were returned and contains no correctness assertion. The README presents this command as server-side cancellation evidence rather than local-wait cancellation.
- Affected projects or subsystems: ScenarioRunner cancellation validation, Host/Roslyn cancellation evidence and future release gates.
- Remediation direction: Define the expected cancellation outcome explicitly, reject any request that was still active at notification time but completes normally, and retain the lease-recovery check and diagnostic timing in the failed report.
- Validation history: Candidate retained after separating the legitimate `CompletedBeforeCancellation` race from a normal completion after the awaited protocol notification. Earlier recorded EF Core evidence expected and observed all five operations cancelling.

## RWMCP-038 — Serialise access to each shared scenario checkout

- Severity: P2
- Confidence: High
- Validation status: Validated against current source on 2026-07-31
- Exact location: `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:48-55` and `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Repositories/RepositoryManager.cs:30-31,46-92`
- Concrete failure scenario: Two runner processes select the same repository and pinned commit. Both pass the clean check against the same cache directory; one then prepares, mutates or restores files while the other's Host loads or queries them. The second run can observe stale or dirty inputs, fail restoration, or restore files underneath the first run, invalidating both correctness and timing evidence.
- Supporting call path/evidence: cache identity is only repository ID plus commit. `PrepareAsync` returns the bare checkout path; no file lock or lease is retained while `ScenarioApplication` starts the Host and later `RepositoryRestorer` mutates the worktree. Unique execution, state, publish and output directories do not isolate the repository. The production commit lock covers Host commit application, not runner preparation or Git restoration.
- Affected projects or subsystems: ScenarioRunner shared cache, repository preparation/restoration, concurrent manual or future matrix release validation.
- Remediation direction: Acquire an OS-backed exclusive per-checkout lease before cleanliness validation and hold it through preparation, every Host/scenario operation and final restoration/validation; fail fast with the owning run information when contended.
- Validation history: Candidate retained after searching the complete runner for a cache/worktree lock and checking the production commit-lock scope. Concurrent read-only scenarios may coexist in theory, but current preparation, generated assets and destructive families share one mutable worktree without classification or coordination.

## Repository-wide revalidation — 2026-07-31

Every candidate was independently checked again against current source, its direct dependencies and its direct consumers during the fourteen final cross-cutting passes.

- RWMCP-008 was retained and its single identity continues to cover the same uncertified Solution/manifest pairing in open, reload and commit promotion.
- RWMCP-009 through RWMCP-014 were retained without severity or confidence changes.
- RWMCP-015 was rejected again because recovery lock acquisition is non-blocking and cannot carry a stale orphan classification across a wait.
- RWMCP-016 through RWMCP-023 were retained without severity or confidence changes.
- RWMCP-024 was rejected again because supported plugin contexts expose immutable/facade state, cache scope becomes inactive at lease completion and mutation staging remains on the Host-owned lease.
- RWMCP-025 through RWMCP-038 were retained without severity or confidence changes. Cross-project revalidation confirmed the reinforcing interactions between RWMCP-025/RWMCP-029 and RWMCP-034/RWMCP-038 without merging their distinct failure boundaries.
- No candidate met the threshold for RWMCP-039.
