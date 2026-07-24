# Integration Testing Stage 0 Evidence

Date: 2026-07-17 Status: Complete Baseline: `IntegrationTestingBaseline-2026-07-17.md`

## Scope

Stage 0 made the existing integration and audit suite safe before fixture reuse or additional parallelism. The work was confined to test projects and test infrastructure; no production code or production contract changed.

## Resource Inventory

The inventory covered the four integration projects, the CodeActions audit project and `Roslyn.Workbench.Mcp.IntegrationTestSupport`.

| Resource | Inventory result | Stage 0 ownership |
| --- | --- | --- |
| Workspace runtimes and coordinators | 64 creation, return or consumption sites across Workspace, Plugins.Core, CodeActions, Host and audit coverage | Every consuming test or audit probe now uses `await using`; helper methods that transfer ownership return an undisposed runtime to the caller |
| `TestWorkspaceFixture`, `InspectionSampleFixture` and `SolutionHierarchyFixture` | 73 creation or fixture-factory sites | Each fixture implements `IAsyncDisposable`; declarations ensure runtime and lease disposal occurs before fixture-directory deletion |
| Recovery and runtime state | The default factory previously mapped every runtime to `/tmp/roslyn-workbench-mcp-state` | Every default runtime now owns a unique state directory; explicitly configured state roots have a `TemporaryDirectory` owner at the scenario level |
| Loaded Roslyn workspaces | Stored in the test runtime's `WorkspaceSessionStore` without runtime teardown | Runtime disposal closes instance-status handles, removes every session and disposes every loaded workspace before state deletion |
| Service providers and Hosts | Four creation sites: three Generic Hosts and the per-invocation Code Action provider | Generic Hosts retain deterministic `using` ownership; the Code Action service provider now uses `await using` |
| MEF composition | Plugin MEF composition already disposes its `CompositionHost` inside production composition; Roslyn `MefHostServices` exposes no disposal contract | No undisposed test-owned MEF container was found; runtime and Host owners release all references they control |
| Plugin package directories | Six Host integration scenarios previously created unowned temporary package roots | Each package root now has a `TemporaryDirectory` owner with actionable deletion failures |
| Child processes | One `Process.Start` site launches the Workspace lock fixture for two scenarios | Startup, graceful exit and forced termination are bounded; failure paths always run process cleanup before directory cleanup |
| Cancellation | 152 `CancellationToken.None` occurrences were identified by the proposal; the active Stage 0 scope contained the remaining occurrences | Integration, audit, Roslyn, filesystem and harness calls now use `TestContext.Current.CancellationToken`; the active scope contains zero `CancellationToken.None` calls |

`TemporaryDirectory` reports the exact undeletable path and reminds the caller to dispose workspaces, service providers and child processes. This turns a leaked handle into an actionable teardown failure.

## Isolation Evidence

`GIVEN_IndependentDefaultRuntimes_WHEN_CreatedAndDisposed_THEN_ShouldUseAndDeleteUniqueStateDirectories` creates two runtimes concurrently and proves that:

- their state-directory paths differ;
- both directories exist while their owners are alive;
- both directories are removed after asynchronous disposal.

Explicitly configured recovery roots remain scenario-owned so restart and recovery cases can deliberately share state without making unrelated runtimes share it.

## Process and Directory Evidence

The full suite was run after recording the number of entries beneath every temporary-root family used by the affected tests. The same counts were recorded after completion and compared with `diff`; there was no difference. This proves the verification run added no residual fixture, state, recovery, transaction, plugin-package or lock directory.

After the run, process inspection found no live `Roslyn.Workbench.Mcp.Workspace.LockFixture` or Host process. The lock-fixture tests also reacquired the same inter-process lock after graceful release and after forced process termination.

Historical entries that pre-dated this verification were not deleted as part of Stage 0. The evidence is based on zero growth during the repeated run, not mutation of unrelated pre-existing temporary data.

## Verification

The solution built with no warnings or errors. Each affected project then passed independently:

| Project                  | Passed | Failed | Skipped |
| ------------------------ | -----: | -----: | ------: |
| Workspace integration    |     63 |      0 |       0 |
| Plugins.Core integration |     21 |      0 |       0 |
| CodeActions integration  |     11 |      0 |       0 |
| Host integration         |     23 |      0 |       0 |
| CodeActions audit        |     95 |      0 |       0 |

A subsequent full solution run passed all 1,968 tests with no failures or skips. That run repeated every affected integration and audit project after the independent checks and produced no new temporary-directory entries or surviving child process.

The pre-change three-run timing and memory measurements remain in `IntegrationTestingBaseline-2026-07-17.md`. Stage 0 does not claim a performance improvement; later stages will compare against that evidence.

## Stage 0 Outcome

- Every current stateful fixture has explicit ownership and deterministic teardown.
- Independent runtimes no longer share default recovery state.
- Test cancellation reaches expensive Roslyn, MSBuild, filesystem and tool-harness work.
- Existing process tests have bounded failure cleanup.
- No production change was required.
- Additional fixture reuse and parallelism remain disabled until their later plan stages.
