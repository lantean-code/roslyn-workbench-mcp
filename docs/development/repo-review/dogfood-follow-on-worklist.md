# Follow-on dogfood validation worklist

**Status:** Approved for design discovery on 2026-08-27.

**Evidence:** [Post-RWMCP3 dogfood usage log](dogfood-improvement-usage.md)

## Purpose

The original [dogfood improvement worklist](dogfood-analysis.md#approved-improvement-worklist) is complete. Analysis of the continuing usage log found no further confirmed product defect, but it identified important workflows and tools that have not yet received published-Host dogfood coverage. This worklist records those follow-on validation items without reopening the completed DOGFOOD-001 through DOGFOOD-007 improvements.

## Worklist

| Order | Identifier | Validation item | Status |
|---:|---|---|---|
| 1 | DOGFOOD-008 | Commit a controlled transaction in a disposable Workspace | Ready for design discovery |
| 2 | DOGFOOD-009 | Exercise the Code Action and Fix All workflows | Pending DOGFOOD-008 |
| 3 | DOGFOOD-010 | Sweep the remaining query surface with representative low limits | Pending DOGFOOD-009 |
| 4 | DOGFOOD-011 | Exercise error-reporting workflows with explicit consent | Pending DOGFOOD-010 and explicit user consent |

### DOGFOOD-008 — Controlled transaction commit

Use a disposable Workspace to perform a supported mutation and invoke `transaction-commit`. Validate the preview before committing, confirm the expected physical file change and final Workspace snapshot after the commit, and then dispose of the fixture. The workflow must not target the working repository or any other non-disposable source tree.

Confirmation requires published-Host evidence for transaction start, mutation staging, preview, commit, the resulting snapshot and filesystem state, plus fixture cleanup.

### DOGFOOD-009 — Code Action and Fix All workflows

Use a disposable Workspace containing a deterministic diagnostic and Code Action. Exercise `list-code-actions`, stage the selected action, inspect its preview, roll back and confirm that both Workspace and filesystem state remain at the baseline. Then exercise `prepare-fix-all` where the fixture exposes a suitable supported scope and validate its bounded preview without committing it unless a separately approved design calls for that.

Confirmation requires published-Host evidence for Code Action discovery, staging, preview and rollback, followed by a suitable Fix All preparation and preview. The evidence must distinguish transaction rollback from fixture disposal.

### DOGFOOD-010 — Representative query-surface sweep

Exercise the currently unused inspection and analysis tools through the published Host using a small, representative fixture or the repository Workspace. Supply deliberately low response limits wherever the contract supports them so bounding, continuations and effective-limit reporting are visible without adding unnecessary agent context. Group tools by workflow and use returned canonical selectors where possible rather than treating every call as an isolated smoke test.

Confirmation requires a recorded request and intelligible outcome for each query tool that remained unused after DOGFOOD-007. Expected structured failures count as useful evidence when the request intentionally exercises a documented boundary; accidental invalid requests must be corrected and retried.

### DOGFOOD-011 — Error-reporting workflows

Exercise the error-reporting tools only after the user gives explicit consent for the concrete submission. Use a controlled, non-sensitive diagnostic payload, explain where the report will be written or sent, and avoid including repository content, machine-specific paths or other incidental data unless the approved workflow specifically requires it.

Confirmation requires evidence that the published contract is usable, consent was obtained before submission, the report reached its documented destination and no unapproved sensitive content was included. If a safe local-only destination is unavailable, record the limitation rather than sending an external report.

## Sequence and process

The items are ordered to broaden mutation coverage safely before expanding general query coverage. DOGFOOD-011 remains last because it may have an external side effect and therefore needs separate, explicit consent at execution time.

For any item that reveals a product change rather than a validation-only workflow, follow the remediation process in [Deep Dive Review](../DeepDiveReview.md): complete design discovery, obtain manual design approval before implementation, implement and validate, obtain user confirmation, stage the confirmed baseline, run the fresh context-free Review Agent, address findings as an unstaged comparison, obtain final confirmation and let the user commit before publishing the next dogfood build.

Every request sent to the published dogfood server must continue to be recorded in the [post-RWMCP3 dogfood usage log](dogfood-improvement-usage.md) until the user explicitly ends the logging requirement.
