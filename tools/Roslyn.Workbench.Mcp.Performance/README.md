# Manual performance runner

This project is the repeatable, manually invoked performance framework for Roslyn Workbench. It is intentionally outside the normal test projects and CI path: elapsed-time assertions are too sensitive to machine load, and large repository preparation is too expensive for ordinary functional validation.

The framework is permanent. Repository clones, restored assets, published Host binaries, workspace state, traces, heap captures and raw measurements are disposable run artifacts kept beneath the gitignored `artifacts/performance` directory in the repository root.

## What it measures

`measure` records each MCP invocation's end-to-end elapsed time, Host CPU time, working set, working-set change, peak working set and structured response size. It writes the raw observations and run environment to `measurements.json` and a median/P95 summary to `summary.md`. Warm-ups are excluded. For mutation scenarios, transaction start and rollback are also excluded from the timed observation, while Host-side validation and staging performed by the measured tool remain included.

Every completed measurement or profile explicitly closes the workspace and then closes the Host's stdin so the stdio server can shut down normally. The runner writes `validation.json` with the Host exit status and stderr, repository commit, recovery-state files and any new workspace coordination or lock files. A run fails when the Host requires forced termination, exits unsuccessfully, leaves tracked repository changes, retains recovery state or leaks new coordination files.

`profile` repeatedly invokes one scenario while one diagnostic collector is attached:

- `trace` captures sampled managed thread time to a `.nettrace` file for CPU and call-stack investigation;
- `counters` records `System.Runtime` counters as JSON for allocation rate, GC, thread-pool and exception investigation; and
- `gcdump` warms the selected workload and then captures a point-in-time managed heap graph.

The trace and counter modes run a fixed-duration workload loop. This avoids spending most of a fixed-length capture on an idle process. Collect each profile independently; attaching several profilers at once would perturb the workload and make the evidence harder to interpret.

BenchmarkDotNet is deliberately not part of this end-to-end runner. Use it later for an isolated helper or algorithm only when an end-to-end trace identifies a sufficiently important hot path.

## Pinned repositories

The checked-in `performance-suite.json` defines exact commits and curated requests for:

- `guardclauses` — small; includes Code Action discovery and the three bundled refactoring scenarios;
- `serilog` — medium; and
- `efcore` — large.

The low/high-limit pairs deliberately submit the same semantic query with different response bounds. A low-limit invocation that costs almost as much as the high-limit invocation can reveal discovery or enrichment work performed before the published bound.

The runner clones each repository into the commit-specific `artifacts/performance/repositories` cache and refuses to reuse a cache at a different commit. Preparation is untimed. The cache boundary contains a generated NuGet configuration that clears package-source mappings inherited from Roslyn Workbench while preserving each target repository's own configuration. Each clone also gets an isolated NuGet package cache beneath `.performance`; this keeps package-supplied source documents inside the server's workspace boundary and prevents the measured repositories from sharing warmed package state accidentally. EF Core uses its complete mixed-language solution so workspace loading also exercises the supported behavior of ignoring projects the Host is not designed to interact with. Its repository-owned `restore.cmd` on Windows and `restore.sh` on Linux/WSL prepare the Arcade SDK and toolchain.

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

The first run clones the pinned repository and restores its dependencies automatically. Subsequent runs reuse the repository-local cache; add `--skip-prepare` when no dependency refresh is required.

For WSL or direct Linux, the wrapper publishes both the Host and performance runner in Release mode, restores the pinned diagnostic tools and injects the published Host path. With no arguments it lists the available repositories and scenarios:

```bash
./tools/Roslyn.Workbench.Mcp.Performance/run-performance.sh
```

The wrapper detects WSL and applies the repository's required build artifacts path only there. Published binaries are placed in a new `artifacts/performance/publish` directory for each invocation, and the path is printed before the requested command starts.

The runner owns repository preparation. On the first invocation for a repository it clones the pinned commit and restores its dependencies into `artifacts/performance/repositories`. Later invocations reuse that exact commit; omit `--skip-prepare` when dependencies may need refreshing.

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

Measurements, summaries, workspace state and diagnostic captures default to a unique directory beneath `artifacts/performance/results`. Use `--output` and `--cache` to override the repository-local defaults.

## Measurement discipline

- Use a Release-published Host and keep the machine otherwise idle.
- Record the Host commit, operating system, processor, memory and .NET SDK/runtime with any retained result set.
- Run each repository and scenario more than once before treating a difference as meaningful.
- Compare like with like: same repository commit, scenario parameters, Host build and machine state.
- Use measurements to locate weaknesses. Do not turn the observed timings into functional-test thresholds.
- Retain dated summaries when they inform an optimisation; raw diagnostic captures can remain in the ignored artifacts directory because they are large and machine-specific.
