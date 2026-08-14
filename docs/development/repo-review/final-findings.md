# Independent Deep-Dive Review — Final Findings

Date: 2026-08-13

## Repository-level assessment

The current repository has strong structural separation, unusually broad automated coverage and explicit snapshot, transaction, plugin and trust-boundary contracts, but the final validation identified four high-confidence P1 defects and thirteen P2 defects. The highest-consequence risks are loss of an external source edit during commit, restoration of unverified recovery artifacts, process-threatening dependency-cycle traversal, and Code Action range interpretation without a caller-supplied snapshot. The remaining validated findings affect deterministic resource cleanup, plugin contract admission, inspection correctness and result bounds, Code Action compatibility/replay assurance, protocol error classification, error-report context integrity and scenario-cache reliability.

Every one of the twenty ledger candidates was independently retraced against the complete current source, its consumers and the applicable product contracts. Seventeen remain substantiated: four P1/High, twelve P2/High and one P2/Medium. `RWMCP2-016` was rejected after current MCP SDK implementation inspection proved that registered call-tool filters wrap both direct registered tools and the fallback handler. `RWMCP2-018` was rejected because lifecycle invalidation controls later consent decisions rather than retroactively revoking an explicitly started submission request for an immutable prepared payload. `RWMCP2-020` was rejected because the documented release-evidence unit is a successfully completed command rather than a resumable partial run. No candidates were merged because the retained superficially related candidates have distinct root causes and failure outcomes.

## Remediation worklist

Last updated: 2026-08-14

This table is the primary RWMCP2 remediation tracker. Its order is the required fix order; update both this row and the corresponding detailed finding whenever status changes. Rejected candidates are not remediation work items and remain documented in the durable ledger.

| Order | Finding | Severity | Status |
| ---: | --- | --- | --- |
| 1 | `RWMCP2-001` — Code Action ranges are not snapshot-bound | P1 | Complete — confirmed 2026-08-13 |
| 2 | `RWMCP2-004` — Commit can overwrite drift captured during planning | P1 | Complete — confirmed 2026-08-13 |
| 3 | `RWMCP2-005` — Recovery does not verify artifact content before restoring source | P1 | Complete — confirmed 2026-08-13 |
| 4 | `RWMCP2-009` — Dependency-cycle traversal is unbounded, recursive and uncancellable | P1 | Complete — confirmed 2026-08-13 |
| 5 | `RWMCP2-002` — Open Workspace resources survive generic Host shutdown | P2 | Complete — confirmed 2026-08-13 |
| 6 | `RWMCP2-003` — Instance-status close failure skips Workspace resource disposal | P2 | Complete — confirmed 2026-08-13 |
| 7 | `RWMCP2-006` — Plugin admission does not enforce the runtime query response contract | P2 | Complete — confirmed 2026-08-13 |
| 8 | `RWMCP2-007` — Project details always omit effective preprocessor symbols | P2 | Complete — confirmed 2026-08-14 |
| 9 | `RWMCP2-008` — Flow analysis reports a different region from the one it analyses | P2 | Complete — confirmed 2026-08-14 |
| 10 | `RWMCP2-010` — Outer limits do not bound several large nested query payloads | P2 | Complete — confirmed 2026-08-14 |
| 11 | `RWMCP2-012` — The current built-in Code Action compatibility gate fails its implement-interface case | P2 | Complete — confirmed 2026-08-14 |
| 12 | `RWMCP2-013` — The built-in replay audit bypasses the replay path it claims to validate | P2 | Pending — remediation not started |
| 13 | `RWMCP2-014` — Uncancelled cancellation exceptions bypass Workbench diagnostic capture | P2 | Pending — remediation not started |
| 14 | `RWMCP2-015` — The unexpected-exception filter remaps deliberate MCP protocol failures | P2 | Pending — remediation not started |
| 15 | `RWMCP2-017` — Error capture loses valid Workspace selectors when several Workspaces are open | P2 | Pending — remediation not started |
| 16 | `RWMCP2-019` — Failed initial preparation poisons the shared scenario cache | P2 | Pending — remediation not started |
| 17 | `RWMCP2-011` — Prepared Fix All references do not bind the operation that was reviewed | P2 | Pending — remediation not started |

## Remediation and release gates

The validated findings in this report are the authoritative RWMCP2 remediation worklist. Each finding must follow the approval-led process in [`DeepDiveReview.md`](../DeepDiveReview.md): current-source revalidation and explanation, proposed design, explicit approval, implementation, required executable validation, a first user code review and confirmation, staging of that complete first-confirmed baseline, an independent Review Agent pass, unstaged correction and re-review of any substantiated feedback, a second user review comparing the staged baseline with those unstaged corrections, final confirmation, durable status update and then user commit. No finding is complete or ready to commit until its final Review Agent pass has no remaining actionable defects or regression gaps and the user has given the second confirmation; any material change after that pass requires another review.

The repository-level assessment and finding narratives preserve the independent review baseline. Per-finding status and remediation-outcome records supersede that baseline as confirmed remediation proceeds.

