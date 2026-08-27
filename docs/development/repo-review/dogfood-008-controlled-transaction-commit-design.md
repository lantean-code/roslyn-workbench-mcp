# DOGFOOD-008 — Large-repository durable commit validation

## Discovery correction

DOGFOOD-008 was originally recommended as missing dogfood coverage because no controlled durable commit appeared in the published dogfood usage log. That conclusion was incorrect: the recommendation did not first check the existing Scenario Runner. The checked-in `rename-dbcontext-durable` scenario already performed a real `transaction-commit`, required `committed: true`, captured non-empty physical repository changes and restored its disposable pinned EF Core checkout exactly.

The implementation described below is therefore optional post-commit validation hardening, not a new controlled-commit scenario or remediation of a missing commit workflow. The design and validation remain useful records of that narrow enhancement, but they must not be cited as evidence that DOGFOOD-008 introduced durable-commit coverage.

## Purpose

DOGFOOD-008 re-runs the existing final transaction boundary on a realistic large solution and strengthens its post-commit assertions. It uses the Scenario Runner's existing pinned EF Core durable-commit scenario to perform a broad semantic rename, inspect the staged preview, write the change through `transaction-commit`, verify the promoted Workspace state and physical Git changes, then restore the checkout exactly.

The earlier proposal to copy the minimal acceptance fixture is superseded. That workflow would be safe, but it would largely repeat existing acceptance coverage and would not exercise realistic solution size, multi-project rename breadth or large commit planning.

## Discovery

The Scenario Runner already contains the requested large-project workflow. The checked-in `rename-dbcontext-durable` scenario opens the pinned EF Core solution, starts a transaction and renames `Microsoft.EntityFrameworkCore.DbContext` to `PerformanceDbContext` across the solution without renaming the file.

The durable-commit runner already:

1. publishes and starts a fresh Host;
2. requires a clean checkout at the exact configured commit;
3. opens the real solution;
4. stages the configured mutation;
5. runs `transaction-preview` and requires at least one changed document;
6. invokes `transaction-commit` and requires `committed: true`;
7. closes the Workspace and Host even when the workload fails;
8. captures the actual tracked replacements, deletions and untracked creations reported by Git;
9. restores tracked paths from the pinned commit and deletes created files;
10. verifies that no tracked or untracked change remains; and
11. verifies clean Host shutdown, no unfinished recovery state and no leaked Workspace coordination files.

Restoration runs with cancellation disabled and cleanup failures are combined with workload failures, so an earlier error does not silently skip later cleanup. This is materially safer than changing the active Roslyn Workbench repository and manually restoring one expected path: the runner starts from a certified clean clone, owns the whole checkout and detects unexpected changes outside the anticipated target set.

## Optional validation hardening

The existing runner observes the snapshot returned by `transaction-commit`, but its durable-commit result previously asserted only `committed: true` before closing the Workspace. The optional hardening adds explicit evidence that the Workspace promoted the committed solution and exited transaction state.

Add a narrow post-commit validation inside `DurableCommitRunner`, outside the measured commit duration:

- require the commit response to contain a snapshot for the same Workspace;
- require its `transactionRevision` to be `null`;
- require its snapshot ID to differ from the staged revision's snapshot ID;
- call `workspace-status` after the commit; and
- require lifecycle state `Ready`, no active transaction and the same promoted snapshot identity.

This is a generic durable-commit invariant, so it belongs in the runner rather than only in the EF Core scenario definition. Every existing durable-commit, commit-cancellation and recovery consumer continues to use its current workflow; only normally completed durable commits receive the new post-commit readiness check.

## Operating-model assessment

- **Actor:** the Scenario Runner's local MCP client using a freshly published Host.
- **Action:** commit a broad semantic rename in a runner-owned pinned EF Core checkout while no user or other tool is editing that checkout.
- **Plausibility:** solution-wide rename is a supported coherent transaction and exercises substantially more realistic planning and filesystem work than the minimal acceptance fixture.
- **Existing controls:** the checkout is pinned and certified clean, the preview is required before commit, all snapshot preconditions are materialised from current responses, the runner owns the Host and checkout, and Git-backed restoration plus recovery validation runs after every iteration.
- **Impact:** success proves durable commit, snapshot promotion, post-commit readiness, physical multi-file persistence and exact repository restoration on a large real solution.
- **Decision:** strengthen and run the existing EF Core durable-commit scenario; do not add a duplicate scenario or mutate the active repository.

## Production and contract design

No Host production code or MCP contract changes are proposed. The only runtime change is in the manual Scenario Runner:

1. Capture the complete staged snapshot immediately before `transaction-commit`.
2. Keep the existing commit timing boundary unchanged.
3. Parse and validate the complete snapshot returned by the successful commit.
4. Invoke `workspace-status` after timing completes and validate `Ready`, `transaction: null` and identical snapshot identity.
5. Retain the existing physical-change capture, restoration and final run-state validation.

