# Performance Investigation Tracker

Date: 2026-07-20

## Purpose

This document tracks performance investigations arising from the initial WSL baseline and the subsequent native-filesystem Windows and WSL comparison captured on 2026-07-20. Items are ordered by dependency and expected impact: shared measurement and Workspace costs come before individual tool tuning because they contribute to every downstream observation.

The measurements are evidence for investigation, not elapsed-time release thresholds. Each optimisation must preserve response contracts, deterministic ordering, bounds, snapshot semantics, external-change detection and transaction safety.

## Initial WSL baseline conditions

- Environment: WSL 2, Ubuntu 24.04, .NET 10, x64, 16 logical processors.
- Target repositories are pinned to exact commits. This initial baseline used repository-local execution data beneath the Windows-mounted repository, so its timings are superseded by the native-filesystem comparison below.
- Each scenario uses one warm-up followed by five measured iterations.
- Repositories run sequentially against a newly published Release Host and runner.
- Every completed run must leave the target commit unchanged, shut down the Host normally and leave no recovery, coordination or lock state.
- Working set is process-wide and cumulative within a repository run. It cannot by itself attribute retained memory to the scenario beside which it appears.

| Repository | Size | Commit | Result | Validation |
| --- | --- | --- | --- | --- |
| GuardClauses | Small | `ad43aa02babf3bc8aee8efc2258f5ad3571c8ec5` | `artifacts/performance/results/20260720-122348-guardclauses-7c59ac493407413f8ea86debfcbc819b` | Passed |
| Serilog | Medium | `0597ddfbd4ec594d9c42edd745fe728a2198bad9` | `artifacts/performance/results/20260720-122433-serilog-5d9ef79f59ee43b2a415438140c71c8e` | Passed |
| EF Core | Large | `12b8d44bf691d2e6933a6d1003647cce4f13c3d3` | `artifacts/performance/results/20260720-125807-efcore-6e2e1fcf4b2f4e0cbfc6b9811a1aef20` | Blocked during `find-references-low-limit`; Host and repository validation passed |

## Native-filesystem comparison

The comparison uses the same source tree, pinned repository commits, scenario definitions, warm-up count, measured iteration count and physical machine. Native Windows stores execution data beneath `%TEMP%`; WSL stores it beneath `/tmp`. Durable result files are written to the repository only after measurements complete. Windows used .NET 10.0.10 and WSL used .NET 10.0.2, so this is strong environment evidence rather than a controlled operating-system benchmark.

| Repository | Windows result | WSL result | Validation |
| --- | --- | --- | --- |
| GuardClauses | `artifacts/performance/results/20260720-141118-guardclauses-9fff60d7285b4007a58c5b0ffb4f0128` | `artifacts/performance/results/20260720-145646-guardclauses-a129f0c06d754a59a2d3b44fc8294b96` | Both passed |
| Serilog | `artifacts/performance/results/20260720-141157-serilog-21337ca716704b368a9ab151e0bd8f92` | `artifacts/performance/results/20260720-145713-serilog-a7ff82be13134743af771d98a500526f` | Both passed |
| EF Core | `artifacts/performance/results/20260720-144928-efcore-5d1e643a37d94cd1ab4cb9981d48b16e` | `artifacts/performance/results/20260720-145912-efcore-21c708f1bd764b22858e19db318e5cc8` | Both passed |

Workspace opening is consistently faster on the native WSL filesystem:

| Repository   |      Windows |          WSL | WSL improvement |
| ------------ | -----------: | -----------: | --------------: |
| GuardClauses |  4,034.74 ms |  2,436.20 ms |           1.66x |
| Serilog      |  7,413.96 ms |  3,379.42 ms |           2.19x |
| EF Core      | 19,656.59 ms | 15,207.63 ms |           1.29x |

Across scenario medians, the geometric-mean WSL elapsed time is 40.0% of Windows for GuardClauses, 38.8% for Serilog and 59.3% for EF Core. Treat these aggregate ratios as orientation only: project-details and solution-structure responses contain platform-specific paths and, in some cases, different loaded analyzer or project details. The like-for-like bounded search, reference and graph responses still show the same broad WSL advantage.

CPU-heavy operations converge across environments: GuardClauses Code Action discovery is 448.19 ms on Windows and 449.88 ms on WSL; diagnostics are 130.51 versus 107.04 ms for GuardClauses, 476.61 versus 459.26 ms for Serilog and 3,386.63 versus 3,282.06 ms for EF Core. This supports the inference that the earlier multi-second WSL floors came from Windows-mounted filesystem and shared Workspace validation costs rather than Roslyn computation or WSL itself.

## Investigation order

### 1. Resolve EF Core unresolved-analyser failure in reference discovery

**Status:** Completed

The clean EF Core run successfully opened the mixed solution and completed solution structure, project details and both symbol-search bounds. Its first reference-discovery call failed with correlation ID `6a012e5f135d430ba7253c7f984ea72c`.

Roslyn threw while computing a project checksum because the loaded solution contains a `Microsoft.CodeAnalysis.Diagnostics.UnresolvedAnalyzerReference`. The exception escaped `SymbolFinder.FindReferencesAsync` and was reduced to the generic `UnhandledException` MCP result. The Host still shut down normally, the target commit remained unchanged and no recovery or coordination state leaked.

The native Windows run reproduced the same failure with correlation ID `91b79bb5ae70445ca272ac7b055fc43a`, confirming that it was independent of the earlier WSL filesystem conditions.

The implemented decision is to remove unresolved solution-level and project-level analyser references from the effective immutable solution while retaining a `WorkspaceAnalyzerReferenceSkipped` warning containing the unresolved path and owning project where applicable. The references are error-reporting placeholders and cannot contribute working analyzers, so removing them does not exclude source projects, metadata references or searchable documents. Focused unit coverage exercises both reference scopes, and an MSBuild integration test loads a real project with a missing analyser before completing Roslyn reference discovery successfully.

The confirming native Windows run is recorded at `artifacts/performance/results/20260720-144928-efcore-5d1e643a37d94cd1ab4cb9981d48b16e`. All eight scenarios completed, including both reference bounds and every later scenario. Every measured invocation returned successfully, the repository remained at the pinned commit, the Host exited normally and no recovery, coordination or locking state remained.

