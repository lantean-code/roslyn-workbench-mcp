# Manual scenario runner

This project is the repeatable, manually invoked external-repository scenario framework for Roslyn Workbench. It covers performance measurement and profiling alongside durability, recovery, conflict, cancellation and lifecycle validation. It is intentionally outside the normal test projects and CI path: elapsed-time assertions are sensitive to machine load, while repository preparation and destructive recovery scenarios are too expensive for ordinary functional validation.

The framework is permanent. Repository clones, restored assets, published Host binaries and Host recovery state are disposable execution data kept beneath the operating system's temporary directory. Results, validation, traces, counters and heap captures are retained beneath the gitignored `artifacts/performance/results` directory in the repository root.

Automated scenario runs are release validation only: invoke them from release branches or by explicit manual dispatch, not for ordinary pull requests, pushes or recurring schedules. Native Windows and Linux provide the authoritative correctness, cleanup and performance evidence. Once the repository is public and v1 release-candidate preparation begins, macOS should run published-Host acceptance, Workspace integration and a curated scenario subset as best-effort evidence. Do not use billed recurring macOS automation while the repository is private, and do not compare macOS timing directly with the Windows or Linux performance baselines.

## Project structure

The project root contains only the executable entry point, project/runtime assets and platform wrappers. Implementation types are grouped by responsibility:

- `Application` owns command-line parsing and top-level orchestration;
- `Configuration` owns the checked-in suite contract and scenario definitions;
- `Hosting` owns the published Host process and MCP client lifetime;
- `Repositories` owns checkout preparation, Git interaction and restoration;
- `Scenarios` owns common invocation measurement plus feature folders for cancellation, commit cancellation, concurrency, conflicts, crash recovery, durable commits and state sequences;
- `Diagnostics` owns trace, counter and heap-capture projection;
- `Validation` owns terminal repository, Workspace and recovery-state validation; and
- `Reporting` owns environment projection and durable result output.

Folders and namespaces are aligned. Scenario-specific execution and result types remain with their scenario family, while shared suite definitions and infrastructure have one project-level owner.

## What it measures

`measure` records each MCP invocation's end-to-end elapsed time, Host CPU time, working set, working-set change, peak working set and structured response size. It also records an exact response hash, mutation `staged` state, every bounded collection's JSON path, item count, `HasMore` value, optional known total and ordered item hashes, and the count, maximum size and total size of any returned Code Action references. These observations prove deterministic ordering and prefix equivalence and expose the reference contribution to Code Action projection without retaining large response bodies. Code Action discovery intentionally issues new GUID references and expiry timestamps, so its exact raw response hash is expected to vary even when its ordered action metadata is stable. The runner writes the raw observations and run environment to `measurements.json` and a first/subsequent plus median/P95 summary to `summary.md`. Warm-ups are excluded and their count is recorded so a zero-warm-up first invocation can be interpreted as cold. For mutation scenarios, transaction start and rollback are also excluded from the timed observation, while Host-side validation and staging performed by the measured tool remain included.

Code Action scenarios use one focused workflow facility. A selection can match one listed action by title fragment, diagnostic ID and exact document span, capture its opaque reference under a scenario-local name, and inject that reference into `prepare-fix-all` or `stage-code-action`. Preparation can capture the resulting prepared reference for a later staging step. Reusing a capture name deliberately replaces its value, so setup can rediscover against each new current revision; a distinct name retains an older reachable-revision reference for undo or redo workflows. This facility is intentionally limited to the three published Code Action orchestration tools and is not a general JSON response-query language.

`cancel` starts one selected query with a known JSON-RPC request ID, sends and awaits the protocol `notifications/cancelled` message after the configured delay, and measures both client-visible cancellation latency and the time until an exclusive transaction lease can be acquired. The explicit notification avoids mistaking cancellation of the runner's local client wait for server-side cancellation. A query that completes before the notification is a valid race; a query that remains active when the notification is sent must report cancellation, otherwise the complete cancellation report is written and the command fails. The lease check polls only the explicit `WorkspaceBusy` result, rolls the verification transaction back, and writes `cancellation.json` plus `cancellation.md`.

`commit-cancellation` validates both sides of the durable application boundary using the selected mutation scenario. It sends a real MCP cancellation notification after the Host publishes `Staging`, requiring cancellation with the staged transaction still previewable and no source changes. A fresh execution sends cancellation after `Applying`, requiring the Host to ignore cancellation and reach a successful durable commit. Each boundary uses a fresh Host, restores the checkout and validates recovery, Workspace and shutdown state. Results are written to `commit-cancellation.json`, `commit-cancellation.md` and `validation.json`.

