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

`latest-all` means all current SDK rules enabled by that analysis mode. Microsoft documents that it excludes the legacy rules `CA1005`, `CA1014`, `CA1017`, `CA1021`, `CA1045`, `CA1060`, `CA1501`, `CA1502`, `CA1505`, `CA1506` and `CA1509`. It also does not turn build-inapplicable `IDExxxx` live-analysis rules into compiler diagnostics. Those exclusions are outside this `CAxxxx` baseline and should only be enabled separately if the repository deliberately adopts them.

## Progress Summary

| Measure | Baseline | Remaining |
| --- | ---: | ---: |
| Analyzer findings | 2,205 | 667 |
| Diagnostic IDs | 32 | 21 |
| Files | 379 | 197 |
| Projects | 22 | 22 |
| Production findings | 149 | 123 |
| Test and fixture findings | 2,056 | 544 |

The remaining counts come from the latest successful solution-wide analyzer build. Resolved diagnostics are excluded from the active inventory and recorded separately below.

The compiler emitted one pre-existing fixture error on the first pass because `HostValidQueryPlugin.cs` used `NuGetVersion` without importing `NuGet.Versioning`. The missing using was added so every project could participate in the successful baseline.

Of the six IDE findings that triggered this audit for `GetCodeContextTool`, the `CA2016` finding is resolved. Four `CA1305` findings and one `CA1859` finding remain in the active inventory.

## Active Diagnostic Inventory

| Diagnostic | Rule meaning | Total | Production | Tests | Initial treatment | Batch |
| --- | --- | ---: | ---: | ---: | --- | --- |
| `CA1000` | Avoid static members on generic types because callers must specify the containing type argument | 15 | 15 | 0 | Review generic factory APIs; preserve intentional fluent contracts | Design/API |
| `CA1002` | Public APIs should not expose mutable `List<T>` implementations | 1 | 0 | 1 | Intentional invalid-contract fixture; use a narrow suppression | Test policy |
| `CA1034` | Externally visible types should not be nested inside other types | 54 | 0 | 54 | Predominantly nested request/response fixtures; use scoped test policy | Test policy |
| `CA1040` | Empty interfaces do not define a behavioural contract | 2 | 2 | 0 | Review marker-interface intent and document or suppress if required | Design/API |
| `CA1068` | A `CancellationToken` parameter should be the final parameter | 2 | 1 | 1 | Review cancellation-token ordering with contract compatibility | Design/API |
| `CA1305` | Supply an `IFormatProvider` when formatting values or messages | 15 | 13 | 2 | Use explicit culture for stable diagnostics and comparisons | Determinism |
| `CA1307` | Supply an explicit `StringComparison` for string operations | 12 | 2 | 10 | Supply explicit string comparison semantics | Determinism |
| `CA1308` | Prefer uppercase for normalised strings because lowercase mappings can lose information | 5 | 5 | 0 | Review protocol normalisation; retain lowercase only with explicit rationale | Determinism |
| `CA1515` | Types in application assemblies can often be internal instead of public | 91 | 32 | 59 | Internalise implementation types; preserve real contracts and discovery surfaces | Design/API |
| `CA1711` | Type names should not use suffixes that imply a different kind of type | 2 | 1 | 1 | Review established contract names before renaming | Design/API |
| `CA1802` | A readonly field holding a compile-time value can be a constant | 1 | 0 | 1 | Use a constant where it does not weaken the test scenario | Performance |
| `CA1812` | An internal type appears never to be instantiated | 33 | 0 | 33 | Reflection/discovery fixtures are expected; suppress narrowly after verification | Test policy |
| `CA1819` | Public properties should not return mutable arrays | 1 | 0 | 1 | Preserve the intentional array-contract fixture with a narrow suppression | Test policy |
| `CA1822` | An instance member that uses no instance state can be static | 6 | 0 | 6 | Make helpers static where test clarity is unchanged | Performance |
| `CA1848` | High-performance logging should use cached `LoggerMessage` delegates | 2 | 2 | 0 | Replace hot logging calls with cached `LoggerMessage` delegates | Performance |
| `CA1849` | Async methods should call asynchronous APIs instead of blocking synchronous ones | 42 | 2 | 40 | Review synchronous I/O; preserve explicitly required durable flush semantics | Performance/async |
| `CA1859` | Private code can use a concrete type when abstraction adds overhead without flexibility | 48 | 33 | 15 | Apply concrete types only to private hot paths, not public contracts | Performance |
| `CA1861` | Repeated constant array arguments allocate a new array on every call | 10 | 0 | 10 | Cache repeated constant arrays where worthwhile | Performance |
| `CA1869` | Repeatedly constructing `JsonSerializerOptions` prevents caching and adds overhead | 11 | 0 | 11 | Reuse immutable serializer options in test infrastructure | Performance |
| `CA2007` | Library awaits should normally state whether the captured context is required | 298 | 15 | 283 | Fix production awaits; decide and enforce a consistent test policy | Async policy |
| `CA2263` | Prefer a generic overload when the type is already known at compile time | 16 | 0 | 16 | Use generic assertion overloads | Test cleanup |

## Resolved Diagnostics

