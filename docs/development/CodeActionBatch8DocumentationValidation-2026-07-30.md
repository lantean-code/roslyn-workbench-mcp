# Code Action Batch 8 Documentation Validation — 2026-07-30

## Outcome

Batch 8 is complete. [Code Actions](../CodeActions.md) is the release-facing workflow, active release and development guidance describes the final composition and exception-policy architecture, and the completed migration item has been removed from [Future Tasks](FutureTasks.md).

## Published surface alignment

The following sources agree on exactly three Host-owned Code Action tools:

| Tool | Release workflow | Source registration | Acceptance evidence |
| --- | --- | --- | --- |
| `list-code-actions` | Discovers bounded Code Fixes and refactorings and returns concise opaque references. | Registered as a query by `BundledCodeActionToolRegistrar`. | `PublishedToolCatalogueSizeIntegrationTests` asserts the published name and contract size; `CodeActionWorkflowIntegrationTests` covers document, selection and caret discovery. |
| `prepare-fix-all` | Revalidates one Code Fix and reports the bounded impact of an explicit Fix All scope without staging. | Registered as a query by `BundledCodeActionToolRegistrar`. | `PublishedToolCatalogueSizeIntegrationTests` asserts the published name and contract size; `CodeActionWorkflowIntegrationTests` covers preparation and staging. |
| `stage-code-action` | Revalidates and stages one discovered action or prepared Fix All operation into the active transaction. | Registered as a mutation by `BundledCodeActionToolRegistrar`. | `PublishedToolCatalogueSizeIntegrationTests` asserts the published name and contract size; `CodeActionWorkflowIntegrationTests` covers staging, replay failures and rollback. |

[Batch 7 validation](CodeActionBatch7Validation-2026-07-30.md) records the passing published-host evidence and measured `tools/list` and response sizes. The release guide derives request shapes, defaults, bounds, snapshot requirements and response semantics from the registered contracts exercised by that evidence.

## Documentation audit

Active release documentation now links to the final workflow and uses provider composition, exception policy, opaque references and unified staging terminology. Active development documentation describes the same architecture and test responsibilities. Historical design plans and dated audit evidence remain only where they preserve architectural rationale or implementation evidence; their status text identifies them as historical, superseded or completed rather than live product guidance.

Repository searches covered Markdown and tool-registration metadata for the removed dedicated tool names, provider allow-list and positive-catalogue terminology, parameterised execution modes, token-based Code Action references and separate staging routes. Remaining matches are confined to completed migration records, retained Roslyn source analysis, explicitly historical audit material, negative assertions proving removed names are absent, and scenario identifiers that do not represent published tools.

The detailed Roslyn provider analysis remains linked from the completed [architecture plan](CodeActionArchitecturePlan-2026-07-27.md), as required.

## Validation

- Every Batch 0 through Batch 7 completion item was checked before the Future Tasks migration item was removed.
- Local Markdown links in the changed documentation resolve.
- `git diff --check` reports no whitespace errors.
- All changed Markdown files use CRLF line endings.
- No build or test run was required because Batch 8 changed documentation only; the behaviour and published-host results are the completed [Batch 7 evidence](CodeActionBatch7Validation-2026-07-30.md).