`commit` starts a fresh Host for each iteration, starts a transaction, stages one selected mutation, previews it and performs a real durable commit. It records staging, preview, commit and repository-restoration timings plus the changed file set, operations and byte volume. During `transaction-commit`, it samples the Host working set and private memory every 10 milliseconds and reports the sampled peaks and increases from the post-staging, post-preview baseline. These operating-system process measurements capture transient recovery-plan pressure without conflating it with mutation staging, but they are not managed-heap attribution. After the Host shuts down, the runner restores only the tracked paths and new files recorded for that iteration, removes coordination files created by that Host and verifies the pinned checkout is clean. `--capture-trace` attaches the phase provider immediately before `transaction-commit`, keeps potentially long mutation staging outside the diagnostic capture and stops only after that commit completes. The trace-backed commit report includes atomic replacement retry counts and planned backoff time without recording filesystem paths. `--duration` applies to the separate `profile` command rather than commit tracing. Results are written to `commit.json`, `commit.md` and `validation.json`.

`conflict` exercises a checked-in controlled-conflict definition. `PreWriteDrift` changes a selected input after staging and proves commit validation rejects before recovery persistence. `DuringApplication` waits for the durable manifest to enter `Applying`, changes the final replacement target while earlier files are being written, then proves recovery restores every server-written file without overwriting or reverting the external edit. Recovery evidence is inspected before the disposable state and checkout are restored. Results are written to `conflict.json`, `conflict.md` and `validation.json`.

`crash-recovery` starts a real durable commit, waits until the scenario's requested create, replace or delete operation is observable, and forcibly terminates the published Host while its manifest remains `Applying`. Scenarios without an explicit operation stop at the first observable mutation. The runner then starts a fresh Host against the same state directory, allowing normal startup recovery to run before MCP initialisation. The run proves that the partially applied repository is restored, recovery artifacts are removed, the Workspace can be reopened, the recovery Host shuts down normally and only the expected persistent commit-lock marker was created before runner cleanup. Results are written to `crash-recovery.json`, `crash-recovery.md` and `validation.json`.

`state-sequence` runs a checked-in sequence against one long-lived Workspace. The external-reload sequence warms a semantic query, changes a source file outside the Host, proves the stale query is rejected, reloads the Workspace and verifies the refreshed query observes the new reference. The live-build sequence warms a projection query, executes the repository's curated full build while the Host and Workspace remain open, records build duration plus Host CPU and working-set impact, and observes whether the Workspace remains ready or requires stale rejection and reload before the post-build query succeeds. The watcher-stress sequence measures an unstressed reload, then makes the Workspace stale again and reloads it while generating a bounded burst of create, rewrite and delete operations beneath an evaluated artifact root. It reports the like-for-like reload delta, Host CPU and working-set impact while the recursive watcher is buffering events before the replacement manifest is available, then verifies those artifact events are filtered once tracking begins. A certification retry or classified steady-state buffer overflow is retained as evidence and followed by a clean reload. The multi-revision sequence warms a semantic query, stages two mutations, traverses undo and redo history, commits the selected revision and verifies the post-commit query resolves the moved definition. Each iteration restores the checkout and validates Host, Workspace and recovery state. Results are written to `state-sequence.json`, `state-sequence.md` and `validation.json`.

`concurrency` starts a configured number of query requests behind one client-side start gate. Successful responses must match the warmed baseline exactly; excess requests may only reject with the documented `WorkspaceBusy` and `Retry` result, after which a sequential retry must succeed with the same response. The command also opens a project Workspace alongside the repository solution, proves both are listed and independently queryable while either owns the single transaction, validates non-owner transaction rejection, and runs one query against each Workspace concurrently. Results are written to `concurrency.json`, `concurrency.md` and `validation.json`.

Every completed measurement or profile explicitly closes the workspace and then closes the Host's stdin so the stdio server can shut down normally. The runner writes `validation.json` with the Host exit status and stderr, repository commit, recovery-state files and any new workspace coordination or lock files. A run fails when the Host requires forced termination, exits unsuccessfully, leaves tracked or untracked repository changes, retains recovery state or leaks new coordination files.

`profile` repeatedly invokes one scenario while one diagnostic collector is attached:

- `trace` captures sampled managed thread time and the opt-in `Roslyn-Workbench-Mcp` phase provider to a `.nettrace` file for CPU, call-stack and boundary investigation;
- `counters` records `System.Runtime` counters as JSON for allocation rate, GC, thread-pool and exception investigation; and
- `gcdump` warms the selected workload and then captures a point-in-time managed heap graph. It accepts a comma-separated scenario sequence so order effects can be compared in fresh Host processes; each scenario receives the configured warm-up and measured iteration counts before the final forced-GC capture. A second `heap-after-close.gcdump` capture is taken after `workspace-close` while the Host remains alive, allowing workspace-owned retention to be separated from process-wide runtime and Roslyn caches.