Use small named validation methods rather than embedding nested JSON traversal in `CommitAsync`. Failure messages must identify whether the commit flag, promoted snapshot or post-commit lifecycle check failed.

Do not add a scenario-specific response assertion framework, change commit timing, change the EF Core mutation, retain the committed checkout, or expose additional data through the Host contract.

## Test and scenario validation design

The Scenario Runner has no checked-in unit-test project. Adding a test-only project for this small invariant would be disproportionate and would not exercise the real published process. Validation therefore uses the affected real scenario through the required platform wrapper.

After implementation:

1. Format the changed Scenario Runner C# file only.
2. Build the Scenario Runner and its dependencies normally.
3. Run the Scenario Runner project with the SDK `latest-all` analyser configuration and review diagnostics in the changed file.
4. Run the existing EF Core durable scenario once with no warm-up:

   `commit --repository efcore --scenario rename-dbcontext-durable --iterations 1 --warmups 0`

5. Require the scenario report to show a non-empty multi-document preview, `committed: true`, successful post-commit snapshot/readiness validation, non-empty physical file changes, successful restoration, clean pinned `HEAD`, no tracked or untracked files, no recovery state, no coordination leak and clean Host shutdown.
6. Verify CRLF and `git diff --check` for the changed repository files.

The first run may need to prepare the pinned EF Core checkout and toolchain. Preparation is outside the measured commit and the wrapper owns the cache. Use `--skip-prepare` only if the runner has already certified a prepared cache for the exact pinned commit.

## Alternatives rejected

- **Commit against the active Roslyn Workbench repository and restore with Git:** this is realistic but unnecessary. The worktree contains intentional documentation changes, runs from the mounted Windows filesystem and is not exclusively owned by the scenario. A failure or unexpected extra path would require more careful recovery around user work.
- **Copy the minimal acceptance fixture:** safe but duplicates existing acceptance evidence and provides little large-solution or multi-file commit coverage.
- **Add another durable-commit scenario:** the large EF Core `rename-dbcontext-durable` scenario already exists and is the correct workload. Duplicating it would increase suite cost without increasing behavioural coverage.
- **Run the current scenario without strengthening it:** this proves physical persistence and cleanup, but it does not explicitly fail when a nominally successful commit returns or retains the wrong post-transaction Workspace state.
- **Add a general post-commit query assertion language:** unnecessary for the invariant under test. `workspace-status` directly exposes lifecycle, transaction and snapshot promotion without adding scenario-definition complexity.

## Approval and process

The user approved this revised design before Scenario Runner code changed or the expensive EF Core scenario was executed. Subsequent review established that discovery should have identified the pre-existing successful commit workflow before DOGFOOD-008 was recommended. The process failure occurred before the approved design: absence from the dogfood usage log was treated as absence of repository coverage without completing the required issue-validation step.

Because this is a behaviour-affecting Scenario Runner change, follow the normal implementation and confirmation process: implement and validate, obtain the user's first confirmation, stage the confirmed baseline, run a fresh context-free Review Agent pass with the current validation evidence, retain any review remediation as an unstaged comparison, obtain final confirmation and let the user commit.

Changing Scenario Runner code requires the affected scenario to pass through the platform wrapper before the item can be confirmed. If the run exposes a Host product defect, stop and return to design discovery before changing Host production code.

## Implementation evidence

The implementation exposes the Scenario Host's already-observed snapshot as a typed `ScenarioSnapshot`, captures the staged snapshot immediately before commit, validates the promoted snapshot returned by `transaction-commit`, and calls `workspace-status` after the measured commit to require `Ready`, no active transaction and the same promoted snapshot.

Validation completed on 2026-08-27:

- the normal Scenario Runner build and the SDK `latest-all` analyser build succeeded with no warnings or errors;
- the `rename-dbcontext-durable` EF Core scenario passed through the platform wrapper with one measured iteration and no warm-up;
- the scenario replaced 948 tracked files and then restored the checkout to the expected pinned commit;
- the generated validation report recorded no issues, no recovery state, no leaked Workspace state files and a clean Host shutdown; and
- `git diff --check` passed and all changed repository files use CRLF line endings.

The fresh context-free Review Agent reported no findings. It identified that the shared durable-commit runner also required representative Serilog coverage before completion. Both affected shapes subsequently passed through the platform wrapper with one measured iteration and no warm-up:

- `rename-ilogger-durable` replaced 27 tracked files and restored the checkout to its pinned commit; and
- `move-no-enumeration-to-file-durable` exercised a Code Action commit that created one file and replaced another, then restored both operations.

Both validation reports recorded the expected pinned commit, no issues or leaked state, exit code 0 and no forced Host termination. These evidence-only documentation updates were retained unstaged against the first-confirmed baseline until the user gave final confirmation.