**Why first:** the failure prevents a complete large-repository baseline and blocks the bounded-reference and memory investigations. It is a functional correctness issue rather than a performance optimisation.

**Exit evidence:** achieved by the confirming native Windows run above.

### 2. Attribute shared execution costs

**Status:** Completed

Add enough phase-level evidence to distinguish:

- workspace selection and lease acquisition;
- external-change validation;
- tool handler and Roslyn execution;
- response projection;
- MCP serialisation and transport; and
- for `workspace-open`, MSBuild loading, compatibility filtering, Workspace validation and input-manifest construction.

Use traces for call stacks and counters for allocation, GC, thread-pool and exception behaviour. Instrumentation must be low overhead and should not make internal timings part of the public MCP contract.

The Host now emits opt-in `Roslyn-Workbench-Mcp` EventSource phase timings only while a trace collector enables the provider. The manual runner captures those events beside sampled managed thread time, writes the raw `.nettrace`, adds the structured summary to `profile.json`, and writes a readable `phases.md`. Normal tool responses and MCP schemas remain unchanged. When tracing is disabled, phase scopes perform only an `IsEnabled` check and do not take timestamps or emit per-call logs.

The same bounded symbol-search request was profiled across the three native-WSL repositories:

| Repository | Result | End-to-end median | Host tool median | Outside Host tool | Change detection | Handler |
| --- | --- | --: | --: | --: | --: | --: |
| GuardClauses | `artifacts/performance/results/20260720-155448-guardclauses-d24b987ca6244e248f9de9a7305e5463` | 7.73 ms | 7.22 ms | 0.51 ms | 2.41 ms / 33.3% | 4.63 ms / 64.1% |
| Serilog | `artifacts/performance/results/20260720-155516-serilog-6beed630d8f44375800ae90ab52e53c5` | 32.24 ms | 31.78 ms | 0.47 ms | 4.68 ms / 14.7% | 26.24 ms / 82.6% |
| EF Core | `artifacts/performance/results/20260720-155542-efcore-d352e638fc02472db33ac689ee9a75c4` | 160.17 ms | 159.13 ms | 1.03 ms | 18.81 ms / 11.8% | 139.64 ms / 87.8% |

Request binding, Workspace lease acquisition, context construction and response projection are each below one percent in these profiles. The MCP SDK, stdio transport and client-side remainder is approximately one millisecond or less. The dominant recurring cost is therefore inside the tool handler and its Roslyn operation, particularly as repository size grows.

**Why first:** every later observation currently includes shared Host and Workspace work. Optimising a tool before separating that cost risks tuning the wrong path.

**Exit evidence:** achieved by the three clean trace profiles above. Each target repository remained at its pinned commit, the Host exited normally and no recovery, coordination or lock state remained.

### 3. Investigate Workspace input tracking and per-invocation change detection

**Status:** Deferred after attribution

The earlier repeatable multi-second floor came from accessing the Windows filesystem through WSL and is not present on native storage. Native phase traces measure external-change detection at 2.41 ms for GuardClauses, 4.68 ms for Serilog and 18.81 ms for EF Core. It remains a measurable shared cost, but it is 33.3%, 14.7% and 11.8% of the corresponding symbol-search handler boundary rather than the dominant medium/large cost.

Investigate both manifest construction during `workspace-open` and `HasChanged` during each query or mutation acquisition. Record manifest file and directory counts, filesystem calls, elapsed time and cancellation behaviour.

Do not optimise this path ahead of the measured handler/Roslyn work. Revisit coalescing, incremental checks or watcher-assisted invalidation only if additional tool profiles show that change detection materially dominates a common cheap query, or if filesystem-scale evidence changes. Any revised design must still detect changes to existing inputs, new or removed source inputs and imported project configuration before a stale Workspace is used.

**Dependencies:** investigation 2, completed.

**Downstream impact:** expected to affect every tool measurement, Workspace opening, cancellation responsiveness and the interpretation of retained memory.

**Exit evidence:** no implementation change is currently justified. The native profiles establish that change detection does not dominate the medium or large symbol-search request.

### 4. Decompose and improve Workspace opening

**Status:** Deferred; current one-time cost accepted

Workspace opening is already a material part of end-to-end use. Profile MSBuild evaluation, solution loading, unsupported-project filtering, compatibility inspection, root validation and manifest construction separately. Record project/document/reference counts and diagnostics so repository size can be related to cost.

The native WSL baseline opens GuardClauses in 2.44 seconds, Serilog in 3.38 seconds and EF Core in 15.21 seconds. These are one-time session costs. The product intentionally retains a stateful, transactional Workspace, replacing a stateless Roslyn server design that reloaded the solution for every tool execution. The current EF Core time is therefore acceptable and should not take priority over recurring tool costs. Phase hooks exist for future Workspace-load, compatibility and manifest attribution if opening regresses or becomes operationally material.

Preserve the supported behaviour of loading mixed solutions while ignoring project types the Host does not interact with. Do not trade load completeness or actionable diagnostics for elapsed time without an explicit contract decision.

**Dependencies:** investigations 2 and 3. Input-manifest work may account for a meaningful part of opening and should not be measured twice as unrelated problems.

**Downstream impact:** changes can alter the effective solution, initial memory footprint and every tool baseline, so tool-specific conclusions must be confirmed afterward.

**Exit evidence:** an attributed cold-open profile exists for all three repositories and the largest confirmed bottleneck has been remediated or consciously accepted.

### 5. Investigate `get-solution-structure` scaling and response size

**Status:** Completed

The native WSL baseline records:

| Repository   | Median elapsed | Median Host CPU |     Response |
| ------------ | -------------: | --------------: | -----------: |
| GuardClauses |      110.12 ms |           70 ms |    17.19 KiB |
| Serilog      |      404.29 ms |          290 ms |   150.19 KiB |
| EF Core      |    1,521.47 ms |        1,180 ms | 1,124.52 KiB |

Separate shared validation from solution traversal, projection and serialisation. Confirm whether document and folder bounds stop traversal early or only reduce the final response. Assess whether the current response shape forces avoidable repeated project/document work.

