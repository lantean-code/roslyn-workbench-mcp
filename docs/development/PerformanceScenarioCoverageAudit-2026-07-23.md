# Performance Scenario Coverage Audit

Date: 2026-07-23

## Purpose

This second-pass audit checks whether the performance programme exercises the production workflows most likely to expose correctness, state-management and platform defects. It follows the durable-commit and conflict measurements, which found critical Windows, linked-document and recovery-path issues that ordinary unit and integration coverage had not exposed.

The audit treats scenario coverage as more important than raw tool count. A separate performance scenario is warranted when scale, operating-system behaviour, process lifetime, concurrency or a sequence of state transitions can change the outcome. Ordinary validation branches should remain in the automated test suite.

## Current coverage

The checked-in suite contains 47 repository/scenario definitions across GuardClauses, Serilog and EF Core. They cover ten distinct tools:

- six bundled query tools;
- Code Action discovery;
- the three bundled refactoring tools; and
- server-owned Workspace and transaction tools used as supporting steps.

The bundled plugin exposes 38 query tools and three mutation tools. The Code Action catalogue registers two query tools and 46 mutation tools before visibility filtering, while the Host owns eleven lifecycle and transaction tools. The suite is therefore representative rather than exhaustive, but its current representation is uneven:

- query evidence is concentrated on solution structure, project details, symbol search, reference discovery, diagnostics and dependency graphs;
- Code Action evidence covers discovery only;
- every durable commit and conflict scenario stages `rename-symbol`;
- every measured durable file operation is a replacement; and
- every repository is opened from a `.sln` file in one Workspace per Host.

The durable runner already records create, replace and delete operations and can restore tracked deletions and untracked files. Its cleanup does not currently remove empty directories created by a mutation, so that must be corrected before a create-in-new-directory scenario can be considered reproducible.

## Risk-ordered gaps

| Priority | Missing scenario | Why existing evidence is insufficient | Recommended evidence |
|---|---|---|---|
| P0 | Published-Host termination during durable application, followed by a fresh Host using the same state directory | The conflict runner lets the original Host catch the failure and perform recovery. Durable integration tests construct persisted manifests and a fresh recovery service, but they do not terminate a published MCP Host while real source writes are in progress. Startup composition, process locks, stdio termination and OS file semantics are therefore not exercised together. | Terminate a broad commit after its manifest reaches `Applying`, start a fresh Host with the same state directory, and prove startup recovery restores the checkout, removes recoverable state, releases locks and permits the Workspace to open. Run on native Windows and WSL. |
| P0 | Durable create and delete operations through a real Code Action | The production planner, writer and recovery code have distinct create and delete paths, including directory creation and delete-marker moves. Current external-repository commits exercise only replacements. Unit and integration tests cover synthetic create/delete manifests, but not Roslyn Code Action output through MCP, staging, preview, commit and cleanup. | Add a deterministic `move-type-to-file` commit that creates a source file and updates the original. Add or curate an action that deletes a source file if one is reliably available. Exercise success first, then recovery after application has crossed a create or delete entry. |
| P1 | Cache freshness across external change, reload and commit | Cache invalidation tests verify calls at the session-store boundary, and lifecycle integration tests verify reload state transitions. No published-Host scenario warms `find-references` or target-framework data, changes an input, proves the stale query is rejected, reloads, and proves the next query is computed from the new snapshot. The same gap exists after a successful commit promotes a new solution. | Add a state-sequence scenario covering warm query, external edit, rejected stale query, `workspace-reload`, and refreshed query. Add a smaller sequence covering warm query, mutation commit and a post-commit query. Validate response hashes as well as cache-hit phase evidence. |
| P1 | Code Action execution and mutation staging | `list-code-actions` establishes discovery cost only. None of the 46 registered mutation paths is measured through the published Host, even though Code Actions use different resolution, replay, operation evaluation and staging code from bundled refactorings. | Add one token-based Code Action, one dedicated replay refactoring, and one scoped/fix-all action. Select representatives by execution architecture, not by attempting to benchmark every action. The create-file action should also satisfy the durable-operation gap above. |
| P1 | Multiple revisions in one real transaction | The runner starts a fresh transaction for one mutation and then commits or rolls it back. Component integration tests cover history and multiple staged documents, but not a sequence of real plugin or Code Action mutations through MCP. This misses revision growth, linked-change merging across revisions, preview aggregation and cache invalidation at the final promotion boundary. | Stage two compatible mutations, inspect preview and history, move backward and forward once, then commit. Add an overlapping or incompatible second mutation as a rejection case in automated acceptance coverage rather than a timing benchmark. |
| P1 | Protocol cancellation at commit phase boundaries | Query cancellation is measured, but transaction commit has deliberately different semantics: cancellation is honoured before application begins and ignored once durable application has started. That contract is not exercised through an MCP cancellation notification. | Cancel once during planning or recovery-plan persistence and prove no source change or unfinished state remains. Cancel once after `Applying` and prove the commit or recovery reaches a durable terminal state despite client cancellation. |
| P2 | Representative unmeasured query families | Thirty-two bundled query handlers have no direct scenario. The static audit improved them, but there is no end-to-end evidence for several materially different cost shapes. | Add a small representative set: `get-change-impact` for combined Roslyn relationship searches; `get-code-metrics` or an analyser for source scanning; `find-duplicate-code` or cycle discovery for whole-solution algorithms; and a deep operation/control-flow projection. Do not add one scenario per tool unless a representative reveals a distinct bottleneck. |
| P2 | Concurrent clients, multiple Workspaces and cross-process commit-lock contention | Integration tests cover transaction ownership and native lock release, but the runner is single-client and opens one Workspace. Contention, queueing and cleanup are not measured through MCP or under repository-scale load. | Add a bounded concurrency scenario with parallel read queries, then a two-Workspace transaction-ownership sequence. Add cross-process lock contention only if the published Host behaviour differs from the existing native integration test. |
| P3 | Other supported open shapes and path environments | All performance repositories use `.sln`. Functional coverage should continue to prove direct project and `.slnx` opening, unsupported-project filtering, WSL-to-Windows warnings, long Windows paths and UNC path conversion. These do not each require a performance baseline. | Keep these in published-host acceptance coverage. Promote one to the runner only if preparation or loading behaviour is materially different or a platform defect is found. |