After all RWMCP2 items are confirmed, committed and validated through the complete release gate, the repository must receive the fresh post-remediation release-candidate review defined in [`DeepDiveReview.md`](../DeepDiveReview.md). That review must use a new agent and `RWMCP3-###` identifiers, exclude RWMCP2 artefacts, Git history and prior conversation context from its evidence, and specifically recheck remediation closure, interactions between fixes, end-to-end boundaries and the truthfulness of regression coverage before v1 release readiness is accepted.

## Validated findings

### RWMCP2-001 — Code Action ranges are not snapshot-bound

**Status:** Complete — remediated, independently reviewed and confirmed on 2026-08-13

**Severity:** P1  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.CodeActions/Contracts/ListCodeActionsRequest.cs:8-20`; `src/Roslyn.Workbench.Mcp.CodeActions/Tools/ListCodeActionsTool.cs:32-46`; `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Requests/CodeActionToolRequestResolver.cs:57-101`; `src/Roslyn.Workbench.Mcp.CodeActions/References/CodeActionInfoFactory.cs:40-54`

A caller can retain a document range from epoch E or transaction revision R, allow the Workspace text to change before that range, and then list Code Actions using the stale coordinates. `ListCodeActionsRequest` has no expected snapshot, `ListCodeActionsTool` forwards the range, and `CodeActionToolRequestResolver` resolves the current document and validates only its current bounds. `CodeActionInfoFactory` then binds the action selected from those reinterpreted coordinates to the current snapshot. Replay snapshot validation therefore cannot prevent the error: it protects the newly selected action, not the caller's original coordinate meaning. A subsequent stage can validly mutate code different from that originally selected.

**Remediation outcome:** `list-code-actions` now requires a non-null `expectedSnapshot` and validates it before resolving the document or interpreting the UTF-16 range. All repository callers, schemas, documentation and scenarios supply the originating snapshot, including implicit scenario-runner discovery. Unit, integration, Host schema, published-host acceptance and representative GuardClauses, Serilog and EF Core scenario coverage passed. The complete Code Action audit retained only the pre-existing `RWMCP2-012` failure. The independent Review Agent identified one stale Host schema assertion; after correction, the Host fast suite passed 481/481 and the repeated Review Agent pass returned no findings.

### RWMCP2-004 — Commit can overwrite drift captured during planning

**Status:** Complete — remediated, independently reviewed and confirmed on 2026-08-13

**Severity:** P1  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Transactions/TransactionCommitService.cs:144-150,175-227,274-278,732-745`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitPlanner.cs:151-218`; `src/Roslyn.Workbench.Mcp.Workspace/ChangeDetection/WorkspaceInputCertification.cs:23-47`; `src/Roslyn.Workbench.Mcp.Workspace/ChangeDetection/WorkspaceInputChangeMonitor.cs:249-256,342-346`

If an editor changes a staged target after commit's baseline `HasChanged` check but before planning reads the target, the planner adopts the external bytes as its `OriginalHash` and backup. Writer revalidation proves only that the target still equals that planner-captured hash, application overwrites it with transaction bytes, and commit certification treats the target as commit-owned while replaying watcher events. Neither immediate revalidation nor later input certification compares the planner-captured bytes with the transaction's Workspace baseline, so the external edit can be silently lost and its backup deleted after successful commit.

**Remediation outcome:** Commit planning now compares every replacement and deletion target's current disk bytes with the original-byte checksum retained by Roslyn for the transaction baseline, using the baseline `SourceText`'s declared SHA-1 or SHA-256 algorithm before any disk bytes can become recovery state. A mismatch transitions the transaction to `TransactionConflicted` without applying or overwriting the external bytes; create existence checks, recovery capture and immediate per-entry writer revalidation remain intact. Focused tests cover replacement and deletion drift, SHA-1 and SHA-256 checksum reproduction, BOM and malformed/non-round-trippable bytes, and a real component commit that injects an external write after initial validation but before the real planner reads the target. Workspace unit and integration suites passed 979/979 and 90/90, the solution and affected `latest-all` analyzer builds passed, and a traced EF Core commit replaced 948 files with 418.57 ms planning time, a 17.62 MB peak private-memory increase and clean checkout/state restoration. The first independent Review Agent pass identified the non-round-trippable-byte defect in the initial re-encoding design; the checksum implementation and regression coverage corrected it, and the repeated Review Agent pass returned no findings.

### RWMCP2-005 — Recovery does not verify artifact content before restoring source

**Status:** Complete — remediated, independently reviewed and confirmed on 2026-08-13

**Severity:** P1  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Recovery/CommitRecoveryStore.cs:226-235,501-542`; `src/Roslyn.Workbench.Mcp.Workspace/Transactions/WorkspaceCommitWriter.cs:43-77,182-252,284-293`; `src/Roslyn.Workbench.Mcp.Workspace/Recovery/WorkspaceCommitRecoveryService.cs:54-68`