Focused EF Core phase tracing identified target-framework evaluation as the dominant cost. The original implementation created and disposed an MSBuild `ProjectCollection` for every selected Roslyn project. In the pre-change trace, project projection occupied 1,953.63 ms of a 1,982.92 ms Host-tool median; repeated target-framework evaluations accounted for virtually all of that time. Solution hierarchy parsing was 1.06 ms, document projection totalled approximately 11 ms per complete invocation, and project-reference projection was below 2 ms per invocation. The trace is retained at `artifacts/performance/results/20260720-191023-efcore-a98784de90bc4c7187d4bfc9b16f574e`.

The implemented change evaluates the already bounded project set as one request-scoped batch. One disposable `ProjectCollection` is shared during the batch, and duplicate project paths reuse the matching loaded project and result. The collection is unloaded and disposed before the request completes; no MSBuild state or cache survives the invocation.

The equivalent post-change EF Core trace records a 1,112.83 ms end-to-end median and 1,072.83 ms Host-tool median, down from 1,995.99 ms and 1,982.92 ms respectively. Target-framework evaluation remains dominant at 1,026.71 ms, but batching removes approximately 44% of the end-to-end trace time. This trace is retained at `artifacts/performance/results/20260720-191532-efcore-9d4a16fda5cd40e59139c8b6cec96bdf`.

Repeatable low-bound and no-document scenarios were added to the manual suite. On EF Core, selecting one project and one folder completes in a 65.17 ms median, including 40.14 ms of target-framework evaluation and 20.89 ms of external-change validation. This confirms that project bounds stop target-framework, reference and document work before projection. The trace is retained at `artifacts/performance/results/20260720-191829-efcore-4b383c4131a94c92b70ae4bcd2b21c0d`.

The high-bound no-document trace records 1,069.10 ms compared with 1,112.83 ms when documents are included. Document projection, response projection and transport therefore account for only approximately 44 ms of the 1.1 MiB response; target-framework evaluation remains approximately 1.03 seconds in both cases. Replacing the clear document projection or adding another document bound is not justified by this performance evidence. The no-document trace is retained at `artifacts/performance/results/20260720-191914-efcore-4780b012f6434316b9e582c1a1532005`.

Untraced post-change measurements confirm the improvement across all three repositories:

| Repository | Previous median | Batched median | Improvement | Result |
| --- | --: | --: | --: | --- |
| GuardClauses | 110.12 ms | 79.60 ms | 27.7% | `artifacts/performance/results/20260720-192209-guardclauses-cf27dbb3aae74191bedb0fe416e3ae18` |
| Serilog | 404.29 ms | 130.86 ms | 67.6% | `artifacts/performance/results/20260720-192217-serilog-4ff452ac4a82484480b5a20d16b53803` |
| EF Core | 1,521.47 ms | 1,059.72 ms | 30.3% | `artifacts/performance/results/20260720-192247-efcore-33c8b84b33534b52bf3a08e998b90170` |

All seven focused runs retained the pinned repository commit, shut down the Host normally and left no recovery, coordination or lock state.

**Dependencies:** investigations 2–4.

**Exit evidence:** achieved by the focused traces and clean measurements above. Remaining repeated target-framework evaluation is a candidate for the separately planned snapshot-scoped cache; retaining a live system-wide MSBuild collection is not part of this change.

### 6. Investigate bounded symbol search and reference discovery

**Status:** Completed; caching separated as a follow-up

Native low and high limits change response size but have little effect on elapsed time:

| Repository/tool | Low-limit median | High-limit median | Low response | High response |
| --- | --: | --: | --: | --: |
| GuardClauses `search-symbols` | 7.09 ms | 8.01 ms | 2.17 KiB | 23.61 KiB |
| GuardClauses `find-references` | 4.00 ms | 5.89 ms | 5.11 KiB | 29.79 KiB |
| Serilog `search-symbols` | 26.39 ms | 27.42 ms | 2.27 KiB | 52.67 KiB |
| Serilog `find-references` | 14.70 ms | 23.31 ms | 4.34 KiB | 95.76 KiB |
| EF Core `search-symbols` | 205.74 ms | 165.72 ms | 2.71 KiB | 144.79 KiB |
| EF Core `find-references` | 711.11 ms | 704.51 ms | 5.15 KiB | 456.11 KiB |

Focused EF Core traces now separate Roslyn discovery, candidate projection, result selection and selected-result enrichment:

| Tool/bound | Result | End-to-end median | Discovery | Candidate projection | Selection | Enrichment |
| --- | --- | --: | --: | --: | --: | --: |
| `search-symbols` low | `artifacts/performance/results/20260720-184755-efcore-a7b75971afc1466faf1c22eb3c0d4078` | 178.95 ms | 155.52 ms / 87.2% | 1.75 ms / 1.0% | <0.01 ms | n/a |
| `search-symbols` high | `artifacts/performance/results/20260720-184852-efcore-4b8a588e0a1543609baf47ac64823af6` | 186.39 ms | 156.31 ms / 86.0% | 1.81 ms / 1.0% | <0.01 ms | n/a |
| `find-references` low | `artifacts/performance/results/20260720-184951-efcore-45ae7e437e244dd2b3f4dff51afa6b2e` | 801.73 ms | 771.00 ms / 96.0% | 4.60 ms / 0.6% | 0.35 ms | 0.42 ms / 0.1% |
| `find-references` high | `artifacts/performance/results/20260720-185056-efcore-d1c752329aa14d169028193064abe3d3` | 912.63 ms | 852.49 ms / 94.4% | 6.18 ms / 0.7% | 0.54 ms / 0.1% | 12.77 ms / 1.4% |

Both Roslyn APIs necessarily discover the complete matching set before the tools can apply deterministic response ordering and bounds. The tools already defer reference context, containing-symbol and write-classification work until after selection. Symbol projection and ordering operate on the complete set because the public ordering keys are projected display name and source path; replacing them with different pre-projection keys would change observable ordering. The measured projection cost is too small to justify that contract change.

