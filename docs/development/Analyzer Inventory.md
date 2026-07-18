# .NET Analyzer Inventory

Date: 2026-07-18

## Purpose

This development inventory establishes the solution-wide .NET analyzer baseline that must be triaged before performance tuning. It records every `CAxxxx` finding produced by the supported `latest-all` SDK rule set, separates production findings from test and fixture findings, and defines remediation batches without treating every analyzer suggestion as an automatic design decision.

The inventory is not release documentation. Remove it when the baseline has been resolved and durable analyzer policy is enforced by repository configuration.

## Reproduction

The baseline was collected under WSL with the .NET SDK pinned by `global.json`:

```text
dotnet build Roslyn.Workbench.Mcp.slnx --no-restore --no-incremental \
  --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp \
  -p:AnalysisLevel=latest-all \
  -p:EnforceCodeStyleInBuild=true \
  -p:CodeAnalysisTreatWarningsAsErrors=false
```

Omit `--artifacts-path` when running directly on Windows or Linux. `--no-incremental` is required for a trustworthy solution baseline; otherwise previously built projects can be absent from the result. `CodeAnalysisTreatWarningsAsErrors=false` leaves compiler warnings governed by the repository's normal warnings-as-errors policy while allowing the inventory build to report all code-analysis warnings.

The clean baseline build succeeded with zero compiler errors.

`latest-all` means all current SDK rules enabled by that analysis mode. Microsoft documents that it excludes the legacy rules `CA1005`, `CA1014`, `CA1017`, `CA1021`, `CA1045`, `CA1060`, `CA1501`, `CA1502`, `CA1505`, `CA1506` and `CA1509`. The pinned .NET 10.0.102 SDK nevertheless emits `CA1014` under this command, so this inventory includes the observed findings rather than omitting them based on the documented exclusion. The command does not turn build-inapplicable `IDExxxx` live-analysis rules into compiler diagnostics.

## Progress Summary

| Measure | Baseline | Remaining |
| --- | ---: | ---: |
| Analyzer findings | 2,228 | 269 |
| Diagnostic IDs | 33 | 14 |
| Files | 379 | 136 |
| Projects | 23 | 23 |
| Production findings | 154 | 91 |
| Test and fixture findings | 2,074 | 178 |

The remaining counts come from the latest successful solution-wide analyzer build. Resolved diagnostics are excluded from the active inventory and recorded separately below.

The compiler emitted one pre-existing fixture error on the first pass because `HostValidQueryPlugin.cs` used `NuGetVersion` without importing `NuGet.Versioning`. The missing using was added so every project could participate in the successful baseline.

Of the six IDE findings that triggered this audit for `GetCodeContextTool`, the `CA2016` finding is resolved. Four `CA1305` findings and one `CA1859` finding remain in the active inventory.

## Active Diagnostic Inventory

