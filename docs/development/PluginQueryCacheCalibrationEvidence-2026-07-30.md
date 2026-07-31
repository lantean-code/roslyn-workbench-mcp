# Plugin Query Cache Calibration Evidence

Date: 2026-07-30

## Purpose

Record the initial scenario-runner evidence used to select supported cache capacity minimums, defaults and maximums for the Workspace query, plugin query and Code Action reference cache families.

The calibration uses dedicated size units for each family. Workspace and Code Action values are Host-calculated relative retained-result units; plugin values are entry counts. The limits are not byte limits.

## Scenario coverage

The checked-in suite now contains `plugin-cache-calibration-reuse` and `plugin-cache-calibration-pressure` for the small, medium and large repositories, plus small-repository null and disposable non-admission workloads and a medium-repository coalescing workload. The coalescing scenario exposes the cumulative factory execution count and fails unless each concurrent batch advances it by exactly one. Trace profiles can run a comma-separated scenario sequence so Workspace queries, Code Action discovery and plugin pressure are captured within one Host lifetime.

The fixture plugin uses the public `IQueryContext.QueryResultCache` contract. It supports synchronous and asynchronous factories, repeated and distinct keys, delayed coalescing, null results and disposable results.

## WSL evidence

| Repository | Size | Workload | Workspace peak units | Plugin peak entries | Code Action peak units | Working-set observation |
| --- | --- | --- | ---: | ---: | ---: | --- |
| GuardClauses | Small | Combined solution structure, reference discovery, Code Action discovery and plugin pressure | 40 | 10 | 15,840 | 271,245,312 bytes before; 512,716,800 after; 569,372,672 peak |
| GuardClauses | Small | Dedicated plugin pressure, 20,000-key space and 1,024-character values | — | 5,015 | — | 271,278,080 bytes before; 301,699,072 after; 314,290,176 peak |
| Serilog | Medium | Combined solution structure, reference discovery, document Code Fix discovery and plugin pressure | 1,751 | 15 | 16,485 | 348,798,976 bytes before; 760,696,832 after; 831,762,432 peak |
| EF Core | Large | Solution structure and reference-discovery sequence | 114 | — | — | 516,460,544 bytes before; 1,673,904,128 after and peak; the 20-second capture completed two large-repository invocations |
| EF Core | Large | Document Code Fix discovery | — | — | 11,866 | 523,108,352 bytes before; 1,314,074,624 after; 1,393,045,504 peak; 44 references admitted and the largest individual charge was 297 |

The dedicated plugin-pressure run admitted 4,787 entries during the attached interval and observed 5,015 retained entries including attachment-boundary activity. The 30,420,992-byte working-set increase corresponds to roughly 6 KiB of process working-set growth per retained 1,024-character fixture value, including cache, key, value, plugin and runtime overhead. A 10,000-entry default therefore permits approximately twice the measured ten-second pressure while keeping the observed order of retained memory bounded. The 50,000-entry maximum is reserved for deliberate high-memory operation rather than ordinary use.

## Windows evidence

The same pinned workloads were repeated with .NET 10.0.10 on 64-bit Windows 10.0.26200 with 16 logical processors.

| Repository | Size | Workload | Workspace peak units | Plugin peak entries | Code Action peak units | Working-set observation |
| --- | --- | --- | ---: | ---: | ---: | --- |
| GuardClauses | Small | Combined solution structure, reference discovery, Code Action discovery and plugin pressure | 6 | — | — | 237,203,456 bytes before; 293,646,336 after; 293,691,392 peak; the old hard-duration loop completed only the first two of four scenarios |
| GuardClauses | Small | Dedicated plugin pressure, 20,000-key space and 1,024-character values | — | 656 | — | 238,546,944 bytes before; 254,328,832 after; 269,197,312 peak |
| Serilog | Medium | Combined solution structure, reference discovery, document Code Fix discovery and plugin pressure | 3,369 | 13 | 14,287 | 339,095,552 bytes before; 787,337,216 after; 862,330,880 peak |
| EF Core | Large | Solution structure and reference-discovery sequence | 114 | — | — | 440,418,304 bytes before; 1,495,404,544 after; 1,495,465,984 peak |
| EF Core | Large | Document Code Fix discovery | — | — | 33,366 | 438,956,032 bytes before; 1,375,920,128 after; 1,461,133,312 peak; 100 references admitted and the largest individual charge was 369 |

