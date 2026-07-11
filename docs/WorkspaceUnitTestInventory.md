# Workspace Unit-Test Inventory

Date: 2026-07-10

## Purpose

This document inventories the production behaviour in `Roslyn.Workbench.Mcp.Workspace`, records its current unit and integration coverage, and defines the work required to establish meaningful Workspace unit tests.

It follows the repository test rules:

- xUnit, Moq and AwesomeAssertions
- one behavioural concern per test
- production collaborators mocked and visible in the test class
- real Roslyn objects used only as in-memory Roslyn data when mocks cannot represent syntax, semantic or solution behaviour
- no temporary projects, real `MSBuildWorkspace`, filesystem persistence, coordinator composition, reflection or test-only production hooks in unit tests
- 100% line and branch coverage for each implementation selected for unit testing, except explicitly approved unreachable Roslyn defensive branches

The inventory does not treat integration coverage as unit coverage. It also does not propose duplicating filesystem and MSBuild acceptance tests in the unit project.

## Baseline

Workspace now owns its selectors, resolution results, diagnostics, change/transaction models, neutral execution contexts, mutation proposal and staging boundary. It has no production dependency on Plugins or CodeActions.

The production assembly currently contains 113 C# source files after the contract-family relocation. Data-only contracts are covered through validation, serialisation and owning-service assertions rather than one reflection test per type.

The existing six Workspace unit tests were removed after review because they did not fully meet the current namespace, reusable Roslyn-data setup and coverage standards. The current replacement baseline is:

| Test class | Tests | Target coverage |
| --- | ---: | ---: |
| `WorkspaceSelectionResultTests` | 4 | 100% line, 100% branch |
| `WorkspaceSelectorServiceTests` | 18 | Supported selection flow: 100% line, 100% branch |
| `WorkspaceResolverTests` | 48 | Public resolver: 100% line; Roslyn defensive branches documented below |
| `WorkspaceResolverFactoryTests` | 1 | Resolver creation against the supplied solution and snapshot context |
| `SelectorValidationTests` | 38 | Every selector and scope validation alternative: 100% line and branch |
| `WorkspaceExecutionLeaseTests` | 7 | Query and mutation acquisition/rejection capability and disposal branches: 100% line and branch |
| `WorkspaceMutationStagerTests` | 1 | Exact mutation-staging delegation: 100% line and branch |
| `WorkspaceExecutionContextFactoryTests` | 25 | 100% line, 100% branch |
| `WorkspaceCoordinatorOptionsTests` | 1 | 100% line, 100% branch |
| `WorkspaceOperationResultFactoryTests` | 9 | 100% line, 100% branch |
| `WorkspaceOperationGateTests` | 8 | 100% line, 100% branch |
| `WorkspaceSessionStoreTests` | 15 | 100% line, 100% branch |
| `WorkspaceStateMachineTests` | 12 | 100% line, 100% branch |
| `WorkspaceStateTransitionsTests` | 6 | 100% line, 100% branch |
| `SnapshotGuardTests` | 8 | 100% line, 100% branch |
| `WorkspaceTransactionTests` | 7 | 100% line, 100% branch |
| `MutationStagingServiceTests` | 34 | Public staging flow and all reachable proposal-validation alternatives covered |
| `WorkspaceDiffBuilderTests` | 12 | Change summaries, document diffs, hunk grouping, context handling and cancellation |
| `WorkspaceDiffServiceTests` | 2 | Summary and detailed-diff delegation |
| `TransactionServiceTests` | 49 | Start, preview, history, commit, rollback and cancellation: 100% line and branch |
| `WorkspaceLoaderTests` | 15 | Path and alias normalization, case-insensitive supported extensions and malformed paths: 100% line and branch for both public normalization methods |
| `WorkspaceLifecycleServiceTests` | 56 | Open, list, close, status, reload and cancellation: 100% line and branch |
| `TransactionCommitServiceTests` | 12 | Commit validation, recovery sequencing, writing, workspace application, success, failures and cancellation: 100% line and branch |

The final pre-removal unit-project Coverlet run produced:

| Assembly | Line coverage | Branch coverage |
| --- | ---: | ---: |
| `Roslyn.Workbench.Mcp.Workspace` | 7.93% | 5.01% |

Only three implementations recorded unit coverage before removal:

