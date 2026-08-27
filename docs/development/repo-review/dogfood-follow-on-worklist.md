# Follow-on dogfood validation worklist

**Status:** Approved for design discovery on 2026-08-27.

**Evidence:** [Post-RWMCP3 dogfood usage log](dogfood-improvement-usage.md)

## Purpose

The original [dogfood improvement worklist](dogfood-analysis.md#approved-improvement-worklist) is complete. Analysis of the continuing usage log found no further confirmed product defect and identified workflows that were not visible in that log. Absence from the usage log is not evidence that repository tests or scenarios lack coverage. Each follow-on item must therefore validate its claimed gap against the existing unit, integration, acceptance and Scenario Runner coverage before proposing design or implementation work.

## Worklist

| Order | Identifier | Validation item | Status |
|---:|---|---|---|
| 1 | DOGFOOD-008 | [Commit a controlled transaction in a disposable Workspace](dogfood-008-controlled-transaction-commit-design.md) | Confirmed; ready to commit |
| 2 | DOGFOOD-009 | Exercise the Code Action and Fix All workflows | Confirmed through normal Codex dogfood validation |
| 3 | DOGFOOD-010 | Sweep the remaining query surface with representative low limits | Pending existing-coverage validation |
| 4 | DOGFOOD-011 | Exercise error-reporting workflows with explicit consent | Pending existing-coverage validation and explicit user consent |

### DOGFOOD-008 — Controlled transaction commit

The existing `rename-dbcontext-durable` Scenario Runner scenario already uses a disposable pinned EF Core checkout to perform a supported mutation, preview it, invoke `transaction-commit`, confirm physical file changes and restore the checkout. DOGFOOD-008 was incorrectly classified as missing because the initial recommendation considered only the published dogfood usage log and did not validate the claim against the Scenario Runner before adding the item.

The implementation produced an optional hardening of that existing scenario: it now also requires snapshot promotion, lifecycle state `Ready` and no active transaction after commit. Confirmation concerns that hardening only; the controlled durable-commit workflow was already covered.

### DOGFOOD-009 — Code Action and Fix All workflows

Existing coverage already exercises this workflow extensively. Published-Host acceptance tests discover and stage a real built-in refactoring, inspect its preview, roll back and verify unchanged files. A second published-Host acceptance test discovers an IDE0003 Code Fix, prepares a bounded document Fix All, proves preparation is read-only, stages the prepared action, inspects the transaction preview, rolls back and verifies the original bytes. Dedicated unit tests cover Fix All limits, failure modes, reference lifetime and replay. The Scenario Runner contains separate `list-code-actions`, `prepare-fix-all` and `stage-prepared-fix-all` cases with transaction rollback, plus a durable Code Action scenario that creates and replaces files.

The original repository-coverage gap is therefore invalid. The narrower surviving opportunity is to exercise the already-covered workflow through Codex's configured dogfood client, validating that the published schemas, opaque action references and response projection are understandable and usable by an agent. That is a client-usability dogfood run, not a request for new production code, acceptance coverage or Scenario Runner definitions. Any such run still requires an approved reversible-workspace design before mutation.

The repository already contains the appropriate deterministic target at `test/TestAssets/Workspaces/InspectionSample/Base/Sample.csproj`. Opening that checked-in project directly and querying `SimplifyThisOrMe.cs` returned exactly one IDE0003 action, `Remove 'this' qualification`, advertising document, project and solution Fix All scopes. The live validation can therefore run against the existing repository asset without copying a fixture, changing the main solution or introducing a new scenario.

Live validation completed successfully against both the checked-in InspectionSample and the main solution. On InspectionSample, direct staging and prepared document Fix All each produced the expected one-line IDE0003 preview, never changed the file hash and rolled back to the original committed snapshot. On the main solution, Codex selected and staged the single `Use expression body for method` refactoring on `ScenarioHost.GetSnapshot`, inspected the exact preview and rolled it back without changing the source hash. Both Workspaces ended `Ready` with no active transaction or reload requirement. No commit was attempted.

The live client exposed one product defect: Code Action request and response enums were published as numbers, unlike the Host's intended agent-facing string-enum contract. The remediation centralises string-enum handling in the MCP schema, request-binding and result-serialization options with numeric fallback disabled, removes enum-level converters so non-MCP internal JSON remains compact, and migrates checked-in MCP clients and examples to the named values. Exact schema tests now lock the Code Action values, runtime tests cover string binding and numeric rejection, and published-host acceptance plus every affected external-repository scenario passes with the string contract.

A fresh published dogfood build then advertised exact string schemas for `list-code-actions.kinds` (`CodeFixes`, `Refactorings`, `All`) and `prepare-fix-all.scope` (`Document`, `Project`, `Solution`). After delayed tool registration completed, Codex's normal callable declarations projected those fields as exact string unions. Calls through the configured dogfood tools accepted `CodeFixes` and `Document`, returned `CodeFix`, `Document`, `Project` and `Solution` as strings, prepared the expected single-document Fix All without staging, staged the expected one-line change and rolled it back with the source hash unchanged. An earlier direct-transport diagnostic using the non-member singular name `CodeFix` was accurately rejected but is not used as Codex dogfood confirmation.

The independent Review Agent found no defect in the MCP enum boundary. It identified that recovery manifests and advisory instance-status files written by the preceding development build used string enums and are not compatible with the new compact numeric internal representation. This was rejected as an intentional internal-format break: the application is greenfield and unreleased, backward compatibility for internal JSON was explicitly excluded from this remediation, and newly written files remain self-consistent.

### DOGFOOD-010 — Representative query-surface sweep

Exercise the currently unused inspection and analysis tools through the published Host using a small, representative fixture or the repository Workspace. Supply deliberately low response limits wherever the contract supports them so bounding, continuations and effective-limit reporting are visible without adding unnecessary agent context. Group tools by workflow and use returned canonical selectors where possible rather than treating every call as an isolated smoke test.

Confirmation requires a recorded request and intelligible outcome for each query tool that remained unused after DOGFOOD-007. Expected structured failures count as useful evidence when the request intentionally exercises a documented boundary; accidental invalid requests must be corrected and retried.

### DOGFOOD-011 — Error-reporting workflows

Exercise the error-reporting tools only after the user gives explicit consent for the concrete submission. Use a controlled, non-sensitive diagnostic payload, explain where the report will be written or sent, and avoid including repository content, machine-specific paths or other incidental data unless the approved workflow specifically requires it.

Confirmation requires evidence that the published contract is usable, consent was obtained before submission, the report reached its documented destination and no unapproved sensitive content was included. If a safe local-only destination is unavailable, record the limitation rather than sending an external report.

## Sequence and process

Before designing each remaining item, inspect the repository's existing unit, integration and acceptance tests, Scenario Runner definitions and supporting runners. Record the exact existing coverage and the concrete surviving gap. If the requested behaviour is already covered, close or narrow the item rather than inventing duplicate work. Only after this validation may design discovery propose a change or a new dogfood run. DOGFOOD-011 remains last because it may have an external side effect and therefore needs separate, explicit consent at execution time.

For any item that reveals a product change rather than a validation-only workflow, follow the remediation process in [Deep Dive Review](../DeepDiveReview.md): complete design discovery, obtain manual design approval before implementation, implement and validate, obtain user confirmation, stage the confirmed baseline, run the fresh context-free Review Agent, address findings as an unstaged comparison, obtain final confirmation and let the user commit before publishing the next dogfood build.

Every request sent to the published dogfood server must continue to be recorded in the [post-RWMCP3 dogfood usage log](dogfood-improvement-usage.md) until the user explicitly ends the logging requirement.