| Diagnostic | Rule meaning | Total | Production | Tests | Initial treatment | Batch |
| --- | --- | ---: | ---: | ---: | --- | --- |
| `CA1000` | Avoid static members on generic types because callers must specify the containing type argument | 15 | 15 | 0 | Review generic factory APIs; preserve intentional fluent contracts | Design/API |
| `CA1014` | Assemblies should explicitly declare whether they are CLS-compliant | 23 | 5 | 18 | Decide the solution-wide CLS-compliance policy before adding assembly attributes | Design/API |
| `CA1040` | Empty interfaces do not define a behavioural contract | 2 | 2 | 0 | Review marker-interface intent and document or suppress if required | Design/API |
| `CA1068` | A `CancellationToken` parameter should be the final parameter | 2 | 1 | 1 | Review cancellation-token ordering with contract compatibility | Design/API |
| `CA1515` | Types in application assemblies can often be internal instead of public | 91 | 32 | 59 | Internalise implementation types; preserve real contracts and discovery surfaces | Design/API |
| `CA1711` | Type names should not use suffixes that imply a different kind of type | 2 | 1 | 1 | Review established contract names before renaming | Design/API |
| `CA1802` | A readonly field holding a compile-time value can be a constant | 1 | 0 | 1 | Use a constant where it does not weaken the test scenario | Performance |
| `CA1822` | An instance member that uses no instance state can be static | 6 | 0 | 6 | Make helpers static where test clarity is unchanged | Performance |
| `CA1848` | High-performance logging should use cached `LoggerMessage` delegates | 2 | 2 | 0 | Replace hot logging calls with cached `LoggerMessage` delegates | Performance |
| `CA1849` | Async methods should call asynchronous APIs instead of blocking synchronous ones | 40 | 0 | 40 | Group test findings by cancellation, ordinary I/O and intentional durable flush semantics | Performance/async |
| `CA1859` | Private code can use a concrete type when abstraction adds overhead without flexibility | 48 | 33 | 15 | Apply concrete types only to private hot paths, not public contracts | Performance |
| `CA1861` | Repeated constant array arguments allocate a new array on every call | 10 | 0 | 10 | Cache repeated constant arrays where worthwhile | Performance |
| `CA1869` | Repeatedly constructing `JsonSerializerOptions` prevents caching and adds overhead | 11 | 0 | 11 | Reuse immutable serializer options in test infrastructure | Performance |
| `CA2263` | Prefer a generic overload when the type is already known at compile time | 16 | 0 | 16 | Use generic assertion overloads | Test cleanup |

## Resolved Diagnostics

| Diagnostic | Baseline | Resolution |
| --- | ---: | --- |
| `CA1001` | 1 | Made the owning test class disposable and disposed its reusable memory stream |
| `CA1002` | 1 | Suppressed for test and plugin-fixture builds because the mutable `List<T>` contract is an intentional negative contract-inspection scenario |
| `CA1031` | 17 | Narrowed token parsing catches and added symbol-scoped rationale to genuine plugin, Roslyn, workspace, disposal, audit and top-level MCP isolation boundaries |
| `CA1034` | 54 | Suppressed for test and plugin-fixture builds because externally visible nested request, response and handler types are deliberate reflection and contract-validation fixtures |
| `CA1062` | 16 | Internalised Host execution leases and shared-test implementation types, made nested serialisation fixtures private, documented the xUnit-supplied theory value, and retained validation only at genuine plugin entry-point boundaries |
| `CA1065` | 1 | Documented the intentional temporary-directory disposal exception, which must surface test cleanup failures instead of hiding them |
| `CA1305` | 15 | Used invariant culture for published Roslyn diagnostics, diagnostic equality and acceptance-process values so output is stable across environments |
| `CA1307` | 12 | Added explicit ordinal semantics to protocol delimiters, source-fragment matching and test line-ending normalisation |
| `CA1308` | 5 | Replaced lowercase normalisation with explicit code and display labels for selector failures and an explicit query/mutation vocabulary for plugin contract diagnostics |
| `CA1508` | 2 | Removed the redundant production null path and added a method-scoped suppression for the deliberate equality-contract test |
| `CA1707` | 1,462 | Suppressed for `IsTestProject` builds because GIVEN/WHEN/THEN names are mandated |
| `CA1812` | 33 | Audited every finding and added source-local pragma scopes around eight fixture groups used through reflection, DI, closed-generic registration, schema metadata or deliberate activation failures; no dead types were found |
| `CA1819` | 1 | Suppressed for test and plugin-fixture builds because the mutable array contract is an intentional negative contract-inspection scenario |
| `CA2000` | 5 | Disposed server and workspace test resources and documented the Roslyn wrapper's explicit workspace-ownership transfer |
| `CA2007` | 298 | Suppressed solution-wide because all repository code executes within a console-hosted application without a synchronization context; existing `ConfigureAwait(false)` calls were removed and prohibited by agent guidance |
| `CA2012` | 13 | Changed Moq setups to create a fresh faulted or cancelled `ValueTask` for every invocation instead of storing reusable instances |
| `CA2016` | 3 | Forwarded the execution cancellation token to all three Roslyn operations |
| `CA2213` | 1 | Added a targeted suppression for `_gate`, which intentionally remains usable for queued, repeated and post-disposal lifecycle calls and never creates an OS wait handle |
| `CA5392` | 3 | Restricted each system-library import to `System32`; the attribute is ignored on Unix |

