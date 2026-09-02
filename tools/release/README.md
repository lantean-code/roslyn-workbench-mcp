# Release evidence utilities

These are repository-maintenance tools, not part of the installed MCP server. Validate them by inspecting generated evidence and exercising their commands; they do not have a separate unit-test project.

## Coverage

Normal CI and release preparation collect native Coverlet JSON and Cobertura reports during the existing six-module unit/contract run. The native JSON preserves individual branch identities, allowing the report to combine hits across modules without double-counting shared production assemblies. Only assemblies belonging to projects under `src` and their non-generated source files contribute to the summary. Raw reports also contain dependencies and remain available for detailed investigation.

After a successful test run, summarise a fresh results directory:

```bash
python tools/release/coverage-report.py \
  --results TestResults/fast --output artifacts/coverage \
  --configuration Debug --commit <tested-commit-sha>
```

Add `--baseline <coverage-summary.json>` for a local comparison. The directory must contain six native `coverage.*.json` files and six successful, non-empty TRX results. A focused local investigation may use `--expected-reports 1`; it will not compare with the six-module baseline. Use the configuration and commit actually built, not the current checkout identity if it has changed since the test run. Local uncommitted changes are recorded explicitly.

The output contains `coverage-summary.json` with source, environment, collection-policy and per-class counts, plus `coverage.md` with assembly totals and percentage-point changes. The script also appends the report to a GitHub job summary when running in Actions. No coverage percentage fails the build; missing reports or failed test results do, because they cannot establish a trustworthy baseline. A baseline is advisory evidence, not a substitute for the existing class-level coverage requirement.

`download-coverage-baseline.py` uses the authenticated `gh` CLI and `GH_REPO` to read prior evidence. CI checks up to 20 recent successful push builds on the base/current branch and selects a retained `coverage-baseline` artefact. Release preparation checks up to 100 published releases, including prereleases, for `coverage-summary.json`. Neither path publishes anything. Unavailable, expired or inaccessible history is reported and does not prevent testing. Incompatible configuration, SDK, platform, module set or policy establishes a fresh baseline instead of producing a misleading delta. Workflow, collector-dependency or aggregation changes deliberately reset the policy identity.

Detailed test and coverage reports use the existing 14-day successful/30-day failed workflow retention. CI baseline artefacts are retained for 90 days. Release aggregates and comparisons are attached to GitHub Releases with checksums, so their history survives workflow-artefact expiry. They remain subject to deliberate release deletion by the maintainer.

## Manual scenario evidence

The scenario runner remains outside hosted CI. Inspect existing `artifacts/performance/results` before deciding whether a new run would add useful evidence. Keep raw results and terminal validation together. Old measurements may still explain behaviour even when they cannot establish a comparable release baseline.

A reusable performance baseline needs the exact Host version and commit, suite revision/hash, pinned target-repository commit, command and parameters, runtime, operating system, architecture and sample counts. Do not infer an old Host identity from today's checkout or attach today's suite hash to an earlier run. Missing identity must remain explicit.

```bash
python tools/release/scenario-report.py \
  --results-root artifacts/performance/results --output artifacts/scenario-history
```

This reads existing files only. `scenario-summary.json` contains normalised common measurement metrics, sample counts, terminal validation and hashes of the original JSON evidence. Family-specific recovery, cancellation, concurrency and profiling output stays with the original results rather than being combined into misleading generic timing series. Successful and failed historical attempts remain present. `scenarios.md` is the readable summary. P95 uses nearest rank; warm-ups are excluded by the runner. Add `--baseline <scenario-summary.json>` for advisory median changes against the latest preceding like-for-like observation.

For a newly measured build, record a `run-identity.json` alongside that run's output when the provenance is known:

```json
{
  "hostCommit": "complete commit SHA of the measured Host",
  "hostVersion": "exact measured version",
  "suiteSha256": "SHA-256 of the scenario-suite.json used by that runner",
  "runnerCommit": "complete commit SHA of the runner",
  "command": "measure --repository guardclauses --scenario document-code-fixes --warmups 1 --iterations 5",
  "machineLabel": "stable non-personal hardware/environment label"
}
```

Record the command and all material parameters; normalise only incidental Host/output paths so credentials and personal paths do not enter the aggregate. Verify the actual Host and runner build identities, not just the source checkout. Do not retrofit this file onto old runs unless retained evidence proves every value. The existing platform wrapper remains the supported scenario execution route; this utility is a report converter, not another execution wrapper.

Comparisons require identical suite, runner, target repository commit, command, machine label, runtime, OS, architecture, processor count, warm-ups and sample count. Host commit/version may differ because that is what is being compared. Missing identity, failed validation, empty samples, absent history or policy drift produces no delta, never a fabricated zero baseline. Preserve source run directories with the aggregate; a hash identifies evidence but does not back it up. After privacy review and explicit publication approval, attach the aggregate and Markdown to the relevant GitHub Release for durable history. Local historical aggregates can remain local when no release association is justified.