## Static performance safety pass

A fresh production-source scan found no new critical async or construction pattern requiring immediate optimisation:

| Signal | Result |
|---|---:|
| `async void` | 0 |
| `GetAwaiter().GetResult()` | 0 |
| Per-call `new JsonSerializerOptions` | 0 |
| `stackalloc` sites | 0 |
| Runtime `new Regex(...)` sites | 0 |
| `Task.Run(...)` sites | 0 |

The broad `.Result` text scan returned 79 matches, all reviewed as namespaces, result contracts, result properties or performance phase names rather than blocking `Task.Result` calls. The three `ContainsKey` sites are membership checks without a following indexer lookup. No static finding takes priority over the scenario gaps above.

## Delivery order

The gaps should not be implemented as one batch. The runner changes alter process lifetime and cleanup rules, while the Code Action work depends on reliable create/delete restoration.

Each completed batch must record a **Production fix required** outcome. Use `Yes` when the scenario exposes a defect or justified performance change in the shipped Host, Workspace, plugin or Code Action projects, and describe the fix and confirming evidence. Use `No` when only the manual runner, scenario definition, documentation or validation model changes. A discovered problem must not be described as a production fix until its ownership has been established.

1. Add runner support for deliberate Host termination and restart recovery. Correct created-directory cleanup as part of the same runner-safety batch.
2. Add the Code Action create/replace durable scenario, then extend it to delete and interrupted recovery once a deterministic delete action is selected.
3. Add lifecycle sequences for cache freshness and multi-revision transactions.
4. Add commit cancellation boundaries.
5. Add the four representative query-family scenarios and measure them before proposing further optimisations.
6. Add concurrency measurements only after the single-client state sequences are stable.

The first deliverable is intentionally the crash/restart harness. It validates the strongest durability claim and provides infrastructure needed to test create/delete interruption safely. Performance tuning should remain paused until the P0 scenarios complete cleanly on Windows and WSL.

## Batch 1 implementation

**Status:** Complete; native WSL and native Windows validation passed.

**Production fix required:** No. The published Host correctly restored the partially applied commit during fresh-process startup. The only failed initial validation came from the manual runner treating the intentionally persistent `commit.lock` marker as leaked lock ownership. The runner now distinguishes the marker file from the OS lock it coordinates and removes the marker as disposable benchmark state after recovery validation.

The permanent runner now provides a `crash-recovery` command. It stages and previews a configured mutation, starts `transaction-commit`, waits until an `Applying` manifest exists and at least one replacement target contains its intended bytes, then forcibly terminates the published Host. A fresh Host starts with the same state directory, performs normal startup recovery before MCP initialisation, reopens and closes the Workspace, and must leave the pinned repository and recovery directory clean.