## Project Inventory

| Project | Remaining | IDs | Files |
| --- | ---: | ---: | ---: |
| `src/Roslyn.Workbench.Mcp` | 48 | 5 | 40 |
| `src/Roslyn.Workbench.Mcp.CodeActions` | 8 | 3 | 3 |
| `src/Roslyn.Workbench.Mcp.Plugins` | 10 | 4 | 5 |
| `src/Roslyn.Workbench.Mcp.Plugins.Core` | 16 | 4 | 8 |
| `src/Roslyn.Workbench.Mcp.Workspace` | 9 | 3 | 5 |
| `test/Roslyn.Workbench.Mcp.AcceptanceTest` | 2 | 2 | 1 |
| `test/Roslyn.Workbench.Mcp.CodeActions.AuditTest` | 7 | 3 | 4 |
| `test/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest` | 2 | 2 | 1 |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test` | 29 | 6 | 12 |
| `test/Roslyn.Workbench.Mcp.IntegrationTest` | 8 | 3 | 3 |
| `test/Roslyn.Workbench.Mcp.IntegrationTestSupport` | 4 | 2 | 1 |
| `test/Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` | 3 | 3 | 2 |
| `test/Roslyn.Workbench.Mcp.Plugins.Core.Test` | 5 | 3 | 3 |
| `test/Roslyn.Workbench.Mcp.Plugins.Test` | 21 | 4 | 8 |
| `test/Roslyn.Workbench.Mcp.Test` | 45 | 8 | 22 |
| `test/Roslyn.Workbench.Mcp.TestSupport` | 3 | 2 | 2 |
| `test/Roslyn.Workbench.Mcp.Workspace.IntegrationTest` | 6 | 3 | 2 |
| `test/Roslyn.Workbench.Mcp.Workspace.LockFixture` | 2 | 2 | 1 |
| `test/Roslyn.Workbench.Mcp.Workspace.Test` | 37 | 6 | 13 |
| `test/TestFixtures/Plugins/Roslyn.Workbench.Mcp.HostMutationPluginFixture` | 1 | 1 | 0 |
| `test/TestFixtures/Plugins/Roslyn.Workbench.Mcp.HostQueryPluginFixture` | 1 | 1 | 0 |
| `test/TestFixtures/Plugins/Roslyn.Workbench.Mcp.InvalidPluginFixture` | 1 | 1 | 0 |
| `test/TestFixtures/Plugins/Roslyn.Workbench.Mcp.ThrowingPluginFixture` | 1 | 1 | 0 |

## Remediation Order

Use cohesive batches and rerun the full analyzer baseline after each batch. Do not mix unrelated analyzer cleanup with performance measurements.

1. Address the remaining test `CA1849` findings, separating ordinary asynchronous alternatives from intentional durable flushes.
2. Address production performance findings using measurements where a suggestion changes abstractions: `CA1848` and `CA1859`.
3. Review design/API findings individually. Do not rename public contracts, remove discovery types or change collection shapes solely to satisfy an analyzer.
4. Clean up remaining test-only performance and assertion findings after production remediation is stable.

The performance-tuning baseline should be recorded only after production determinism, async and performance findings are resolved or explicitly accepted, because those changes can affect allocations, cancellation and execution timing.

## Completion Criteria

This inventory is complete when:

- every diagnostic family has an implemented fix or an explicit repository policy;
- suppressions are scoped to the narrowest applicable project, directory, file or symbol and include a clear rationale where the reason is not self-evident;
- normal build and test commands remain green with warnings as errors;
- the solution-wide `latest-all` build reports no unexplained findings; and
- the durable analyzer configuration prevents resolved findings from silently returning.