| Implementation | Line coverage | Branch coverage |
| --- | ---: | ---: |
| `WorkspaceOperationGate` | 100% | 75% |
| `WorkspaceStateMachine` | 71.42% | 100% reported for currently visited decisions |
| `WorkspaceDiffBuilder` | 97.29% | 85% |

These figures are retained only as the evidence that motivated this inventory; they are no longer current test coverage. Compiler-generated async classes are not separate test targets and will be covered through their owning public methods.

The current Workspace unit checkpoint discovers 388 tests and measures 87.13% line and 85.29% branch coverage across the production assembly. Lifecycle, commit orchestration, leases, mutation-stager delegation and contract validation now measure 100% independently. The remaining uncovered executable areas are concentrated in recovery persistence, commit writing, loaded-workspace adaptation, input-manifest infrastructure and MSBuild compatibility boundaries.

### Current integration safety net

`Roslyn.Workbench.Mcp.Workspace.IntegrationTest` contains 42 tests:

| Test class | Tests | Boundary coverage |
| --- | ---: | --- |
| `WorkspaceCoordinatorIntegrationTests` | 30 | Open/close/reload/status, multi-workspace selection, transaction workflow, staging, history, commit, encoding, change detection, recovery and cancellation |
| `WorkspaceResolverIntegrationTests` | 9 | Real-workspace project/document ambiguity, snapshots, locations and symbols |
| `MsBuildProjectUtilitiesIntegrationTests` | 2 | SDK-style and legacy project compatibility |
| `WorkspaceInputManifestBuilderIntegrationTests` | 1 | Evaluated MSBuild import tracking |

These tests are retained. They prove MSBuild, filesystem, `MSBuildWorkspace`, coordinator and transaction-pipeline behaviour, but they do not provide isolated branch evidence for the production services listed below.

## Coverage Classification

Each executable implementation is assigned one of four dispositions:

| Disposition | Meaning |
| --- | --- |
| Add now | Can be tested as a true unit with the current production design |
| Expand | Existing unit tests need additional scenarios to close reachable branches |
| Seam required | Important orchestration logic exists, but strict unit coverage needs an approved production abstraction or responsibility split |
| Integration boundary | The implementation exists specifically to exercise filesystem, MSBuild or real workspace infrastructure and should remain integration coverage |

Data-only records, enums, constants and interfaces do not receive direct tests unless they contain calculated behaviour. They are covered through the implementation that consumes or produces them.

## Immediate Unit-Test Inventory

### Configuration

| Target | Test class | Required coverage | Priority |
| --- | --- | --- | --- |
| `WorkspaceCoordinatorOptions` | `WorkspaceCoordinatorOptionsTests` | Covered: default concurrent-query, result-limit, revision, loaded-workspace and recovery-directory values | Complete |

These tests lock operational defaults rather than record shape. Property initialisers themselves do not need one test per property beyond the single default projection.

### Execution contexts

The capability-boundary redesign is complete. `IWorkspaceExecutionContext` contains only solution, identity, transaction revision, result limit and resolver. Query and mutation acquisition use `WorkspaceSelector`; mutation staging is a separate `IWorkspaceMutationStager` held by the lease, not implemented by the handler context. Unit coverage should now target every acquisition rejection/conflict branch, disposal and separation of context from staging capability.

### Operations

| Target | Test class | Required coverage | Priority |
| --- | --- | --- | --- |
| `WorkspaceOperationResultFactory` | `WorkspaceOperationResultFactoryTests` | Covered: success, both rejection overloads, both conflict overloads, fault and no-change; supplied/default values | Complete |

`WorkspaceOperationContext`, `WorkspaceOperationError`, `WorkspaceOperationResult<TOutcome>` and `WorkspaceOperationStatus` are data/result shapes. They are asserted through the factory and service tests rather than through reflection or property-only tests.

### Selection

| Target | Test class | Required coverage | Priority |
| --- | --- | --- | --- |
| `WorkspaceSelectionResult` | `WorkspaceSelectionResultTests` | Success/failure state, `HasError`, retained value and null guards | Medium |
| `WorkspaceSelectorService` | `WorkspaceSelectorServiceTests` | Covered: implicit selection, explicit ID/alias/path selection, not-found cases, empty selectors and cross-field mismatch | Complete |
| `WorkspaceResolver` | `WorkspaceResolverTests` | Covered: path normalisation, document/project/location/symbol resolution, snapshot validation and canonical references using in-memory Roslyn solutions | Complete with documented Roslyn guards |

