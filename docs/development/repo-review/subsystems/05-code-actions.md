# Review Unit 5: Code Actions

Date: 2026-08-13

**Status:** Complete

## Evidence boundary

This review used only the current checked-out source, tests, project and configuration files, the current normative review programme and current Code Action/tool documentation, plus current official Roslyn API documentation where external API semantics required confirmation. It did not use Git history, diffs, changed-file discovery, commits, branches, tags, stashes, reflogs, deleted or renamed artefacts, external backups, historical audits or previous review findings as evidence.

## Scope completed

The review covered provider assembly resolution and MEF composition; C# provider selection, identity and policy; built-in analyzer discovery and activation; diagnostic collection; refactoring and Code Fix discovery; nested action flattening; action identity, replay recipes and reference storage; expiry, pressure and Workspace lifecycle invalidation; list and scope resolution; Fix All diagnostic provision, creation, preparation and replay; operation evaluation; Workspace candidate processing and mutation staging; Code Action execution contexts; Host publication and typed adapters; DI registrations and startup configuration; controlled and real-provider fixtures; and all Code Action unit, integration, audit, Host and relevant acceptance claims.

Complete traces were followed across `Roslyn.Workbench.Mcp.Abstractions`, `Roslyn.Workbench.Mcp.Workspace`, `Roslyn.Workbench.Mcp.CodeActions` and `Roslyn.Workbench.Mcp`, including the Roslyn MEF, analyzer, provider, operation and immutable-solution boundaries. No production code was modified.

## Composition, policy and activation

The Host registers default `CodeActionCompositionOptions`, resolves Roslyn Workspaces and Features assemblies through `MefCodeActionComposition`, and supplies the resulting `HostServices` to every loaded `MSBuildWorkspace`. Composition selects C# `CodeFixProvider` and `CodeRefactoringProvider` exports, derives stable provider IDs from implementation types, rejects duplicate identities and exposes a single immutable status. Additional assemblies are supported as a DI option, but the production startup path currently uses the built-in defaults.

Policy admits configured providers while excluding provider types known to require unsupported UI/options or inappropriate editor context. The compatibility adapter isolates Roslyn API shape differences used to obtain nested actions and Fix All support. Provider selection, exclusions, duplicate identity, assembly-resolution failure, composition failure and disposal have focused unit coverage; component tests prove real built-in MEF composition and controlled-provider participation.

`BuiltInAnalyzerIndex` discovers C# diagnostic analyzers from the composed feature assemblies. `CodeActionAnalyzerActivator` creates supported analyzer instances and records activation failures. `CodeActionDiagnosticService` combines compiler diagnostics, project analyzer references and activated built-in analyzers, filters requested diagnostic IDs and document/span scope, de-duplicates results and observes cancellation. Code Fix discovery then groups diagnostics by source span and calls only providers whose fixable IDs intersect those diagnostics. Current tests cover compiler, analyzer-reference and built-in activation paths, failures and duplicate diagnostics.

## Catalogue, contracts and Host boundary

`BundledCodeActionCatalog` publishes exactly three server-owned tools: `list-code-actions`, `prepare-fix-all` and `stage-code-action`. They use their own typed catalogue and Host adapters rather than the third-party plugin catalogue. The list and preparation adapters acquire query contexts; the staging adapter acquires mutation context, obtains a `WorkspaceMutationCandidate`, calls Unit 2's staging service and removes the reference only after successful staging. Rejections are mapped to the normal MCP result envelope, and cancellation passes through the context, provider, evaluator and staging boundaries.

Startup configuration maps and validates Code Action reference lifetime and cache size. Services are singleton because composition, provider inventory and reference state are process-wide; each invocation receives an immutable Workspace execution context. `CodeActionReferenceLifecycleObserver` invalidates references by complete Workspace snapshot identity on lifecycle/revision events, and the bounded state maintains Workspace, transaction and snapshot indexes for targeted invalidation.

`ListCodeActionsRequest` selects a document and optional UTF-16 range plus inclusion and result bounds. It has no expected snapshot field. `PrepareFixAllRequest` and `StageCodeActionRequest` carry action IDs; staging also carries the expected snapshot. The list contract defect first recorded as `RWMCP2-001` was revalidated through the complete Unit 5 path: the unguarded range is interpreted against the current document, and the resulting reference is then legitimately bound to that current snapshot.

## Discovery, identity and replay

For refactorings, discovery invokes every admitted provider for the resolved document/span. For Code Fixes, it obtains scoped diagnostics, invokes matching providers and preserves each registration's diagnostic identities. Nested actions are recursively flattened into leaf actions with deterministic integer action paths. Each emitted result records provider ID, kind, title, equivalence key, path, diagnostic identities, origin document/path/span, exact Workspace snapshot and expiry in a bounded in-memory replay recipe.