After an interrupted applying commit, a truncated or altered backup can remain path-valid and under the size limit. The recovery store validates containment, size and the manifest's hash syntax but does not compare artifact bytes with those hashes. `WorkspaceCommitWriter.RestoreAsync` confirms that the target is in the applied state and then writes the unchecked backup, or moves an unchecked delete marker, before returning `Restored`. `WorkspaceCommitRecoveryService` persists that terminal result and deletes the recovery evidence. No later target verification establishes that restored bytes equal `OriginalHash`, so corrupt bytes can replace source and the remaining recovery evidence can be removed.

**Remediation outcome:** Commit application now authenticates staged artifact bytes against `IntendedHash` before writing them. Recovery authenticates replacement backups and delete-marker bytes against `OriginalHash` before consuming them, retains integrity mismatches as `RecoveryConflict`, and revalidates the complete original target existence, hash and Unix-mode state before returning `Restored` and permitting evidence cleanup. Delete planning and manifest validation now preserve and require the original Unix mode so permission-only marker corruption is also retained as a conflict. Unit and real-filesystem integration coverage exercises corrupted staged artifacts, backups, delete markers, final-state certification and Unix marker-mode changes. Workspace unit and integration suites passed 989/989 and 94/94, the solution and affected `latest-all` analyzer builds passed, and the repeated Review Agent pass returned no findings after its initial Unix delete-mode finding was corrected. The user gave final confirmation.

### RWMCP2-009 — Dependency-cycle traversal is unbounded, recursive and uncancellable

**Status:** Complete — remediated, independently reviewed and confirmed on 2026-08-13

**Severity:** P1  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Plugins/Analysis/DependencyAnalysisService.cs:15-58,60-106,372-389,508-565,673-707`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/FindDependencyCyclesTool.cs:6-36`