The requested limit therefore bounds response size and expensive per-result enrichment, but cannot bound the underlying Roslyn discovery operation. The low/high timings differ mainly in response enrichment and serialisation, not discovery. No additional post-bound optimisation is supported by the measurements.

The traces do confirm substantial repeatable discovery, particularly the approximately 0.8-second EF Core reference search. Snapshot-scoped cross-invocation caching is consequently promoted from a conditional idea to a separate planned deliverable in `FutureTasks.md`. It is not included in this investigation because ownership, size accounting, invalidation and snapshot-safe value design are architectural concerns that need their own implementation and retained-memory measurements.

The phase attribution confirms that handler/Roslyn execution accounts for 64.1%, 82.6% and 87.8% of bounded symbol-search time as repository size grows, while projection is below one percent. The focused phase evidence above completes that earlier handler-level attribution.

**Dependencies:** investigations 1 and 2, completed. Cross-invocation caching also depends on a measured repeatable discovery cost after shared validation is excluded.

**Exit evidence:** achieved by the four focused traces above. Low/high behaviour is explained and no material avoidable post-bound work remains. Cache effectiveness and retention safety are exit criteria for the separate caching deliverable rather than this completed attribution task.

### 7. Investigate Code Action discovery CPU and memory

**Status:** Completed

On GuardClauses, `list-code-actions` records a 260.99 ms median, 990 ms median aggregate Host CPU and a process peak of 374.05 MiB. Capture a trace and counters to distinguish compilation, provider discovery, MEF composition, parallel execution and result projection.

Run Code Action scenarios in isolated Host processes before attributing working-set growth to them. Compare first and subsequent calls to determine whether retained discovery state provides a useful warm-path benefit.

The capability audit found avoidable work before tracing: `list-code-actions` invoked every composed provider and classified each resulting action afterwards, even though deterministic execution support had already been audited in the built-in ledger. Capability metadata is now indexed once in a frozen provider lookup. Known-hidden and unaudited providers are excluded before Roslyn provider execution, while test-only action-dependent overrides remain conservatively discoverable. Each discovered action carries its resolved descriptor, so listing, token resolution, replay and location-fix flows reuse that classification rather than repeating it. The remove-unused-usings family now records its concrete provider identity instead of relying on title-based classification.

This structural optimisation preserves the required distinction between capability and applicability: Roslyn still rediscovers an action against the current document and snapshot before execution, but the server does not re-audit whether that provider family can be executed deterministically.

The refreshed isolated GuardClauses trace then identified diagnostic collection as the dominant remaining cost. It consumed 245.56 ms median and 803.51 ms P95, or 96.9% of the median instrumented tool duration. CPU stacks showed project-wide analyzer execution, including unrelated xUnit analyzer work. Code Fix provider execution and response projection were negligible at 0.02 ms and 0.12 ms median respectively.

`list-code-actions` now derives the distinct diagnostic IDs supported by its discoverable Code Fix providers, intersects them with any request filter and skips diagnostic collection when the intersection is empty. The diagnostic service uses the effective IDs to execute only project analyzers whose declared `SupportedDiagnostics` can contribute to the request. Compiler and analyzer results are still filtered by the same IDs, so the optimisation changes work selection rather than the observable action set.

The equivalent post-change isolated trace recorded 4.91 ms end-to-end median and 16.42 ms P95 across 4,991 profiled invocations, down from 254.36 ms and 826.82 ms across the comparable pre-change trace. Diagnostic collection fell to 0.68 ms median and 3.53 ms P95. The first cold invocation also fell from approximately 3.0 seconds to 1.9 seconds. The remaining median tool time is split principally between refactoring-provider discovery at 1.95 ms and Workspace external-change detection at 1.65 ms; neither justifies another Code Actions-specific latency change at this scale.

An isolated 30-second counter run completed 5,495 invocations at 4.40 ms median and 14.86 ms P95. It allocated 5.89 GB in aggregate, approximately 1.07 MB per invocation, while working set rose from 283.20 MiB to 376.69 MiB and GC committed memory from 67.17 MiB to 109.66 MiB. No Gen 2 collection occurred during that high-throughput capture, so the growth is not evidence of a leak. The remaining transient allocation is inferred to be dominated by Roslyn provider/refactoring discovery because server-owned projection is below one percent of median time. Retained-object ownership and forced-GC comparison remain investigation 8 rather than a reason to add an unbounded Code Actions cache here.

**Dependencies:** investigations 2–4. Shared validation and initial Workspace memory must be separated before judging Code Action-specific cost.

**Exit evidence:** achieved by isolated pre/post traces and the post-change counter run. Project-wide analyzer execution was removed from the listing hot path, the remaining CPU and transient-allocation work is attributable to Roslyn provider/refactoring discovery rather than server projection, and no new retained state or cache was introduced. Code Actions unit/contract coverage and the existing refactoring scenarios remain behaviourally unchanged. Retained-memory ownership is intentionally carried by investigation 8.

### 8. Characterise retained memory and process-order effects

**Status:** Completed

Working set rises through each sequential suite, but the current summaries report a process-wide peak rather than ownership by scenario. Use isolated runs, runtime counters and heap captures to distinguish live Workspace state, Roslyn compilation caches, tool caches, transient allocations awaiting collection and unintended retention.

Repeat selected scenarios in different orders and in fresh processes. Record post-GC retained size only as diagnostic evidence; do not force collection in normal product code or ordinary latency measurements.

The profiling runner now accepts a comma-separated scenario sequence for `gcdump` profiles and records the sequence in `profile.json`. It takes one forced-GC heap capture after the selected workload and a second after `workspace-close` while the Host remains alive. This keeps forced collection confined to manual diagnostics and separates workspace-owned state from process-wide caches.

Fresh GuardClauses Hosts running `list-code-actions` retained 39.96 MiB after one invocation, 40.52 MiB after ten and 41.01 MiB after one hundred. The 2.6% increase between one and one hundred invocations is consistent with bounded warm-up rather than per-call growth. The largest retained groups were Roslyn syntax, compilation and parser caches. Workbench-owned retained collections were small and stable.

Running ten lightweight queries followed by ten Code Action queries retained 40.76 MiB. Reversing that order in another fresh Host retained 40.55 MiB, a difference of approximately 0.5%. No material process-order effect was observed.