Listing sorts deterministic projected action data, applies the response limit and emits only references that were successfully stored. Capacity failure is explicit rather than silently returning an unusable ID. Whole-document and selected-range paths, empty results, code-fix/refactoring filters, nested actions, truncation and cancellation are covered by focused tests and controlled integration fixtures.

Staging validates the action ID, expiry and expected Workspace snapshot, resolves the recorded document/span and rediscoveries current actions. `CodeActionResolver` requires the complete provider/kind/title/equivalence/path/diagnostic/span recipe to produce exactly one match; zero matches become unavailable and multiple matches become ambiguous. Provider removal, changed/duplicate actions, stale snapshots, expired references and Workspace lifecycle invalidation are rejected. This deliberately avoids retaining a live Roslyn `CodeAction` across calls.

Replay identity is stronger than title-only matching and preserves nested action paths and diagnostic instances. Controlled unit and integration tests cover exact, changed, duplicate, nested and unavailable replay. The current real-provider audit, however, does not traverse the stored-reference resolver despite naming its outcomes and CI job as replay compatibility; it directly evaluates the first Roslyn action object from its own discovery. That coverage defect is `RWMCP2-013`.

## Operation evaluation and mutation staging

`CodeActionEvaluator` asks the Roslyn action for operations against the current immutable solution. It accepts exactly one `ApplyChangesOperation`; no apply operation, multiple apply operations and any additional unsupported operation are rejected. The only auxiliary operation admitted is Roslyn's private wrapping bookkeeping operation identified by its full runtime type name. The evaluator returns an immutable candidate solution and never calls `Workspace.TryApplyChanges`.

The Host's Code Action mutation adapter sends the candidate through `IMutationStagingService`, so Unit 2's candidate processor validates solution identity, source-document-only effects, linked files and added/removed document contexts before a new transaction revision is retained. Tests cover single- and multi-document changes, created source files, no change, unsupported/no/multiple operations, staging failure and successful reference consumption. This review found no direct-write path or Code Action-specific bypass of the transaction system.

The private-type allowance for wrapping is version-sensitive but intentionally narrow and fail-closed. Unsupported Roslyn operations are rejected with guidance rather than partially applied. Current tests construct representative operation mixtures; built-in audit cases inspect direct operation output but, as recorded in `RWMCP2-013`, do not prove production replay/staging compatibility.

## Fix All trace

Preparation first resolves an ordinary diagnostic-backed action reference at its exact snapshot, verifies the provider's Fix All support and maps document, project or solution scope into `FixAllContext`. `CodeActionFixAllDiagnosticProvider` supplies scoped document/project diagnostics using the same diagnostic service. The resulting Fix All action is evaluated, passed through the Workspace candidate processor, counted by changed source document and rejected when it exceeds `EffectiveMaxChanges`. The response returns a bounded affected-document preview and a new action ID.

The prepared replay recipe stores the original action identity plus only `PreparedFixAllScope`. During `stage-code-action`, `PreparedFixAllResolver` rediscoveries the original Code Fix, creates a new scoped Fix All action from the provider and returns it to the ordinary evaluator. The staging path does not retain or compare the processed solution evaluated during preparation and does not store or re-enforce the caller's maximum. A provider whose output changes without a Workspace snapshot change can therefore stage a different or larger operation than the reviewed preparation. This is `RWMCP2-011`.

Document, project and solution Fix All scopes, unavailable providers/scopes, analyzer diagnostics, changed-document counting, preview truncation and successful controlled-provider stage traces are covered. The fixtures are deterministic, and the prepared resolver unit test explicitly expects recreation; no current case makes the provider return different solutions between preparation and stage.

## Representative outcomes