`WorkspaceSelectorServiceTests` must cover:

- null selector with zero, one and multiple workspaces
- workspace ID found and not found
- alias found and not found
- absolute path normalisation, found and not found
- selector with no populated fields
- two or three selector fields resolving to the same workspace
- fields resolving to different workspaces
- exact/case-sensitive alias and path behaviour
- returned error code, message and required action for required, not-found and mismatch results

`WorkspaceSelectorService` and its compiler-generated local-function class now measure 100% line and branch coverage. The redundant null-candidate guard was removed after explicit approval because every caller has already rejected null or lookup failure before invoking the matcher.

`WorkspaceResolver` has 100% line coverage across its public synchronous flow. The remaining generated async branches are defensive Roslyn guards for a missing semantic model or compilation from an otherwise valid C# project/document, plus condition lowering around supported span and selection validation. They cannot be reached with supported in-memory Roslyn objects without fake Roslyn runtime implementations and are retained under the repository's documented Roslyn defensive exception.

`WorkspaceResolverTests` should use narrow in-memory Roslyn document and solution factories. Required behaviour includes:

- absolute, workspace-relative, empty and slash-normalised document/project paths
- document references containing Roslyn document/project IDs and normalised paths
- resolved source locations with one-based line/column, span and snapshot identity
- non-source locations, missing source trees and missing workspace identity returning no resolved location
- symbol references with and without source locations
- absent snapshot, absent workspace identity, workspace-ID mismatch, epoch mismatch, revision mismatch and match
- document resolution by valid ID, invalid ID with path fallback, path, ambiguous path and not found
- project resolution by ID, name, path, combined fields, ambiguity and not found
- location selector with neither span nor selection
- valid, negative and out-of-range text spans
- text selection with missing document/text, one match, multiple matches, no match, `ContextBefore`, `ContextAfter` and context mismatch
- propagation of ambiguous document resolution into location and symbol results
- symbol resolution by declaration/reference documentation-comment ID, missing ID and multiple source matches
- symbol resolution at a location through `SymbolFinder` and the declared/symbol-info fallback
- cancellation while searching compilations

Potential approved defensive exceptions must be measured rather than assumed. In particular, a missing syntax tree or semantic model from an otherwise supported in-memory C# document may be unreachable through the public resolver flow.

`ResolvedDocumentSpan` and `WorkspaceSelection` are internal data carriers and need no direct tests.

`WorkspaceSelectionResultTests` was completed as the generation-pattern calibration item. It uses the required `Roslyn.Workbench.Mcp.Workspace.Test.Selection` namespace and achieves 100% line and branch coverage.

### State

| Target | Test class | Required coverage | Priority |
| --- | --- | --- | --- |
| `WorkspaceOperationGate` | `WorkspaceOperationGateTests` | Covered: all acquisition/release and idempotent-disposal branches | Complete |
| `WorkspaceSessionStore` | `WorkspaceSessionStoreTests` | Covered: allocation, validation, read, replace, remove, ownership and immutable snapshots | Complete |
| `WorkspaceStateMachine` | `WorkspaceStateMachineTests` | Covered: every permitted transition and invalid trigger | Complete |
| `WorkspaceStateTransitions` | `WorkspaceStateTransitionsTests` | Covered: ready/out-of-date, active/conflicted and unchanged-state behaviour | Complete |

Additional `WorkspaceOperationGateTests`:

- zero and negative maximum concurrency throw
- an exclusive lease blocks shared acquisition
- an exclusive lease blocks another exclusive acquisition
- releasing exclusive permits shared acquisition
- releasing shared permits another shared acquisition at the limit
- disposing the same lease twice does not release twice or corrupt the gate

Additional `WorkspaceStateMachineTests`:

- permitted triggers for `TransactionActive`, `TransactionConflicted` and `WorkspaceOutOfDate`
- every valid transition: external change, start, commit, rollback, conflict, conflicted rollback and reload
- an invalid trigger/state pair throws and leaves the state unchanged

`WorkspaceSessionStoreTests`:

- initial empty immutable snapshot
- monotonically allocated IDs and epochs
- null session and null validator guards
- validation rejection does not mutate state
- successful add and subsequent read
- missing removal
- removal clears ownership only for the owner workspace
- removal preserves a different owner
- replace updates only the matching session
- replace-and-set-owner changes both values atomically
- returned snapshots are not mutated by later operations

