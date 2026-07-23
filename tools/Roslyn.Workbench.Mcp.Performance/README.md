# Manual performance runner

This project is the repeatable, manually invoked performance framework for Roslyn Workbench. It is intentionally outside the normal test projects and CI path: elapsed-time assertions are too sensitive to machine load, and large repository preparation is too expensive for ordinary functional validation.

The framework is permanent. Repository clones, restored assets, published Host binaries and Host recovery state are disposable execution data kept beneath the operating system's temporary directory. Results, validation, traces, counters and heap captures are retained beneath the gitignored `artifacts/performance/results` directory in the repository root.

## What it measures

`measure` records each MCP invocation's end-to-end elapsed time, Host CPU time, working set, working-set change, peak working set and structured response size. It also records an exact response hash, mutation `staged` state and every bounded collection's JSON path, `HasMore` value and ordered item hashes. These observations prove deterministic ordering and prefix equivalence without retaining large response bodies. It writes the raw observations and run environment to `measurements.json` and a first/subsequent plus median/P95 summary to `summary.md`. Warm-ups are excluded and their count is recorded so a zero-warm-up first invocation can be interpreted as cold. For mutation scenarios, transaction start and rollback are also excluded from the timed observation, while Host-side validation and staging performed by the measured tool remain included.

`cancel` starts one selected query with a known JSON-RPC request ID, sends and awaits the protocol `notifications/cancelled` message after the configured delay, and measures both client-visible cancellation latency and the time until an exclusive transaction lease can be acquired. The explicit notification avoids mistaking cancellation of the runner's local client wait for server-side cancellation. The lease check polls only the explicit `WorkspaceBusy` result, rolls the verification transaction back, and writes `cancellation.json` plus `cancellation.md`.

`commit-cancellation` validates both sides of the durable application boundary using the selected mutation scenario. It sends a real MCP cancellation notification after the Host publishes `Staging`, requiring cancellation with the staged transaction still previewable and no source changes. A fresh execution sends cancellation after `Applying`, requiring the Host to ignore cancellation and reach a successful durable commit. Each boundary uses a fresh Host, restores the checkout and validates recovery, Workspace and shutdown state. Results are written to `commit-cancellation.json`, `commit-cancellation.md` and `validation.json`.

`commit` starts a fresh Host for each iteration, starts a transaction, stages one selected mutation, previews it and performs a real durable commit. It records staging, preview, commit and repository-restoration timings plus the changed file set, operations and byte volume. After the Host shuts down, the runner restores only the tracked paths and new files recorded for that iteration, removes coordination files created by that Host and verifies the pinned checkout is clean. `--capture-trace` attaches the phase provider immediately before `transaction-commit`, keeping potentially long mutation staging outside the diagnostic capture. Results are written to `commit.json`, `commit.md` and `validation.json`.

`conflict` exercises a checked-in controlled-conflict definition. `PreWriteDrift` changes a selected input after staging and proves commit validation rejects before recovery persistence. `DuringApplication` waits for the durable manifest to enter `Applying`, changes the final replacement target while earlier files are being written, then proves recovery restores every server-written file without overwriting or reverting the external edit. Recovery evidence is inspected before the disposable state and checkout are restored. Results are written to `conflict.json`, `conflict.md` and `validation.json`.

`crash-recovery` starts a real durable commit, waits until the scenario's requested create, replace or delete operation is observable, and forcibly terminates the published Host while its manifest remains `Applying`. Scenarios without an explicit operation stop at the first observable mutation. The runner then starts a fresh Host against the same state directory, allowing normal startup recovery to run before MCP initialisation. The run proves that the partially applied repository is restored, recovery artifacts are removed, the Workspace can be reopened, the recovery Host shuts down normally and only the expected persistent commit-lock marker was created before runner cleanup. Results are written to `crash-recovery.json`, `crash-recovery.md` and `validation.json`.