| Trace | Current outcome | Evidence assessment |
| --- | --- | --- |
| Built-in MEF composition | Workspaces/Features assemblies compose C# providers and analyzer inventory; configured failures make the subsystem unavailable. | Unit and real-component coverage cross composition. |
| Controlled additional provider | Additional assembly providers participate with stable identities and plugin-independent service scope. | Controlled integration fixtures cross listing and staging. |
| Refactoring list/replay | Range resolves, leaf actions are flattened, recipe is stored, exact rediscovery is required and one candidate is evaluated. | Exact/changed/duplicate/nested controlled cases covered; stale input range remains `RWMCP2-001`. |
| Diagnostic Code Fix list/replay | Compiler/project/built-in diagnostics are scoped, provider registrations are invoked and diagnostic identities participate in replay. | Unit and component coverage includes real diagnostics and controlled providers. |
| Expired, stale or evicted reference | Expiry, expected-snapshot mismatch, capacity pressure and lifecycle invalidation reject without staging. | Focused state/store/resolver/lifecycle tests cover each boundary. |
| Multi-document and create-file action | One supported apply operation becomes a candidate and Workspace staging validates all source-document effects. | Built-in/controlled staging tests cross the transaction boundary. |
| Unsupported operations | None, multiple apply operations or unsupported auxiliary operations reject atomically. | Evaluator and stager tests cover all three outcomes. |
| Fix All prepare/stage | Preparation validates and previews one provider result; stage recreates another provider result. | Deterministic happy paths pass; `RWMCP2-011` records the missing binding/revalidation. |
| Built-in provider audit | Direct provider discovery and operation evaluation cover 120 current cases. | 119 pass; implement-interface is currently not offered at its fixture span (`RWMCP2-012`). Stored-reference replay is bypassed (`RWMCP2-013`). |

## Candidate findings

| ID | Severity | Confidence | Summary |
| --- | --- | --- | --- |
| `RWMCP2-001` | P1 | High | Revalidated: list ranges have no snapshot precondition, so stale caller coordinates can be rebound to the current snapshot and later staged correctly against the wrong selected code. |
| `RWMCP2-011` | P2 | High | A prepared Fix All reference retains only scope and recreates the provider action at stage time without binding or revalidating the reviewed candidate, affected set or maximum. |
| `RWMCP2-012` | P2 | High | The current built-in compatibility suite fails its implement-interface case, leaving the configured CI gate red under the pinned provider behaviour. |
| `RWMCP2-013` | P2 | High | The built-in audit labels direct operation evaluation as replayable but never exercises the stored action ID, resolver or staging path it claims to validate. |

No additional candidate was recorded for composition resource ownership, operation rejection, action identity, reference pressure or Workspace invalidation because current source and focused tests establish fail-closed or bounded behaviour for the reviewed scenarios.

## Test and executable evidence

| Evidence | Result | Boundary established |
| --- | --- | --- |
| `Roslyn.Workbench.Mcp.CodeActions.Test` | 282/282 passed | Composition, policy, analyzers, diagnostics, discovery, references, replay, Fix All, evaluator, contexts, catalogue and tools. |
| `Roslyn.Workbench.Mcp.CodeActions.IntegrationTest` | 18/18 passed | Real Workspace/MEF and controlled-provider composition, diagnostics, listing and mutation staging. |
| Host Code Action-filtered tests in `Roslyn.Workbench.Mcp.Test` | 45/45 passed | DI/configuration, Host catalogue/adapters, result mapping and mutation staging boundary. |
| `Roslyn.Workbench.Mcp.CodeActions.AuditTest` | 119/120 passed | Direct current built-in provider offer/operation compatibility; implement-interface failed as `NotOffered`. |
| Focused refactoring audit class | 58/59 passed | Reproduced the same implement-interface failure in isolation. |

The pinned .NET 10 SDK was available. WSL test commands used the required `/tmp/artifacts/roslyn-workbench-mcp` artefact routing. The acceptance source was inspected for built-in list/stage, caret/selection, stale/unknown references, expiry and Fix All workflows, but the acceptance suite was not executed because no acceptance artefact was changed and repository policy does not authorise an automatic run for this docs-only review. Its deterministic provider path does not cover `RWMCP2-011`.

The repository-required Roslyn MCP was not available in the active tool set, so solution and call-site navigation used current local source inspection. This is a tooling limitation, not an evidence-boundary expansion. Current official Microsoft Learn documentation was used only where Roslyn API semantics required confirmation.

## Earlier-unit revisits

Unit 1's `RWMCP2-001` was revalidated and its ledger history extended after following action-reference creation and exact replay. No other Unit 1 Workspace identity, lease or cache conclusion was invalidated. Unit 2's staging boundary was rechecked for ordinary and Fix All candidates: the Host still routes successful candidates through Workspace mutation staging, and no Code Action writes source directly. `RWMCP2-011` is a preparation/review identity defect before that boundary, not a newly observed transaction-persistence bypass. The architecture map's Code Action audit claim was narrowed to the boundary the current harness actually executes.

## Unit conclusion

The current Code Action subsystem has a coherent fail-closed architecture: immutable Workspace contexts, deterministic leaf identities, bounded expiring references, exact snapshot replay, restrictive operation evaluation and transaction-only staging. Three independently substantiated Unit 5 candidates remain. Prepared Fix All does not preserve the operation reviewed during preparation; one representative built-in compatibility case currently fails; and the audit's replay claim bypasses production replay. Review unit 5 is complete. Review units 6–8 and repository-wide/final validation remain outside this unit and have not begun.