Paired before/after-close captures produced the following results:

| Repository | Workload | Workspace open | After close | Released | Result |
| --- | --- | --: | --: | --: | --- |
| GuardClauses | 10 Code Action listings | 40.25 MiB | 27.57 MiB | 31.5% | `artifacts/performance/results/20260722-175232-guardclauses-48aac8e169ca4ae5813f218d19384d48` |
| Serilog | 5 representative queries, 3 invocations each | 159.68 MiB | 56.34 MiB | 64.7% | `artifacts/performance/results/20260722-175402-serilog-19e465961df04515913b6c28c2f229bd` |
| EF Core | 5 representative queries, 1 invocation each | 762.40 MiB | 88.40 MiB | 88.4% | `artifacts/performance/results/20260722-175447-efcore-bebf665ef68342c9a88d714767ac632b` |

In the medium and large captures the live `MSBuildWorkspace` and populated `WorkspaceInputFileFingerprint` array were present before close and absent afterwards. The small capture likewise released its populated fingerprint array. Residual Workbench types were static catalogues, JSON metadata and empty singleton collections; the dominant residual allocations were shared runtime and Roslyn caches. All three runs passed repository and state-file validation.

No production change is justified. Retained memory scales with the live Roslyn solution while a workspace is open, is stable across repeated requests and is released when the workspace closes. Future cross-invocation caches must still provide explicit size limits and snapshot-scoped invalidation, and should repeat these paired captures before adoption.

**Dependencies:** investigations 3–7. Earlier fixes may legitimately change retained memory and should land before a final memory judgement.

**Exit evidence:** achieved by the isolated repeated-invocation, reversed-order and three-repository paired heap captures above. Major retained groups and their lifetimes are understood, repeated invocations reach a defensible steady state, and the live Workspace graph is released at `workspace-close`.

### 9. Complete resilience and comparative measurements

**Status:** Completed

After shared and tool-specific fixes, refresh the three-repository baseline and add focused evidence for:

- cold versus warm execution;
- cancellation latency during large scans;
- zero, one, default, high and above-result-count bounds;
- deterministic ordering, `HasMore` and response equivalence;
- mutation no-change versus staged-change outcomes; and
- direct Windows, WSL-mounted Windows storage and native Linux storage where filesystem behaviour is material.

The runner now retains the evidence needed to validate behaviour alongside latency: exact response hashes, mutation `staged` state, and every bounded collection's `items` count, `HasMore` value and ordered item hashes. Summaries record the warm-up count plus first and subsequent measured timings. A dedicated cancellation command separately records client-visible cancellation and recovery of an exclusive Workspace lease.

The final native-WSL baseline uses one warm-up and five measured invocations per scenario:

| Repository | Result | Validation |
| --- | --- | --- |
| GuardClauses | `artifacts/performance/results/20260722-182616-guardclauses-709ccbeeda144875a95b3b9d63fd3bfe` | Passed |
| Serilog | `artifacts/performance/results/20260722-182636-serilog-4c8132a85a574498b8054c0264180c41` | Passed |
| EF Core | `artifacts/performance/results/20260722-182701-efcore-a3691b5ae3324dd39c655eca82e3b037` | Passed |

Representative final medians are:

| Repository | Solution structure | Symbol search low/high | References low/high | Diagnostics | Dependency graph |
| --- | --: | --: | --: | --: | --: |
| GuardClauses | 90.22 ms | 8.53 / 8.93 ms | 4.36 / 6.88 ms | 144.18 ms | 13.36 ms |
| Serilog | 145.66 ms | 32.53 / 35.42 ms | 16.66 / 30.65 ms | 560.00 ms | 33.52 ms |
| EF Core | 1,031.45 ms | 171.64 / 176.18 ms | 781.10 / 799.57 ms | 3,236.92 ms | 141.54 ms |

The final results preserve the improvements established by the focused investigations. In particular, EF Core solution structure remains approximately 32% below the original 1,521.47 millisecond baseline, and GuardClauses Code Action listing records 22.27 milliseconds rather than the original 448.19 milliseconds.

A zero-warm-up GuardClauses search recorded 1,090.09 milliseconds for its first invocation and 9.45 milliseconds for the subsequent median. The focused EF Core bound run recorded 22,470.78 milliseconds for the first symbol search that constructed the cold compilation and 205.78 milliseconds for subsequent zero-bound calls. This confirms that compilation state materially affects the first semantic query while steady-state measurements remain representative of an open Workspace session. The focused evidence is retained at `artifacts/performance/results/20260722-180937-guardclauses-3187b970a54b46219b4a7a5c788df802` and `artifacts/performance/results/20260722-181537-efcore-7e505e850a304b70ac0334e343c6503f`.

The EF Core bound matrix exercised zero, one, omitted/default, high and above-result-count limits for both symbol and reference search. The published default is 100 items for each tool. Zero, one, default and high responses were exact ordered prefixes of the complete response. `HasMore` was true for every truncated response and false for the complete 398-symbol and 3,382-reference responses. All three invocations of every variant produced identical hashes.

The first cross-repository deterministic pass exposed equal-location reference ties in Serilog's multi-target solution. `find-references` sorted only by path and span start, leaving project variants in Roslyn enumeration order. Project ID, document ID, span length and definition state now provide stable tie-breakers without changing the established primary ordering. Five repeated low- and high-bound Serilog invocations then produced one identical ordered sequence at each bound; the confirming result is `artifacts/performance/results/20260722-182522-serilog-f64b1596b8754952abcecf6e52e134b9`.

Mutation evidence distinguishes no-change and staged outcomes. On GuardClauses, formatting and sorting returned `staged: false`, a real rename returned `staged: true`, and renaming to the current symbol name returned `staged: false`. The last case initially staged an equivalent Roslyn solution because reference equality is not a content-equivalence check; `rename-symbol` now rejects that work before invoking Roslyn. Final medians were 12.81 milliseconds for no-change formatting, 55.58 milliseconds for a staged rename and 3.08 milliseconds for the direct rename no-change path.

