# Exception Flow-Control Audit

## Scope

This audit covers production C# beneath `src` as of 2026-07-15. It reviews
explicit `throw`, `ThrowIf*` and `catch` sites, together with callers where a
locally thrown exception is translated into a result. Test code is excluded
because exception assertions and deliberately throwing fakes are test
mechanisms rather than production control flow.

The initial scan found 240 candidate lines across 85 production files: 175 throw or
guard lines and 65 catch lines. These numbers are a review baseline, not a
violation count. Cancellation, invariant enforcement and translation of
genuine framework, operating-system, filesystem, Roslyn, MSBuild, MEF or plugin
failures remain valid uses of exceptions.

The final repeat scan found 154 candidate lines across 62 production files: 89
explicit throw or guard lines and 65 catch lines. Every remaining candidate is
covered by the completed classifications below. The reduced throw/guard count
reflects the explicit-result and validation work completed during remediation;
the stable catch count reflects retained external-boundary translation,
resource cleanup, cancellation and invariant handling rather than unresolved
flow-control violations.

The classification follows the .NET guidance to handle routine conditions in
ordinary code and reserve exceptions for unusual or unexpected failures:

- [Best practices for exceptions](https://learn.microsoft.com/dotnet/standard/exceptions/best-practices-for-exceptions)
- [Exception throwing guidelines](https://learn.microsoft.com/dotnet/standard/design-guidelines/exception-throwing)

## Completed Remediation

Completed remediation through 2026-07-15 covers every confirmed violation family:

- `PluginAssemblyMetadataReader` now returns explicit validation failures from
  custom-attribute parsing. Its outer exception boundary remains responsible
  only for genuine PE, metadata and filesystem exceptions.
- `MefPluginComposer` now returns `PluginCompositionResult` for zero, one or
  multiple exports. `LoadedPluginPreparer` maps an expected composition failure
  to a `PluginComposition` diagnostic, while unexpected MEF or plugin-code
  exceptions continue to be isolated by candidate preparation.
- `WorkspaceCommitPlanner` and `WorkspaceCommitWriter` now return explicit
  planning and validation results when a target drifts, changes existence or
  has a conflicting delete marker. `TransactionCommitService` maps those
  anticipated outcomes to `TransactionConflicted`, while genuine filesystem
  and access exceptions continue through durable restoration and recovery.
- `CodeActionDiscoveryService` groups fixable diagnostics by their exact source
  span before constructing `CodeFixContext`. Each valid group follows one
  deterministic registration path, with no speculative call or retry catch.
- `CommitRecoveryStore` validates persisted artifact paths through a `Try*`
  path before accepting a manifest. Required artifact resolution throws only
  when an already-validated recovery invariant is violated.
- `MsBuildRegistrationService` checks `MSBuildLocator.IsRegistered` before
  registration and owns its cached status as a DI singleton rather than through
  a separate static state holder. The filtered catch remains only for an
  external registration race between the check and `RegisterDefaults`.
- `WorkspaceProjectInputResolver` now returns an explicit evaluation result
  instead of converting MSBuild, I/O and access failures into an empty input
  list. Open and reload return a retryable Workspace fault before registering
  an incomplete session. A failure discovered after a durable commit retains
  the committed solution, publishes diagnostics and marks the session out of
  date.
- `DefaultProjectStructureService` now distinguishes successful empty metadata
  from project or solution evaluation failure. Its public plugin service returns
  explicit target-framework and hierarchy results; the bundled inspection tools
  map failures to retryable `ProjectStructureUnavailable` rejections. The
  remaining catches are limited to genuine MSBuild, solution-format, XML,
  filesystem and access exceptions.
- `CommitRecoveryStore` now treats every malformed or unreadable owner and
  legacy status record as authoritative `RecoveryConflict` evidence. Only
  validated orphan owners are eligible for automatic cleanup, so persisted
  evidence can no longer disappear through an empty catch.
- `WorkspaceInstanceStatusPublisher` now returns an explicit status result that
  distinguishes availability, known live instances and unreadable live hints.
  Workspace open and status responses publish advisory diagnostics without
  turning advisory-file failures into authoritative recovery failures.
- `TransactionCommitService` now observes whether the final recovery state was
  persisted and enriches commit faults when retained recovery evidence may
  still report an earlier phase.

## Remaining Confirmed Violations

No confirmed exception-for-flow-control violations remain from this audit.

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

## Completed Diagnostic-Fidelity Review

The separate review of best-effort recovery and advisory-status paths is
complete:

- Authoritative recovery reads never suppress malformed, inaccessible or
  unreadable evidence; they return a blocking conflict.
- Advisory instance publication and scanning remain non-blocking, but failures
  and unreadable live hints are visible through workspace diagnostics.
- Recovery-state persistence failure is included in the existing commit fault
  channel.
- Empty catches remain only for secondary temporary-file cleanup and advisory
  update/delete cleanup where propagating the cleanup failure would hide the
  primary failure or incorrectly fail an otherwise safe operation. Stale
  advisory files remain discoverable and removable by later scans.

## Ongoing Enforcement

No remediation item remains from this audit. Future changes should retain the
source guidance prohibiting exceptions for expected flow control. An analyser
can guard simple syntactic patterns, but caller-aware review remains necessary
because a violation can consist of a throw in one type and result conversion in
another.