Trace mode treats `--duration` as a minimum collection duration and completes whole round-robin passes, so every selected scenario receives at least one traced invocation and a successful multi-scenario trace reports an invocation count that is a multiple of the scenario count. Counter mode retains the diagnostic tool's fixed-duration workload loop. A comma-separated scenario sequence runs round-robin within the same Host lifetime, allowing related cache families to be calibrated together; quote the sequence when invoking the PowerShell wrapper so PowerShell does not split it into separate arguments. This avoids spending most of a capture on an idle process. Collect each profile independently; attaching several profilers at once would perturb the workload and make the evidence harder to interpret.

Trace profiles also write `phases.md` and include the same structured phase summary in `profile.json`. The profile JSON additionally retains per-family cache metrics, including pressure peaks, hits, misses, single-flight joins, admission refusals, eviction reasons, late stores and factory failures or cancellations. The summary reconciles the median end-to-end MCP invocation with the instrumented Host tool boundary, then separates request binding, Workspace context acquisition and external-change detection, handler execution, mutation staging, response projection and other relevant phases. Nested phases overlap their parents and must not be added together. The difference between end-to-end and the Host tool boundary includes MCP SDK serialisation, stdio transport and client-side handling. Phase counts can differ slightly from the runner's invocation count because EventPipe attachment and detachment occur while the workload loop is active.

The custom phase provider is disabled during normal operation. A phase scope checks whether the provider is enabled before taking a timestamp, so ordinary server execution does not emit per-call logs or publish timings through the MCP contract.

The wrappers also publish and load the external query fixture used by the checked-in `plugin-cache-calibration-*` scenarios. The fixture exercises only the public plugin cache contract; it is not linked into the Host.

BenchmarkDotNet is deliberately not part of this end-to-end runner. Use it later for an isolated helper or algorithm only when an end-to-end trace identifies a sufficiently important hot path.

## Pinned repositories

The checked-in `scenario-suite.json` defines exact commits and curated requests for:

- `guardclauses` — small; includes document and selection Code Action discovery, separate Fix All preparation and prepared-staging measurements, two bundled mutation scenarios and the Roslyn-backed import-ordering scenario;
- `serilog` — medium; and
- `efcore` — large.

Each repository includes a `document-code-fixes` scenario so complete-document discovery can be compared across small, medium and large solutions. Run it with zero warm-ups for cold built-in analyzer activation and with at least one warm-up for cached reuse. GuardClauses also separates `prepare-fix-all` and `stage-prepared-fix-all`, keeping discovery, preparation and staging outside one another's timed invocation.

The low/high-limit pairs deliberately submit the same semantic query with different response bounds. A low-limit invocation that costs almost as much as the high-limit invocation can reveal discovery or enrichment work performed before the published bound.

The runner clones each repository into an operating-system-local, commit-specific cache beneath the temporary directory and refuses to reuse a cache at a different commit. Windows uses compact runner-owned directory names beneath `%TEMP%\rwmcp` because repository toolchains can still contain components that are not fully long-path-aware. Windows and Linux/WSL therefore cannot share incompatible SDKs, native tools or generated assets. Preparation is untimed. The cache boundary contains a generated NuGet configuration that clears package-source mappings inherited from Roslyn Workbench while preserving each target repository's own configuration. Each clone gets an isolated NuGet package cache beside, rather than inside, its Git checkout. This prevents measured repositories from sharing warmed package state while preserving the clean-checkout invariant required for reliable restoration. Package-supplied source documents are treated as external read-only Workspace inputs. EF Core uses its complete mixed-language solution so workspace loading also exercises the supported behavior of ignoring projects the Host is not designed to interact with. Its repository-owned `restore.cmd` on Windows and `restore.sh` on Linux/WSL prepare the Arcade SDK and toolchain.

## Build and run

On Windows, run the PowerShell wrapper from the repository root. It publishes both the Host and scenario runner in Release mode, restores the pinned diagnostic tools and injects the published Host path. With no arguments it lists the available repositories and scenarios:

```powershell
.\tools\Roslyn.Workbench.Mcp.ScenarioRunner\run-scenarios.ps1
```

Run the complete small-repository measurement suite:

```powershell
.\tools\Roslyn.Workbench.Mcp.ScenarioRunner\run-scenarios.ps1 `
  measure --repository guardclauses --scenario all
