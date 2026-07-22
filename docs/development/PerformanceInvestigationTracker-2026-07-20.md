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
|---|---|---|---|---|
| GuardClauses | Small | `ad43aa02babf3bc8aee8efc2258f5ad3571c8ec5` | `artifacts/performance/results/20260720-122348-guardclauses-7c59ac493407413f8ea86debfcbc819b` | Passed |
| Serilog | Medium | `0597ddfbd4ec594d9c42edd745fe728a2198bad9` | `artifacts/performance/results/20260720-122433-serilog-5d9ef79f59ee43b2a415438140c71c8e` | Passed |
| EF Core | Large | `12b8d44bf691d2e6933a6d1003647cce4f13c3d3` | `artifacts/performance/results/20260720-125807-efcore-6e2e1fcf4b2f4e0cbfc6b9811a1aef20` | Blocked during `find-references-low-limit`; Host and repository validation passed |

## Native-filesystem comparison

The comparison uses the same source tree, pinned repository commits, scenario definitions, warm-up count, measured iteration count and physical machine. Native Windows stores execution data beneath `%TEMP%`; WSL stores it beneath `/tmp`. Durable result files are written to the repository only after measurements complete. Windows used .NET 10.0.10 and WSL used .NET 10.0.2, so this is strong environment evidence rather than a controlled operating-system benchmark.

| Repository | Windows result | WSL result | Validation |
|---|---|---|---|
| GuardClauses | `artifacts/performance/results/20260720-141118-guardclauses-9fff60d7285b4007a58c5b0ffb4f0128` | `artifacts/performance/results/20260720-145646-guardclauses-a129f0c06d754a59a2d3b44fc8294b96` | Both passed |
| Serilog | `artifacts/performance/results/20260720-141157-serilog-21337ca716704b368a9ab151e0bd8f92` | `artifacts/performance/results/20260720-145713-serilog-a7ff82be13134743af771d98a500526f` | Both passed |
| EF Core | `artifacts/performance/results/20260720-144928-efcore-5d1e643a37d94cd1ab4cb9981d48b16e` | `artifacts/performance/results/20260720-145912-efcore-21c708f1bd764b22858e19db318e5cc8` | Both passed |

Workspace opening is consistently faster on the native WSL filesystem:

| Repository | Windows | WSL | WSL improvement |
|---|---:|---:|---:|
| GuardClauses | 4,034.74 ms | 2,436.20 ms | 1.66x |
| Serilog | 7,413.96 ms | 3,379.42 ms | 2.19x |
| EF Core | 19,656.59 ms | 15,207.63 ms | 1.29x |

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
|---|---|---:|---:|---:|---:|---:|
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

| Repository | Median elapsed | Median Host CPU | Response |
|---|---:|---:|---:|
| GuardClauses | 110.12 ms | 70 ms | 17.19 KiB |
| Serilog | 404.29 ms | 290 ms | 150.19 KiB |
| EF Core | 1,521.47 ms | 1,180 ms | 1,124.52 KiB |

Separate shared validation from solution traversal, projection and serialisation. Confirm whether document and folder bounds stop traversal early or only reduce the final response. Assess whether the current response shape forces avoidable repeated project/document work.

Focused EF Core phase tracing identified target-framework evaluation as the dominant cost. The original implementation created and disposed an MSBuild `ProjectCollection` for every selected Roslyn project. In the pre-change trace, project projection occupied 1,953.63 ms of a 1,982.92 ms Host-tool median; repeated target-framework evaluations accounted for virtually all of that time. Solution hierarchy parsing was 1.06 ms, document projection totalled approximately 11 ms per complete invocation, and project-reference projection was below 2 ms per invocation. The trace is retained at `artifacts/performance/results/20260720-191023-efcore-a98784de90bc4c7187d4bfc9b16f574e`.

The implemented change evaluates the already bounded project set as one request-scoped batch. One disposable `ProjectCollection` is shared during the batch, and duplicate project paths reuse the matching loaded project and result. The collection is unloaded and disposed before the request completes; no MSBuild state or cache survives the invocation.

The equivalent post-change EF Core trace records a 1,112.83 ms end-to-end median and 1,072.83 ms Host-tool median, down from 1,995.99 ms and 1,982.92 ms respectively. Target-framework evaluation remains dominant at 1,026.71 ms, but batching removes approximately 44% of the end-to-end trace time. This trace is retained at `artifacts/performance/results/20260720-191532-efcore-9d4a16fda5cd40e59139c8b6cec96bdf`.

