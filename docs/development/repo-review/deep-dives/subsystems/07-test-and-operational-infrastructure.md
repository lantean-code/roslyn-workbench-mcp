# Deep dive 7 — Test and operational infrastructure

Date: 2026-07-31

Status: Complete

## Scope and infrastructure map

The review covered the repository's six Unit/Contract projects, four component-integration projects, Code Action audit project, published-Host acceptance project, two shared support libraries, plugin and lock fixtures, checked-in workspace/package-consumer assets, MSBuild category enforcement, CI workflows, acceptance wrappers and the external-repository scenario runner. Production implementation was revisited only where a real fixture or end-to-end trace depended on it.

`test/Directory.Build.targets` applies `Integration` or `Audit` at assembly level from the project suffix and fails builds whose declared category disagrees. It also prevents ordinary unit projects from consuming `IntegrationTestSupport`. The normal CI workflow builds the complete solution, runs Unit/Contract tests with integration and audit categories excluded, then runs Workspace, Plugins.Core, CodeActions and Host integration projects independently. Published Release Host acceptance runs on Ubuntu and Windows; Windows additionally repeats the Workspace integration suite for native durability and locking coverage. The Code Action compatibility audit is a separate path-filtered, main, scheduled and manual workflow. Every job writes TRX, verifies a minimum count and uploads results. The canonical strategy's statement that there are five Unit/Contract projects is stale because `Roslyn.Workbench.Mcp.Plugins.Analyzers.Test` makes six, but the solution-wide fast job still includes it.

`IntegrationTestSupport` owns real composition, temporary workspace materialisation and cleanup. Its component fixture uses the production registration extensions, validates DI scopes, initialises the real state directory and attempts Workspace closure, Host shutdown, provider disposal and state cleanup independently. Mutable fixtures copy checked-in assets into unique temporary roots. The package-consumer integration test packs the current Plugins package, restores a clean external project from that local feed and proves analyser activation through real `dotnet build` results.

Acceptance has no production reference. Its fixture launches the explicitly published executable through the MCP SDK's stdio transport, uses isolated workspace/state/plugin roots, captures stderr and completion details, applies per-invocation and shutdown bounds, and retains diagnostics on the test failure paths. The platform wrappers publish into unique temporary directories and remove those binaries at exit. CI therefore tests the distribution boundary rather than an in-process approximation.

The scenario runner is a separate Release validation and performance system. It publishes the Host and a public cache-calibration plugin, clones exact external commits into an operating-system-local shared cache, runs MCP operations against a unique Host state directory and writes JSON, Markdown, EventPipe, counter or heap evidence. Mutation families use `RepositoryRestorer` to capture the changed tracked paths and new untracked files, restore only those paths, remove empty created directories and verify a clean tracked checkout. Host shutdown, recovery files and Workspace coordination files are validated after each completed destructive iteration.

## Representative traces

### Real component fixture lifecycle

`TestWorkspaceFixture.Create` materialises a checked-in workspace under a unique temporary directory and creates a sibling private state root. `ComponentWorkspace.Create` builds the real owner-specific service graph, validates the provider and initialises the state directory. Tests open real MSBuild workspaces and transact through production services. Disposal snapshots loaded Workspace IDs, closes advisory status, removes and disposes each loaded workspace, stops and disposes the Host, then deletes an owned state root while retaining the first cleanup failure. The outer materialised asset subsequently deletes the complete scenario root. No shared mutable workspace fixture was found.

### Published-Host acceptance

The shell and PowerShell wrappers publish Release output to unique temporary roots and set `ROSLYN_WORKBENCH_MCP_ACCEPTANCE_HOST_PATH`. `AcceptanceProcessFixture` copies assets, prepares a private state directory, starts the published executable with inherited environment disabled, communicates through the official stdio client and captures Host stderr. Tool calls are bounded to thirty seconds. Test bodies wrap assertions so a failure marks the scenario root for retention; disposal otherwise stops the client and removes the root with bounded retry. CI runs this on Linux and Windows and uploads retained roots on failure. This boundary is materially stronger than the component tests and did not reveal a duplicate in-process substitute.

### External repository preparation and reuse

`RepositoryManager.PrepareAsync` derives one cache path from repository ID and pinned commit, clones only when `.git` is absent, validates HEAD and tracked cleanliness, and then runs repository-owned preparation. RWMCP-034 occurs because validation happens only before preparation. The checked-in EF Core Windows preparation has already deleted three tracked zero-byte sentinel files. The same invocation continued after the deletion and could measure a checkout different from the pinned commit; only the next invocation rejected the dirty cache. The existing Future Task accurately records the repeatability symptom but does not remove the current operational defect.

