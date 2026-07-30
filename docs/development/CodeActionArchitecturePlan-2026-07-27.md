# Code Action Architecture Plan — 2026-07-27

Status: Approved implementation authority

## Purpose

This plan replaces the provider allow-list, dedicated-tool and provider-level classification model currently used by `Roslyn.Workbench.Mcp.CodeActions`. It defines the target MCP surface, runtime composition and exclusion policy, document and selection discovery, built-in diagnostic activation, opaque replay references, unified staging, Fix All preparation, migration order, validation requirements and documentation cleanup.

The plan preserves the parts of the current implementation that have proved sound: Roslyn MEF composition, action discovery, replay recipes, snapshot-bound references, operation evaluation and Workspace transaction staging. It replaces the positive supported-provider ledger and the large callable tool surface with generic discovery and staging over ordinary Roslyn actions.

## Problem Statement

The current design treats each audited Roslyn provider family as a separately published MCP capability. This has produced dedicated request types, handlers, registrations and tests for ordinary actions that Roslyn already discovers and constructs. It also hides unlisted providers by default, which prevented a large set of ordinary Code Fixes from being available and made built-in `IDE` diagnostic activation appear to be a provider implementation problem.

The runtime currently composes 250 C# providers from the pinned Roslyn 5.6 assemblies: 169 Code Fix providers and 81 refactoring providers. Source inspection shows that 232 are ordinary replay providers, while the remainder can be represented by a small set of exclusions or mixed-provider leaf rules. The current positive ledger therefore carries substantially more data and callable surface than the safety policy requires.

The agent-facing result also exposes provider and replay implementation details that an agent does not need to choose or invoke an action. The redesign must reduce both the published tool list and the response context while retaining precise diagnostic, location and transaction semantics.

## Roslyn Source Analysis Authority

