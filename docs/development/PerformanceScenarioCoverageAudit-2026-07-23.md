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

1. Add runner support for deliberate Host termination and restart recovery. Correct created-directory cleanup as part of the same runner-safety batch.
2. Add the Code Action create/replace durable scenario, then extend it to delete and interrupted recovery once a deterministic delete action is selected.
3. Add lifecycle sequences for cache freshness and multi-revision transactions.
4. Add commit cancellation boundaries.
5. Add the four representative query-family scenarios and measure them before proposing further optimisations.
6. Add concurrency measurements only after the single-client state sequences are stable.

The first deliverable is intentionally the crash/restart harness. It validates the strongest durability claim and provides infrastructure needed to test create/delete interruption safely. Performance tuning should remain paused until the P0 scenarios complete cleanly on Windows and WSL.