`state-sequence` runs a checked-in sequence against one long-lived Workspace. The external-reload sequence warms a semantic query, changes a source file outside the Host, proves the stale query is rejected, reloads the Workspace and verifies the refreshed query observes the new reference. The multi-revision sequence warms a semantic query, stages two mutations, traverses undo and redo history, commits the selected revision and verifies the post-commit query resolves the moved definition. Each iteration restores the checkout and validates Host, Workspace and recovery state. Results are written to `state-sequence.json`, `state-sequence.md` and `validation.json`.

Every completed measurement or profile explicitly closes the workspace and then closes the Host's stdin so the stdio server can shut down normally. The runner writes `validation.json` with the Host exit status and stderr, repository commit, recovery-state files and any new workspace coordination or lock files. A run fails when the Host requires forced termination, exits unsuccessfully, leaves tracked repository changes, retains recovery state or leaks new coordination files.

`profile` repeatedly invokes one scenario while one diagnostic collector is attached:

- `trace` captures sampled managed thread time and the opt-in `Roslyn-Workbench-Mcp` phase provider to a `.nettrace` file for CPU, call-stack and boundary investigation;
- `counters` records `System.Runtime` counters as JSON for allocation rate, GC, thread-pool and exception investigation; and
- `gcdump` warms the selected workload and then captures a point-in-time managed heap graph. It accepts a comma-separated scenario sequence so order effects can be compared in fresh Host processes; each scenario receives the configured warm-up and measured iteration counts before the final forced-GC capture. A second `heap-after-close.gcdump` capture is taken after `workspace-close` while the Host remains alive, allowing workspace-owned retention to be separated from process-wide runtime and Roslyn caches.

The trace and counter modes run a fixed-duration workload loop. This avoids spending most of a fixed-length capture on an idle process. Collect each profile independently; attaching several profilers at once would perturb the workload and make the evidence harder to interpret.

Trace profiles also write `phases.md` and include the same structured phase summary in `profile.json`. The summary reconciles the median end-to-end MCP invocation with the instrumented Host tool boundary, then separates request binding, Workspace context acquisition and external-change detection, handler execution, mutation staging, response projection and other relevant phases. Nested phases overlap their parents and must not be added together. The difference between end-to-end and the Host tool boundary includes MCP SDK serialisation, stdio transport and client-side handling. Phase counts can differ slightly from the runner's invocation count because EventPipe attachment and detachment occur while the workload loop is active.

The custom phase provider is disabled during normal operation. A phase scope checks whether the provider is enabled before taking a timestamp, so ordinary server execution does not emit per-call logs or publish timings through the MCP contract.

BenchmarkDotNet is deliberately not part of this end-to-end runner. Use it later for an isolated helper or algorithm only when an end-to-end trace identifies a sufficiently important hot path.

## Pinned repositories

The checked-in `performance-suite.json` defines exact commits and curated requests for:

- `guardclauses` — small; includes Code Action discovery and the three bundled refactoring scenarios;
- `serilog` — medium; and
- `efcore` — large.

The low/high-limit pairs deliberately submit the same semantic query with different response bounds. A low-limit invocation that costs almost as much as the high-limit invocation can reveal discovery or enrichment work performed before the published bound.

The runner clones each repository into an operating-system-local, commit-specific cache beneath the temporary directory and refuses to reuse a cache at a different commit. Windows and Linux/WSL therefore cannot share incompatible SDKs, native tools or generated assets. Preparation is untimed. The cache boundary contains a generated NuGet configuration that clears package-source mappings inherited from Roslyn Workbench while preserving each target repository's own configuration. Each clone also gets an isolated NuGet package cache beneath `.performance`; this keeps package-supplied source documents inside the server's workspace boundary and prevents the measured repositories from sharing warmed package state accidentally. EF Core uses its complete mixed-language solution so workspace loading also exercises the supported behavior of ignoring projects the Host is not designed to interact with. Its repository-owned `restore.cmd` on Windows and `restore.sh` on Linux/WSL prepare the Arcade SDK and toolchain.

## Build and run

On Windows, run the PowerShell wrapper from the repository root. It publishes both the Host and performance runner in Release mode, restores the pinned diagnostic tools and injects the published Host path. With no arguments it lists the available repositories and scenarios:

```powershell
.\tools\Roslyn.Workbench.Mcp.Performance\run-performance.ps1
```