The Windows coalescing workload completed both two-request batches successfully with identical paired responses and also completed its multi-Workspace isolation sequence. Every Windows run shut the Host down normally, retained the pinned repository commit and passed runner state validation.

The ten-second Windows plugin-pressure trace completed 667 invocations and observed 656 retained entries. It demonstrates the platform's lower traced throughput for this synthetic workload but does not independently reach the configured plugin minimum. The WSL result remains the higher logical-pressure observation, while the Windows working-set result provides a second platform reference.

## Findings and selected limits

The highest Workspace pressure observed across both platforms was 3,369 units in Serilog on Windows. A 5,000-unit minimum provides approximately 1.5 times measured headroom, the 10,000-unit default approximately 3 times, and the largest individual Workspace charge of 3,344 units remains admissible at the minimum. The 100,000-unit maximum permits deliberately broader future Host-owned cache consumers without becoming unbounded.

The highest Code Action pressure observed was 33,366 units in EF Core on Windows, compared with 18,683 units in the earlier WSL Serilog calibration. The original 25,000-unit minimum was below a demonstrated workload and the original 50,000-unit default provided only approximately 1.5 times Windows headroom. The revised 40,000-unit minimum provides approximately 1.2 times headroom, the revised 75,000-unit default approximately 2.25 times, and the 250,000-unit maximum remains appropriate for deliberate large Fix All workflows.

| Family | Minimum | Default | Maximum |
| --- | ---: | ---: | ---: |
| Workspace query results | 5,000 units | 10,000 units | 100,000 units |
| Plugin query results | 7,500 entries | 10,000 entries | 50,000 entries |
| Code Action references | 40,000 units | 75,000 units | 250,000 units |

## Methodology correction

The original trace loop allowed the diagnostic collector's hard duration to expire part-way through a multi-scenario round. The ten-second Windows GuardClauses run therefore traced only solution structure and reference discovery, although four scenarios were selected. Trace mode now owns the EventPipe session directly, completes whole round-robin passes until the requested duration has elapsed, and then stops and finalises the session through the diagnostics client API. The requested duration is consequently a minimum, every selected scenario is traced at least once, and a successful multi-scenario trace invocation count is a multiple of the selected scenario count. A one-second four-scenario GuardClauses proof run completed four traced invocations, recorded all three cache families, produced 39 phase summaries and passed Host shutdown and repository-state validation. Counter mode retains its fixed-duration diagnostic-tool behaviour.

## Reproduction

Run the platform wrapper so it publishes the Host, scenario runner and calibration fixture plugin:

```bash
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh profile --repository guardclauses --scenario solution-structure,find-references-low-limit,list-code-actions,plugin-cache-calibration-pressure --profile trace --duration 00:00:10 --warmups 0 --skip-prepare
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh profile --repository guardclauses --scenario plugin-cache-calibration-pressure --profile trace --duration 00:00:10 --warmups 0 --skip-prepare
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh profile --repository serilog --scenario solution-structure,find-references-low-limit,document-code-fixes,plugin-cache-calibration-pressure --profile trace --duration 00:00:15 --warmups 0 --skip-prepare
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh profile --repository efcore --scenario solution-structure,find-references-low-limit --profile trace --duration 00:00:20 --warmups 0 --skip-prepare
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh profile --repository efcore --scenario document-code-fixes --profile trace --duration 00:00:30 --warmups 0 --skip-prepare
./tools/Roslyn.Workbench.Mcp.ScenarioRunner/run-scenarios.sh concurrency --repository serilog --scenario plugin-cache-calibration-coalescing --parallelism 2 --iterations 2 --warmups 0 --skip-prepare
```

The profile JSON is the permanent machine-readable output. Compare future releases against these logical peaks, admission refusals, eviction reasons and working-set fields rather than treating these measurements as timeless constants.

When running a comma-separated sequence through PowerShell, quote the scenario value, for example `--scenario "solution-structure,find-references-low-limit"`, because an unquoted comma creates separate PowerShell arguments.