`WorkspaceStateTransitionsTests`:

- null session guard
- `Ready` becomes `WorkspaceOutOfDate`
- `TransactionActive` becomes `TransactionConflicted`
- already out-of-date, conflicted and other unsupported states return the same session instance
- `Fire(...)` delegates all valid state-machine transitions

`WorkspaceHostSnapshot`, `WorkspaceSessionSnapshot`, `WorkspaceErrorCodes`, `WorkspaceTrigger` and the state interfaces are covered through these behavioural tests.

### Transactions: pure behaviour

| Target | Test class | Required coverage | Priority |
| --- | --- | --- | --- |
| `SnapshotGuard` | `SnapshotGuardTests` | Covered: null guard, no transaction/precondition, every snapshot mismatch and exact match | Complete |
| `WorkspaceTransaction` | `WorkspaceTransactionTests` | Covered: current solution selection and every calculated `TransactionInfo` capability | Complete |
| `WorkspaceDiffBuilder` | `WorkspaceDiffBuilderTests` | Covered across added, modified and deleted summaries, missing/added/deleted/modified document diffs, context handling, hunk grouping, line-ending normalisation and cancellation | Complete with production refactors identified |

`SnapshotGuardTests` must independently cover workspace ID, epoch and revision mismatches, a blank optional workspace ID, an exact match, no transaction and no expected snapshot.

`WorkspaceTransactionTests` must cover:

- revision zero returns the baseline solution
- positive revision returns the selected revision solution
- zero, middle, maximum and over-capacity revisions
- conflicted and non-conflicted capability projections
- undo, redo, commit, mutate and rollback flags
- remaining revisions clamped at zero

Additional `WorkspaceDiffBuilderTests`:

- no solution changes
- added document summary
- deleted document summary
- line replacement counted as changed rather than separate add/remove
- mixed add/modify/delete across projects
- added-only document diff
- deleted-only document diff
- document missing from both solutions returns null
- current document reference preferred when present; baseline reference used for deletion
- zero and negative context lines
- adjacent edits merged into one hunk and distant edits remain separate
- CRLF/LF normalisation
- hunk headers with omitted and explicit counts
- no-newline marker omitted from returned hunk lines
- cancellation during summary enumeration

Null documents returned for Roslyn change IDs appear defensive and may be unreachable with a valid `SolutionChanges` graph. Measure those paths and document them as approved Roslyn defensive exceptions if required.

The builder trusts the invariants of Roslyn's added, changed and removed document ID collections and parses the explicit-count hunk headers emitted by DiffPlex. Truly empty split entries are discarded so the renderer's terminal newline does not add a spurious blank entry to the final hunk, while meaningful blank diff lines retain their prefix. The builder measures 100% line and branch coverage.

## Production Seams and Remaining Coverage

The following classes contain important orchestration logic, but adding partial tests around only their early exits would leave the implementation below the repository's coverage rule. Do not use real workspaces, filesystem fixtures or general integration harnesses in the unit project to force coverage.

### Execution-context capability boundary

This production seam is now complete. Workspace creates only neutral contexts and separate staging leases. Plugins and CodeActions each adapt those leases in their own assembly, and Host chooses the correct typed adapter from the corresponding catalogue without runtime type discrimination.

Existing boundary evidence covers lease disposal and confirms that the mutation stager is separate from the Workspace handler context. Remaining Workspace unit coverage should cover:

- base Workspace contexts expose only shared properties and no staging capability
- code-action handlers receive the explicit CodeActions-owned adapter
- query and mutation acquisition rejection paths, external-change transitions and lease ownership/release
- mutation owner, out-of-date, conflicted, missing-transaction and revision-capacity branches
- staging-result mapping for success, rejection, conflict, fault and no-change
- code-action request delegation and replay-selection mapping in `Roslyn.Workbench.Mcp.CodeActions.Test`, not the Workspace unit project

`WorkspaceExecutionContextFactoryTests` now cover every acquisition, selection, gate, disappearance, external-change, owner, state, transaction and capacity branch at 100% line and branch coverage. The session stores an `IWorkspaceOperationGate`; the production gate remains the concrete runtime implementation.