| Diagnostic | Baseline | Resolution |
| --- | ---: | --- |
| `CA1001` | 1 | Made the owning test class disposable and disposed its reusable memory stream |
| `CA1031` | 17 | Narrowed token parsing catches and added symbol-scoped rationale to genuine plugin, Roslyn, workspace, disposal, audit and top-level MCP isolation boundaries |
| `CA1062` | 16 | Internalised Host execution leases and shared-test implementation types, made nested serialisation fixtures private, documented the xUnit-supplied theory value, and retained validation only at genuine plugin entry-point boundaries |
| `CA1065` | 1 | Documented the intentional temporary-directory disposal exception, which must surface test cleanup failures instead of hiding them |
| `CA1508` | 2 | Removed the redundant production null path and added a method-scoped suppression for the deliberate equality-contract test |
| `CA1707` | 1,462 | Suppressed for `IsTestProject` builds because GIVEN/WHEN/THEN names are mandated |
| `CA2000` | 5 | Disposed server and workspace test resources and documented the Roslyn wrapper's explicit workspace-ownership transfer |
| `CA2012` | 13 | Changed Moq setups to create a fresh faulted or cancelled `ValueTask` for every invocation instead of storing reusable instances |
| `CA2016` | 3 | Forwarded the execution cancellation token to all three Roslyn operations |
| `CA2213` | 1 | Added a targeted suppression for `_gate`, which intentionally remains usable for queued, repeated and post-disposal lifecycle calls and never creates an OS wait handle |
| `CA5392` | 3 | Restricted each system-library import to `System32`; the attribute is ignored on Unix |

## Project Inventory

| Project | Remaining | IDs | Files |
| --- | ---: | ---: | ---: |
| `src/Roslyn.Workbench.Mcp` | 49 | 6 | 41 |
| `src/Roslyn.Workbench.Mcp.CodeActions` | 10 | 4 | 5 |
| `src/Roslyn.Workbench.Mcp.Plugins` | 10 | 4 | 6 |
| `src/Roslyn.Workbench.Mcp.Plugins.Core` | 30 | 5 | 13 |
| `src/Roslyn.Workbench.Mcp.Workspace` | 24 | 4 | 11 |
| `test/Roslyn.Workbench.Mcp.AcceptanceTest` | 28 | 3 | 9 |
| `test/Roslyn.Workbench.Mcp.CodeActions.AuditTest` | 61 | 3 | 5 |
| `test/Roslyn.Workbench.Mcp.CodeActions.IntegrationTest` | 6 | 2 | 3 |
| `test/Roslyn.Workbench.Mcp.CodeActions.Test` | 48 | 8 | 17 |
| `test/Roslyn.Workbench.Mcp.IntegrationTest` | 11 | 4 | 4 |
| `test/Roslyn.Workbench.Mcp.IntegrationTestSupport` | 21 | 2 | 4 |
| `test/Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` | 12 | 4 | 7 |
| `test/Roslyn.Workbench.Mcp.Plugins.Core.Test` | 24 | 4 | 10 |
| `test/Roslyn.Workbench.Mcp.Plugins.Test` | 60 | 6 | 11 |
| `test/Roslyn.Workbench.Mcp.Test` | 98 | 12 | 22 |
| `test/Roslyn.Workbench.Mcp.TestSupport` | 2 | 1 | 2 |
| `test/Roslyn.Workbench.Mcp.Workspace.IntegrationTest` | 58 | 3 | 6 |
| `test/Roslyn.Workbench.Mcp.Workspace.LockFixture` | 1 | 1 | 1 |
| `test/Roslyn.Workbench.Mcp.Workspace.Test` | 104 | 7 | 14 |
| `test/TestFixtures/Plugins/Roslyn.Workbench.Mcp.HostMutationPluginFixture` | 1 | 1 | 1 |
| `test/TestFixtures/Plugins/Roslyn.Workbench.Mcp.HostQueryPluginFixture` | 2 | 1 | 1 |
| `test/TestFixtures/Plugins/Roslyn.Workbench.Mcp.InvalidPluginFixture` | 7 | 1 | 4 |

## Remediation Order

Use cohesive batches and rerun the full analyzer baseline after each batch. Do not mix unrelated analyzer cleanup with performance measurements.

1. Continue establishing narrow test policy for intentional negative fixtures.
2. Address production determinism findings: `CA1305`, `CA1307` and `CA1308`.
3. Address production async findings before changing test-wide async policy: production `CA2007` and `CA1849`.
4. Establish the test async policy for test `CA2007` and `CA1849` findings.
5. Address production performance findings using measurements where a suggestion changes abstractions: `CA1848` and `CA1859`.
6. Review design/API findings individually. Do not rename public contracts, remove discovery types or change collection shapes solely to satisfy an analyzer.
7. Clean up remaining test-only performance and assertion findings after production remediation is stable.

The performance-tuning baseline should be recorded only after production determinism, async and performance findings are resolved or explicitly accepted, because those changes can affect allocations, cancellation and execution timing.

## Completion Criteria

This inventory is complete when:

- every diagnostic family has an implemented fix or an explicit repository policy;
- suppressions are scoped to the narrowest applicable project, directory, file or symbol and include a clear rationale where the reason is not self-evident;
- normal build and test commands remain green with warnings as errors;
- the solution-wide `latest-all` build reports no unexplained findings; and
- the durable analyzer configuration prevents resolved findings from silently returning.
