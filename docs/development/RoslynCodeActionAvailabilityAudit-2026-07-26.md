# Roslyn Code Action Availability Audit — 2026-07-26

## Purpose

This audit reassesses every built-in Code Action family that is hidden by the production ledger, every C# provider present in the Roslyn 5.6 runtime assemblies but absent from that ledger, and the aspirational mutation families previously described as blocked by Roslyn APIs.

The audit distinguishes between a limitation of the current Workbench discovery model, a Roslyn provider that can already be replayed, a workflow that can be implemented using other public Roslyn APIs, a workflow that would require a substantial custom semantic transformation, and a workflow that should remain outside the product boundary.

This is an availability and planning audit only. It does not promote providers, add tools or change production behaviour.

## Evidence and Scope

The repository references `Microsoft.CodeAnalysis.Features` and `Microsoft.CodeAnalysis.CSharp.Features` 5.6.0. Their NuGet metadata and SourceLink information identify Roslyn commit [`c0573ed0a7dc3e3b4d2e70da47f97cc51a35524f`](https://github.com/dotnet/roslyn/tree/c0573ed0a7dc3e3b4d2e70da47f97cc51a35524f).

The initial audit inspected:

- the original 89 providers in `BuiltInCodeActionLedger`, comprising 73 visible providers and 16 hidden providers;
- all production `ExportCodeFixProvider` and `ExportCodeRefactoringProvider` declarations for C# under Roslyn `Features/Core`, `Features/CSharp`, `EditorFeatures/Core` and `EditorFeatures/CSharp`;
- provider type names present in the exact 5.6.0 Features assemblies used by this repository;
- the Workbench discovery, descriptor and staging path; and
- the stable public Roslyn editing APIs available through Workspaces.

The previous audit covered C#-specific source folders but did not include language-neutral Core providers exported for C#. Ten such runtime providers are present in the 5.6 Features assemblies but absent from the original ledger. Rename tracking is an additional editor-host provider in Roslyn source, but it is not present in the Features assemblies composed by Workbench.

## Runtime Inventory Correction

The P0 implementation subsequently compared the ledger with the providers produced by the server's real MEF composition. That check found 81 C# refactoring providers and 169 C# code-fix providers, 250 providers in total. Refactoring coverage matched the expanded ledger, but 151 additional code-fix providers had never been assessed because the source scan and historical compatibility cases covered only a curated subset.

The ledger now records all 250 composed providers. The audit test compares the exact provider identities and kinds with the ledger, rejects duplicates and rejects entries without a disposition, so a future Roslyn package change cannot silently add or remove a provider.

The 151 newly discovered code-fix providers have now also been classified. Their runtime metadata identifies 50 providers accepting at least one compiler diagnostic and 101 accepting only built-in `IDE` diagnostics. A scan of every `CodeActionWithOptions` subtype in the loaded Features assemblies found no option-backed action associated with these 151 providers. The final dispositions are 47 compiler-backed replay candidates, 94 providers requiring general built-in diagnostic support, eight providers covered by existing tools and two project-system exclusions.

The classifications below therefore cover the complete composed runtime inventory. All newly assessed providers remain hidden until their stated prerequisite is completed.

## Corrected Availability Model

`ImpossibleUnderCurrentRules` is safe as a production hide state, but it is too broad for planning. It currently combines seven materially different outcomes:

| Classification | Meaning |
| --- | --- |
| Replay candidate | The exact Roslyn provider emits ordinary deterministic Code Actions. Workbench should be able to support it after a compatibility fixture proves discovery, selection and staging. |
| Mixed provider | The provider emits both replayable actions and `CodeActionWithOptions` actions. The current provider-level descriptor cannot expose only the safe leaves. |
| Existing coverage | A current dedicated Workbench tool already supplies the useful headless behaviour, so exposing the original provider would add host-state coupling or duplication. |
| Public-API dedicated implementation | The original provider depends on IDE diagnostics or services, but the operation can be implemented with stable public Roslyn semantic and editing APIs. |
| Custom semantic implementation | Roslyn's end-to-end implementation is internal. Workbench could build the operation using public primitives, but would own the transformation, validation and compatibility burden. |
| Product-boundary exclusion | The operation is technically possible but requires project/package mutation, network access or another effect outside the source transaction contract. |
| Editor/external exclusion | The provider depends on transient editor state or an external intelligence service that is deliberately absent from the local deterministic server. |

## Summary

The composed runtime contains 73 visible providers and 177 hidden providers. The hidden providers now divide as follows:

| Outcome | Count | Release interpretation |
| --- | ---: | --- |
| Replay candidate | 53 | Six refactorings and 47 compiler-backed code fixes require compatibility validation rather than a new transformation. |
| Built-in diagnostic support required | 94 | The actions are composed, but general discovery does not currently run and map Roslyn's built-in IDE analysers. |
| Mixed provider | 3 | Safe built-in branches exist, but production needs action-level capability classification or a dedicated explicit-input wrapper. |
| Existing coverage | 9 | Keep the duplicate provider hidden and retain the current tool. |
| Dedicated implementation | 12 | Feasible through public primitives, but Workbench would own part or all of the transformation. |
| Product-boundary or external exclusion | 6 | Keep unavailable unless the source-only transaction boundary changes. |

`RenameTracking` remains an editor-only exclusion outside these counts because it is not present in the Features assemblies composed by Workbench.

## Classification of the 151 Newly Inventoried Code-Fix Providers

### Compiler-backed replay candidates

Forty-seven providers consume compiler diagnostics already available from `Compilation.GetDiagnostics`. They remain hidden as `PendingReplayValidation`; compiler diagnostic availability establishes reachability but does not prove deterministic action selection or staging.

The smaller, local correction group includes anonymous-member naming, obsolete attributes, out-parameter assignment, explicit casts, `inheritdoc`, nullable declarations, constraint and return-type repair, base-member modifiers, iterator repair, required members, asynchronous statements, record keyword placement, interpolated-condition parentheses, conflict-marker resolution, documentation nodes, static/member/type modifier repair, unused local-function removal, default literals, explicit expression-tree arrays, explicit const types and interpolated-verbatim strings.

The more semantic or generative group includes `AddParameter`, ambiguous-type aliasing, conversion and method generation, deconstruction and enum-member generation, constructor generation, variable generation, abstract-class and interface implementation, and async conversion. These should be validated after the local group because they can offer multiple actions or change several declarations.

`OrderModifiers` accepts both compiler diagnostic `CS0267` and built-in diagnostic `IDE0036`; its compiler-backed branch is sufficient to place it in replay validation. `ConvertToRecord` is also compiler-backed but is classified as existing coverage because the `convert-to-record` tool already owns that operation.

### Built-in diagnostic support required

Ninety-four providers consume only `IDE` diagnostics and cannot be reached by general Code Action discovery today. Their providers and transformations are already composed; the missing capability is a production mapping from provider diagnostic IDs to the built-in analyser instances that produce them, together with bounded diagnostic execution and compatibility fixtures.

This group includes code-style and modernisation fixes, formatting and newline placement, simplification and redundant-code removal, collection and object initialisation, null and pattern-matching transformations, naming style, suppression maintenance, primary-constructor and lock modernisation, and configured file-header fixes. These are not dedicated-implementation requirements merely because their diagnostics are currently absent.

The configured `CSharpFileHeaderCodeFixProvider` remains in this group rather than existing coverage: `add-file-banner` copies a sibling banner, while diagnostic `IDE0073` applies the configured required-header policy and may replace or remove a non-compliant header.

### Existing coverage

| Hidden code-fix provider | Existing tool |
| --- | --- |
| `CSharpChangeNamespaceToMatchFolderCodeFixProvider` | `sync-namespace` |
| `ConvertNamespaceCodeFixProvider` | `convert-namespace` |
| `CSharpConvertToRecordCodeFixProvider` | `convert-to-record` |
| `MakeLocalFunctionStaticCodeFixProvider` | `make-local-function-static` |
| `UseExplicitTypeCodeFixProvider` | `use-explicit-type` |
| `UseImplicitTypeCodeFixProvider` | `use-implicit-type` |
| `UseExpressionBodyCodeFixProvider` | `convert-expression-body` |
| `UseExpressionBodyForLambdaCodeFixProvider` | `convert-expression-body` |

### Product-boundary exclusions

| Provider | Reason |
| --- | --- |
| `CSharpUpdateProjectToAllowUnsafeCodeFixProvider` | Changes project compilation settings rather than staged source documents. |
| `CSharpUpgradeProjectCodeFixProvider` | Changes the project language version rather than staged source documents. |

### Option-backed assessment

The loaded Features assemblies contain ten `CodeActionWithOptions` implementations, all belonging to the previously classified change-signature, extraction, member-selection, generate-type, move-static-members and move-to-namespace families. None belongs to the 151 newly inventoried code-fix providers. No additional action-level-classification dependency was found in this set.

## Replay Candidates Omitted from the Ledger

These providers are present in the exact 5.6 runtime assemblies, emit ordinary Code Actions in Roslyn source and do not require Workbench to reproduce their transformations.

| Family | Current Roslyn route | Complexity | Recommendation |
| --- | --- | ---: | --- |
| `AddConstructorParametersFromMembers` | Selected members produce ordinary actions that add required or optional constructor parameters. | Low | Add a controlled fixture and promote if discovery, deterministic selection and staging succeed. |
| `GenerateComparisonOperators` | Registers ordinary generation actions, including nested alternatives. | Low | Validate and promote through replay. |
| `ImplementInterface` | The language-neutral provider obtains the C# implementation service and registers its returned ordinary actions. | Low | Validate and promote. This is the missing-members workflow; the existing C# explicit/implicit providers only change the style of members that already exist. |
| `OrganizeImports` | Registers an ordinary syntax-editing action and supports broader import shapes than the current top-level `sort-usings` implementation. | Low | Validate through replay, then decide whether it replaces, supplements or remains hidden behind `sort-usings` to avoid duplicate public concepts. |
| `ReplaceMethodWithProperty` | Registers ordinary replacement actions. | Low | Validate and promote through replay. |
| `ReplacePropertyWithMethods` | Registers an ordinary replacement action. | Low | Validate and promote through replay. |

These providers should remain hidden until their compatibility fixtures pass. Source inspection establishes feasibility, not compatibility with the exact Workbench MEF composition, selector and transaction pipeline.

## Mixed Providers Incorrectly Classified as Entirely Impossible

The current discovery service asks the descriptor registry whether a provider should be invoked before it computes actions. Production capability is provider-level; action-dependent overrides exist only as test input. Consequently, marking one of these providers visible would also expose its unsupported dialog action.

| Family | Replayable branch | Unsupported branch | Required Workbench change |
| --- | --- | --- | --- |
| `GenerateConstructorFromMembers` | A selected member span produces ordinary field-delegating and, where applicable, constructor-delegating actions. Missing base-constructor cases can also produce ordinary actions. | A caret-only request uses `GenerateConstructorWithDialogCodeAction` and `IPickMembersService`. | Add production action-level classification that hides the dialog leaf, or provide a dedicated tool whose required member selectors drive the selected-member branch. |
| `GenerateEqualsAndGetHashCodeFromMembers` | Explicitly selected members produce an ordinary `GenerateEqualsAndGetHashCodeAction`. | A caret-only request uses a pick-members dialog and additional options. | Use action-level classification for selected-member replay or a dedicated explicit-member tool. |
| `GenerateType` | Roslyn emits ordinary actions for a type in a new file, in the current file or nested in an existing type where applicable. | The provider also emits `GenerateTypeCodeActionWithOption` for the configurable dialog route. | Hide the options leaf through action-level classification and validate the deterministic leaves, or expose a dedicated parameterised tool for explicit type kind, accessibility and target choices. |

This is a current Workbench architecture limitation, not a lack of Roslyn functionality. The prerequisite is a production-safe way to classify a provider's individual action tree without title-only heuristics.

## Existing Coverage

| Family | Assessment | Recommendation |
| --- | --- | --- |
| `AddMissingImports` refactoring | The Roslyn provider requires paste-tracking state because it is designed for an IDE paste operation. Workbench already stages the `AddImport` code fix across an explicit scope through `add-missing-usings`; the public `ImportAdder.AddImportsAsync` API is also available if the dedicated behaviour later needs expansion. | Keep the paste-specific provider hidden. Improve the dedicated tool rather than emulating editor paste history. |

Rename tracking is similar in intent but belongs to the editor-only exclusions below because the actual provider is not part of the composed runtime.

## Dedicated Implementations Using Public Roslyn APIs

These built-in code-fix providers depend on IDE diagnostics that Workbench does not currently obtain. That blocks direct provider replay, but not the underlying operation.

| Family | Public implementation route | Complexity | Recommendation |
| --- | --- | ---: | --- |
| `SimplifyTypeNames` | Resolve the requested span semantically, annotate the intended syntax and call `Simplifier.ReduceAsync`. | Low to moderate | A dedicated `simplify-names` mutation is viable and should be preferred over importing IDE diagnostic infrastructure. |
| `SimplifyThisOrMe` | Use semantic binding to prove that removing `this.` preserves the symbol, then edit and format the selected syntax; it may be offered as a mode of `simplify-names`. | Low | Combine with the simplification workflow unless a separate contract is clearer. |
| `PreferFrameworkType` | Use semantic type information and the Simplifier or an explicit keyword/framework-type mode. | Low | Treat as a simplification option, not a separate MEF dependency. |
| `UsePatternMatchingIsAndCastCheckWithoutName` | Detect the supported `is` plus cast shape with `SemanticModel`, construct the pattern syntax and stage the edited solution. | Moderate | Implement a narrowly defined dedicated mutation if the agent use case justifies it; do not promise a broad pattern-modernisation engine under this provider's name. |
| `JsonDetection` | Detect a sufficiently structured JSON literal and add the `lang=json` marker comment. | Low | Technically easy, but low value for a headless MCP server because the result primarily enables IDE embedded-language features. Defer unless a concrete client benefit appears. |

The relevant stable APIs include [`Simplifier.ReduceAsync`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.simplification.simplifier.reduceasync), [`SyntaxGenerator`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.editing.syntaxgenerator), [`DocumentEditor`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.editing.documenteditor), [`SymbolEditor`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.editing.symboleditor), [`ImportAdder.AddImportsAsync`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.editing.importadder.addimportsasync), [`SymbolFinder`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.findsymbols.symbolfinder) and [`Renamer.RenameSymbolAsync`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.rename.renamer.renamesymbolasync).

## Workbench-Owned Semantic Implementations

The following workflows do not have a suitable public end-to-end Roslyn feature API. They are still implementable, but Workbench would own behaviour that Visual Studio obtains from internal services.

| Family | Why direct replay is blocked | Public primitives available | Complexity and recommendation |
| --- | --- | --- | --- |
| `GenerateOverrides` | The provider only registers a `CodeActionWithOptions` backed by `IPickMembersService`. | Enumerate overridable symbols and generate selected declarations with `SyntaxGenerator` or `DocumentEditor`. | Moderate. A dedicated tool with required member selectors is feasible after the replay and mixed-provider batches. |
| `MoveToNamespace` | The action and `IMoveToNamespaceOptionsService` are internal and option-backed. | Symbol/reference discovery, namespace syntax editing, import editing and document relocation. | High. Defer until after v1 unless it becomes a priority workflow. |
| `PullMemberUp` | The C# provider requires an internal options service before it offers work. | Symbol discovery, declaration movement, reference analysis and document editing. | High. A dedicated destination-and-member contract is possible, but Workbench would own conflict and accessibility semantics. |
| `MoveStaticMembers` | Roslyn's flow is options-backed and internal. | Symbol/reference analysis and multi-document editing. | High. Defer; it is a solution-wide semantic refactoring rather than a provider toggle. |
| `ExtractInterface` | The provider requires internal extraction and options services. | Symbol selection, interface generation, base-list editing and new-document creation. | High. Implement only as a dedicated explicit contract with extensive compatibility and acceptance coverage. |
| `ExtractClass` / aspirational `extract-base-class` | The Roslyn workflow is controlled by internal member-pick and extraction services. | Symbol analysis, declaration movement, inheritance editing and reference updates. | High. Treat as a custom refactoring, not as waiting for one public method. |
| `ChangeSignature` | The provider uses internal analysis, option and cascade services through `CodeActionWithOptions`. | `SymbolFinder`, semantic binding, syntax editing and solution-wide reference updates. | Very high. Parameter binding, overloads, method groups, delegates, XML documentation and call-site rewrites make an independent implementation unsuitable for pre-v1 work. |

Public primitives make these operations technically possible, but they do not transfer Roslyn's internal correctness guarantees. Each would need its own design, preview result, invariant set, fixtures, acceptance coverage and performance scenarios.

## Product-Boundary and Editor/External Exclusions

| Family | Exclusion reason | Recommendation |
| --- | --- | --- |
| `AddMissingReference` | Changes project/reference state rather than only staged source documents. | Keep excluded unless the transaction and persistence model deliberately expands to project-system mutations. |
| `AddPackage` | Performs package/project mutation and may involve network and package-source policy. | Keep excluded. Package installation should remain an explicit agent or build-system operation outside Code Action replay. |
| `CopilotSuggestions` | Requires Roslyn's external Copilot analysis service and editor options. | Keep excluded from a local deterministic server. |
| `CopilotImplementNotImplementedException` | Requires the same external Copilot service. | Keep excluded; a future local implementation would be a new Workbench feature, not this provider. |
| `RenameTracking` | Exists in EditorFeatures, depends on text-buffer change tracking and Visual Studio undo history, and is absent from the Features assemblies composed by Workbench. | Keep excluded. The explicit `rename-symbol` tool already represents the intended headless operation without inferring intent from typed editor changes. |

These should not be labelled “waiting for a public Roslyn API”. They are unavailable because of an intentional product boundary.

## Aspirational Mutation Catalogue Reassessment

| Aspirational tool | Correct classification | Recommended disposition |
| --- | --- | --- |
| `move-type-to-namespace` | Custom semantic implementation over public primitives. | High complexity; defer beyond the low-risk provider work. |
| `convert-to-async` | Entirely Workbench-owned solution-wide transformation; no public end-to-end Roslyn conversion API supplies the promised propagation workflow. | Very high complexity and semantic risk; do not place on the pre-v1 critical path. |
| `convert-to-pattern-matching` | Dedicated public-API implementation, provided the contract is narrowed to explicitly supported patterns. | Moderate; design after simplification work. |
| `generate-constructor` | Mixed provider with a replayable explicit-selection branch. | Reassess early after action-level capability support. |
| `generate-tostring` | Entirely Workbench-owned generation using semantic member selection and public editing APIs. | Moderate; feasible but not API-blocked. |
| `extract-interface` | Custom semantic implementation. | High; defer. |
| `extract-base-class` | Custom semantic implementation related to Roslyn's internal extract-class workflow. | High; defer. |
| `change-signature` | Custom solution-wide semantic implementation. | Very high; defer. |
| `generate-equals-hashcode` | Mixed provider with a replayable explicit-selection branch. | Reassess early after action-level capability support. |
| `generate-overrides` | Dedicated custom generator over public symbol/editing APIs. | Moderate; feasible after lower-risk batches. |
| `implement-interface` | Existing ordinary MEF provider omitted from the ledger. | Validate and promote through the current replay pipeline. |

The prior trigger “wait until Roslyn exposes a supported API” is therefore valid only for choosing whether to avoid owning the high-complexity implementations. It is not a technical prerequisite for most of the catalogue.

## Recommended Order

### P0 — Make the inventory complete

**Status:** Complete.

The runtime-backed audit now enumerates the actual composed C# providers for the pinned Roslyn version and requires every provider to have an explicit ledger disposition. It includes language-neutral Core providers and excludes editor-host providers that are not part of the runtime composition.

### P1 — Assess the code-fix inventory

**Status:** Complete.

The 151 providers are classified as 47 compiler-backed replay candidates, 94 requiring built-in diagnostic support, eight covered by existing tools and two project-system exclusions. No additional option-backed providers were found.

### P2a — Validate ordinary replay candidates

**Status:** Started.

Test `AddConstructorParametersFromMembers`, `GenerateComparisonOperators`, `ImplementInterface`, `OrganizeImports`, `ReplaceMethodWithProperty` and `ReplacePropertyWithMethods` together with a first batch of local compiler-backed code fixes. Promotion decisions can be made per fixture without coupling their implementations.

`CandidateCompatibilityCases` now permanently tracks all 47 compiler-backed code-fix candidates. Eight local fixtures currently execute against the real pinned MEF composition and prove the expected compiler diagnostic, a single matching action, a supported `ApplyChangesOperation`, the expected source mutation and continued production hiding. They cover anonymous-member naming, explicit casts, `inheritdoc`, unnecessary `new`, conditional interpolation parentheses, invalid `in` arguments, default-literal replacement and explicit const types. A candidate moves to the supported compatibility suite only when its fixture is approved for production promotion; staging, preview and rollback coverage then runs through the public production path rather than a test-only visibility bypass.

### P2b — Add built-in diagnostic support

Design bounded activation of built-in Roslyn analysers and an authoritative diagnostic-to-analyser mapping. Validate a representative style fix before expanding across the 94 affected providers.

### P2c — Support safe leaves of mixed providers

Design production action-level capability classification without title-only matching, then validate `GenerateConstructorFromMembers`, `GenerateEqualsAndGetHashCodeFromMembers` and `GenerateType`. This batch depends on the classification mechanism; it should not be combined with ordinary replay promotion.

### P3 — Add high-value public-API tools

Design `simplify-names` first, including the `this.` and framework-type cases where the contract remains coherent. Consider a narrowly scoped pattern-matching mutation separately.

### P4 — Consider custom generation

Evaluate `GenerateOverrides` and `generate-tostring` as explicit-member dedicated tools. Their contracts can reuse the established symbol selector and transaction patterns, but their generated syntax still requires focused compatibility coverage.

### Deferred

Keep move-to-namespace, pull-member-up, move-static-members, extract-interface, extract-base-class, change-signature and convert-to-async outside the pre-v1 critical path unless product priority changes. They are possible, but their risk comes from Workbench owning complex semantic transformations rather than from a small missing integration.

## Pre-Release Decision

P0 and P1 are complete. The full runtime provider inventory now has an evidence-backed disposition. Release scope can be selected from the 53 replay candidates without first building general IDE diagnostic support; the 94 IDE-diagnostic providers form a separate infrastructure decision rather than an accidental pre-release commitment. Public-API and custom semantic implementations can remain future functionality without weakening the current supported surface.

No unavailable family should be promoted solely from source inspection. The existing support rule remains valid: a controlled fixture must prove that Roslyn offers the action, Workbench selects it deterministically, staging succeeds and the public surface exposes it intentionally.
