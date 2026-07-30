# Code Action Batch 7 Validation

Date: 2026-07-30

## Outcome

Batch 7 rebuilds validation around the three published Code Action orchestration tools. The complete acceptance suite and representative affected external-repository scenarios are green through both the WSL and native Windows wrappers.

## Composition and architecture evidence

`BuiltInCodeActionInventoryTests` retains the 250-provider count and uniqueness assertions and now pins the SHA-256 identity snapshot `215A5BE5A0E994F1288BFE1090C1F43142D39F60A5A6B4FB80C68D5F6092B385`. Sorting the complete provider identity set before hashing makes provider additions, removals and identity changes visible, while the independent uniqueness assertion detects duplicates.

The removed dedicated acceptance matrix remains absent. The final audit and integration suites cover provider policy, diagnostic sources and mappings, mixed safe and excluded leaves, exceptional diagnostic routes, ordinary replay references, execution paths and built-in analyser composition. The complete audit suite passed 120 tests and the Code Action integration suite passed 17 tests.

Published-host acceptance now exercises the three orchestration contracts, document/selection/caret discovery, concise location-aware results, compiler, built-in IDE diagnostic and refactoring actions, staging and rollback, unknown/stale/expired references, Fix All preparation and staging, provider exclusions, durable create-and-replace commit and the external Plugin boundary. `PublishedToolCatalogueSizeIntegrationTests` verifies that only `list-code-actions`, `prepare-fix-all` and `stage-code-action` comprise the Code Action surface, rejects removed names and records both catalogue and concise discovery response sizes. The measured complete 54-tool catalogue was 88,479 UTF-8 bytes, its three Code Action contracts were 2,968 bytes, and the representative concise Code Action discovery response was 965 bytes.

## WSL acceptance and build evidence

The complete published-host wrapper passed 53 of 53 tests:

```bash
./test/Roslyn.Workbench.Mcp.AcceptanceTest/run-acceptance-tests.sh --no-restore
```

The normal solution build and scenario-runner build completed with no warnings or errors. The affected non-acceptance suites passed 262 unit tests, 17 integration tests and 120 audit tests. `latest-all` analyser builds for the acceptance, audit and scenario-runner projects completed with no warnings or errors after a serial rerun removed a transient shared-output copy contention caused by concurrent builds.

## Scenario workflow and WSL evidence

The focused scenario workflow can deterministically select a discovered action using title, diagnostic ID and exact location; capture its opaque reference by name; inject a captured reference into preparation or staging; capture a prepared Fix All reference; overwrite a named capture when rediscovering for the current revision; and retain a distinct older reference for undo or redo coverage. It remains deliberately limited to Code Action orchestration rather than introducing a general response-query or workflow language.

Representative WSL scenarios passed for every affected repository size and scenario family:

| Repository | Size | Scenario | Result evidence |
|---|---|---|---|
| GuardClauses | Small | Cold document Code Fix discovery | First 2,180.24 ms; median 697.69 ms; 3.01 KiB response |
| GuardClauses | Small | Warm document Code Fix discovery | First 712.22 ms; median 588.46 ms; 3.01 KiB response |
| Serilog | Medium | Warm document Code Fix discovery | Median 616.68 ms; P95 660.32 ms; 1.85 KiB response |
| EF Core | Large | Warm document Code Fix discovery | 7,777.01 ms; 10.60 KiB response |
| GuardClauses | Small | Fix All preparation | 689.21 ms; 0.31 KiB response |
| GuardClauses | Small | Prepared Fix All staging | 369.51 ms; 0.11 KiB response |
| GuardClauses | Small | Ordinary replay staging (`organize-imports`) | 930.78 ms; 0.08 KiB response |
| Serilog | Medium | Durable create-and-replace commit | Two changed files; one create and one replace; 2,040.40 ms staging; 407.56 ms commit |
| Serilog | Medium | Create-and-replace crash recovery | Two partially applied files recovered; 682.59 ms fresh-host recovery |
| Serilog | Medium | Multi-revision undo/redo and commit | Code Action revision staged, later revision staged, undo/redo traversed and selected revision committed |

The results are retained beneath `artifacts/performance/results` in the run directories dated `20260730-111336` through `20260730-111908`. No production remediation is required from this evidence: response sizes remain concise, the warm small and medium discovery measurements are bounded, the cold activation overhead is isolated as intended, and the large EF Core measurement reflects its substantially larger mixed-language workspace. Future material regressions should be investigated against these separate discovery, preparation and staging baselines.

## Windows wrapper evidence

The complete native Windows published-host acceptance wrapper passed 53 of 53 tests. Representative Windows scenario runs also passed with successful repository, Host shutdown, recovery-state and Workspace-state validation:

| Repository | Scenario | Result evidence |
|---|---|---|
| GuardClauses | Cold document Code Fix discovery | 2,730.18 ms; 3.01 KiB response |
| GuardClauses | Warm document Code Fix discovery | Median 662.30 ms; 3.01 KiB response |
| GuardClauses | Fix All preparation | 753.12 ms; 0.31 KiB response |
| GuardClauses | Prepared Fix All staging | 398.10 ms; 0.11 KiB response |
| GuardClauses | Ordinary replay staging (`organize-imports`) | 1,106.98 ms; 0.08 KiB response |
| Serilog | Warm document Code Fix discovery | Median 748.30 ms; 1.85 KiB response |
| Serilog | Durable create-and-replace commit | Two changed files; one create and one replace; 2,644.83 ms staging; 859.04 ms commit |
| Serilog | Create-and-replace crash recovery | Two partially applied files recovered; 678.95 ms fresh-host recovery |
| Serilog | Multi-revision undo/redo and commit | Code Action revision staged, later revision staged, undo/redo traversed and selected revision committed |
| EF Core | Cold document Code Fix discovery | 22,426.35 ms; 27.60 KiB response |

The Windows results are retained in run directories `20260730-113715` through `20260730-114404`.

## External-repository preparation follow-up

The Windows EF Core preparation exposed a repeatability defect outside the Code Action workflow: a preparation attempt removed the pinned commit's only three zero-byte tracked sentinel files, so the next cache reuse correctly rejected the resulting tracked deletions. Restoring those exact pinned files and reusing the prepared cache allowed the EF Core scenario to complete successfully. The runner must continue rejecting unexplained tracked changes rather than silently cleaning evidence, so a permanent preparation-boundary fix is recorded in [Future Tasks](FutureTasks.md#make-external-repository-preparation-clean-and-repeatable).