The source analysis uses the exact Roslyn commit [`c0573ed0a7dc3e3b4d2e70da47f97cc51a35524f`](https://github.com/dotnet/roslyn/tree/c0573ed0a7dc3e3b4d2e70da47f97cc51a35524f), identified by the SourceLink and package metadata for `Microsoft.CodeAnalysis.Features` and `Microsoft.CodeAnalysis.CSharp.Features` 5.6.0. The repository-local ignored reference checkout is `ref/roslyn` at that detached commit; it is analysis input and is not a build or release dependency.

The analysis covered C# and language-neutral providers exported for C# under Roslyn Features and EditorFeatures, the Language Server Code Action path and the Workspaces Code Action abstractions. It was reconciled with the providers produced by the Host's real MEF composition rather than relying only on source-folder enumeration.

The detailed discoveries, exceptional families and exhaustive provider-by-provider classification are retained in [Roslyn Code Action Source Analysis — 2026-07-27](RoslynCodeActionSourceAnalysis-2026-07-27.md). This plan consumes that evidence but does not replace it.

The composed runtime inventory contains:

| Classification | Provider count |
| --- | ---: |
| Ordinary replay | 232 |
| Mixed provider | 5 |
| Option-backed only | 6 |
| Internal-service dependent | 1 |
| Product-boundary exclusion | 6 |
| Workbench-owned custom semantic implementation | 0 |
| **Total** | **250** |

The 250 composed providers comprise 169 Code Fix providers and 81 refactoring providers. Rename Tracking is a separate source-only EditorFeatures provider that is not present in the Features assemblies composed by Workbench; it depends on editor text-buffer and undo state and remains outside the runtime total.

Provider is the stable inventory unit because concrete action titles, nesting and leaf counts depend on the supplied document, span, diagnostics and semantic state. Mixed-provider conclusions were therefore validated against the provider source and must be completed with exact action-type compatibility cases during implementation. The initial exclusions and mixed-provider rules in this plan are the retained architectural outputs of that analysis. The exact provider composition snapshot and compatibility cases remain the executable upgrade evidence after the superseded working audits are deleted.

## Goals

- Publish a small, stable set of Code Action orchestration tools.
- Discover ordinary Roslyn Code Fixes and refactorings without a positive supported-provider allow-list.
- Support compiler diagnostics, project-supplied analysers and selectively activated Roslyn built-in analysers.
- Allow document, selection and caret discovery with precise action locations.
- Return only the context an agent needs to choose and invoke an action.
- Exclude known unsupported providers and leaves through a small production policy.
- Revalidate every opaque reference and rediscover exactly one action before staging.
- Use one CodeAction-specific mutation stager for single actions and prepared Fix All actions.
- Preserve the existing source-only Workspace transaction boundary.
- Keep Workbench-owned semantic transformations in bundled or third-party mutation plugins rather than presenting them as Roslyn Code Actions.
- Retain exhaustive provider inventory and compatibility evidence in tests without using either as runtime visibility policy.
- Remove obsolete development documentation and publish one current release-facing Code Action workflow.

## Non-Goals

- Executing arbitrary Roslyn `CodeActionWithOptions` instances.
- Reimplementing dialog-backed Roslyn features as part of the generic Code Action pipeline.
- Adding project, package, reference, analyser-configuration or other non-source mutation support.
- Discovering every possible caret refactoring by probing every syntax position in a document.
- Allowing runtime configuration to disable product-boundary safety rules.
- Exposing provider identities, equivalence keys, action paths or internal policy decisions to agents.
- Supporting third-party Code Action provider packages through the public plugin authoring contract.

## Agreed Architectural Decisions

1. The callable Code Action surface consists of `list-code-actions`, `prepare-fix-all` and `stage-code-action`.
2. `list-code-actions` accepts a required document and an optional range. An omitted range means document discovery, a non-empty range means selection discovery and an empty range means caret discovery.
3. The request requires an explicit action-kind choice: Code Fixes, refactorings or both.
4. The response contains only the action reference, display title, action kind, precise location, associated diagnostic context and supported Fix All scopes.
5. Roslyn provider identity and replay metadata remain server-side.
6. Runtime support is allow-by-default for ordinary composed action leaves. A production exclusion policy records only unsupported providers and leaves.
7. Provider composition, runtime exclusion policy and development inventory are separate concepts.
8. `ICodeActionProviderCatalog` becomes `ICodeActionComposition`; `MefCodeActionProviderCatalog` becomes `MefCodeActionComposition`.
9. Provider-level exclusions are evaluated for initial discovery; the resulting process-local references cannot outlive the static provider composition and policy that approved them.
10. Leaf-level exclusions are evaluated after nested actions are flattened during initial discovery; staging requires an exact rediscovery identity match and relies on operation and Workspace validation for the recreated action.
11. A stored reference contains a replay recipe, never a Roslyn `CodeAction` object.
12. One `CodeActionStager` handles ordinary Code Fixes, refactorings and prepared Fix All references.
13. Fix All has separate read-only preparation because the caller must be able to understand scope and impact before staging.
14. Workbench-owned transformations such as rename and formatting remain Plugins.Core mutation tools. Future custom semantic implementations belong there as well.
15. Replay-reference lifetime follows reachable Workspace transaction history: undo and redo do not evict references for revisions that remain reachable, while irreversible lifecycle transitions actively evict references that can never become current again.

## Target Runtime Flow

```text
MEF Code Action composition
    -> provider exclusion partition
    -> request-scoped provider and diagnostic selection
    -> compiler, project and built-in diagnostic collection
    -> Code Fix and refactoring discovery
    -> nested action flattening
    -> leaf exclusion policy
    -> concise projection and opaque replay reference

opaque action reference
    -> snapshot and reference validation
    -> exact action rediscovery
    -> Roslyn operation evaluation
    -> Workspace mutation candidate validation
    -> transaction staging
```

## Callable Tool Contracts

### `list-code-actions`

`list-code-actions` is a query tool and the sole entry point for discovering ordinary Roslyn Code Fixes and refactorings.

The request shape is:

| Property | Requirement | Meaning |
| --- | --- | --- |
| `Document` | Required | Project-aware `DocumentSelector` identifying the source document. |
| `Range` | Optional | Selection or caret range within the document. Omission selects document mode. |
| `Kinds` | Required | `CodeFixes`, `Refactorings` or `All`; no inferred default. |
| `DiagnosticIds` | Optional | Narrows Code Fix discovery and analyser activation to requested diagnostic IDs. |
| `Limit` | Optional with published curated default | Bounds returned action leaves. |

Discovery always runs against the current solution held by the query execution lease. Each returned opaque reference records that actual immutable Workspace snapshot identity for later staging revalidation; the query does not accept a snapshot precondition.

The response is a bounded collection. Each action contains only:

| Property | When returned | Meaning |
| --- | --- | --- |
| `ActionId` | Always | Opaque temporary replay reference. |
| `Title` | Always | Roslyn display title explaining what the action will do. |
| `Kind` | Always | Code Fix or refactoring. |
| `Location` | Always | Precise resolved location to which the action applies. This is required even when the request targeted a complete document. |
| `Diagnostics` | Code Fixes only | A `BoundedCollection` of concise `{ Id, Message }` values explaining why the action is offered without requiring a diagnostic lookup. |
| `FixAllScopes` | Only when supported | Scopes advertised by the originating `FixAllProvider`. |

The `Actions` property uses `BoundedCollection<CodeActionListItem>` so it reports the returned items, `HasMore` and optional `TotalCount` when the complete count is already known cheaply. Each action's nested `Diagnostics` collection uses the same contract with a fixed default maximum of 10 contexts per action, preventing an action list from multiplying an unbounded inner collection. `FixAllScopes` remains an ordinary list because Roslyn exposes a closed set of only document, project and solution scopes. The response does not return provider identity, CLR action type, equivalence key, action path, Workspace identity, expiry, execution mode, executor tool, internal requirements or exclusion rationale.

### Document and range semantics

Document mode collects diagnostics across the selected source document and returns every eligible Code Fix leaf up to the request bound. Each result records the precise diagnostic location and its own replay recipe.

Document mode invokes only refactoring discovery that legitimately applies to the document selection. It does not scan every syntax node or caret position. The implementation must document and test the exact span supplied to providers for document selection so callers are not led to believe that every possible local refactoring has been enumerated.

Selection mode collects diagnostics intersecting the selected range and invokes refactoring providers with that exact span. A zero-length range represents a caret. Diagnostic intersection semantics must be shared by initial discovery and staging rediscovery.

### `prepare-fix-all`

`prepare-fix-all` is a read-only query tool derived from one discovered Code Fix reference.

The request contains:

| Property | Requirement | Meaning |
| --- | --- | --- |
| `ActionId` | Required | Opaque reference returned by `list-code-actions`. |
| `Scope` | Required | One explicit supported document, project or solution scope. |
| `MaxChanges` | Optional with published curated default | Maximum source documents the prepared operation may change. |
| `AffectedDocumentsLimit` | Optional with published curated default | Bounds the affected-document list returned to the agent. |
| `ExpectedSnapshot` | Required | Revalidates the Workspace and transaction revision. |

Preparation revalidates the originating Code Fix, verifies its `FixAllProvider` and requested scope, constructs the Fix All action, evaluates its candidate solution without staging it, applies the same linked-document merge and source-only candidate validation used by Workspace staging, and records a replayable Fix All recipe.

The response contains:

| Property | Meaning |
| --- | --- |
| `ActionId` | New opaque reference representing the prepared Fix All operation. |
| `Scope` | The accepted Fix All scope. |
| `AffectedDiagnosticCount` | Complete number of diagnostics addressed when cheaply and authoritatively available. |
| `AffectedDocuments` | `BoundedCollection<DocumentReference>` containing project-aware document identities, truncation state and the complete changed-document count. |

The response does not contain source diffs. The agent stages the prepared action and uses the existing transaction preview when it needs the actual changes.

### `stage-code-action`

`stage-code-action` is the only callable Code Action mutation tool.

Its request contains only:

| Property | Requirement | Meaning |
| --- | --- | --- |
| `ActionId` | Required | Opaque reference representing a single or prepared Fix All action. |
| `ExpectedSnapshot` | Required | Revalidates the Workspace and transaction revision. |

The stored recipe determines whether the action is a Code Fix, refactoring or prepared Fix All operation. The agent does not repeat the document, range, diagnostics, provider, kind or scope.

The handler returns the existing `MutationData` shape produced by Workspace transaction staging. Successful staging removes the reference from the cache. Failures leave it available for retry unless resolution proves that it is expired, invalid or bound to a superseded snapshot.

## Composition

The current `ICodeActionProviderCatalog` is not a curated catalogue. It owns the result of creating Roslyn MEF host services and reading the composed C# provider exports.

Rename the composition boundary as follows:

| Current | Target |
| --- | --- |
| `ICodeActionProviderCatalog` | `ICodeActionComposition` |
| `MefCodeActionProviderCatalog` | `MefCodeActionComposition` |
| `CodeActionProviderCatalogStatus` | `CodeActionCompositionStatus` |
| `CodeActionProviderCatalogComposition` | `CodeActionCompositionState` |

`ICodeActionComposition` continues to expose composition status, `WorkspaceHostServices`, refactoring providers and Code Fix providers. The existing compatibility adapter remains the sole reflection boundary for Roslyn's non-public provider export enumeration.

Composition reports what was loaded. It does not determine which providers are supported and must not reference the runtime exclusion policy.

## Runtime Exclusion Policy

Add a dedicated `Policy` folder owned by the CodeActions project:

```text
Policy/
    ICodeActionPolicy.cs
    CodeActionPolicy.cs
    CodeActionPolicyDecision.cs
    CodeActionExclusions.cs
```

The policy is production code tied to the pinned Roslyn version. It is not user configuration.

The policy exposes two logical evaluations:

```csharp
CodeActionPolicyDecision EvaluateProvider(string providerId);

CodeActionPolicyDecision EvaluateAction(
    string providerId,
    CodeAction action);
```

Provider decisions are used to partition the composed provider sets once at startup and are applied during initial discovery. Replay references are process-local and therefore cannot outlive that static composition or policy. Leaf decisions run after nested actions are flattened during initial discovery; replay bypasses policy filtering and instead requires exactly one leaf matching the complete stored identity before operation and Workspace validation.

`CodeActionExclusions` contains only exact provider or internal action-type identities that require special treatment. Internal reason codes support tests, diagnostics and logs but are not projected into ordinary list results.

### Initial provider exclusions

The initial policy must encode and test these known provider-level exclusions from the Roslyn 5.6 source audit:

| Provider family | Reason |
| --- | --- |
| `AddMissingImports` refactoring | Requires editor paste-tracking state; the explicit import workflow remains a separate product decision. |
| `ChangeSignature` | Option-backed internal signature editor. |
| `GenerateOverrides` | Option-backed member picker. |
| `ExtractInterface` | Option-backed extraction and member selection. |
| `ExtractClass` | Option-backed extraction and member selection. |
| `MoveStaticMembers` | Option-backed destination and member selection. |
| `MoveToNamespace` | Option-backed namespace selection. |
| `AddMissingReference` | Project-reference mutation outside source transactions. |
| `AddPackage` | Package/project mutation and possible network access. |
| `UpdateProjectToAllowUnsafe` | Project compilation-setting mutation. |
| `UpgradeProject` | Project language-version mutation. |
| Copilot suggestion providers | External intelligence and editor-option dependency. |

### Mixed providers

Mixed providers remain eligible. Their unsupported leaves are removed after discovery:

| Provider family | Eligible branch | Excluded branch |
| --- | --- | --- |
| `AddImport` Code Fix | Source-document import action. | Project, assembly or package-reference effects. |
| `GenerateType` | Ordinary deterministic placement leaves. | `CodeActionWithOptions` configuration leaf. |
| `GenerateConstructorFromMembers` | Selected-member and deterministic base-constructor leaves. | Caret-only member-picker leaf. |
| `GenerateEqualsAndGetHashCodeFromMembers` | Selected-member generation leaf. | Caret-only member-picker leaf. |
| `PullMemberUp` | Any ordinary leaf proved safe through compatibility evidence. | Option-backed member and destination workflow. |

The implementation batch must confirm the exact internal action-type identities against the pinned source before enabling each mixed provider. Unknown titles or title text must never be used as policy keys.

### Generic safety checks

The exclusion list does not duplicate structural safety:

- `CodeActionWithOptions` leaves are rejected structurally.
- Unknown or unsupported `CodeActionOperation` types are rejected by operation evaluation.
- More than one `ApplyChangesOperation` is rejected.
- Project, reference, option, analyser, additional-document, analyser-config and unsafe file changes are rejected by `WorkspaceMutationCandidateValidator`.
- The known Roslyn wrapping bookkeeping operation remains a narrowly documented operation exception.

## Diagnostic Discovery

Code Fix discovery joins eligible providers with diagnostics from three sources:

1. compiler diagnostics;
2. project-supplied analyser diagnostics; and
3. Roslyn built-in analyser diagnostics from the pinned Features assemblies.

The effective diagnostic set is derived only from eligible providers and any request `DiagnosticIds` filter. Excluded providers must not cause analyser activation.

### Built-in analyser index

Build an immutable diagnostic-to-analyser index for the pinned Roslyn runtime. Prefer runtime metadata over a hand-maintained provider mapping:

1. Enumerate candidate C# `DiagnosticAnalyzer` types from the same trusted built-in Roslyn assemblies used for Code Action composition.
2. Keep non-public construction and assembly inspection inside the existing analyser compatibility boundary.
3. Read each successfully activated analyser's `SupportedDiagnostics`.
4. Index analyser instances by diagnostic ID.
5. Record unavailable or incompatible analysers as component diagnostics without failing Host startup.
6. Cache the immutable index for the process lifetime.

If runtime metadata cannot produce a reliable mapping for a provider diagnostic, add the smallest explicit compatibility mapping required and lock it with an exact-version integration test. Do not restore a positive Code Action provider ledger merely to carry analyser type names.

### Bounded execution

For a selection request, analyse only the owning document and filter diagnostics to the selected span. For document mode, analyse the selected document. Do not run solution-wide built-in analyser discovery from `list-code-actions`.

Use the project's analyser options so `.editorconfig` severity and Code Style settings are respected. De-duplicate diagnostics obtained from more than one source by a stable diagnostic identity including project, document, ID, source span and relevant diagnostic properties.

Cancellation must flow through diagnostic collection and provider invocation. Diagnostic failures from an individual optional built-in analyser should produce bounded component warnings and should not convert unrelated compiler-backed actions into a failed query.

## Discovery and Projection

`CodeActionDiscoveryService` remains the owner of provider invocation and nested action flattening. It changes from descriptor-registry visibility to policy eligibility.

The service must:

- consume eligible providers from composition plus policy partitioning;
- invoke each provider at most once for each required document/span/diagnostic group;
- flatten nested actions deterministically;
- preserve the exact registered diagnostics for every Code Fix leaf;
- record precise target spans for document-mode results;
- evaluate every leaf through `ICodeActionPolicy`;
- return only eligible internal `DiscoveredCodeAction` values; and
- avoid LINQ-heavy or allocation-heavy transformations in provider and diagnostic hot paths.

Projection creates the concise result and replay recipe together. If the result bound has been reached, discovery should stop where Roslyn's provider APIs permit rather than constructing references that cannot be returned. Work that Roslyn necessarily performs as a complete provider or analyser operation is acknowledged separately from avoidable post-processing.

## Replay References

Continue using bounded `IMemoryCache` storage with configurable absolute expiry. Store recipes rather than Roslyn action objects.

Introduce an internal reference kind:

```text
Single
PreparedFixAll
```

A single-action recipe records:

- provider identity;
- Code Fix or refactoring kind;
- immutable Workspace snapshot identity, including Workspace ID and epoch, stable snapshot ID and optional transaction ID;
- project and document identity;
- precise target span;
- associated diagnostic identities, including diagnostic locations;
- equivalence key;
- nested action path;
- title as a secondary consistency check; and
- expiry.

A prepared Fix All recipe additionally records:

- originating single-action identity;
- requested Fix All scope;
- diagnostic IDs;
- equivalence key;
- approved maximum changed-document count; and
- enough scope identity to recreate the same operation.

Reference resolution must not use title as the primary selector. Rediscovery must match exactly one leaf by provider, kind, location, diagnostics, equivalence key and action path, with title retained only as a consistency check. Zero matches reject as expired or unavailable; multiple matches reject as ambiguous. Both instruct the caller to list again.

References are bound to an immutable snapshot identity, not merely to a reusable transaction revision number. That identity must distinguish the committed Workspace snapshot, the transaction instance and the stable identity of each revision within that transaction. Use strongly typed internal identifiers backed by process-local monotonically increasing `long` values; global `Guid` identity is unnecessary because replay references cannot outlive the process. Allocate identifiers through a shared thread-safe allocator and reserve the default value as invalid. Do not derive replay identity from Roslyn `Solution.Version`, because replay lifecycle identity is owned by Workbench.

The displayed transaction revision remains positional `int` transaction information used by `TransactionInfo` and `SnapshotPrecondition`; it is not replaced by, or exposed as, the internal identity. A positional revision is insufficient as a replay identity because staging after undo can discard a redo branch and reuse its revision number.

Undo and redo make different retained revisions current, so references for every reachable revision remain cached and become usable only while their exact snapshot identity is current. A successful stage removes the consumed reference immediately but does not evict references for earlier reachable revisions. Staging after undo actively evicts references for the discarded redo branch before any positional revision numbers can be reused.

Transaction start, commit and rollback actively evict references that cannot participate in the resulting lifecycle state. Workspace unload, reload, replacement and epoch change actively evict every reference owned by the superseded Workspace instance. Absolute expiry and bounded cache capacity remain fallback protections rather than the primary mechanism for reclaiming references known to be permanently stale.

## Unified Staging

Retain one thin callable handler:

```text
StageCodeActionTool
    -> CodeActionStager
        -> CodeActionReferenceResolver
            -> SingleCodeActionResolver
            -> PreparedFixAllResolver
        -> CodeActionPolicy
        -> CodeActionEvaluator
        -> Workspace mutation staging
```

`CodeActionStager` performs orchestration only:

1. Verify Code Action composition availability.
2. Validate the request snapshot.
3. Resolve and validate the opaque reference.
4. Dispatch to the resolver for its internal reference kind.
5. Evaluate Roslyn operations into a candidate solution.
6. Return a `WorkspaceMutationCandidate` to the existing mutation execution lease.

The Workspace layer remains responsible for candidate validation, linked-document merging, transaction revision creation, preview data and commit/rollback behaviour.

The single-action resolver reuses the current `CodeActionResolver` behaviour with stronger diagnostic identity and no caller-supplied expected kind. The prepared Fix All resolver reuses the current `FixAllActionFactory` but returns a Roslyn `CodeAction` to the common evaluator rather than staging through a separate service.

## Plugins.Core Boundary

The generic Code Action pipeline executes actions constructed by Roslyn providers. It does not own custom semantic transformations.

Current and future Workbench-owned operations remain bundled mutation plugins:

- `rename-symbol`;
- `format-document`; and
- any future implementation of change signature, move to namespace, generate overrides, extraction or another workflow rebuilt from public Roslyn primitives.

Existing dedicated Code Action tools require a migration classification:

| Classification | Disposition |
| --- | --- |
| Ordinary replay wrapper | Remove the callable tool; discovery returns the Roslyn leaf. |
| Selector over several ordinary Roslyn leaves | Remove the callable tool; each leaf receives its own reference. |
| Diagnostic-driven location wrapper | Remove the callable tool; list and stage use the exact diagnostic leaf. |
| Scoped aggregation already represented by `FixAllProvider` | Replace with `prepare-fix-all`. |
| Useful aggregation not represented by Roslyn Fix All | Assess separately as a Plugins.Core mutation; do not keep it implicitly inside CodeActions. |
| Genuine Workbench-owned transformation | Move to Plugins.Core with an explicit contract and handler. |

## Component Disposition

| Current component | Target disposition |
| --- | --- |
| `MefCodeActionProviderCatalog` and interface | Rename to the composition terminology and retain behaviour. |
| `CodeActionDescriptorRegistry` | Remove after policy migration. |
| `BuiltInCodeActionLedger` | Remove. |
| `BuiltInCodeActionFamily` and execution-mode contracts | Remove when no remaining consumer requires them. |
| `BundledCodeActionCatalog` | Replace with the three-tool orchestration registration. |
| `BundledCodeActionToolRegistrar` | Reduce to the three callable handlers. |
| `CodeActionDiscoveryService` | Retain and adapt for policy, built-in diagnostics and document mode. |
| `CodeActionInfoFactory` | Retain responsibility but replace the verbose descriptor projection with the concise response and stronger recipe. |
| `CodeActionReferenceStore` | Retain bounded cache ownership; add explicit successful-consumption, reachable-revision retention and active lifecycle eviction. |
| `CodeActionResolver` | Retain as the basis of single-action resolution. |
| `CodeActionReferenceStager` | Replace with the unified `CodeActionStager`. |
| `CodeActionFixAllStager` | Split preparation from prepared-action resolution; remove as a stager. |
| `CodeActionSelectionStager` | Remove. |
| `LocationCodeFixStager` | Remove. |
| `ScopedCodeFixStager` | Remove after every consumer has been classified. |
| Dedicated Code Fix, refactoring and conversion handlers | Remove or move according to the Plugins.Core boundary classification. |
| `CodeActionEvaluator` | Retain operation materialisation and supported-operation validation. |
| `WorkspaceMutationCandidateValidator` | Retain as the authoritative source-only candidate boundary. |

Removal must be reference-driven. Do not delete a shared stager or request contract until all current dedicated handlers using it have a recorded disposition and replacement test.

## Implementation Batches

Implement and review the batches one at a time in dependency order. Resolve architectural questions and review findings within the active batch before beginning the next batch. Do not overlap batches merely to accelerate the migration; any necessary dependency-safe exception must be recorded in this plan before work begins. If a batch number is requested but the previous one is not complete, do not proceed; call it out.

### Completion checklist rule

Every batch has an exit checklist. A checkbox must remain unchecked while work is merely started, partially implemented or awaiting validation. Check an item only after its complete outcome has been verified in the working tree with the required tests, analysis or documentation evidence. A batch is complete only when every checkbox in its exit checklist is checked; completing implementation steps without completing the checklist does not complete the batch.

### Batch 0 — Establish the new authority and remove conflicting plans

1. Approve this document as the sole Code Action architecture plan.
2. Replace the two obsolete Code Action entries in `FutureTasks.md` with one active migration item pointing here.
3. Retain the detailed source discoveries and provider classification in the linked source-analysis document, with the architectural conclusions summarised here.
4. Delete the superseded Code Action audits and architecture validation documents listed in the documentation section.
5. Update immediate cross-references so no active backlog item points to a deleted document.

Completion checklist:

- [x] This plan has been reviewed and approved as the sole active Code Action architecture authority.
- [x] `FutureTasks.md` contains one active migration item and no superseded Code Action promotion or tool-surface task.
- [x] The linked source-analysis document retains the exact source authority, material discoveries, exclusion rationale and exhaustive provider classification.
- [x] Every superseded document listed for removal has been deleted.
- [x] All active documentation links resolve and no active backlog or design document points to a deleted Code Action audit.
- [x] Changed Markdown complies with repository formatting and CRLF requirements.

This batch changes documentation only and precedes code changes.

### Batch 1 — Separate composition from policy

1. Rename the MEF provider catalogue types to composition terminology.
2. Add the exception-based `ICodeActionPolicy`.
3. Encode provider-level exclusions and generic option-backed rejection.
4. Partition eligible provider lists once after composition.
5. Add exact policy and composition tests.
6. Preserve current visible behaviour temporarily while the new discovery path is built behind focused internal tests.

Completion checklist:

- [x] All provider-catalogue composition types, consumers, tests and documentation use the approved composition terminology.
- [x] `ICodeActionPolicy` supports provider and action-leaf decisions without introducing a positive supported-provider list.
- [x] Every initial provider exclusion and generic option-backed rejection has focused unit coverage.
- [x] Eligible providers are partitioned once after composition and excluded providers are not invoked.
- [x] Composition remains non-throwing for supported external compatibility failures and continues to publish component status.
- [x] Current behaviour intended to remain available during migration has no unexplained regression.
- [x] The affected build, non-acceptance tests, formatting and `latest-all` analyser validation are green.

This batch is the dependency for diagnostic activation and generic discovery.

### Batch 2 — Complete diagnostic sources

1. Build the immutable built-in analyser index.
2. Join eligible Code Fix provider diagnostic IDs with compiler, project and built-in analyser sources.
3. Implement document and selection diagnostic collection with de-duplication.
4. Respect analyser configuration and cancellation.
5. Validate at least one compiler, project-analyser and built-in `IDE` diagnostic through the real pinned composition.
6. Measure analyser activation and document diagnostic collection separately from action discovery.

Completion checklist:

- [x] Compiler, project-supplied and Roslyn built-in diagnostic sources are implemented behind one coherent discovery boundary.
- [x] The built-in diagnostic-to-analyser index is immutable, process-cached and derived from the pinned runtime except for any explicitly justified compatibility mapping.
- [x] Document and selection diagnostic collection use the agreed scope and stable de-duplication identity.
- [x] `.editorconfig` options, severity, cancellation and individual optional-analyser failures have verified behaviour.
- [x] Real-composition tests prove at least one compiler, project-analyser and built-in `IDE` diagnostic reaches its Code Fix provider.
- [x] Activation and diagnostic-collection measurements are recorded separately from provider discovery.
- [x] The affected build, non-acceptance tests, formatting and `latest-all` analyser validation are green.

This batch must complete before the large ordinary Code Fix inventory can become visible.

### Batch 3 — Replace discovery and list contracts

1. Introduce the document-plus-optional-range request.
2. Replace include booleans with the required action-kind value.
3. Add curated result limits and published defaults.
4. Apply provider and leaf policy during discovery.
5. Strengthen diagnostic identity and document-mode target locations.
6. Replace the verbose descriptor response with the concise agent-facing shape.
7. Update reference creation to the new single-action recipe.
8. Add unit, real-composition and published-schema coverage.

Completion checklist:

- [x] The published request uses required `Document`, optional `Range`, required `Kinds`, optional diagnostic IDs and a curated limit, and discovery runs against the current query snapshot.
- [x] Document, selection and caret semantics are implemented and independently tested.
- [x] Provider and leaf policy is applied before references or response items are created.
- [x] Every returned action has an opaque ID, title, kind and precise location; Code Fixes also have concise diagnostic ID and message context.
- [x] The response omits provider identity, CLR type, equivalence key, action path, execution mode, executor tool and other internal replay metadata.
- [x] Collection bounds and metadata use the repository's bounded-collection conventions and publish their curated defaults.
- [x] Replay recipes contain the strengthened diagnostic and location identity required for document discovery.
- [x] Unit, real-composition and published-schema tests pass, with no unexplained loss of currently intended generic discovery behaviour.
- [x] The affected build, formatting and `latest-all` analyser validation are green.

This batch may temporarily coexist with the old dedicated tools, but the new generic list result must not advertise those executor tools.

### Batch 4 — Unify single-action staging

1. Remove caller-supplied expected-kind branching.
2. Adapt `CodeActionResolver` to the stronger recipe and process-local replay invariant.
3. Replace `CodeActionReferenceStager` with `CodeActionStager`.
4. Route both Code Fix and refactoring references through `stage-code-action`.
5. Remove references after successful staging.
6. Validate stale, expired, ambiguous, excluded, unsupported-operation, no-change and successful staging paths.
7. Retain Workspace transaction staging unchanged except for any result mapping required by the unified handler.

Completion checklist:

- [x] `stage-code-action` is the only single-action Code Action mutation entry point.
- [x] One `CodeActionStager` handles both Code Fix and refactoring references without a caller-supplied expected kind.
- [x] Rediscovery requires exactly one matching leaf using the strengthened internal recipe identity.
- [x] Replay references cannot outlive the static process composition and policy that approved them; replay bypasses duplicate policy evaluation.
- [x] Successful staging removes the consumed reference; retryable failures retain it and invalid references are rejected consistently.
- [x] Stale, expired, ambiguous, excluded, unsupported-operation, no-change and successful paths have focused coverage.
- [x] Workspace candidate validation, linked-document handling, transaction revisions, preview and commit/rollback responsibilities remain owned by Workspace.
- [x] The affected build, non-acceptance tests, formatting and `latest-all` analyser validation are green.

This batch creates the final single-action path before Fix All is migrated.

### Batch 4A — Add replay-reference lifecycle and active eviction

1. Define strongly typed internal `WorkspaceSnapshotId` and `WorkspaceTransactionId` values, or equivalently named types with the same separation of concerns, backed by process-local monotonically increasing `long` values. Allocate them through a shared thread-safe allocator, reserve zero/default as invalid and do not use `Guid`, Roslyn `Solution.Version` or the positional transaction revision as replay identity.
2. Add a stable transaction identity to `WorkspaceTransaction`, add a stable snapshot or revision identity to `WorkspaceTransactionRevision`, and carry a committed snapshot identity in the Workspace session state. Assign a new transaction identity when a transaction starts and a new snapshot identity whenever initial load, reload, Workspace replacement, staging or durable commit establishes a new snapshot.
3. Keep `WorkspaceTransaction.CurrentRevision`, `TransactionInfo.Revision` and `SnapshotPrecondition.TransactionRevision` as positional `int` values. Preserve the stable identity of each retained revision when undo or redo changes which positional revision is current.
4. Record the immutable replay snapshot identity in single-action recipes and require an exact match with the current Workspace execution context before rediscovery.
5. Change `WorkspaceTransaction.Append` or its staging caller to report the stable identities of revisions discarded by `Take(CurrentRevision)` so lifecycle eviction does not have to infer which redo branch was removed.
6. Extend the bounded reference store with lifecycle indexes sufficient to evict by Workspace instance, transaction and discarded revision branch without scanning unrelated references.
7. Keep references for all revisions that remain reachable through undo or redo. A reference for a non-current revision must be rejected for staging while that revision is not current and must become usable again if undo or redo restores its exact snapshot identity.
8. When staging after undo truncates the redo branch, actively evict every reference owned by the discarded revision identities before a new branch can reuse their positional revision numbers.
9. On successful transaction commit or rollback, actively evict every reference owned by that transaction. On transaction start, evict references whose previous non-transaction snapshot identity is no longer stageable in the new lifecycle context.
10. On Workspace unload, reload, replacement or epoch change, actively evict every reference owned by the superseded Workspace instance.
11. Keep immediate successful-consumption removal, absolute expiry and bounded cache capacity. Expiry, capacity eviction and explicit lifecycle eviction must also remove lifecycle-index entries so the indexes cannot retain recipes or identifiers after the cache entry is gone.
12. Make cache entry creation, lifecycle registration and invalidation race-safe so an entry cannot survive an invalidation that overlaps its creation, and so eviction callbacks cannot remove a newer registration for a reused structural identity.
13. Publish lifecycle changes through a neutral Workspace-owned notification or invalidation abstraction. The Workspace transaction layer must not depend on Code Action types, while the Code Action reference store remains responsible for translating lifecycle events into reference eviction.
14. Keep lifecycle storage and indexing independent of replay-reference kind, and require Batch 5 to store prepared Fix All references through the same path rather than introducing a second cache-lifetime model.
15. Add focused unit and integration coverage for transaction start, multiple staged revisions, non-current rejection, undo restoration, redo restoration, branch truncation, positional revision-number reuse, commit, rollback, Workspace reload, Workspace unload, expiry, capacity eviction and lifecycle/creation races.
16. Retain the process-local composition invariant and prove active lifecycle eviction does not weaken exact rediscovery, operation evaluation or Workspace mutation validation.
17. Demonstrate that permanently stale cache entries and their lifecycle indexes are removed immediately rather than waiting for access, absolute expiry or capacity pressure, including under repeated discovery and lifecycle churn.

Completion checklist:

- [x] Replay recipes use an immutable snapshot identity that cannot collide when a discarded branch's positional revision number is reused.
- [x] Snapshot and transaction identities are distinct strongly typed, process-local, monotonically allocated `long` values with an invalid default; replay identity does not depend on `Guid`, Roslyn `Solution.Version` or positional revision.
- [x] `WorkspaceTransaction`, `WorkspaceTransactionRevision` and Workspace session state carry the transaction, staged-revision and committed-snapshot identities required by replay.
- [x] Public transaction revision values remain positional `int` values and retain their existing contract semantics.
- [x] Appending after undo reports the stable identities removed from the redo branch.
- [x] References for reachable non-current revisions remain cached, are rejected while non-current and become usable again when undo or redo restores the exact revision.
- [x] Staging after undo actively removes references for the discarded redo branch before replacement revisions are exposed.
- [x] Transaction start, commit and rollback actively remove every reference that can no longer become valid in the resulting lifecycle state.
- [x] Workspace unload, reload, replacement and epoch change actively remove references for the superseded Workspace instance.
- [x] Successful consumption, absolute expiry and bounded capacity remain enforced, and every removal path also clears lifecycle-index state.
- [x] Reference creation and invalidation are race-safe under concurrent discovery and Workspace lifecycle changes.
- [x] Workspace publishes lifecycle invalidation without depending on Code Actions, and Code Actions retains ownership of replay-reference storage and eviction.
- [x] Lifecycle storage and eviction are reference-kind-independent, and Batch 5 explicitly adopts the same path for prepared Fix All references.
- [x] Focused unit and integration tests cover reachable-history retention, irreversible eviction, revision-number reuse, expiry, capacity and concurrency.
- [x] Repeated discovery followed by branch, transaction and Workspace invalidation leaves neither stale cache entries nor growing lifecycle-index state.
- [x] The affected build, non-acceptance tests, formatting and `latest-all` analyser validation are green.

This batch closes replay-cache lifecycle and memory-pressure gaps before Fix All adds another reference kind. Batch 7 retains responsibility for end-to-end workflow and performance evidence.

### Batch 5 — Add Fix All preparation

1. Add `prepare-fix-all` as a read-only query handler.
2. Revalidate the originating Code Fix and requested scope.
3. Construct and evaluate the Fix All action without changing transaction state.
4. Enforce maximum changed-document limits.
5. Return concise impact totals and a bounded affected-document list.
6. Store a prepared Fix All recipe through the Batch 4A reference lifecycle and eviction path.
7. Add `PreparedFixAllResolver` and route its recreated action through `CodeActionStager`.
8. Prove preparation is read-only and staging, preview and rollback use the standard Workspace transaction path.

Completion checklist:

- [x] `prepare-fix-all` is published as a query and accepts only a valid originating Code Fix reference and one supported explicit scope.
- [x] Preparation produces no transaction revision and performs no filesystem mutation.
- [x] Changed-document and affected-document limits are enforced with published curated defaults.
- [x] The response contains only the prepared action ID, accepted scope, authoritative impact totals and bounded affected-document identities.
- [x] Prepared Fix All recipes contain enough identity to recreate the same operation without retaining a Roslyn action or candidate solution.
- [x] Prepared Fix All references use the same immutable snapshot identity, reachable-history retention and active eviction path as single-action references.
- [x] `PreparedFixAllResolver` recreates the action and routes it through the same evaluator and `CodeActionStager` path as a single action.
- [x] Unit and integration coverage prove preparation, staging, preview, rollback, expiry, stale scope and unsupported Fix All behaviour.
- [x] The affected build, non-acceptance tests, formatting and `latest-all` analyser validation are green.

### Batch 6 — Remove the dedicated surface and obsolete internals

1. Classify every dedicated Code Action handler using the Plugins.Core boundary table.
2. Remove ordinary replay, selection and diagnostic wrappers.
3. Move any approved Workbench-owned operation to Plugins.Core.
4. Remove `stage-code-fix`, `stage-fix-all` and `describe-code-action`.
5. Remove the positive ledger, descriptor registry, execution-mode metadata and unused request contracts.
6. Remove obsolete selection, location, scoped and Fix All stagers after their final consumers are gone. Remove their `AnalyzerTypeName` compatibility path, reduce `ICodeActionAnalyzerActivator` to exact `Type` activation for the built-in analyser index and remove the direct activator dependency from `CodeActionDiagnosticService`.
7. Reduce Host registration, reserved-name and status logic to the three orchestration tools and Code Action component status.
8. Verify no ordinary provider requires a dedicated MCP registration.

Completion checklist:

- [x] Every existing dedicated Code Action handler has a recorded ordinary replay, leaf-selector, Fix All, Plugins.Core or explicit exclusion disposition in [CodeActionBatch6Disposition-2026-07-29.md](CodeActionBatch6Disposition-2026-07-29.md).
- [x] Every approved Workbench-owned transformation has moved to Plugins.Core with an appropriate contract, or has an explicit deferred decision. The classification found no new Workbench-owned transformation; the existing `rename-symbol` and `format-document` operations remain in Plugins.Core.
- [x] `describe-code-action`, `stage-code-fix`, `stage-fix-all` and all ordinary dedicated replay tools are absent from Host registration and published metadata.
- [x] The positive ledger, descriptor registry, execution-mode metadata and unused dedicated request contracts have been removed.
- [x] Selection, location, scoped and Fix All stagers and their `AnalyzerTypeName` compatibility path have been removed after their final consumers were migrated; `ICodeActionAnalyzerActivator` exposes only exact `Type` activation and `CodeActionDiagnosticService` no longer depends on it directly.
- [x] Host reserved-name, collision, status and DI composition logic reflects exactly the three orchestration tools.
- [x] Source and test searches find no dead dedicated registrations or ordinary provider requiring a dedicated MCP tool.
- [x] The affected build, non-acceptance tests, formatting and `latest-all` analyser validation are green.

### Batch 7 — Rebuild validation around the architecture

1. Remove the dedicated-tool acceptance matrix, including `DedicatedCodeActionToolIntegrationTests`, `CodeActionAcceptanceCases`, `CodeActionAcceptanceCase`, `CodeActionAcceptanceManifest` and the dedicated Code Action tool-name manifest; do not convert it into one generic MCP call per provider.
2. Retain the exact 250-provider composition snapshot as Roslyn-upgrade detection.
3. Retain provider and diagnostic compatibility in audit and integration tests, including every exceptional exclusion, mixed-provider safe and excluded leaves, diagnostic-specific multi-diagnostic cases, representative ordinary replay and built-in analyser-to-diagnostic mapping.
4. Rebuild acceptance around the published architecture: exactly three tool contracts; document, selection and caret discovery; concise results; compiler, built-in diagnostic and refactoring staging; reference identity and failure cases; Fix All preparation and staging; transaction behaviour; exclusion enforcement; durable create-and-replace commit; and separation from Plugins.
5. Rewrite `DurableMutationIntegrationTests` to discover and stage its Code Action through the orchestration tools while retaining its existing durability evidence.
6. Retain `PublishedToolCatalogueSizeIntegrationTests`, but measure the three-tool surface and the concise orchestration responses.
7. Add a focused scenario-runner Code Action workflow that captures an action reference from discovery, injects it into staging, rediscovers when an action for the new current revision is required and can retain references when testing undo or redo restoration; do not add a general-purpose JSONPath or workflow scripting language.
8. Migrate every affected scenario from direct dedicated-tool invocation while retaining ordinary rollback, durable commit, crash recovery, multi-revision and response-size evidence.
9. Add document Code Fix discovery on small, medium and large repositories, cold and warm built-in analyser activation, `prepare-fix-all` impact measurement and prepared Fix All staging on a realistic project, with discovery, preparation and staging timed separately.
10. Run the complete acceptance suite on WSL and Windows, then run representative affected scenario families and repositories through both platform wrappers because shared acceptance and scenario infrastructure is changing.

Completion checklist:

- [x] The exact 250-provider composition snapshot remains green and detects provider additions, removals and duplicates.
- [x] The dedicated-tool acceptance matrix and its case, manifest and tool-name infrastructure have been removed rather than translated into per-provider generic MCP calls.
- [x] Policy, diagnostic-source, mixed-provider, replay-reference and execution-path audit and integration tests cover the final architecture rather than removed dedicated tools.
- [x] Diagnostic-specific compatibility evidence remains for representative and exceptional multi-diagnostic routes.
- [x] Published-host acceptance covers the complete architecture-focused case list in the Validation Strategy, including durable create-and-replace commit and separation from Plugins.
- [x] Acceptance proves that only the three orchestration tools are published and `PublishedToolCatalogueSizeIntegrationTests` records the resulting `tools/list` and response sizes.
- [x] The complete acceptance suite has passed on both WSL and Windows because the acceptance workflows changed.
- [x] The scenario runner can select a discovered action deterministically, capture its opaque reference, inject it into staging, rediscover for a new current revision and retain references when exercising undo or redo restoration.
- [x] Every affected external-repository scenario has passed through both platform wrappers with representative coverage of each affected repository and scenario family; unaffected scenarios are not run merely for ceremony.
- [x] Performance measurements for small, medium and large document discovery, cold and warm built-in diagnostics, replay staging and Fix All preparation and staging are recorded with any production remediation decision in [CodeActionBatch7Validation-2026-07-30.md](CodeActionBatch7Validation-2026-07-30.md).
- [x] The full normal build, affected non-acceptance tests, formatting and `latest-all` analyser validation are green.

WSL and native Windows evidence is recorded in [CodeActionBatch7Validation-2026-07-30.md](CodeActionBatch7Validation-2026-07-30.md).

### Batch 8 — Final documentation and release alignment

1. Write `docs/CodeActions.md` as the release-facing workflow.
2. Update all release and development documents that refer to positive catalogues, dedicated Code Action tools, parameterised execution, tokens or separate staging tools.
3. Remove any migration-only wording from release documentation.
4. Search all Markdown and tool metadata for obsolete names and concepts.
5. Mark the Future Tasks migration item complete by removing it.
6. Confirm `tools/list`, release docs, acceptance evidence and source registration describe the same three-tool surface.

Completion checklist:

- [ ] `docs/CodeActions.md` describes the final agent-facing discovery, Fix All preparation and staging workflow without migration terminology.
- [ ] Every active release and development document uses composition and exception-policy terminology rather than the positive catalogue model.
- [ ] Repository-wide searches find no active reference to removed tools, execution modes, dedicated ordinary replay tools or token-based Code Action references.
- [ ] All documentation links resolve and retained source-analysis evidence remains linked from this plan.
- [ ] The Code Action migration item has been removed from `FutureTasks.md` only after every earlier batch checklist is complete.
- [ ] `tools/list`, tool metadata, release documentation and acceptance evidence agree on the same three-tool surface and contract shapes.
- [ ] Changed Markdown complies with repository formatting and CRLF requirements.

## Validation Strategy

Batch 7 intentionally changes both the acceptance architecture and shared scenario-runner workflows, so its completion evidence requires the complete acceptance suite and representative affected scenario coverage on both WSL and Windows. This is a migration-specific requirement, not a rule that every production Code Action change must run both suites.

### Unit coverage

- Provider and action exclusion decisions, including every exact initial exclusion.
- Generic `CodeActionWithOptions` rejection.
- Eligible-provider partitioning.
- Compiler, project and built-in diagnostic source selection and de-duplication.
- Document, selection and caret range semantics.
- Nested action flattening and mixed-provider leaf filtering.
- Concise response projection and collection bounds.
- Single and prepared Fix All replay recipe construction, expiry and successful consumption.
- Immutable replay snapshot identity, reachable-revision retention and active lifecycle eviction.
- Branch truncation, positional revision-number reuse and reference-store lifecycle-index cleanup.
- Exact rediscovery, zero-match and ambiguous-match handling.
- Operation shape validation and known wrapping exception.
- Unified staging result mapping.

### Real-composition and integration coverage

- Exact provider snapshot against Roslyn 5.6 composition.
- Built-in analyser indexing and at least one real `IDE` diagnostic-to-Code-Fix route.
- Mixed-provider ordinary and excluded leaves against controlled fixtures.
- MEF composition failure remains isolated to Code Action component availability.
- Workspace host services continue to use the Code Action MEF composition.
- Source-only candidate validation rejects project, reference and non-source effects regardless of policy.

### Acceptance coverage

The current dedicated-tool acceptance matrix is an implementation artefact of the positive catalogue and must be removed. `DedicatedCodeActionToolIntegrationTests`, `CodeActionAcceptanceCases`, `CodeActionAcceptanceCase`, `CodeActionAcceptanceManifest` and the dedicated Code Action tool-name manifest must not be converted into hundreds of generic orchestration calls. Provider-by-provider and diagnostic-by-diagnostic compatibility belongs in the real-composition and integration layer.

The replacement acceptance suite must prove the following published workflows:

- The published Host exposes exactly `list-code-actions`, `prepare-fix-all` and `stage-code-action`, with valid schemas and no dedicated ordinary Code Action tools.
- `list-code-actions` works for document, selection and caret requests.
- Responses remain concise and omit provider types, replay recipes and other internal metadata.
- Code Fix responses include the diagnostic ID and a concise diagnostic message.
- Refactoring responses contain sufficient title and location context for an agent to choose an action.
- A compiler-diagnostic Code Fix, a built-in `IDE` diagnostic Code Fix and a refactoring can each be discovered by the generic list route and staged by opaque reference.
- Two otherwise-identical diagnostics at different locations produce distinct, independently stageable references.
- Unknown, expired and stale references return concise recovery guidance.
- Fix All preparation is read-only and reports impact without changing transaction state.
- A prepared Fix All action stages, previews and rolls back through the standard Workspace transaction path.
- Excluded providers, option-backed actions and other unsupported leaves are omitted from discovery.
- A create-and-replace Code Action is discovered and staged before durable commit, preserving the durability evidence currently exercised through the direct `move-type-to-file` invocation.
- Code Action orchestration remains a Host-owned surface distinct from third-party Plugin registration and execution.

`PublishedToolCatalogueSizeIntegrationTests` remains as the acceptance guard for the reduced surface and concise response size. `DurableMutationIntegrationTests` remains as durability evidence, but its setup must use list and stage rather than a removed dedicated tool.

### Performance and scenario evidence

The existing scenario suite contains direct or obsolete Code Action calls for listing, organising imports, durable move-type commit, move-type crash recovery and multi-revision mutation. Each affected scenario must be migrated to the orchestration workflow while retaining the behaviour it was intended to measure or validate.

The scenario runner needs a focused Code Action workflow facility rather than a general-purpose response-query language. It must:

1. Call `list-code-actions`.
2. Select one returned action deterministically by title, location and optional diagnostic ID.
3. Capture its opaque `ActionId`.
4. Inject that value into `stage-code-action`.
5. Rediscover after staging when the next action must target the new current revision; retain captured references when a scenario intentionally exercises undo or redo restoration of a reachable revision.

Retained scenario evidence must cover selection-refactoring discovery; ordinary staging and rollback; create-and-replace durable commit; crash recovery involving a Code Action; multi-revision interaction; and reference and response sizes. New scenario evidence must cover document Code Fix discovery on small, medium and large repositories; cold and warm built-in analyser activation; `prepare-fix-all` impact measurement; and prepared Fix All staging on a realistic project. Scenarios are workflow and performance evidence, not a provider-by-provider compatibility matrix.

Measure:

- document diagnostic collection on small, medium and large solutions;
- built-in analyser activation and cached reuse;
- Code Fix provider invocation;
- refactoring discovery for selection and document mode;
- policy filtering;
- replay rediscovery and operation materialisation;
- Fix All preparation and staging; and
- response size and approximate agent-context impact.

Policy evaluation is expected to be negligible because provider decisions are pre-partitioned and leaf decisions are type checks and frozen lookups. The main performance risk is document-wide diagnostic and provider discovery. Measurements must keep these phases separate.

## Documentation Cleanup

### Superseded documents to remove

After this plan is approved and its required evidence has been incorporated, remove:

- `docs/development/CodeActionsArchitectureValidation.md`;
- `docs/development/CodeActionsUnitTestInventory.md`;
- `docs/development/RoslynCodeActionsAudit.md`;
- `docs/development/RoslynCodeActionAvailabilityAudit-2026-07-26.md`.

Git history preserves their historical value. Retaining them beside the new architecture would create conflicting instructions.

### Retained development evidence

Retain `docs/development/RoslynCodeActionSourceAnalysis-2026-07-27.md` as the source-analysis record linked by this plan. It owns the exact Roslyn commit, audit scope, material discoveries, exceptional-family rationale and exhaustive 250-provider classification. Update it only when the pinned Roslyn version or source analysis changes; do not turn it into runtime support policy.

### Documents to update

At the relevant migration batch, update:

- `docs/README.md`;
- `docs/ToolDiscovery.md`;
- `docs/Configuration.md`;
- `docs/WorkspacesAndTransactions.md`;
- `docs/PluginAuthoring.md`;
- `docs/development/FutureTasks.md`;
- `docs/development/RoslynMcpToolContracts.md`;
- `docs/development/Tool Test Inventory.md`;
- `docs/development/AcceptanceCoverageAudit-2026-07-23.md`;
- Host architecture validation or test inventory documents that refer to a Code Action catalogue; and
- historical plan/spec references only when they are presented as current authority rather than clearly historical context.

Search terms include `Code Action catalogue`, `BuiltInCodeActionLedger`, `dedicated Code Action`, `describe-code-action`, `stage-code-fix`, `stage-fix-all`, `ExecutionMode`, parameterised Code Action execution and token-based Code Action references.

## Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| An unknown ordinary provider becomes visible after a Roslyn upgrade. | The Host pins Roslyn; the exact composition snapshot fails on provider additions or removals and forces an audit before the upgraded build is released. |
| A mixed provider exposes an unsafe leaf. | Structural option-backed rejection and exact mixed-provider action exclusions prevent reference creation; exact replay identity plus final operation and Workspace validation protect staging. |
| Document discovery becomes slow or noisy. | Required kind, diagnostic filters, curated bounds, document-only analyser scope, phased measurements and no syntax-position probing. |
| Built-in analyser activation depends on internal Roslyn construction. | Isolate it in one compatibility adapter, record typed activation failures and cover the pinned runtime with real integration tests. |
| Dynamic leaf ordering makes replay ambiguous. | Store exact location, diagnostic identity, equivalence key and action path; require one rediscovered match and reject ambiguity. |
| Replay recipes accumulate after their snapshots become permanently unreachable. | Index references by immutable Workspace, transaction and revision identity; retain reachable undo/redo history and actively evict discarded branches and completed or superseded lifecycle scopes. |
| A discarded redo branch reuses a positional revision number and revives an unrelated reference. | Give transactions and revisions stable non-reusable identities and require the complete immutable snapshot identity during resolution. |
| Fix All preparation consumes excessive memory or CPU. | Require one explicit scope, enforce changed-document bounds, return bounded affected documents and store only a replay recipe. |
| Removing dedicated tools loses useful aggregate behaviour. | Classify every handler before deletion and move genuine Workbench-owned aggregation to Plugins.Core only with an explicit contract and evidence. |
| Old documentation continues to direct agents or maintainers to removed paths. | Delete superseded audits, update all active cross-references and run repository-wide terminology searches before completion. |

## Completion Criteria

The migration is complete when:

- the Host publishes only `list-code-actions`, `prepare-fix-all` and `stage-code-action` for Code Actions;
- ordinary composed Roslyn leaves are eligible by default and only the reviewed exclusion policy controls known unsupported cases;
- compiler, project and built-in diagnostics participate in bounded discovery;
- document, selection and caret requests produce concise, precise and independently stageable results;
- one CodeAction-specific stager handles single and prepared Fix All references;
- replay references follow reachable transaction history and are actively evicted when their branch, transaction or Workspace instance becomes permanently unreachable;
- the positive provider ledger, descriptor execution modes, dedicated replay tools and alternative Code Action stagers have been removed;
- Workbench-owned transformations are clearly separated into Plugins.Core;
- provider snapshot, compatibility, unit, integration, acceptance and affected scenario evidence is green;
- performance evidence shows no material regression in the new document and built-in diagnostic paths;
- release documentation describes the final workflow; and
- no active document refers to the superseded catalogue or dedicated-tool architecture as current behaviour.