The original cancellation evidence at `artifacts/performance/results/20260722-181322-efcore-285393468a594045b3a70242e5777d8f` and `artifacts/performance/results/20260722-181743-efcore-cacd48422a9d455693589b54ca21cc5f` cancelled only the pinned C# SDK client's local wait; a controlled protocol test observed no corresponding `notifications/cancelled` message at the server. The runner now uses a known request ID and explicitly sends and awaits the standard notification, so its lease measurement represents actual server cancellation.

The corrected EF Core run proved that cancellation reaches the Host handler and Roslyn reference search. It also exposed an intermittent raw-lease leak when cancellation occurred during external-change validation after Workspace acquisition but before the disposable execution-context lease was returned. Query and mutation context creation now release ownership on every exceptional validation or construction path. After that correction, all five warmed reference searches cancelled successfully: median client cancellation latency was 0.20 milliseconds, while exclusive-lease recovery measured a 14.51 millisecond median and 45.01 millisecond P95. Repository and state validation passed with no residual non-cancellable Roslyn stage. Evidence is retained at `artifacts/performance/results/20260722-190812-efcore-546a761dafd44caaa3a8c0c3a79ed14d`.

The earlier native Windows, WSL-native and WSL-mounted comparison remains the relevant filesystem evidence because it used the same physical machine and pinned inputs and already isolated the material storage effect. WSL-mounted execution is retained only as evidence of the penalty and warning requirement; it is not repeated as a recommended operating mode. The final post-remediation baseline uses native WSL storage, while the recorded Windows/native-WSL results continue to establish supported-platform behaviour without treating their different SDK patches as one controlled timing population.

**Dependencies:** investigations 1 and 3–8. Running the full comparison earlier would measure known shared bottlenecks repeatedly and would need to be discarded again after they change.

**Exit evidence:** achieved by the final three-repository baseline and focused cold/warm, bound, deterministic-ordering, mutation and corrected protocol-cancellation runs above. Repository and state validation passed throughout. Server-side cancellation now reaches active Roslyn work and releases the Workspace lease within the measured bound.

### 10. Introduce snapshot-scoped reference-discovery caching

**Status:** Completed for `find-references`; further cache candidates remain separate decisions

The reference traces in investigation 6 established a repeatable approximately 0.8-second Roslyn discovery cost after compilation was warm. A dedicated Workspace-owned, DI-managed `IMemoryCache` now stores only complete successful reference-discovery results. Options configure its 50,000-unit size limit and ten-minute sliding expiration. Tools receive a `QueryCache` runtime object exposing only the bounded query read/write contract through `IToolExecutionServices`, while lifecycle code receives a distinct `WorkspaceQueryCache` invalidation object; private shared state coordinates per-workspace invalidation without making the tool-facing runtime type invalidatable. Each entry is sized by its referenced-symbol and reference-location counts.

Keys combine the stable workspace ID with the immutable `Solution` instance, semantic symbol identity and sorted document IDs. Request limits, definition inclusion and context enrichment do not alter Roslyn discovery and are therefore excluded, allowing a later higher-bound request to reuse the same complete result. Cancelled or failed discovery is never stored. Workspace close always invalidates; replacement solutions, workspace epochs and stale transitions invalidate during session replacement, covering mutation staging, rollback, commit, reload and external changes without discarding entries for state-only transitions over the same snapshot.

The focused zero-warm-up EF Core measurement retained at `artifacts/performance/results/20260722-195101-efcore-82ba0c647c8c4cba81a4461cb8085b25` records:

| Scenario | Previous warm median | First cold miss | Cached subsequent median | Improvement from previous warm median |
| --- | --: | --: | --: | --: |
| References low bound | 778.25 ms | 25,326.10 ms | 24.02 ms | 96.9% / 32.4x |
| References high bound | 787.79 ms | 95.88 ms after the low-bound population | 57.75 ms | 92.7% / 13.6x |

The cold miss remains expensive because it constructs compilation and Roslyn reference state; this cache targets successive invocations within the open immutable Workspace. The high-bound response reused the low-bound discovery while still performing the additional selected-result enrichment and serialisation required for 500 results. Exact response hashes remained stable, and repository, Host shutdown, recovery, coordination and lock-state validation passed.

The equivalent five-query forced-GC sequence retained at `artifacts/performance/results/20260722-195444-efcore-ff73a452045243d19f446c2643a8bd99` measured 761.62 MB with the Workspace open and 89.14 MB after close. The pre-cache sequence measured 762.40 MB and 88.40 MB respectively. Both differences are below one percent. After close, the singleton cache shell remains as expected, but no reference-discovery cache entry or key remains; the Workspace graph and its cached query values were released. The repository and state-file validation passed.

**Dependencies:** investigations 2, 6 and 8, completed.

**Exit evidence:** achieved for reference discovery. Cache-hit latency is materially lower, low/high responses remain deterministic, retained memory is unchanged within measurement noise, and workspace close releases the cached snapshot. Target-framework metadata and any other operation require their own evidence before adoption.

### 11. Introduce snapshot-scoped target-framework caching

**Status:** Completed

The fresh pre-change EF Core measurement retained at `artifacts/performance/results/20260722-205048-efcore-60dfc55f71a24ab185e442c07d526d2a` confirmed that repeated `get-solution-structure` calls still spent a 919.22-millisecond subsequent median evaluating target-framework metadata. The response was stable at 1.1 MiB and repository/state validation passed, so the repeated MSBuild evaluation remained an attributable cost rather than response projection or workspace corruption.

Target-framework results are now cached per workspace, immutable `Solution` instance and project path. Entries contain copied framework strings only; no MSBuild `Project`, `ProjectCollection` or evaluation graph survives the request. A request evaluates all cache misses through the existing request-scoped batch and stores them only when the complete miss batch succeeds and cancellation has been rechecked. Cached projects retain their original result positions, while a later larger request evaluates only newly selected project paths. Project-less in-memory Roslyn projects continue to return an empty successful result without entering the cache.

