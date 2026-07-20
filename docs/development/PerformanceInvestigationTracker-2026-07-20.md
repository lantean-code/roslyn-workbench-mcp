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

**Status:** Not started

Add enough phase-level evidence to distinguish:

- workspace selection and lease acquisition;
- external-change validation;
- tool handler and Roslyn execution;
- response projection;
- MCP serialisation and transport; and
- for `workspace-open`, MSBuild loading, compatibility filtering, Workspace validation and input-manifest construction.

Use traces for call stacks and counters for allocation, GC, thread-pool and exception behaviour. Instrumentation must be low overhead and should not make internal timings part of the public MCP contract.

**Why first:** every later observation currently includes shared Host and Workspace work. Optimising a tool before separating that cost risks tuning the wrong path.

**Exit evidence:** representative small, medium and large calls have an attributable phase breakdown whose total reconciles with the end-to-end measurement closely enough to direct optimisation.

### 3. Investigate Workspace input tracking and per-invocation change detection

**Status:** Not started

The medium baseline shows a repeatable floor of roughly 2.3 seconds across searches, diagnostics and dependency analysis despite materially different operations and low Host CPU. Determine how much comes from checking the Workspace input manifest, especially filesystem metadata access on Windows drives mounted through WSL.

Investigate both manifest construction during `workspace-open` and `HasChanged` during each query or mutation acquisition. Record manifest file and directory counts, filesystem calls, elapsed time and cancellation behaviour.

Any revised design must still detect changes to existing inputs, new or removed source inputs and imported project configuration before a stale Workspace is used. Consider coalescing, incremental checks or watcher-assisted invalidation only after their failure and recovery semantics are explicit.

**Dependencies:** investigation 2.

**Downstream impact:** expected to affect every tool measurement, Workspace opening, cancellation responsiveness and the interpretation of retained memory.

**Exit evidence:** change detection retains its correctness coverage and no longer dominates a no-op query on the medium or large WSL workspace.

### 4. Decompose and improve Workspace opening

**Status:** Not started

Workspace opening is already a material part of end-to-end use. Profile MSBuild evaluation, solution loading, unsupported-project filtering, compatibility inspection, root validation and manifest construction separately. Record project/document/reference counts and diagnostics so repository size can be related to cost.

Preserve the supported behaviour of loading mixed solutions while ignoring project types the Host does not interact with. Do not trade load completeness or actionable diagnostics for elapsed time without an explicit contract decision.

**Dependencies:** investigations 2 and 3. Input-manifest work may account for a meaningful part of opening and should not be measured twice as unrelated problems.

**Downstream impact:** changes can alter the effective solution, initial memory footprint and every tool baseline, so tool-specific conclusions must be confirmed afterward.

**Exit evidence:** an attributed cold-open profile exists for all three repositories and the largest confirmed bottleneck has been remediated or consciously accepted.

### 5. Investigate `get-solution-structure` scaling and response size

**Status:** Not started

The clean baseline currently records:

| Repository | Median elapsed | Median Host CPU | Response |
|---|---:|---:|---:|
| GuardClauses | 899.60 ms | 610 ms | 17.19 KiB |
| Serilog | 11,890.83 ms | 1,920 ms | 151.32 KiB |
| EF Core | Pending | Pending | Pending |

Separate shared validation from solution traversal, projection and serialisation. Confirm whether document and folder bounds stop traversal early or only reduce the final response. Assess whether the current response shape forces avoidable repeated project/document work.

**Dependencies:** investigations 2–4.

**Exit evidence:** scaling is explained by phase and input count, bounds prevent work that cannot affect the result where contract semantics permit it, and output equivalence is retained.

### 6. Investigate bounded symbol search and reference discovery

**Status:** Not started

Low and high limits currently change response size but have little effect on elapsed time:

| Repository/tool | Low-limit median | High-limit median | Low response | High response |
|---|---:|---:|---:|---:|
| GuardClauses `search-symbols` | 170.18 ms | 157.61 ms | 2.17 KiB | 23.61 KiB |
| GuardClauses `find-references` | 169.20 ms | 163.70 ms | 5.11 KiB | 29.79 KiB |
| Serilog `search-symbols` | 2,308.27 ms | 2,283.49 ms | 2.27 KiB | 52.67 KiB |
| Serilog `find-references` | 2,287.27 ms | 2,287.46 ms | 4.34 KiB | 95.76 KiB |

After removing shared acquisition cost, determine which Roslyn operations necessarily complete discovery and which projection or enrichment work can stop at the requested bound. Measure reference counts, selected counts and context/enrichment costs.

Evaluate snapshot-scoped cross-invocation caching only when the trace confirms substantial repeatable discovery. Cache design must be size-limited, snapshot-keyed and invalidated on close, reload, commit and snapshot advancement.

**Dependencies:** investigations 1–4. Cross-invocation caching also depends on a measured repeatable discovery cost after shared validation is excluded.

**Exit evidence:** low/high behaviour is explained, avoidable post-bound work is removed, and any cache demonstrates useful hit rate and latency without unsafe retention.

### 7. Investigate Code Action discovery CPU and memory

**Status:** Not started

On GuardClauses, `list-code-actions` records a 260.99 ms median, 990 ms median aggregate Host CPU and a process peak of 374.05 MiB. Capture a trace and counters to distinguish compilation, provider discovery, MEF composition, parallel execution and result projection.

Run Code Action scenarios in isolated Host processes before attributing working-set growth to them. Compare first and subsequent calls to determine whether retained discovery state provides a useful warm-path benefit.

**Dependencies:** investigations 2–4. Shared validation and initial Workspace memory must be separated before judging Code Action-specific cost.

**Exit evidence:** CPU and allocation hot paths are identified; any retained state has an owner, bound and invalidation lifetime; the three refactoring scenarios remain behaviourally unchanged.

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