RWMCP-038 is a separate concurrency problem. `PrepareAsync` returns a bare shared checkout path and no lease spans preparation, Host execution, mutation capture and restoration. Two runner processes for the same repository and commit can both pass the clean check. One can then restore or prepare files while the other's Host is querying or committing them. Unique Host state and output directories do not isolate this shared worktree, and the production commit lock does not cover runner preparation or restoration.

### Cancelled and failed processes

Repository preparation, Git and build commands drain stdout and stderr concurrently. On cancellation or failure they kill the process tree and wait for the owned process before disposal. `ScenarioHost` similarly captures stderr asynchronously, closes stdin for normal shutdown, applies a five-second bound and kills the process tree when necessary. Microsoft Learn confirms that cancelling `WaitForExitAsync` does not terminate the process itself and that `Kill` is asynchronous; the explicit kill-and-wait paths are therefore necessary and present. No substantiated owned-process leak was retained, although .NET documents that waiting for the root process does not prove every descendant has exited, so platform-specific stress evidence remains useful.

The query cancellation command does not enforce its own claimed behaviour. RWMCP-037 records that after it sends and awaits the protocol cancellation notification, a normally completed request is merely recorded with `OperationCanceled = false`; the command still verifies lease availability, writes a report and exits successfully. A server regression that ignores cancellation can therefore produce green release automation with zero cancelled invocations.

### Source mutation, restoration and result production

Durable commit, commit-cancellation, conflict, crash-recovery and state-sequence iterations attempt Host shutdown, repository restoration, Workspace-state cleanup and validation even when the workload fails. Restoration is non-cancellable after failure and verifies both tracked changes and new untracked paths against its baseline. This is a careful failure boundary once a clean baseline has been established.

RWMCP-035 breaks the optional commit trace path before mutation. `RunDurableCommitIterationAsync` passes `ProfileKind.Trace` to `DiagnosticCollector.StartDurationProfile`, while that method explicitly throws because trace profiles must use a directly controlled EventPipe session. The ordinary `profile --profile trace` path was migrated to `TraceCollection`; `commit --capture-trace` was not.

RWMCP-036 records the evidence-loss boundary. Measurement and destructive commands accumulate results in memory and invoke `ResultWriter` only after every warm-up and measured iteration succeeds. If scenario two of `--scenario all` or iteration three of a commit run fails, the general catch prints only the exception message; already completed measurements and validations are never written. Cleanup is attempted, but the evidence needed to distinguish workload, shutdown, restoration and later-iteration failures is lost.

## Findings and validation

Independent current-source validation retained five P2/high-confidence findings:

- RWMCP-034 — validate and preserve the pinned checkout after repository preparation;
- RWMCP-035 — use the EventPipe trace collector for commit trace capture;
- RWMCP-036 — persist partial scenario and iteration evidence before propagating a later failure;
- RWMCP-037 — fail cancellation validation when an in-flight request ignores the notification; and
- RWMCP-038 — hold an exclusive per-checkout runner lease across preparation, execution and restoration.

The repository's solution-wide fast gate passed 1,943 Unit/Contract cases with integration, audit and acceptance assemblies correctly filtered out. The scenario-runner project builds successfully with .NET SDK 10.0.102 and its checked-in suite deserialises and lists all repositories/scenarios. Compilation and suite loading cannot exercise the five runtime paths above. External-repository scenarios and acceptance were not run because repository policy requires explicit scenario or acceptance authorisation. The repository's earlier native Windows evidence independently substantiates RWMCP-034.

No scenario-runner test project exists. In particular, there is no hermetic coverage for preparation side effects, repeat cache use, per-checkout concurrency, partial result persistence, cancellation ignored by the Host, commit trace selection, child-process cancellation or restoration after injected failures. The runner is compiled by the normal solution build and has extensive manually recorded evidence, but these orchestration regressions can compile cleanly. Release-branch scenario automation, durable aggregate comparison and best-effort public-repository macOS validation are explicitly deferred in [Future tasks](../../../FutureTasks.md); their absence was not duplicated as a new finding.

The all-seven-unit implementation review is now complete. Repository-wide validation passes remain a separate next stage and must revisit these operational findings alongside every prior cross-project conclusion before the programme's final report is considered complete.
