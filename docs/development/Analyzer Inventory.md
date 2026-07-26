# .NET Analyzer Inventory

Date: 2026-07-19

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

| Measure                   | Baseline | Remaining |
| ------------------------- | -------: | --------: |
| Analyzer findings         |    2,228 |         0 |
| Diagnostic IDs            |       33 |         0 |
| Files                     |      379 |         0 |
| Projects                  |       23 |         0 |
| Production findings       |      154 |         0 |
| Test and fixture findings |    2,074 |         0 |

The remaining counts come from the latest successful solution-wide analyzer build. Resolved diagnostics are excluded from the active inventory and recorded separately below.

The compiler emitted one pre-existing fixture error on the first pass because `HostValidQueryPlugin.cs` used `NuGetVersion` without importing `NuGet.Versioning`. The missing using was added so every project could participate in the successful baseline.

Of the six IDE findings that triggered this audit for `GetCodeContextTool`, the `CA2016` finding is resolved. Four `CA1305` findings and one `CA1859` finding remain in the active inventory.

## Active Diagnostic Inventory

There are no active diagnostics in the solution-wide `latest-all` baseline.

## Resolved Diagnostics

| Diagnostic | Baseline | Resolution |
| --- | --: | --- |
| `CA1000` | 15 | Moved generic outcome and bounded-collection factories to non-generic companion types, updated call sites, and removed the suppressions |
| `CA1001` | 1 | Made the owning test class disposable and disposed its reusable memory stream |
| `CA1002` | 1 | Suppressed for test and plugin-fixture builds because the mutable `List<T>` contract is an intentional negative contract-inspection scenario |
| `CA1014` | 23 | Suppressed solution-wide because the repository does not promise CLS compliance and Microsoft classifies this legacy rule as excluded from `latest-all` |
| `CA1031` | 17 | Narrowed token parsing catches and added symbol-scoped rationale to genuine plugin, Roslyn, workspace, disposal, audit and top-level MCP isolation boundaries |
| `CA1034` | 54 | Suppressed for test and plugin-fixture builds because externally visible nested request, response and handler types are deliberate reflection and contract-validation fixtures |
| `CA1040` | 2 | Retained and documented the non-generic query and mutation handler interfaces as intentional discovery and registration markers |
| `CA1062` | 16 | Internalised Host execution leases and shared-test implementation types, made nested serialisation fixtures private, documented the xUnit-supplied theory value, and retained validation only at genuine plugin entry-point boundaries |
| `CA1065` | 1 | Documented the intentional temporary-directory disposal exception, which must surface test cleanup failures instead of hiding them |
| `CA1068` | 2 | Reordered the private acceptance timeout helper and documented the replay method's intentional token placement before optional selector filters |
| `CA1305` | 15 | Used invariant culture for published Roslyn diagnostics, diagnostic equality and acceptance-process values so output is stable across environments |
| `CA1307` | 12 | Added explicit ordinal semantics to protocol delimiters, source-fragment matching and test line-ending normalisation |
| `CA1308` | 5 | Replaced lowercase normalisation with explicit code and display labels for selector failures and an explicit query/mutation vocabulary for plugin contract diagnostics |
| `CA1508` | 2 | Removed the redundant production null path and added a method-scoped suppression for the deliberate equality-contract test |
| `CA1515` | 91 | Internalised 32 Host CLR and wire-model implementation types without changing their JSON or MCP shapes, reduced test helpers to the narrowest accessibility, and retained only genuine plugin-contract, schema, xUnit discovery and dynamic-proxy surfaces with source-local rationale |
| `CA1707` | 1,462 | Suppressed for `IsTestProject` builds because GIVEN/WHEN/THEN names are mandated |
| `CA1711` | 2 | Renamed the xUnit collection-definition type and retained `BoundedCollection` because it accurately names the bounded collection wire contract |
| `CA1802` | 1 | Retained the readonly static field in the warning-inspector fixture so it remains distinct from constant state |
| `CA1812` | 33 | Audited every original finding and the private fixtures exposed by the accessibility audit; added source-local pragma scopes for types used through reflection, DI, deserialisation, closed-generic registration, schema metadata or deliberate activation failures; no dead types were found |
| `CA1819` | 1 | Suppressed for test and plugin-fixture builds because the mutable array contract is an intentional negative contract-inspection scenario |
| `CA1822` | 6 | Made stateless test helper methods static |
| `CA1848` | 2 | Replaced the Host's startup-warning and unhandled-tool-exception extension calls with source-generated `LoggerMessage` methods while preserving structured fields and exception details |
| `CA1849` | 42 | Replaced ordinary synchronous operations with asynchronous alternatives, removed fake asynchronous creation and disposal from the synchronous test-fixture ownership chain, and retained narrowly documented durable disk flushes |
| `CA1859` | 48 | Audited every suggested abstraction, used concrete types only in private implementation and test paths, propagated newly exposed concrete shapes, and retained the uniform interface return required by reflection-bound registration factories |
| `CA1861` | 10 | Retained fresh mutable array inputs with source-local rationale so test scenarios cannot share mutable state |
| `CA1869` | 11 | Retained fresh mutable serializer options with source-local rationale so contract, status and recovery scenarios remain isolated |
| `CA2000` | 5 | Disposed server and workspace test resources and documented the Roslyn wrapper's explicit workspace-ownership transfer |
| `CA2007` | 298 | Suppressed solution-wide because all repository code executes within a console-hosted application without a synchronization context; existing `ConfigureAwait(false)` calls were removed and prohibited by agent guidance |
| `CA2012` | 13 | Changed Moq setups to create a fresh faulted or cancelled `ValueTask` for every invocation instead of storing reusable instances |
| `CA2016` | 3 | Forwarded the execution cancellation token to all three Roslyn operations |
| `CA2213` | 1 | Added a targeted suppression for `_gate`, which intentionally remains usable for queued, repeated and post-disposal lifecycle calls and never creates an OS wait handle |
| `CA2263` | 16 | Used generic schema and assertion overloads when the type is compile-time known, while retaining runtime-type overloads in tests that verify dynamic schema dispatch |
| `CA5392` | 3 | Restricted each system-library import to `System32`; the attribute is ignored on Unix |

## Project Inventory

No project has remaining `latest-all` diagnostics.

## Remediation Order

No remediation batches remain. The analyzer baseline is clean, so performance measurements can proceed without known analyzer findings obscuring the results.

## Completion Criteria

This inventory is complete. The final solution-wide `latest-all` build succeeded with zero warnings and zero errors, with every diagnostic family resolved through an implemented fix or an explicit scoped policy.

The completion criteria were:

- every diagnostic family has an implemented fix or an explicit repository policy;
- suppressions are scoped to the narrowest applicable project, directory, file or symbol and include a clear rationale where the reason is not self-evident;
- normal build and test commands remain green with warnings as errors;
- the solution-wide `latest-all` build reports no unexplained findings; and
- the durable analyzer configuration prevents resolved findings from silently returning.
