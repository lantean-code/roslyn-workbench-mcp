# Exception Flow-Control Audit

## Scope

This audit covers production C# beneath `src` as of 2026-07-14. It reviews
explicit `throw`, `ThrowIf*` and `catch` sites, together with callers where a
locally thrown exception is translated into a result. Test code is excluded
because exception assertions and deliberately throwing fakes are test
mechanisms rather than production control flow.

The scan found 240 candidate lines across 85 production files: 175 throw or
guard lines and 65 catch lines. These numbers are a review baseline, not a
violation count. Cancellation, invariant enforcement and translation of
genuine framework, operating-system, filesystem, Roslyn, MSBuild, MEF or plugin
failures remain valid uses of exceptions.

The classification follows the .NET guidance to handle routine conditions in
ordinary code and reserve exceptions for unusual or unexpected failures:

- [Best practices for exceptions](https://learn.microsoft.com/dotnet/standard/exceptions/best-practices-for-exceptions)
- [Exception throwing guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/exception-throwing)

## Completed Remediation

The 2026-07-14 plugin-composition remediation removed two violation families:

- `PluginAssemblyMetadataReader` now returns explicit validation failures from
  custom-attribute parsing. Its outer exception boundary remains responsible
  only for genuine PE, metadata and filesystem exceptions.
- `MefPluginComposer` now returns `PluginCompositionResult` for zero, one or
  multiple exports. `LoadedPluginPreparer` maps an expected composition failure
  to a `PluginComposition` diagnostic, while unexpected MEF or plugin-code
  exceptions continue to be isolated by candidate preparation.

## Remaining Confirmed Violations

| Area | Current exception path | Why it is flow control | Recommended replacement |
|---|---|---|---|
| `CodeActionDiscoveryService` | A batch `RegisterCodeFixesAsync` call catches `ArgumentException`, clears its output and retries every diagnostic separately. | An exception selects between two supported discovery algorithms. | Use one deterministic registration strategy that satisfies `CodeFixContext` preconditions, or select the strategy from inspected diagnostic spans before invoking the provider. |
| `MsBuildRegistration` | `RegisterDefaults` is called and an `InvalidOperationException` filtered by `MSBuildLocator.IsRegistered` is converted into an available status. | The routine already-registered state is represented by an exception even though `IsRegistered` exposes it directly. | Check `IsRegistered` before registration while holding the local lock. Retain only a narrowly documented catch if an unavoidable external registration race still exists. |
| `CommitRecoveryStore` | `GetArtifactPath` throws `InvalidDataException` for an escaping persisted path; `IsValidManifest` catches it and returns `false`. | Unsafe or malformed persisted recovery metadata is an expected validation outcome. | Separate artifact-path validation from required path resolution, returning a validation result for manifest inspection and throwing only if an already-validated internal invariant is later violated. |
| `WorkspaceCommitPlanner`, `WorkspaceCommitWriter` and `TransactionCommitService` | Planner and writer methods throw `IOException` after explicit existence, marker and hash checks; the transaction service catches those exceptions to choose preparation failure or recovery. | Target drift and revalidation rejection are anticipated transactional outcomes, while actual filesystem exceptions are exceptional. Using the same exception type conflates both paths. | Return a commit-plan/revalidation result for detected drift and unsafe targets. Keep real filesystem failures exceptional and translate them at the transaction boundary. Duplicate or out-of-bound targets may remain invariant failures if the caller cannot legitimately produce them; otherwise include them in the planning result. |

## Review Findings That Are Not Flow-Control Violations

| Area | Disposition |
|---|---|
| MCP and server-tool boundaries | Broad catches publish safe MCP failures and preserve cancellation. They translate genuinely unexpected handler, binding and transport failures rather than selecting routine behaviour. |
| Plugin load and materialisation boundaries | Catches around assembly loading, MEF execution, handler construction and reflective generic materialisation isolate third-party code. These remain appropriate after expected validation paths return diagnostics. |
| Workspace loading and project evaluation | Roslyn and MSBuild APIs expose loading/evaluation failures through exceptions. Converting those failures into Workspace diagnostics is a legitimate boundary translation. |
| File locking | `FileStream.Lock` reports lock contention with `IOException`; `FileStreamWorkspaceFileLockProvider.TryAcquire` converts that operating-system API outcome into `null`. There is no managed non-throwing acquisition API to use instead. |
| Atomic writes, recovery and instance-status storage | Catches handle real filesystem, access and JSON failures, clean up partial resources, or preserve recovery state. These are external-boundary failures rather than locally manufactured branches. |
| Cancellation | `ThrowIfCancellationRequested`, filtered `OperationCanceledException` catches and rethrows are intentional stack unwinding, not business-flow discrimination. |
| Exhaustive switches and discriminated-state accessors | Throws for unsupported enum states or missing data in a successful state enforce internal invariants. They should remain unreachable through valid construction. |
| Public configuration freeze checks | Throwing when a plugin mutates configuration after `Configure` returns reports misuse of the public plugin contract, not a normal configuration outcome. |

## Additional Error-Handling Concerns

The following are not exception-for-flow-control violations, but the audit found
that they warrant a separate error-reporting review:

- `DefaultProjectStructureService` catches every non-cancellation exception and
  returns empty framework or hierarchy data. This can make an evaluation failure
  indistinguishable from a project with no data.
- `WorkspaceProjectInputResolver` converts MSBuild, I/O and access failures into
  an empty input list. That can weaken change detection without surfacing why
  evaluated inputs were unavailable.
- Several best-effort recovery and advisory-status paths intentionally suppress
  I/O failures. Their behaviour is defensible, but each silent catch should be
  retained only where the caller has no actionable result channel and the
  failure cannot compromise authoritative recovery state.

## Recommended Remediation Order

1. Remove the Code Action discovery retry-by-exception and cover the chosen
   diagnostic grouping behaviour directly.
2. Separate transaction validation results from genuine filesystem exceptions
   across planner, writer and commit orchestration.
3. Convert recovery artifact validation and the MSBuild already-registered path
   to ordinary result/state handling.
4. Review the silent-degradation findings independently; they concern diagnostic
   fidelity rather than flow-control mechanics.

After remediation, repeat the source scan and update this document with the
remaining reviewed exception boundaries. An analyser can guard simple syntactic
patterns, but caller-aware review is still required because the principal
violation can be a throw in one type and result conversion in another.