Crash-specific validation distinguishes the persistent `commit.lock` marker from live lock ownership. The fresh Host must successfully consume and remove the recovery manifest, restore the repository and shut down normally. The marker is then removed as disposable runner state before the ordinary final validation. Mutation restoration also removes newly empty parent directories after deleting runner-observed created files, preparing the restoration boundary for the create/delete scenarios in Batch 2.

The native WSL Serilog run terminated the Host with an `Applying` manifest containing 54 artifacts after four of 27 replacement files had become observable. Fresh-Host startup restored the repository in 700.44 ms, the recovered Workspace reopened successfully, the recovery Host exited normally and final repository, recovery, coordination and lock-state validation passed. Evidence: `artifacts/performance/results/20260723-104059-serilog-9263da897f3f464f821dd7cd990c13fe`.

The first native Windows attempt completed all 27 replacements inside the runner's 2 ms polling interval, so no crash was injected. Replacing that polling with filesystem notifications still allowed native Windows to complete before its notification callback was scheduled. These failures exposed runner timing flaws rather than production defects. The runner now obtains the changed target paths from `transaction-preview`, captures their file state before starting the commit, and actively monitors those targets without an asynchronous scheduling interval so it can terminate the Host as soon as a replacement becomes visible. A repeat WSL run using the active monitor interrupted after three observable replacements and again passed recovery and final-state validation. Evidence: `artifacts/performance/results/20260723-110222-serilog-3e3c247555d043f2b4ce1183dd3a679c`.

The native Windows confirmation interrupted after two observable replacements with an `Applying` manifest containing all 54 artifacts. Fresh-Host startup recovery completed in 708.97 ms, the Workspace reopened successfully, the interrupted Host recorded forced termination, the recovery Host exited normally, and final validation found no repository changes, unfinished recovery data, new Workspace state files or lock residue. Evidence: `artifacts/performance/results/20260723-110330-serilog-b75ae3d8b19e4212bf7fc06a6a0f8e9d`.

## Batch 2 implementation

**Status:** Complete for the currently available Code Action operation shapes; create/replace success and interrupted recovery passed on native WSL and native Windows. Delete coverage is awaiting a genuinely available built-in Code Action.

**Production fix required:** No. The real Code Action, durable planner, create/replace writer paths and fresh-Host recovery completed correctly on both platforms. Batch 2 required only performance-runner scenario and operation-selection changes.

The `move-no-enumeration-to-file-durable` scenario runs the published `move-type-to-file` tool against Serilog's `NoEnumerationAttribute`, which initially shares `Guard.cs`. The successful commit replaced `Guard.cs`, created `NoEnumerationAttribute.cs`, then restored the pinned checkout without leaving the created file or an empty directory. Mutation staging took 1961.08 ms, preview 8.33 ms, durable commit 625.63 ms and runner restoration 47.97 ms. Evidence: `artifacts/performance/results/20260723-111135-serilog-677c2503f95a4e2384b94582a35ce5fa`.

Crash recovery can now select the operation at which to interrupt from the transaction preview rather than always stopping at the first replacement. The create-path run waited until `Guard.cs` had been replaced and `NoEnumerationAttribute.cs` had been created, then terminated the Host with an `Applying` manifest and three durable artifacts. Fresh-Host startup removed the created file, restored the original document, reopened the Workspace and left final state clean. Evidence: `artifacts/performance/results/20260723-111426-serilog-c40db9c9aa7b449aba2a03c9a7ee1ef2`.

Native Windows confirmed both paths. The successful commit staged in 2166.98 ms, previewed in 7.28 ms, committed in 1432.35 ms and restored the checkout in 218.31 ms; the Host exited normally and final validation was clean. The interrupted run terminated 116.81 ms after commit invocation with the created file and original-file replacement both observable, retained an `Applying` manifest with three artifacts, recovered during fresh-Host startup in 693.99 ms, reopened the Workspace and left no repository, recovery, coordination or lock residue. Evidence: `artifacts/performance/results/20260723-111919-serilog-dc17a6b6599f4d9b860feb2ff19a4b3d` and `artifacts/performance/results/20260723-111942-serilog-aaa727d9a67f43d58f03123ed8152a30`.

The delete candidate audit found no currently validated built-in Code Action that removes a source document. Roslyn's move-type provider creates a new document while retaining and updating the original when types share a file. It does not offer the move-to-file action for the sole type in a mismatched filename, as confirmed against EF Core's `NullableStructCurrentProviderValueComparer<TModel,TProvider>`. No production Code Action implementation or compatibility fixture uses `Solution.RemoveDocument`; the only occurrence is synthetic solution-change counting coverage. A delete scenario must therefore remain dependent on a real, deterministic action becoming available rather than using a fabricated Workspace mutation.