The equivalent post-change measurement retained at `artifacts/performance/results/20260722-205734-efcore-9262be0611264f97befc8d3829091923` reduced the subsequent median to 55.93 milliseconds, a 93.9% reduction, while preserving the exact response hash and 1.1 MiB response size. The first measured call after one warm-up was 100.35 milliseconds. A focused ten-second trace retained at `artifacts/performance/results/20260722-210016-efcore-e321af9f8a89426c9f79fe7fc8c9159a` recorded 283 invocations at a 38.39-millisecond end-to-end median. Target-framework evaluation fell to a 0.01-millisecond median and 0.03-millisecond P95; external-change detection is now the largest measured phase at 18.99 milliseconds.

The forced-GC evidence retained at `artifacts/performance/results/20260722-205854-efcore-d75a7c391d084504ad7d49a2db8583f7` measured a 62.45 MB managed heap with the Workspace open and 45.70 MB after close. The open capture contained 61 target-framework cache keys and 53 copied-value entries; the after-close capture contained none of those types. Repository, Host shutdown, recovery, coordination and lock-state validation passed.

**Dependencies:** investigations 5, 8 and 10, completed.

**Exit evidence:** achieved. Repeated target-framework evaluation is removed from the warm path, partial cache misses remain request-batched, response output is unchanged, and workspace close releases all target-framework entries. Further operations require their own attributable latency and retained-memory evidence rather than automatic cache adoption.

### 12. Measure durable multi-file mutation commit performance

**Status:** Completed

The mutation evidence in investigation 9 ends after staging. It proves the cost and outcome of `rename-symbol`, `format-document` and `sort-usings`, but does not measure `transaction-preview` or the durable `transaction-commit` path. Commit work includes filesystem and recovery guarantees that are not represented by mutation-tool latency.

Extend the permanent runner with an end-to-end transaction sequence: start a transaction, stage a deterministic mutation, preview it, commit it and validate both the resulting Workspace snapshot and files on disk. Include at least one symbol rename selected for broad reference fan-out across many files. Record the changed-file count, create/replace/delete operation counts and changed-byte volume so scaling can be distinguished from fixed transaction overhead. Use small and broad commits rather than treating one rename as representative.

Add phase evidence for external-change validation, commit planning and diff construction, recovery-artifact and journal persistence, file-lock acquisition, atomic file operations, Workspace promotion or reload, input-manifest refresh, query-cache invalidation and recovery cleanup. Capture elapsed time, CPU, allocation and filesystem activity. Compare native Windows and native WSL storage if durable-write cost is material; WSL access to Windows-mounted storage remains a warning case rather than a recommended baseline.

Include controlled external-change scenarios between staging and commit. Measure the pre-write case separately: external drift discovered during commit validation should reject without creating a rollback requirement. Also exercise a conflict or injected write failure after a broad multi-file commit has begun, forcing recovery to restore files already written. Record conflict detection, rollback and cleanup phases independently, and prove that files changed by the external actor are neither overwritten nor reverted by server recovery.

Measurements must use disposable checkouts at pinned commits. After successful and deliberately interrupted scenarios, verify file contents, transaction state, recovery manifests and artifacts, coordination data, lock state, Host shutdown and repository reproducibility. Do not optimise or weaken durability guarantees until phase evidence identifies a material cost that scales with the commit size.

The permanent runner now has a dedicated `commit` command. Every warm-up and measured iteration uses a fresh Host, starts and previews a transaction, performs the real durable commit, records changed paths and byte volume, closes the Workspace and Host, restores only the recorded repository paths and removes coordination files created by that iteration. The pinned checkout, Host shutdown and recovery state are validated after restoration. Optional tracing starts immediately before `transaction-commit`, so a broad Roslyn rename cannot consume the diagnostic window before the commit begins.

The first Serilog attempt exposed a functional dependency: identical physical source files appear in several Roslyn projects for a multi-target build, and the commit planner rejected those identical projections as duplicate targets. The planner now coalesces only duplicates with the same operation and intended bytes into one durable entry. Conflicting content or operations for one canonical target remain rejected, with focused planner coverage.

The native-Windows Serilog commit then exposed a second linked-document case that WSL could not exercise. Serilog adds `net48`, `net471` and `net462` target-framework projects only on Windows, and their conditional-compilation views produced compatible but non-identical rename edits for the same physical test file. Mutation staging now reconciles linked source documents before validation, preview and revision storage: identical edits are deduplicated, non-overlapping edits are combined and propagated to every linked Roslyn document, and overlapping edits return `LinkedDocumentConflict` rather than allowing the commit planner to choose one projection. Focused linked-document and staging tests cover those outcomes. The confirming native-Windows run committed the same 27 physical files across three stable measured iterations and restored the pinned checkout and state after each one. Evidence: `artifacts/performance/results/20260723-091651-serilog-476bd0335bc54d3398382a449df52aac`.

The first native-Windows EF Core commit reached source application but failed on a 269-character same-directory temporary path. Managed file creation supported the path, while the direct `MoveFileExW` call received an unextended path and returned `ERROR_PATH_NOT_FOUND`. The Windows atomic committer now converts local and UNC inputs to absolute extended-length paths before invoking `MoveFileExW`, covering both replacement and durable delete-marker moves without depending on machine-level long-path policy. A focused native-Windows executable validated the production committer with a 267-character temporary path. Commit failures now also retain the operation/path and base operating-system error in the MCP response after successful recovery, rather than discarding the detail needed to diagnose a retryable fault. The confirming full native-Windows EF Core run committed and restored all 948 files cleanly. Evidence: `artifacts/performance/results/20260723-100133-efcore-3b1df8a58aa847ebb1fa521f59ace249`.

Successful native-WSL measurements produced:

| Repository | Changed files | Original bytes | Mutation staging | Durable commit | Runner restoration | Result |
| --- | --: | --: | --: | --: | --: | --- |
| GuardClauses | 1 | 6,291 | 1,580.93 ms median | 224.22 ms median | 22.40 ms median | `artifacts/performance/results/20260723-075558-guardclauses-3ba8b81fdddf4a2786a35bacff4d4296` |
| Serilog | 27 | 366,488 | 3,911.25 ms median | 768.66 ms median | 50.74 ms median | `artifacts/performance/results/20260723-075634-serilog-f30bec42cd4240cf92cbfdeceb42904f` |
| EF Core | 948 | 18,100,444 | 33,650.86 ms | 11,725.67 ms | 415.59 ms | `artifacts/performance/results/20260723-075041-efcore-5aef58e506644786a87ab837784a2bcf` |

