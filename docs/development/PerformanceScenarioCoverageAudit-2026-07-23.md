# Performance Scenario Coverage Audit

Date: 2026-07-23

## Purpose

This second-pass audit checks whether the performance programme exercises the production workflows most likely to expose correctness, state-management and platform defects. It follows the durable-commit and conflict measurements, which found critical Windows, linked-document and recovery-path issues that ordinary unit and integration coverage had not exposed.

The audit treats scenario coverage as more important than raw tool count. A separate performance scenario is warranted when scale, operating-system behaviour, process lifetime, concurrency or a sequence of state transitions can change the outcome. Ordinary validation branches should remain in the automated test suite.

## Current coverage

The checked-in suite contains 59 repository/scenario definitions across GuardClauses, Serilog and EF Core. They cover fifteen distinct primary tools:

- ten bundled query tools;
- Code Action discovery;
- one Code Action mutation;
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
| P2 | Representative unmeasured query families | Twenty-eight bundled query handlers have no direct scenario. The static audit improved them, but most remaining handlers are represented by an already measured cost shape rather than needing one scenario per tool. | Completed in Batch 5 with `get-change-impact`, `get-code-metrics`, `find-duplicate-code` and shallow/deep `get-control-flow-graph` scenarios. Add another query only when it presents a materially different cost shape. |
| P2 | Concurrent clients, multiple Workspaces and cross-process commit-lock contention | Bounded parallel reads and two-Workspace ownership are complete in Batch 6. Existing native integration coverage remains sufficient for cross-process commit-lock contention; add a published-Host scenario only if future evidence shows a behavioural difference. | Completed for the MCP and repository-scale gaps. |
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

## Batch 3 implementation

**Status:** Complete; native WSL and native Windows validation passed.

**Production fix required:** No. Both published-Host lifecycle sequences returned fresh semantic results at their intended invalidation boundaries. The initial external-edit assertions were runner defects: commonly referenced symbols saturated the 500-item response bound, and one physical Serilog source edit appears in 25 loaded Roslyn documents across target-framework projects. The curated scenario now uses the lower-cardinality `NoEnumerationAttribute` symbol and requires the refreshed semantic result to grow without assuming a one-file-to-one-document mapping.

The permanent runner now provides a `state-sequence` command. Each iteration keeps one Host and Workspace alive for the complete scenario, records every MCP step, captures response hashes and relevant semantic or transaction state, restores any committed changes, and performs the ordinary repository, recovery-state, Workspace-state and Host-shutdown validation.

The external-reload sequence warmed `find-references` from 2169.58 ms to 10.70 ms, changed `Logger.cs` outside the Host, and received `WorkspaceOutOfDate` with `ReloadWorkspace` from the next query. After `workspace-reload`, the response changed and its bounded semantic references increased from 35 to 60. The external change was restored before repository validation, the Host exited normally, and no source, recovery, coordination or lock residue remained. Evidence: `artifacts/performance/results/20260723-113440-serilog-22b9796adaa446a6bfa0bdf3c508a14b`.

The multi-revision sequence warmed the same query from 2227.81 ms to 12.61 ms, staged `move-type-to-file` as revision 1 and `rename-symbol` as revision 2, then verified preview at revision 2, undo to revision 1 and redo to revision 2 before committing. The post-commit query resolved the moved definition from `NoEnumerationAttribute.cs` rather than `Guard.cs`, proving it did not serve the warmed pre-transaction entry. The commit created one file and replaced seven, after which runner restoration returned the checkout to its pinned state and final validation passed. Evidence: `artifacts/performance/results/20260723-113420-serilog-6fb18ceaab7e42c3872495ae436693c7`.

Native Windows confirmed both sequences. The external-reload run reproduced the warmed response, rejected the stale query with `WorkspaceOutOfDate` and `ReloadWorkspace`, then increased the semantic reference result from 63 to 112 after reload. It restored the external edit, exited normally and reported no validation issues. The multi-revision run reproduced revisions 0, 1 and 2, moved backward to revision 1 and forward to revision 2, committed one created and seven replaced files, and resolved the moved definition from `NoEnumerationAttribute.cs`. Runner restoration returned the checkout to the pinned commit; both runs left no recovery or new Workspace state files. Evidence: `artifacts/performance/results/20260723-121349-serilog-d89cca1bdabe4d4cb69c1ba698d5a5ac` and `artifacts/performance/results/20260723-121549-serilog-1b05634fe5a44106a4a31215b8be7b86`.