Run the complete small-repository measurement suite:

```powershell
.\tools\Roslyn.Workbench.Mcp.Performance\run-performance.ps1 `
  measure --repository guardclauses --scenario all
```

The first run clones the pinned repository and restores its dependencies automatically. Subsequent runs reuse the operating-system-local temporary cache; add `--skip-prepare` when no dependency refresh is required.

For WSL or direct Linux, the wrapper publishes both the Host and performance runner in Release mode, restores the pinned diagnostic tools and injects the published Host path. With no arguments it lists the available repositories and scenarios:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh
```

The wrapper detects WSL and applies the repository's required build artifacts path only there. Published binaries are placed in a unique temporary directory for each invocation, the path is printed before the requested command starts, and the directory is removed when the wrapper exits.

The runner owns repository preparation. On the first invocation for a repository it clones the pinned commit and restores its dependencies into the operating system's temporary cache. Later invocations on that operating system reuse the exact commit while the temporary cache remains available; omit `--skip-prepare` when dependencies may need refreshing.

Measure every small-repository scenario with the normal warm-up and iteration defaults:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
  measure --repository guardclauses --scenario all
```

Collect a 30-second CPU trace for a suspected weak scenario:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
    profile --repository serilog --scenario find-references-low-limit \
    --profile trace --duration 00:00:30 --skip-prepare
```

Measure and trace a real medium multi-file commit:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
  commit --repository serilog --scenario rename-ilogger-durable \
  --iterations 1 --warmups 0 --capture-trace --duration 00:00:05 --skip-prepare
```

Measure a real Code Action that creates a source file and updates its original document:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
  commit --repository serilog --scenario move-no-enumeration-to-file-durable \
  --iterations 1 --warmups 0 --skip-prepare
```

Measure pre-write conflict detection and broad in-progress recovery:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
  conflict --repository guardclauses --scenario rename-symbol-pre-write-drift \
  --iterations 5 --warmups 1 --skip-prepare

./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
  conflict --repository efcore --scenario rename-dbcontext-application-conflict \
  --iterations 2 --warmups 0 --skip-prepare
```

Terminate a partially applied replacement commit and validate fresh-Host startup recovery:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
  crash-recovery --repository serilog --scenario rename-ilogger-durable \
  --iterations 1 --warmups 0 --skip-prepare
```

Terminate the Code Action commit after its created file is observable and validate fresh-Host recovery of both operations:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
  crash-recovery --repository serilog --scenario move-no-enumeration-to-file-durable \
  --iterations 1 --warmups 0 --skip-prepare
```

Validate cache freshness across an external edit/reload and a committed multi-revision transaction:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
  state-sequence --repository serilog --scenario find-no-enumeration-cache-external-reload \
  --iterations 1 --warmups 0 --skip-prepare

./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
  state-sequence --repository serilog --scenario find-no-enumeration-cache-multi-revision \
  --iterations 1 --warmups 0 --skip-prepare
```

Validate cancellation immediately before and after durable application begins:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
  commit-cancellation --repository serilog --scenario rename-ilogger-durable \
  --iterations 1 --warmups 0 --skip-prepare
```

Measure cancellation and Workspace lease recovery for a large scan:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh \
  cancel --repository efcore --scenario find-references-high-limit \
  --cancel-after 00:00:00.050 --iterations 5 --skip-prepare
```

Measurements, summaries, workspace state and diagnostic captures default to a unique directory beneath `artifacts/performance/results`. Use `--output` to override that location or `--cache` to override the temporary repository cache.

## Measurement discipline

- Use a Release-published Host and keep the machine otherwise idle.
- Record the Host commit, operating system, processor, memory and .NET SDK/runtime with any retained result set.
- Run each repository and scenario more than once before treating a difference as meaningful.
- Compare like with like: same repository commit, scenario parameters, Host build and machine state.
- Use measurements to locate weaknesses. Do not turn the observed timings into functional-test thresholds.
- Retain dated summaries when they inform an optimisation; raw diagnostic captures can remain in the ignored artifacts directory because they are large and machine-specific.