`MutationStagingServiceTests` cover the complete public staging flow and every proposal rejection alternative through an injected `IWorkspaceDiffBuilder`. This includes every mutable project option, reference family, additional-document operation, analyzer-config-document operation, source-document kind/path rule and successful revision replacement. Non-source-document validation precedes effective compilation-option validation so analyzer-config deltas reach their specific rejection. The service measures 100% line and branch coverage.

### `WorkspaceLifecycleService`

Recovery persistence is now owned by the injected `ICommitRecoveryStore` implementation rather than static filesystem access. Loaded Roslyn workspace lifetime and solution application are owned by `ILoadedWorkspace`, with the concrete `MSBuildWorkspace` confined to the loader adapter. `WorkspaceLifecycleServiceTests` cover every open, list, close, status, reload and entry-point cancellation branch and measure 100% line and branch coverage.

Once approved, `WorkspaceLifecycleServiceTests` should cover:

- open: invalid path, capacity, duplicate path, duplicate alias, pending recovery, compatibility diagnostics, non-SDK project, load failure, loaded-solution compatibility failure, race-time validation failure and success
- list: empty/multiple workspaces, deterministic ordering and transaction owner
- close: no workspace, selector error, busy, disappeared session, active/conflicted transaction, failed removal and success/disposal
- status: no workspace, selector error, busy, disappeared session, unchanged state, detected change from ready/active, minimal/standard/full projection
- reload: no workspace, selector error, busy, disappeared session, active/conflicted transaction, reload not required, compatibility failures, load failure and success preserving identity/gate while increasing epoch
- cancellation before work and during post-load project validation

### `MutationStagingService`

Current blockers:

- calls static `WorkspaceDiffBuilder`
- constructs a concrete `WorkspaceResolver`
- proposal validation is embedded in the orchestration class

Recommended design decision before tests:

- extract a focused mutation-proposal validator, or inject an `IMutationProposalValidator`
- inject a narrow diff service and resolver factory if staging orchestration itself must remain a strict unit

After approval, cover:

- no owner and missing owner transaction
- null candidate solution
- different Roslyn workspace or solution path
- project add/remove and project identity/options changes
- metadata, project, analyzer, additional-document and analyzer-config changes
- source-document metadata-only changes
- added, changed and removed source documents that are non-regular, pathless or outside the project directory
- valid text change, add and delete
- redo truncation after undo
- revision append/current-solution update
- propagation and concatenation of diagnostics, input warnings and proposal warnings
- operation, summary, preview, changes and transaction projection
- cancellation before store access and during diff creation

### `TransactionService`

The summary-only staging seam has been replaced by a shared `IWorkspaceDiffBuilder`, and both staging and transaction preview now consume it. Workspace resolver creation is provided by `IWorkspaceResolverFactory`, shared with staging and execution-context creation. `TransactionServiceTests` cover every start, preview, history, commit, rollback and entry-point cancellation branch and measure 100% line and branch coverage. `IWorkspaceChangeDetector` remains injected and stored by `TransactionService` without being used; removing that redundant dependency is a separate production cleanup.

After approval, `TransactionServiceTests` should cover every branch of:

- start: no workspace, selector error, busy, disappeared session, out-of-date, other owner, existing transaction and success
- preview: no workspace, selector error, busy, missing transaction, summary-only, diff omitted, unresolved document and resolved document diff
- history: no workspace, selector error, busy, missing transaction, snapshot mismatch, conflicted transaction, undo/redo success and unavailable move
- commit routing: no workspace, selector error, busy and exact delegation to the commit service
- rollback: no workspace, selector error, busy, missing transaction, normal rollback and conflicted rollback
- owner display-name alias/path/ID/unknown fallback
- cancellation at every public entry point

### `TransactionCommitService`

Filesystem creation, writing, deletion and encoding preservation are owned by the injected `IWorkspaceCommitWriter`. Recovery writes/deletes use the lifecycle-owned `ICommitRecoveryStore`, and workspace application uses `ILoadedWorkspace`. `TransactionCommitServiceTests` cover every orchestration and cancellation branch and measure 100% line and branch coverage. Concrete writer and recovery durability remain integration responsibilities.

After approval, `TransactionCommitServiceTests` should cover:

- missing session/transaction
- snapshot mismatch
- already-conflicted transaction
- zero-revision no-change
- external change transition and conflict
- prepared/applying/recovery-incomplete recovery state sequence
- successful writer invocation, workspace apply, manifest rebuild, state transition, session replacement, owner clearing and recovery deletion
- writer `IOException` and `UnauthorizedAccessException` fault mapping
- cancellation before work and during write

Concrete file creation, modification, deletion, encoding preservation and recovery durability remain integration tests for the extracted boundary implementations.

## Mixed and Integration-Only Inventory

These implementations must not be moved into the unit project merely to improve the assembly percentage.

| Target | Disposition | Existing coverage | Remaining boundary inventory |
| --- | --- | --- | --- |
| `WorkspaceLoader` | Mixed | Coordinator integration covers project/solution load and diagnostics; unit tests cover `NormalizeOpenPath` and `NormalizeAlias` at 100% line and branch | Keep `InspectCompatibility` and `LoadAsync` integration; normalization preserves path casing, recognizes supported extensions case-insensitively and rejects malformed rooted paths without throwing |
| `MsBuildProjectUtilities` | Integration boundary | SDK and legacy compatibility tests; imported props through manifest test | Missing path, malformed project, evaluated-import de-duplication and recoverable I/O failure |
| `WorkspaceHostServicesAccessor` | Composition/data adapter | Host composition and coordinator integration | No dedicated unit test required unless it gains branching behaviour |
| `WorkspaceInputFileFingerprint` | Integration boundary | Indirect manifest/change tests | File path, length and timestamp capture |
| `WorkspaceInputDirectoryFingerprint` | Integration boundary | Indirect manifest/change tests | Directory path and timestamp capture |
| `WorkspaceInputManifestBuilder` | Integration boundary | Imported props test plus coordinator input-change scenarios | Source/additional/analyzer-config documents, analyzer/metadata references, directory discovery, de-duplication and `bin`/`obj` exclusion |
| `WorkspaceInputManifestValidator` | Integration boundary | Coordinator detects changed/deleted/added inputs | Null manifest, unchanged input, missing directory/file, timestamp change, length-only change and cancellation |
| `WorkspaceChangeDetector` | Integration boundary adapter | Coordinator integration | No direct unit test while it only delegates to static filesystem implementations |
| `CommitRecoveryStore` | Integration boundary | Recovery status and pending-open scenarios | Blank/missing store, multiple records, malformed JSON, deletion present/absent and recoverable read failure |

`WorkspaceLoader.NormalizeOpenPath` has a contract decision to resolve before locking tests: it currently accepts only lower-case `.sln`, `.slnx` and `.csproj`, while `LoadAsync` checks `.csproj` case-insensitively. Decide whether extension matching is intended to be case-sensitive across platforms.

## Types Requiring No Direct Tests

### Interfaces

The following interfaces are seams and receive coverage through their implementations and Moq-based consumers:

- `IWorkspaceChangeDetector`
- `IWorkspaceExecutionContextFactory`
- `IWorkspaceLifecycleService`
- `IWorkspaceLoader`
- `IWorkspaceOperationResultFactory`
- `IWorkspaceSelector`
- `IWorkspaceSessionStore`
- `IWorkspaceStateTransitions`
- `IMutationStagingService`
- `ISnapshotGuard`
- `ITransactionCommitService`
- `ITransactionService`

### Data-only records and classes

These contain no calculated behaviour and are asserted as outputs or test data through their owning services:

- `WorkspaceInputManifest`
- `WorkspaceCloseOutcome`
- `WorkspaceListOutcome`
- `WorkspaceOpenOutcome`
- `WorkspaceReloadOutcome`
- `WorkspaceStatusOutcome`
- `WorkspaceLoadResult`
- `WorkspaceOperationContext`
- `WorkspaceOperationError`
- `WorkspaceOperationResult<TOutcome>`
- `ResolvedDocumentSpan`
- `WorkspaceSelection`
- `WorkspaceHostSnapshot`
- `WorkspaceSessionSnapshot`
- `MutationStagingOutcome`
- `TransactionCommitOutcome`
- `TransactionHistoryOutcome`
- `TransactionPreviewOutcome`
- `TransactionRollbackOutcome`
- `TransactionStartOutcome`
- `WorkspaceTransactionRevision`

The enums `WorkspaceOperationStatus` and `WorkspaceTrigger`, and the constants in `WorkspaceErrorCodes`, are covered through branch and result assertions. Do not add reflection or constant-value-only tests.