## Batch 4 implementation

**Status:** Complete; native WSL and native Windows validation passed.

**Production fix required:** No. The published Host honoured cancellation while the commit was still staging and ignored it after durable application began. Initial failures came from the runner assuming the cancelled client task and server-side lease settled simultaneously, then assuming a post-`Applying` client task would receive the eventual success response. MCP cancellation completes the client request first in both cases; the server's published commit phase is the authoritative durability evidence.

The permanent runner now provides a `commit-cancellation` command. It uses the Workspace instance-status record to observe exact `Staging`, `Applying` and `Committed` phases, sends a real MCP `notifications/cancelled` message for the active `transaction-commit` request, and records client-notification, client-completion and server-settlement timings. Each boundary uses a fresh Host and staged transaction, restores any committed files, and validates recovery state, Workspace coordination, checkout cleanliness and Host shutdown.

The pre-application Serilog run observed `Staging`, delivered cancellation in 1.69 ms and received client cancellation after another 0.38 ms. The server released its lease and cleaned partial recovery data within 53.35 ms. All 101 preview documents remained staged until the runner deliberately rolled the transaction back, no source file changed, and no recovery artifact remained.

The post-application run observed `Applying`, delivered cancellation in 0.16 ms and received client cancellation after 0.07 ms. The server continued independently for 516.08 ms, published `Committed`, replaced all 27 intended files and removed its recovery state. The runner then restored the pinned checkout. Both Hosts exited normally and final validation reported no repository, recovery, coordination or lock issue. Evidence: `artifacts/performance/results/20260723-122709-serilog-c17e7f1b67c34cb585db96b07b332725`.

Native Windows confirmed both boundaries. Pre-application cancellation was delivered in 1.65 ms, completed the client request after another 0.40 ms and settled the server lease within 64.79 ms. All 137 preview documents remained staged until deliberate rollback, no source file changed and recovery state was empty. Post-application cancellation was delivered in 0.14 ms and completed the client request after 0.07 ms; the server continued for 1690.97 ms, published `Committed`, replaced all 27 files and removed its recovery state. Runner restoration returned the checkout to the pinned commit. Both Hosts exited normally without recovery, coordination or lock residue. Evidence: `artifacts/performance/results/20260723-123134-serilog-0408ddb402f24eccbd64747870b7ea94`.

## Batch 5 implementation

**Status:** Complete on native WSL storage. The measured query paths are platform-neutral Roslyn and managed projection work, so a second native-Windows correctness run is not required.

**Production fix required:** No. The representative families produced deterministic bounded responses, completed within proportionate steady-state times and left the repository, Workspace coordination and recovery state clean. The measurements do not justify another production cache or algorithm rewrite.

The permanent suite now includes:

- low/high `get-change-impact` scenarios over `Serilog.ILogger`, combining reference and implementation discovery with 5- and 100-location projections;
- low/high project-scoped `get-code-metrics` scenarios over Serilog, plus a five-result EF Core project scan;
- project-scoped `find-duplicate-code` scenarios over Serilog and EF Core; and
- shallow/deep `get-control-flow-graph` projections over the same project-qualified Serilog method.

The five-measurement Serilog run recorded:

| Scenario | Median | P95 | Returned bound |
|---|---:|---:|---|
| Change impact, 5 locations | 9.85 ms | 10.78 ms | 5, `HasMore: true` |
| Change impact, 100 locations | 17.93 ms | 22.37 ms | 100, `HasMore: true` |
| Code metrics, 5 results | 29.79 ms | 47.40 ms | 5, `HasMore: true` |
| Code metrics, 100 results | 49.42 ms | 52.32 ms | 100, `HasMore: true` |
| Duplicate code | 80.21 ms | 132.92 ms | 3 complete groups |
| Control flow, shallow | 5.84 ms | 6.05 ms | 2 blocks and 2 regions |
| Control flow, deep | 5.84 ms | 6.00 ms | complete graph |

The matching EF Core scale check retained a 429.22-millisecond median for five code metrics and a 610.08-millisecond median for ten duplicate groups. Later invocations settled at 368.14 milliseconds and 465.25 milliseconds respectively as Roslyn and filesystem caches warmed. Both bounded collections reported `HasMore: true`; response hashes were stable. Evidence: `artifacts/performance/results/20260723-124230-serilog-0f8ca3ba1fba4fb5af6868c0136bbfde` and `artifacts/performance/results/20260723-124754-efcore-2bb866fe6eb34bd5ab3727ca726c0784`.

