# Integration Testing Stage 6 Results

Date: 2026-07-17

## Outcome

Stage 6 separates fast source governance from the version-sensitive Roslyn Code Action compatibility audit.

- `InternalArgumentNullGuardAuditTests`, `ProductionNullForgivingOperatorAuditTests` and their shared production-source scanner now live under the Host test project's `Architecture` area.
- Their source rules are unchanged, and the tests no longer carry the `Audit` category, so both run in the default fast loop.
- The audit project now contains only built-in provider visibility/replay and replay-family compatibility checks.
- The unused Moq dependency was removed from the audit project.

## Compatibility grouping and ownership

The supported/impossible provider ledger remains the source of truth. Supported compatibility cases continue to come from the immutable audit case catalogue; mutable fixtures, Workspaces and provider execution remain isolated per case.

Provider compatibility is split into independently schedulable refactoring and code-fix classes. Replay-family compatibility remains a separate class. Assembly concurrency is capped at two threads because cases create real Roslyn/MSBuild workspaces and the measured audit still peaks above 1 GiB; this retains class-level parallelism without allowing the peak to scale with processor count.

All 93 compatibility cases passed in each of three measured warm runs, so the selected grouping and concurrency limit remained deterministic.

## CI policy

The compatibility audit has a dedicated workflow. It runs:

- for pull requests that change CodeActions, Roslyn dependency inputs, provider/replay audit code, audit support or the audit fixture/ledger;
- for every push to `main`;
- weekly as a full drift check; and
- by manual dispatch.

The general test workflow no longer runs the audit unconditionally for every pull request.

## Measurement

Measurements used an already-built audit project with `--no-build --no-restore`. They are local WSL comparison evidence, not performance assertions.

| Run | Tests | Wall time | Peak resident memory |
| --- | ---: | ---: | ---: |
| 1 | 93 | 53.28 s | 1,182,648 KiB |
| 2 | 93 | 53.14 s | 1,088,416 KiB |
| 3 | 93 | 54.06 s | 1,099,944 KiB |
| Median | 93 | 53.28 s | 1,099,944 KiB (1,074.2 MiB) |

The Stage 0 median was 57.23 seconds and 1,124.9 MiB for 95 tests. After moving the two source-governance scans, the compatibility-only audit is 3.95 seconds (6.9%) faster and uses 50.7 MiB (4.5%) less median peak memory. The remaining memory cost is intrinsic to the real Roslyn/MSBuild compatibility cases and motivates the explicit two-thread ceiling.

## Verification

- Host fast loop: 277 passed, including both source-governance checks.
- Code Action compatibility audit: 93 passed in the verification run and in all three measurement runs.
- Full repository suite against a freshly published acceptance Host: 1,952 passed.