## Proposed Delivery Phases

### W1: Core state, results and selectors

Add or expand:

- `WorkspaceOperationResultFactoryTests`
- `WorkspaceSelectionResultTests` (complete)
- `WorkspaceSelectorServiceTests`
- `WorkspaceOperationGateTests`
- `WorkspaceSessionStoreTests`
- `WorkspaceStateMachineTests`
- `WorkspaceStateTransitionsTests`
- `SnapshotGuardTests`
- `WorkspaceTransactionTests`

This phase is pure, fast and requires no production changes.

### W2: Resolver and diff algorithms

Add/expand:

- `WorkspaceResolverTests`
- `WorkspaceDiffBuilderTests`

Use only narrow in-memory Roslyn data factories and visible Moq resolver configuration. Record any genuinely unreachable Roslyn defensive guards.

### W3: Context and normalisation plumbing

Add:

- `WorkspaceExecutionLeaseTests` (initial boundary coverage complete; expand with any uncovered lease branches)
- `WorkspaceExecutionContextFactoryTests`
- `WorkspaceCoordinatorOptionsTests`
- the pure normalisation cases in `WorkspaceLoaderTests`

Production unavailable fallbacks have been removed. This phase closes the remaining immediate plumbing behaviour against the neutral Workspace execution boundary.

### W4: Coverage checkpoint and seam approval

Run per-class line and branch coverage. Confirm that W1-W3 implementations meet 100% or have documented approved defensive exceptions.

Review and explicitly approve or reject the remaining proposed seams for:

- recovery storage
- loaded workspace lifetime/apply behaviour
- diff/resolver creation
- mutation proposal validation
- commit filesystem writing

Do not begin partial orchestration test classes until this decision is made.

### W5: Lifecycle and transaction orchestration

After the remaining approved production seams are implemented, add:

- `WorkspaceLifecycleServiceTests`
- `MutationStagingServiceTests`
- `TransactionServiceTests`
- `TransactionCommitServiceTests`

Keep the existing Workspace integration tests and add only the missing infrastructure-bound cases identified above.

CodeActions-owned context-adapter coverage already lives in `Roslyn.Workbench.Mcp.CodeActions.Test`; it is not part of the deferred Workspace programme.

## Test-Support Rules for This Work

- Do not reintroduce a workspace coordinator, temporary-project fixture or production service graph into `Roslyn.Workbench.Mcp.TestSupport`.
- Prefer visible per-class `Mock<T>` fields for Workspace service collaborators.
- Reuse existing Roslyn-only factories from `Roslyn.Workbench.Mcp.TestSupport` where their shapes are sufficient.
- If Workspace-specific snapshot data becomes repetitive, use a narrow project-local data factory only after confirming it does not hide mocks, setup, verification or branch state.
- Do not assign a real `MSBuildWorkspace` to a unit-test snapshot merely to satisfy a required property. A target that touches that property is integration coverage or needs a production seam.
- Avoid `null!` for runtime collaborators except in a test whose target provably cannot observe that member; prefer a seam decision when several tests require it.
- Use theories for input matrices only when every data row remains visible to xUnit and does not trigger analyzer rule `xUnit1044`.

## Completion Criteria

The Workspace unit-testing round is complete when:

- every production source file has one documented disposition from this inventory
- every `Add now` or `Expand` implementation has behaviour-focused unit coverage
- each selected implementation reaches 100% line and branch coverage, or its exact unreachable defensive branch is documented
- service tests use Moq for production collaborators and no real filesystem, `MSBuildWorkspace`, coordinator or transaction pipeline
- integration-only boundaries remain in integration projects
- any approved production seams are justified by runtime responsibility, not exposed as test-only hooks
- fast-loop category filtering still selects no integration tests
- build, Workspace unit tests, Workspace integration tests, fast loop and full suite pass

## Measurement Commands

```bash
dotnet test test/Roslyn.Workbench.Mcp.Workspace.Test/Roslyn.Workbench.Mcp.Workspace.Test.csproj --collect:"XPlat Code Coverage" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test test/Roslyn.Workbench.Mcp.Workspace.IntegrationTest/Roslyn.Workbench.Mcp.Workspace.IntegrationTest.csproj --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test --filter "Category!=Integration&Category!=Audit" --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
dotnet test --artifacts-path=/tmp/artifacts/roslyn-workbench-mcp
```