Equivalent native-Windows measurements produced:

| Repository | Changed files | Mutation staging | Durable commit | Runner restoration | Windows/WSL commit ratio | Result |
| --- | --: | --: | --: | --: | --: | --- |
| GuardClauses | 1 | 1,773.47 ms median | 449.35 ms median | 126.15 ms median | 2.00x | `artifacts/performance/results/20260723-083554-guardclauses-38ca2599a6ee4801b409ed3626476e8c` |
| Serilog | 27 | 4,422.26 ms median | 1,520.86 ms median | 228.94 ms median | 1.98x | `artifacts/performance/results/20260723-091651-serilog-476bd0335bc54d3398382a449df52aac` |
| EF Core | 948 | 44,199.04 ms | 21,059.58 ms | 1,662.74 ms | 1.80x | `artifacts/performance/results/20260723-100133-efcore-3b1df8a58aa847ebb1fa521f59ace249` |

The initial traced EF Core commit wrote Host recovery state beneath the retained result directory on `/mnt/c`. It took 54,904.50 ms, of which recovery-plan persistence consumed 43,325.85 ms. Host recovery state is execution data, not a retained result, so the runner now places it in a unique OS-temporary directory and deletes that directory after validation. With native WSL state storage, the equivalent traced commit took 12,141.40 ms: plan persistence was 6,483.79 ms, atomic source application 3,286.18 ms and rebuilding the committed Workspace input manifest 2,050.04 ms. Planning, revalidation, locking and response work together remained below 300 ms. The clean trace is retained at `artifacts/performance/results/20260723-075215-efcore-091a627e99af4706ba229753533c6ea5`.

The native-Windows trace recorded 4,632.51 ms for recovery-plan persistence and 8,962.72 ms for atomic source application. Its configured 20-second collection window ended before the 21,059.58-millisecond commit completed, so the final Workspace-promotion, cleanup and response scopes are absent; the remaining tail is bounded at approximately 6.77 seconds and occurs after source application. This does not affect the main attribution: Windows plan persistence was faster than native WSL, while its 948 durable replacements were 2.73 times slower. The full commit remained a one-time 21.06-second operation for an unusually broad 948-file mutation.

The evidence does not support another query cache or a durability redesign. Small and medium commits complete in approximately 0.45 and 1.52 seconds on Windows. Broad-commit cost scales through required recovery artifacts, per-file durable atomic replacement and input-manifest refresh rather than repeated semantic query work. Bundling artifacts, parallelising durable writes or incrementally rebuilding the input manifest would add recovery and correctness complexity to optimise an extreme one-time case. The current cost is consciously accepted; those changes require new product latency requirements and equivalent recovery-safety evidence rather than being inferred from this baseline.

Both controlled conflict paths now have repeatable evidence:

- GuardClauses pre-write drift returned `TransactionConflicted/RollbackTransaction` in a 7.56 ms median. It created no recovery manifest or artifact, left only the external edit on disk, preserved its exact hash, rolled the conflicted transaction back and closed the Workspace normally. Two measured iterations and the warm-up restored the checkout and coordination state cleanly. Evidence: `artifacts/performance/results/20260723-081931-guardclauses-42f0eb242b89469b929cf6b901992051`.
- EF Core in-progress conflict waited for the recovery manifest to enter `Applying`, selected the final replacement entry and changed it only while its hash still matched the manifest's original hash. Across two measured iterations, injection occurred after 6,422.24–6,786.89 ms and recovery completed 6,707.38–6,779.59 ms later. Both calls returned `CommitFailed/ResolveRecovery`, retained `RecoveryConflict` with 1,896 artifacts, restored every server-written file and preserved the exact external bytes as the only repository difference. The Host exited normally, then the runner restored the disposable checkout and state. Evidence: `artifacts/performance/results/20260723-082201-efcore-8b7bae005780422abee4818a0e9eeed2`.

Native Windows produced the same safety outcomes. GuardClauses pre-write drift rejected in a 24.81-millisecond median with no recovery artifact and only the exact external edit left before runner restoration. EF Core injection occurred after 4,840.23–4,841.77 ms; recovery then took 16,950.05–19,312.91 ms, returned `CommitFailed/ResolveRecovery`, retained `RecoveryConflict` with 1,896 artifacts, restored every server-written file and preserved the external target. All Hosts exited normally and every pinned checkout and state directory validated cleanly. Evidence: `artifacts/performance/results/20260723-100639-guardclauses-f3443c6f11cf4f61b5ff7cfd240112bd` and `artifacts/performance/results/20260723-101215-efcore-97a2f561cf0f43feb6ada616cf2738ac`.

The recovery tests confirm that the existing safety model works under a late broad-commit conflict on both supported operating-system families. They also reinforce the successful-commit attribution: recovery reads and durably rewrites hundreds of individual backup artifacts, so any future persistence optimisation must be evaluated against both successful and recovery timings and must preserve the externally modified target.

**Dependencies:** investigations 2, 8 and 9, completed. The existing phase instrumentation, retained-memory method and mutation validation evidence should be reused rather than creating a separate measurement model.

**Exit evidence:** clean small and broad commit profiles exist, changed-file and byte counts explain the measured scaling, pre-write external drift rejects safely, an in-progress failure restores only server-written files, success and interruption leave the expected durable state, and any material attributable bottleneck has been remediated or consciously accepted.

## Working rules

- Change one attributable cost centre at a time and retain before/after evidence from the same environment.
- Do not compare Windows and WSL numbers as though they came from the same performance population.
- Run repositories sequentially and use isolated Host processes when attributing memory.
- Preserve exact repository commits and validate repository/state cleanliness after every run.
- Prefer end-to-end traces for Roslyn and Workspace behaviour; use BenchmarkDotNet only for isolated deterministic helpers.
- Update status and evidence here when an investigation produces a decision or implementation batch. Remove resolved risks from `FutureTasks.md` only when the complete intended outcome is delivered.