```

The first run clones the pinned repository and restores its dependencies automatically. Subsequent runs reuse the operating-system-local temporary cache; add `--skip-prepare` when no dependency refresh is required.

Measure peak Host memory while committing a broad EF Core mutation:

```powershell
.\tools\Roslyn.Workbench.Mcp.ScenarioRunner\run-scenarios.ps1 `
  commit --repository efcore --scenario rename-dbcontext-durable `
  --iterations 3 --warmups 0 --skip-prepare
```

For WSL or direct Linux, the wrapper publishes both the Host and scenario runner in Release mode, restores the pinned diagnostic tools and injects the published Host path. With no arguments it lists the available repositories and scenarios:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh
```

The wrapper detects WSL and applies the repository's required build artifacts path only there. Published binaries are placed in a unique temporary directory for each invocation, the path is printed before the requested command starts, and the directory is removed when the wrapper exits.

Both wrappers clear `ROSLYN_WORKBENCH_SENTRY_DSN` while publishing the scenario Host, so manual scenarios always use the stderr logging dispatcher and cannot submit to Sentry. A future provider-specific reporting scenario must supply an explicit test provider and destination rather than relaxing this wrapper-level isolation.

The runner owns repository preparation. On the first invocation for a repository it clones the pinned commit and restores its dependencies into the operating system's temporary cache. Later invocations on that operating system reuse the exact commit while the temporary cache remains available; omit `--skip-prepare` when dependencies may need refreshing.

Measure every small-repository scenario with the normal warm-up and iteration defaults:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  measure --repository guardclauses --scenario all
```

Measure cold and warm document Code Fix discovery:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  measure --repository guardclauses --scenario document-code-fixes \
  --iterations 1 --warmups 0 --skip-prepare

./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  measure --repository guardclauses --scenario document-code-fixes \
  --iterations 5 --warmups 1 --skip-prepare
```

Measure Fix All preparation and prepared staging separately:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  measure --repository guardclauses --scenario prepare-fix-all \
  --iterations 5 --warmups 1 --skip-prepare

./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  measure --repository guardclauses --scenario stage-prepared-fix-all \
  --iterations 5 --warmups 1 --skip-prepare
```

Collect a 30-second CPU trace for a suspected weak scenario:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
    profile --repository serilog --scenario find-references-low-limit \
    --profile trace --duration 00:00:30 --skip-prepare
```

Measure and trace a real medium multi-file commit:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  commit --repository serilog --scenario rename-ilogger-durable \
  --iterations 1 --warmups 0 --capture-trace --skip-prepare
```

Measure peak Host memory while committing a broad large-repository mutation:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  commit --repository efcore --scenario rename-dbcontext-durable \
  --iterations 3 --warmups 0 --skip-prepare
```

Measure a real Code Action that creates a source file and updates its original document:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  commit --repository serilog --scenario move-no-enumeration-to-file-durable \
  --iterations 1 --warmups 0 --skip-prepare
```

Measure pre-write conflict detection and broad in-progress recovery:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  conflict --repository guardclauses --scenario rename-symbol-pre-write-drift \
  --iterations 5 --warmups 1 --skip-prepare

./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  conflict --repository efcore --scenario rename-dbcontext-application-conflict \
  --iterations 2 --warmups 0 --skip-prepare
```

Terminate a partially applied replacement commit and validate fresh-Host startup recovery:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  crash-recovery --repository serilog --scenario rename-ilogger-durable \
  --iterations 1 --warmups 0 --skip-prepare
```

Terminate the Code Action commit after its created file is observable and validate fresh-Host recovery of both operations:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  crash-recovery --repository serilog --scenario move-no-enumeration-to-file-durable \
  --iterations 1 --warmups 0 --skip-prepare
```

Validate cache freshness across an external edit/reload and a committed multi-revision transaction:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  state-sequence --repository serilog --scenario find-no-enumeration-cache-external-reload \
  --iterations 1 --warmups 0 --skip-prepare

./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  state-sequence --repository serilog --scenario find-no-enumeration-cache-multi-revision \
  --iterations 1 --warmups 0 --skip-prepare
```

Measure the effect of a full repository build on an open Workspace:

```powershell
.\tools\Roslyn.Workbench.Mcp.ScenarioRunner\run-scenarios.ps1 `
  state-sequence --repository guardclauses --scenario live-build `
  --iterations 1 --warmups 0 --skip-prepare

.\tools\Roslyn.Workbench.Mcp.ScenarioRunner\run-scenarios.ps1 `
  state-sequence --repository serilog --scenario live-build `
  --iterations 1 --warmups 0 --skip-prepare