The limit comparisons confirm the intended boundary. `get-code-metrics` must still identify and globally order lightweight candidates across the selected project, but it calculates syntax metrics and projects DTOs only for returned candidates. `find-duplicate-code` must complete normalization and grouping before it can know which groups are duplicates; it already defers occurrence projection until after the group bound. The shallow/deep control-flow timings are indistinguishable, showing that its bounded projection is not a material cost beside Roslyn graph construction.

A focused static pass over the four measured handlers found no `async void`, blocking `Task` access, per-call JSON options, runtime regular expressions, `Task.Run`, culture-sensitive string search or case-normalization pattern. It counted fifteen explicit `List`, `Dictionary` or `HashSet` constructions, twelve LINQ operations and two `string.Join` calls. Each collection is request-local analysis state or bounded response state; the remaining LINQ calls are small terminal aggregation/ordering operations or the complete discovery required by the contracts. All eight reference types in the selected files are sealed; the ninth declaration is a `readonly record struct`. No static signal contradicts the measured outcome.

An attempted location-based operation-tree scenario also exposed a separate contract gap: a path-only `DocumentSelector` is ambiguous when the same physical file appears in several target-framework projects. The suite used project-qualified symbol selection for control flow at the time; the document selector has since gained an optional project qualifier that resolves the broader location-selector gap.

## Batch 6 implementation

**Status:** Complete; native WSL and native Windows validation passed.

**Production fix required:** No. The published Host enforced its documented non-waiting query bound, recovered after every rejected request, kept two overlapping Workspace snapshots independently queryable and enforced single-Workspace transaction ownership. Batch 6 changed only the manual scenario runner, its checked-in scenario definition and documentation.

The permanent runner now provides a `concurrency` command. It releases a configurable number of MCP query calls from one client-side start gate and records batch and individual elapsed time, response size, exact response hash, `WorkspaceBusy` outcomes and successful retries. Successful calls must match the warmed baseline. An excess request may only return `WorkspaceBusy` with `Retry`, and every such request is retried after the batch to prove lease recovery.

The Serilog scenario opens `Serilog.sln` as the primary Workspace and `src/Serilog/Serilog.csproj` as a second Workspace in the same published Host. This deliberately gives the two snapshots overlapping physical source rather than using unrelated repositories. The runner requires `workspace-list` to report both IDs, queries the second Workspace while the first owns the transaction, rejects a second transaction with `TransactionOwnedByWorkspace` and `CommitOrRollback`, swaps transaction ownership, queries the first Workspace, rolls back, and finally queries both Workspaces concurrently. Each query is checked against the baseline for its own Workspace.

With four simultaneous `get-solution-structure` requests and the default two-query limit, both measured batches accepted two requests and rejected two with `WorkspaceBusy`; all four rejected calls subsequently retried successfully. Median batch time was 15.58 ms and P95 was 19.98 ms. The parallel cross-Workspace query pair completed in 26.97 ms. Both Workspace queries remained byte-for-byte stable while the other Workspace owned the transaction, transaction ownership transferred cleanly after rollback, both Workspaces closed, the Host exited normally, and final validation found no repository changes, recovery state, coordination files or lock residue. Evidence: `artifacts/performance/results/20260723-131452-serilog-d4eef56c8d8948529bbcfda07d771cca`.

Native Windows confirmed the same boundary over five measured batches. Every batch accepted two requests and rejected two with `WorkspaceBusy` and `Retry`; all ten rejected calls subsequently retried successfully. Median batch time was 50.65 ms, P95 was 54.98 ms, median successful query time was 49.34 ms and the parallel cross-Workspace query pair completed in 48.89 ms. Primary and secondary response hashes remained stable throughout transaction-ownership changes, the non-owner transaction returned `TransactionOwnedByWorkspace` and `CommitOrRollback`, both Workspaces closed, the Host exited normally, and final validation found no repository changes, recovery state, coordination files or lock residue. Evidence: `artifacts/performance/results/20260723-133035-serilog-a888744f566b44dab6320eccd9a7ba7d`.

Cross-process commit-lock contention was not duplicated in the manual runner. The existing native integration scenario exercises the production lock implementation and lock release directly; Batch 6 found no published-MCP behaviour that invalidates that evidence. A second process-level scenario would add destructive orchestration without closing a demonstrated gap.