Repeatable low-bound and no-document scenarios were added to the manual suite. On EF Core, selecting one project and one folder completes in a 65.17 ms median, including 40.14 ms of target-framework evaluation and 20.89 ms of external-change validation. This confirms that project bounds stop target-framework, reference and document work before projection. The trace is retained at `artifacts/performance/results/20260720-191829-efcore-4b383c4131a94c92b70ae4bcd2b21c0d`.

The high-bound no-document trace records 1,069.10 ms compared with 1,112.83 ms when documents are included. Document projection, response projection and transport therefore account for only approximately 44 ms of the 1.1 MiB response; target-framework evaluation remains approximately 1.03 seconds in both cases. Replacing the clear document projection or adding another document bound is not justified by this performance evidence. The no-document trace is retained at `artifacts/performance/results/20260720-191914-efcore-4780b012f6434316b9e582c1a1532005`.

Untraced post-change measurements confirm the improvement across all three repositories:

| Repository | Previous median | Batched median | Improvement | Result |
|---|---:|---:|---:|---|
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
|---|---:|---:|---:|---:|
| GuardClauses `search-symbols` | 7.09 ms | 8.01 ms | 2.17 KiB | 23.61 KiB |
| GuardClauses `find-references` | 4.00 ms | 5.89 ms | 5.11 KiB | 29.79 KiB |
| Serilog `search-symbols` | 26.39 ms | 27.42 ms | 2.27 KiB | 52.67 KiB |
| Serilog `find-references` | 14.70 ms | 23.31 ms | 4.34 KiB | 95.76 KiB |
| EF Core `search-symbols` | 205.74 ms | 165.72 ms | 2.71 KiB | 144.79 KiB |
| EF Core `find-references` | 711.11 ms | 704.51 ms | 5.15 KiB | 456.11 KiB |

Focused EF Core traces now separate Roslyn discovery, candidate projection, result selection and selected-result enrichment:

| Tool/bound | Result | End-to-end median | Discovery | Candidate projection | Selection | Enrichment |
|---|---|---:|---:|---:|---:|---:|
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

**Status:** Not started

Working set rises through each sequential suite, but the current summaries report a process-wide peak rather than ownership by scenario. Use isolated runs, runtime counters and heap captures to distinguish live Workspace state, Roslyn compilation caches, tool caches, transient allocations awaiting collection and unintended retention.

Repeat selected scenarios in different orders and in fresh processes. Record post-GC retained size only as diagnostic evidence; do not force collection in normal product code or ordinary latency measurements.

**Dependencies:** investigations 3–7. Earlier fixes may legitimately change retained memory and should land before a final memory judgement.

**Exit evidence:** major retained object groups and their lifetimes are understood, repeated invocations reach a defensible steady state, and no workspace or snapshot remains reachable beyond its intended lifetime.

### 9. Complete resilience and comparative measurements

**Status:** Not started

After shared and tool-specific fixes, refresh the three-repository baseline and add focused evidence for:

- cold versus warm execution;
- cancellation latency during large scans;
- zero, one, default, high and above-result-count bounds;
- deterministic ordering, `HasMore` and response equivalence;
- mutation no-change versus staged-change outcomes; and
- direct Windows, WSL-mounted Windows storage and native Linux storage where filesystem behaviour is material.

**Dependencies:** investigations 1 and 3–8. Running the full comparison earlier would measure known shared bottlenecks repeatedly and would need to be discarded again after they change.

**Exit evidence:** the dated post-remediation baseline is complete, comparable and linked from this tracker, with remaining risks either scheduled or explicitly accepted.

## Working rules

- Change one attributable cost centre at a time and retain before/after evidence from the same environment.
- Do not compare Windows and WSL numbers as though they came from the same performance population.
- Run repositories sequentially and use isolated Host processes when attributing memory.
- Preserve exact repository commits and validate repository/state cleanliness after every run.
- Prefer end-to-end traces for Roslyn and Workspace behaviour; use BenchmarkDotNet only for isolated deterministic helpers.
- Update status and evidence here when an investigation produces a decision or implementation batch. Remove resolved risks from `FutureTasks.md` only when the complete intended outcome is delivered.