.\tools\Roslyn.Workbench.Mcp.ScenarioRunner\run-scenarios.ps1 `
  state-sequence --repository efcore --scenario live-build `
  --iterations 1 --warmups 0 --skip-prepare
```

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  state-sequence --repository guardclauses --scenario live-build \
  --iterations 1 --warmups 0 --skip-prepare

./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  state-sequence --repository serilog --scenario live-build \
  --iterations 1 --warmups 0 --skip-prepare

./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  state-sequence --repository efcore --scenario live-build \
  --iterations 1 --warmups 0 --skip-prepare
```

Stress the recursive filesystem monitor with generated artifact events during an EF Core reload:

```powershell
.\tools\Roslyn.Workbench.Mcp.ScenarioRunner\run-scenarios.ps1 `
  state-sequence --repository efcore --scenario watcher-stress `
  --iterations 1 --warmups 0 --skip-prepare
```

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  state-sequence --repository efcore --scenario watcher-stress \
  --iterations 1 --warmups 0 --skip-prepare
```

Measure the same EF Core watcher workload while 32 external roots contribute 1,024 evaluated `AdditionalFiles` through 64 wildcard items:

```powershell
.\tools\Roslyn.Workbench.Mcp.ScenarioRunner\run-scenarios.ps1 `
  state-sequence --repository efcore --scenario external-wildcard-watcher-stress `
  --iterations 1 --warmups 0 --skip-prepare
```

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  state-sequence --repository efcore --scenario external-wildcard-watcher-stress \
  --iterations 1 --warmups 0 --skip-prepare
```

Validate cancellation immediately before and after durable application begins:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  commit-cancellation --repository serilog --scenario rename-ilogger-durable \
  --iterations 1 --warmups 0 --skip-prepare
```

Measure the default two-query Workspace bound with four simultaneous requests, then validate two loaded Workspaces and transaction ownership:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  concurrency --repository serilog --scenario solution-structure \
  --parallelism 4 --iterations 5 --warmups 1 --skip-prepare
```

Measure cancellation and Workspace lease recovery for a large scan:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  cancel --repository efcore --scenario find-references-high-limit \
  --cancel-after 00:00:00.050 --iterations 5 --skip-prepare
```

Measure the representative relationship, whole-project and deep-projection query families:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  measure --repository serilog \
  --scenario async-analysis,change-impact-low-limit,change-impact-high-limit,duplicate-code,operation-tree,control-flow-shallow,control-flow-deep \
  --iterations 5 --warmups 2 --skip-prepare

./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh \
  measure --repository efcore \
  --scenario async-analysis,duplicate-code \
  --iterations 5 --warmups 3 --skip-prepare
```

Measurements, summaries, workspace state and diagnostic captures default to a unique directory beneath `artifacts/performance/results`. Use `--output` to override that location or `--cache` to override the temporary repository cache. One runner owns a cache at a time so preparation, execution and restoration cannot overlap against the same checkout; use distinct `--cache` paths when running scenarios concurrently. Keep custom Windows cache paths short because deeply nested repositories such as EF Core can exceed path limits in parts of the MSBuild toolchain.

## Measurement discipline

- Use a Release-published Host and keep the machine otherwise idle.
- Record the Host commit, operating system, processor, memory and .NET SDK/runtime with any retained result set.
- Run each repository and scenario more than once before treating a difference as meaningful.
- Compare like with like: same repository commit, scenario parameters, Host build and machine state.
- Use measurements to locate weaknesses. Do not turn the observed timings into functional-test thresholds.
- Retain dated summaries when they inform an optimisation; raw diagnostic captures can remain in the ignored artifacts directory because they are large and machine-specific.

## Release automation and retained metrics

The external-repository suite is release validation, not pull-request validation. Automated runs should be limited to release branches and explicit manual dispatch. Pull requests use the published-Host acceptance project with small checked-in fixtures.

Release-branch runs upload the complete result directory as a temporary workflow artifact and also produce a versioned normalised aggregate for comparison. The final aggregate and Markdown comparison are attached to the GitHub release; they are not committed to the source branch. The next release downloads the previous release's aggregate as its default baseline.

Like-for-like comparison requires the same command, scenario, target repository commit, operating system and architecture. The aggregate also records the Host commit and version, scenario-suite hash, .NET runtime, parameters, warm-ups and sample count so environmental or scenario drift is visible. Initially, timing differences are advisory. Correctness and cleanup validation remain release-gating.

Raw traces, counters and heap captures remain workflow artifacts unless they are needed to explain a release decision. This keeps permanent release assets small while preserving detailed diagnostics during release validation.