`FindDependencyCyclesTool` passes its result count only as a final output limit. `DependencyAnalysisService` first builds source or type graphs without node or edge limits and traverses them using a recursive local function with no cancellation checks, then applies `maxResults`. A repository with a sufficiently deep dependency chain can exhaust the process stack, and a large graph continues consuming CPU and memory even for zero or very small result limits or after cancellation. Current [.NET documentation](https://learn.microsoft.com/dotnet/api/system.stackoverflowexception?view=net-10.0) confirms that user code cannot catch `StackOverflowException` and the process terminates by default, so this can terminate the Host rather than merely fail one tool call.

**Remediation outcome:** Dependency-cycle requests now impose schema-published node and edge work bounds with defaults of 25,000/100,000 and maxima of 100,000/500,000, reject incomplete analysis through a typed result, and retain `cyclesLimit` solely as the output bound. Project analysis uses Roslyn's dependency graph; Namespace and Type discovery stop when the node bound is exceeded; Type and Project identities keep multi-target framework compilations distinct. Cycle detection is iterative and cancellation-aware, avoiding recursion and polling cancellation throughout traversal. Unit and component coverage exercises Project, Namespace and Type behaviour, node/edge rejection, cancellation, 20,000-node deep cyclic and acyclic graphs, multi-target identity and schema publication. Plugins, Core and component suites passed 146/146, 271/271 and 9/9, focused schema integration passed 14/14, and EF Core completed default Type analysis over 15,981 nodes and 44,050 edges in approximately 14.3 seconds. The independent Review Agent found no code defect and one missing contract-catalogue entry; after that documentation correction, the user gave final confirmation. A positive cyclic Roslyn Project fixture is intentionally absent because supported Roslyn solution APIs reject circular project references before the dependency graph can represent them; positive strongly connected-component behaviour is covered at the detector boundary.

### RWMCP2-002 — Open Workspace resources survive generic Host shutdown

**Status:** Complete — remediated, independently reviewed and confirmed on 2026-08-13

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Workspace/State/WorkspaceSessionStore.cs:5-23`; `src/Roslyn.Workbench.Mcp/Hosting/RoslynWorkbenchServiceCollectionExtensions.cs:107-115`; `src/Roslyn.Workbench.Mcp/Program.cs:9-13`; `src/Roslyn.Workbench.Mcp.Workspace/Loading/LoadedWorkspace.cs:18-20`; `src/Roslyn.Workbench.Mcp.Workspace/ChangeDetection/WorkspaceInputManifest.cs:23-25`

Opening a Workspace transfers a disposable loaded Workspace and input manifest into the singleton session store. Explicit `workspace_close` disposes them, but the store is not disposable and no Host shutdown service drains it. `Program` relies on generic Host disposal, which has no ownership path to these session-held resources. Ending a stdio session with open Workspaces therefore leaves `MSBuildWorkspace` instances and filesystem watchers undisposed until process teardown; this is directly observable for in-process Host start/stop and prevents deterministic resource release.

**Remediation outcome:** The session store now provides an atomic terminal drain, and a Host-managed Workspace lifecycle service invokes it after the MCP transport has stopped and completed in-flight requests. Every drained session receives best-effort status closure, input-manifest disposal and loaded-Workspace disposal even when another session fails. Terminal cleanup failures are logged without disrupting the remaining Host shutdown process, while the lower-level lifecycle retains the complete failures. Unit and integration coverage proves atomic and repeated drains, cleanup of every session, transport-before-Workspace shutdown ordering, terminal failure logging and disposal of a real stored session through in-process Host shutdown. The solution build, 1,000 Workspace unit tests, 94 Workspace integration tests, 484 Host unit tests and 71 Host integration tests passed; affected `latest-all` analyzer builds were clean, and the independent Review Agent returned no findings.

### RWMCP2-003 — Instance-status close failure skips Workspace resource disposal

**Status:** Complete — remediated, independently reviewed and confirmed on 2026-08-13

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Workspace/Lifecycle/WorkspaceLifecycleService.cs:231-242`; `src/Roslyn.Workbench.Mcp.Workspace/Coordination/WorkspaceInstanceStatusPublisher.cs:175-189`; `src/Roslyn.Workbench.Mcp.Workspace/Coordination/WorkspaceInstanceStatusHandle.cs:49-52`

`WorkspaceLifecycleService.CloseAsync` removes the session, awaits instance-status close, and only then disposes the input manifest and loaded Workspace, without a `finally`. `WorkspaceInstanceStatusPublisher` removes its handle before directly disposing the underlying stream, which can throw. A status-stream close failure therefore skips both Workspace resource disposals after the authoritative session has already disappeared, and no retry through Workspace selection is possible. Tests model throwing status-stream disposal at the publisher boundary but do not connect it to lifecycle cleanup.

**Remediation outcome:** Explicit close and terminal shutdown now share a focused session-cleanup service that independently attempts advisory status closure, input-manifest disposal and loaded-Workspace disposal. It preserves and rethrows a single failure with its original stack or aggregates multiple failures only after every owned resource has been attempted. `workspace_close` continues to propagate cleanup failure to its caller, while only the terminal Host boundary logs and suppresses it. Focused unit coverage exercises each individual cleanup failure, three simultaneous failures, successful explicit-close delegation and exact failure propagation through `WorkspaceLifecycleService.CloseAsync`. The complete validation and independent Review Agent result are recorded with `RWMCP2-002`, with which this ownership remediation was implemented and reviewed.

### RWMCP2-006 — Plugin admission does not enforce the runtime query response contract

**Status:** Complete — remediated, independently reviewed and confirmed on 2026-08-13

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp/Configuration/StartupOptions.cs:23-25`; `src/Roslyn.Workbench.Mcp/PluginLoading/PluginTransportSchemaPreflight.cs:14-42`; `src/Roslyn.Workbench.Mcp/PluginLoading/PluginCatalogEntryMaterializer.cs:26-51`; `src/Roslyn.Workbench.Mcp/PluginLoading/QueryResponseContractInspector.cs:24-70`; `src/Roslyn.Workbench.Mcp/ToolExecution/Plugins/PluginQueryMcpServerTool.cs:74-78`; `src/Roslyn.Workbench.Mcp/Protocol/Results/ToolResultEnvelopeSerializer.cs:19-38,179-188`

The default `ToolOutputSchemaMode.Omit` validates the request but skips response metadata/schema creation. The response inspector explicitly excludes `string` from raw collection rejection and has no scalar/object-shape rule, while the public generic query contract permits such a response. The catalogue can consequently enable and advertise a query returning `string`, but a successful invocation reaches `ToolResultEnvelopeSerializer`, whose success data must serialize as a JSON object, and throws. Converter or metadata failures for otherwise object-shaped responses are likewise deferred in omission mode. Narrow current tests confirm each seam independently—scalar serialization throws and omission skips response validation—but do not join admission to invocation.

**Remediation outcome:** Query handlers now require a Plugins-owned `IQueryResponse` root DTO, while reusable `BoundedCollection<TItem>` values remain neutral Abstractions components nested inside those DTOs. Plugin admission always generates response schema metadata and validates the runtime serializer's exact JSON contract before constructing handlers, regardless of whether output schemas are published. Only object contracts are admitted; scalar, collection, dictionary and top-level custom-converter contracts are rejected before advertisement, and runtime object projection remains as defence in depth. Bundled responses, external fixtures, analyser models, public API locks and plugin-author guidance were migrated together. Unit, plugin, analyser, integration and published-host acceptance coverage passed, including default output-schema omission, malformed response contracts and explicit JSON-kind rejection; affected `latest-all` analyser builds were clean. The first independent Review Agent pass returned no findings. After the user narrowed admission to dedicated object DTOs, the repeated pass found one misleading RWMCP014 remediation message; the message and its descriptor contract test were corrected, and the user gave final confirmation.

### RWMCP2-007 — Project details always omit effective preprocessor symbols

**Status:** Complete — remediated, independently reviewed and confirmed on 2026-08-14

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/CompilationOptionsInfo.cs:33-36`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Projections/InspectionProjectionFactory.cs:60-75`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetProjectDetailsTool.cs:67-75`

The published project-details contract claims to return preprocessor symbols, but `InspectionProjectionFactory.CreateCompilationOptionsInfo` assigns an empty collection in both the C# and non-C# branches. `GetProjectDetailsTool` publishes that projection without another source of symbols. A C# project with `DEBUG`, target-framework constants or custom `DefineConstants` therefore reports none of them, even though the current document parse options expose the effective names and sibling document-option projection already reads them.

**Remediation outcome:** `get-project-details` now supplies the selected project's parse options to the compilation-options projection. Project and document option responses share one null-safe projection of `ParseOptions.PreprocessorSymbolNames`, materialised in ordinal order while preserving the existing wire contract. Dedicated `InspectionProjectionFactoryTests` exercise every public factory method, every reachable branch, C# and language-neutral option handling, analyzer configuration, analyzer and metadata references, associated types, source and metadata definitions, modifiers, diagnostic severities, parameters, projects, references and null/default projections; the public tool test independently proves parse-option forwarding. Measured factory coverage is 100% of lines and 98.57% of branches. The only uncovered branch is the defensive `Project.AssemblyName ?? string.Empty` fallback: Roslyn's public project-construction API rejects a null assembly name before a real `Project` can be obtained, so covering it would require invalid artificial Roslyn state and qualifies for the repository's documented defensive Roslyn guard exception. A real MSBuild integration profile proves that explicit `DefineConstants` and the SDK-provided `NET10_0` symbol reach the public tool result. The solution build passed with zero warnings/errors; Core unit/contract tests passed 296/296; Core integration tests passed 10/10; and `latest-all` analyzer builds for the production, unit-test, integration-support and integration-test projects passed with zero warnings/errors. No acceptance-consumed artefact was changed. The first independent Review Agent pass found no production or test defect and one P3 tracking defect; after its unstaged correction, the repeated independent pass returned no findings. The user gave final confirmation on 2026-08-14. Residual risks are limited to the single-target integration fixture and helper-level non-C# coverage because Workspace admission currently supports C# only.

### RWMCP2-008 — Flow analysis reports a different region from the one it analyses

**Status:** Complete — confirmed 2026-08-14

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeControlFlowTool.cs:6-56`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/AnalyzeDataFlowTool.cs:6-44`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/FlowAnalysisRegionResolver.cs:7-241`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/AnalyzeControlFlowRequest.cs:3-19`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/AnalyzeDataFlowRequest.cs:3-19`

Both tools resolve the caller's exact location, find the containing statement, and invoke Roslyn's single-statement flow analysis overload on that full statement. Their response nevertheless reports the original requested location as `Region`. An expression-only or partial-statement request therefore labels statement-wide results as if they described only the narrower caller range. Bounds and snapshot validation prevent stale or invalid locations, but nothing reconciles the reported region with the syntax actually analysed; existing examples select whole statements and do not expose the mismatch.

**Remediation outcome:** Both tools now share a strict region resolver. Data-flow analysis accepts an exact expression, an exact complete statement or an exact contiguous range of sibling statements; control-flow analysis accepts an exact complete statement or exact contiguous sibling range. Empty, partial, mixed-nesting and cross-body selections are rejected before analysis. Valid statement ranges use Roslyn's first/last-statement overloads, and the response returns the exact selected `ResolvedLocation`; it remains a pointer containing document identity/path, UTF-16 span, line/column and Workspace snapshot identity, with no source text or file bytes added to the MCP payload. The resolved-region classes are passive data carriers only; following first-review feedback, the Roslyn analysis calls were moved into the consuming tools so operational behaviour remains with the tool rather than the resolved data. Public request documentation, tool descriptions and design/contract documentation describe the supported shapes. Unit coverage exercises successful and unsupported exact expressions, exact statements, block/switch/top-level ranges through both Roslyn flow APIs, partial and cross-body rejection, malformed top-level adjacency and exact returned pointers; real MSBuild integration coverage proves exact expression and complete-statement execution and rejects the former header-only selection. The solution build passed with zero warnings/errors; Core unit/contract tests passed 309/309; Core integration tests passed 10/10; and `latest-all` analyser builds for the production, unit-test and integration-test projects passed with zero warnings/errors. Measured coverage is 100% for the new region value types, expression data-flow tool lines and all normally reachable resolver branches. The remaining uncovered paths are defensive Roslyn null branches in both tool analysis calls and in syntax-root, semantic-model and token-parent acquisition; those states cannot be produced through the real SourceTree-backed public flow without artificial Roslyn objects and are retained under the repository's documented defensive Roslyn guard exception. No acceptance-consumed artefact was changed. A pre-isolation primary-agent pass found one P2 edge case: filtering compilation-unit members could treat top-level statements separated by a type or namespace declaration in malformed source as contiguous. The unstaged correction now preserves only adjacent runs of `GlobalStatementSyntax`, and focused malformed-source coverage proves rejection. At the user's request, a fresh subagent without conversation context then performed the required Review Agent pass and found one further P2 defect: an exact expression can produce a non-null Roslyn `DataFlowAnalysis` with `Succeeded == false`, which the tool would have published as successful. The unstaged correction now rejects unsuccessful analyses, with a focused `System.Console` namespace/type-expression regression test. A second fresh context-free subagent repeated the complete Review Agent pass after both material corrections and returned no findings. Its residual-risk note identified that switch-section and top-level ranges executed real Roslyn data-flow analysis but not the control-flow tool path; two additional tool-level tests now execute those exact ranges through `AnalyzeControlFlowTool`, assert success and verify the exact returned pointer. A third fresh context-free Review Agent subagent reviewed the expanded proposal and returned no findings; residual risk is limited to defensive Roslyn states unreachable through the normal SourceTree-backed flow and unusual malformed-syntax recovery shapes beyond the focused top-level case. The user gave final confirmation on 2026-08-14.

### RWMCP2-010 — Outer limits do not bound several large nested query payloads

**Status:** Complete — remediated, independently reviewed and confirmed on 2026-08-14

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/GetSolutionStructureRequest.cs:6-32`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetSolutionStructureTool.cs:107-155`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Contracts/Inspection/GetDocumentOutlineRequest.cs:6-16`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Projections/DocumentOutlineProjectionFactory.cs:3-66`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/FindCallersTool.cs:23-76`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/FindDuplicateCodeTool.cs:84-118`; `src/Roslyn.Workbench.Mcp.Plugins.Core/Inspection/GetControlFlowGraphTool.cs:70-80,86-115`

Several public `maxResults`-style controls bound only a top-level collection while each admitted item can retain an unbounded nested graph: all documents per selected project, recursive outline descendants, every caller location and context, every duplicate occurrence, or all control-flow operations including full syntax. The Host serializer has no independent byte or nested-item budget. A request respecting its documented outer limit can therefore produce a repository-scale response, high allocation pressure and long serialization that the apparent limit does not constrain. These are manifestations of one shared result-bounding contract defect rather than independent findings.

**Remediation outcome:** Solution structure now publishes bounded per-project documents and project references; document outline and operation-tree projections enforce global node and safe depth limits with explicit truncation; callers and duplicate groups publish bounded nested collections; and CFG blocks publish bounded operation metadata. Caller locations remain paired with explicitly requested context, while duplicate occurrences, CFG operations and operation-tree nodes return exact source pointers instead of embedded source or syntax payloads. Bounds are schema-published, zero-limit and deterministic-order semantics are covered, and contract/design documentation reflects the replaced wire shapes. Core tests passed 316/316, Host tests 499/499, Core integration 10/10, Host integration 71/71 and the complete published-host acceptance wrapper 63/63; the solution build passed without warnings or errors, changed production analyser builds were clean, and representative Serilog Scenario Runner workloads successfully recorded the new nested bounded collections with clean shutdown and repository restoration. The first fresh context-free Review Agent pass returned no findings and identified only published-boundary residual coverage opportunities; the unstaged follow-up added real published-Host assertions for two independently truncated callers, a truncated CFG operation collection and root operation-tree child truncation. The complete acceptance wrapper passed again, and a second fresh context-free Review Agent pass found no defects or material test gaps. The user gave final confirmation on 2026-08-14.

### RWMCP2-012 — The current built-in Code Action compatibility gate fails its implement-interface case

**Status:** Complete — remediated, independently reviewed and confirmed on 2026-08-14

**Severity:** P2  
**Confidence:** High  
**Location:** `test/Roslyn.Workbench.Mcp.CodeActions.AuditTest/BuiltInCodeActionAuditCases.cs:948-960`; `test/Roslyn.Workbench.Mcp.IntegrationTestSupport/InspectionSampleFixture.cs:127-138`; `test/TestAssets/Workspaces/InspectionSample/Base/CandidateRefactorings.cs:31-38`; `.github/workflows/code-action-audit.yml:31-52`

The checked-in compatibility inventory requires the built-in implement-interface refactoring to be offered and replayable for its controlled fixture, and CI executes that audit as a required project. A fresh complete audit run against the current source produced 119 passes and one failure: this case returned `NotOffered` instead of `OfferedAndReplayable`. The same stale fixture shape appears in shared inspection assets. This is a current compatibility-gate failure, not an inferred provider risk.

**Remediation outcome:** The shared inspection fixture now provides a genuine interior class-body insertion line for `InterfaceImplementationCandidate`, so the existing zero-length selector reaches the position required by the pinned implement-interface provider. The provider ID, exact action title, expected mutation, selector semantics and production code remain unchanged. The complete Code Action audit passed 120/120, Code Actions integration passed 18/18, Plugins.Core integration passed 10/10, Host integration passed 71/71, and the solution and changed-project `latest-all` analyser builds completed without warnings or errors. A fresh context-free Review Agent pass found no defects or material coverage gaps and confirmed that the separate `RWMCP2-013` production replay limitation remains unchanged. The user reviewed the implementation and confirmed completion on 2026-08-14, explicitly waiving redundant later review stages for this item only because that independent pass had already completed with no findings.

### RWMCP2-013 — The built-in replay audit bypasses the replay path it claims to validate

**Status:** Pending — remediation not started

**Severity:** P2  
**Confidence:** High  
**Location:** `test/Roslyn.Workbench.Mcp.CodeActions.AuditTest/BuiltInCodeActionAuditHarness.cs:79-127,150-199`; `.github/workflows/code-action-audit.yml:31-52`; `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Replay/CodeActionResolver.cs:114-209`; `src/Roslyn.Workbench.Mcp.CodeActions/Staging/CodeActionStager.cs:38-82`

For cases labelled replayable, the audit discovers providers directly, finds matching actions, selects `matching[0]`, and calls that action's `GetOperationsAsync`. It does not create a production `ActionId`, store the recipe, rediscover uniquely through `CodeActionResolver`, or pass through `CodeActionStager`. A passing audit can therefore coexist with unstable action identity, ambiguity, reference expiry/invalidation, changed rediscovery, or staging failure—the precise production behaviours the replay label appears to assure. CI's test-count gate confirms case execution, not traversal of that boundary.

### RWMCP2-014 — Uncancelled cancellation exceptions bypass Workbench diagnostic capture

**Status:** Pending — remediation not started

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp/ToolExecution/UnhandledToolExceptionFilter.cs:35-45`; `test/Roslyn.Workbench.Mcp.Test/ToolExecution/UnhandledToolExceptionFilterTests.cs:77-90`

The Workbench call-tool filter rethrows every `OperationCanceledException` without confirming that the current MCP request token is cancelled. A handler can throw with an unrelated token or throw an uncancelled cancellation exception; this bypasses Workbench unexpected-error logging, correlation, retained details and its structured `UnhandledException` contract. Inspection of the current MCP SDK's combined call-tool pipeline shows that the outer SDK dispatcher then catches this exception when the request token is still active and returns its generic error result. The failure is therefore contained by the SDK but loses the Workbench diagnostic and user-facing status path.

### RWMCP2-015 — The unexpected-exception filter remaps deliberate MCP protocol failures

**Status:** Pending — remediation not started

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp/Hosting/RoslynWorkbenchHostApplicationBuilderExtensions.cs:33-46`; `src/Roslyn.Workbench.Mcp/Hosting/PluginMcpRequestHandler.cs:39-57`; `src/Roslyn.Workbench.Mcp/ToolExecution/UnhandledToolExceptionFilter.cs:35-78`; `test/Roslyn.Workbench.Mcp.IntegrationTest/Protocol/PluginMcpRequestHandlerProtocolIntegrationTests.cs:11-95`

The fallback plugin handler deliberately throws `McpProtocolException` for protocol-level failures such as an unknown tool. The registered Workbench filter surrounds the combined SDK dispatch path and catches that exception in its general unexpected-exception branch, logging/capturing it and returning a normal structured `UnhandledException` tool result. Because the filter has already returned a result, the SDK's outer protocol handling never sees the deliberate protocol exception. Unknown-tool and equivalent fallback failures are thus misclassified as internal correlated errors rather than preserving their intended MCP protocol semantics.

### RWMCP2-017 — Error capture loses valid Workspace selectors when several Workspaces are open

**Status:** Pending — remediation not started

**Severity:** P2  
**Confidence:** High  
**Location:** `src/Roslyn.Workbench.Mcp/ErrorReporting/Capture/ErrorCaptureService.cs:79-109,234-248`; `src/Roslyn.Workbench.Mcp.Abstractions/Workspace/Selectors/WorkspaceSelector.cs:6-27`; `src/Roslyn.Workbench.Mcp/Protocol/ToolRequestBinder.cs:361-370`; `src/Roslyn.Workbench.Mcp/ErrorReporting/Tools/PrepareErrorReportTool.cs:74-108`

Public binding accepts Workspace selectors by canonical ID, alias or path and treats JSON property names case-insensitively. Error capture examines only exact lower-case `workspace` or `workspaceId` properties and retains the value only when it parses as a canonical ID; sole-Workspace fallback works only when exactly one session is open. With several Workspaces, an unexpected error from a valid alias/path selector or differently cased property loses its Workspace context. Error-report preparation then cannot include or apply the appropriate Workspace-scoped context and consent semantics, and no later layer reconstructs the selector from the bound request.

### RWMCP2-019 — Failed initial preparation poisons the shared scenario cache

**Status:** Pending — remediation not started

**Severity:** P2  
**Confidence:** High  
**Location:** `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Repositories/RepositoryManager.cs:23-80`; `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Repositories/ExternalCommand.cs:8-43`; `tools/Roslyn.Workbench.Mcp.ScenarioRunner/Application/ScenarioApplication.cs:50-64`

Initial external-repository preparation clones and checks out directly in the persistent shared cache directory. Cancellation or command failure after `.git` is created leaves that partial repository in place. On the next locked preparation, `RepositoryManager` sees `.git`, skips clone, and validates the pinned clean checkout; the incomplete cache fails that validation repeatedly and is neither repaired nor replaced. Process-tree termination limits orphan processes but cannot restore repository state. The release wrappers reuse the same cache, so a single interrupted first preparation can poison subsequent scenario runs.

### RWMCP2-011 — Prepared Fix All references do not bind the operation that was reviewed

**Status:** Pending — remediation not started

**Severity:** P2  
**Confidence:** Medium  
**Location:** `src/Roslyn.Workbench.Mcp.CodeActions/Tools/PrepareFixAllTool.cs:82-169`; `src/Roslyn.Workbench.Mcp.CodeActions/References/CodeActionReplayRecipe.cs:3-20`; `src/Roslyn.Workbench.Mcp.CodeActions/Resolution/Replay/PreparedFixAllResolver.cs:25-76`; `src/Roslyn.Workbench.Mcp.CodeActions/Staging/CodeActionStager.cs:38-82`

Preparation creates a Fix All action, evaluates and processes its candidate, counts changed documents and enforces the requested maximum. The stored recipe adds only the Fix All scope; it does not bind the reviewed operation, its changed-document identity or the approved maximum. Staging later rediscovers the provider, creates a fresh Fix All action, evaluates it and returns its candidate without comparing it with the prepared candidate or reapplying the maximum. A provider whose output changes between calls can therefore stage more or different changes than were reviewed. The path is complete and has no later prevention, but confidence is Medium because occurrence depends on provider instability or changing provider-external state; deterministic built-in providers will usually recreate the same operation.

## Notable test gaps

- No current test supplies a stale Code Action list range across reload or transaction revision because the public list contract cannot carry the originating snapshot.
- Commit coverage exercises drift before commit or after planning/application, but not an external write between the initial baseline check and planner capture.
- Recovery tests use valid artifacts and do not corrupt a backup, staged artifact or delete marker before startup recovery.
- Dependency and nested-result tests do not exercise repository-scale depth, cancellation during graph construction/traversal, stack depth or serialized response size.
- Plugin tests independently preserve the default admission and scalar-serialization behaviours but do not invoke a response-only-invalid plugin admitted under output-schema omission.
- Workspace shutdown tests do not retain an open Workspace through generic Host disposal, and lifecycle tests do not connect a throwing status close to owned Workspace cleanup.
- Flow-analysis fixtures use whole statements, so they do not compare a partial requested region with the actual statement analysed.
- The Code Action audit does not exercise production replay, and its current implement-interface case fails before it can establish the claimed compatibility outcome.
- Error-report tests do not cover alias/path/case-variant Workspace selectors with several open Workspaces.
- The Scenario Runner has no executable failure-injection coverage for partial initial preparation and repeated cache use.

## Areas not reviewable with high confidence

- `RWMCP2-011` remains Medium confidence because the source proves that preparation and staging can evaluate different Fix All operations, but the practical frequency depends on provider determinism and provider-external state that the controlled fixtures do not represent.
- Platform-specific filesystem behaviour—especially Windows sharing violations, reparse handling and atomic replacement—was assessed from current implementation and tests but was not reproduced across every supported filesystem/platform combination during final validation.
- Real external repository, EventPipe and performance scenarios were not rerun, so operational conclusions rely on complete current call-path inspection and checked-in orchestration rather than fresh external-system execution.
- Sentry/network transport behaviour was not exercised against a live external service; the review remained within configured source, local trust-boundary tests and current package behaviour.
- Third-party plugin and Roslyn-provider behaviour beyond the checked-in packages, fixtures and current installed dependencies cannot be exhaustively enumerated; findings that depend on variability state that limitation explicitly.

## Validation limitations

The review used only the current checked-out tracked source, tests, configuration, project/package definitions and normative design documentation. It did not inspect Git diffs, history, commits, branches, tags, stashes, reflogs, deleted or renamed review artefacts, external backups or earlier review findings outside this review's durable artefacts.

Roslyn MCP navigation was unavailable in this environment, so current-source navigation used direct file inspection and repository text search. For the three MCP filter-pipeline candidates, the currently installed `ModelContextProtocol.Core` 1.4.1 assembly was decompiled in a temporary directory outside the repository to establish the actual external call-tool composition; that evidence rejected `RWMCP2-016` and refined, but did not invalidate, `RWMCP2-014` and `RWMCP2-015`.

A fresh complete Code Action audit ran 120 tests and reproduced the single `RWMCP2-012` failure; 119 passed. A focused Host run covering result serialization, plugin preflight, response inspection and the Workbench exception filter passed 21 tests while confirming the individual seams described above. Deeper disposition review compared consent and Scenario Runner candidates with their current normative product contracts and used current official .NET documentation to confirm the process consequence of recursive stack exhaustion. The complete acceptance suite and external-repository scenario suite were not run during this docs-only final stage, consistent with repository execution policy; their absence is reflected in the confidence and limitation statements. No production code or test code was modified.
