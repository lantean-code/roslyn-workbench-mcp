# DOGFOOD-017 — Required string contract semantics

## Status

Complete. Implemented and validated with non-acceptance coverage, independently reviewed with no remaining findings or test gaps, and manually confirmed.

## Audit boundary

The audit covered public request and response shapes reachable from server-owned tools, Workspace-owned nested results, shared abstractions, bundled Core plugin tools and Code Actions. It traced every public `string` property in those areas that uses `string.Empty`, `""` or another construction default through its producers and supported failure paths. Internal models mapped into a different published contract were excluded from schema classification, but immediate producer models now carry the same completeness invariant where a published required value depends on them.

No direct Code Action request or response string property uses an empty default. `CodeActionExecutionError` and `CodeActionToolMetadata` are internal execution and registration models and therefore do not affect the published schema.

## Findings

The issue is confirmed. Most empty defaults do not describe a supported state: successful producers always supply the value, while the default merely lets incomplete objects compile and causes the generated schema to advertise the property as optional.

### Values that are contractually required

| Area | Types and properties | Producer evidence |
|---|---|---|
| Shared selectors and results | `DiagnosticInfo.Id`, `DiagnosticInfo.Message`, `WarningInfo.Code`, `WarningInfo.Message`, `PluginExecutionError.Code`, `PluginExecutionError.Message`, `WorkspaceIdentity.LoadedPath`, `WorkspaceIdentity.WorkspaceRoot`, `DocumentReference.DocumentId`, `DocumentReference.Path`, `DocumentReference.ProjectId`, `SymbolReference.DisplayName`, `SymbolReference.Kind`, `SolutionFolderInfo.Name`, `SolutionFolderInfo.Path` | Resolution, loading and diagnostic producers always construct these values from validated identities, normalized paths, execution failures or Roslyn symbols. |
| Text selection requests | `TextSelectionSelector.SelectedText` | Empty text is not a useful selection state: `WorkspaceResolver` immediately returns `NotFound`. The current default hides a missing required search value. |
| Workspace and server results | `WorkspaceInstanceInfo.InstanceId`, `WorkspaceInstanceInfo.LoadedPath`, `WorkspaceInstanceInfo.WorkspaceRoot`, `MutationPreview.Summary`, `RecoveryStatus.CommitId`, `WorkspaceCloseData.ClosedPath` | Successful lifecycle, transaction and recovery producers always assign these values. Malformed recovery evidence may lack a safe solution path, but its commit identifier still comes from the manifest, owner directory or evidence filename. `MutationData` is excluded because the mutation envelope deliberately projects a different minimal published shape. |
| Plugin graph contracts | every string on `GraphNode` and `GraphEdge` | The graph builders always project stable symbol IDs, display names and relationship kinds. These public plugin service contracts are also nested in bundled graph responses. |
| Core inspection results | `UnusedSymbolCandidate.Confidence`; both strings on `TypeInfo`; `ControlFlowExit.Kind`; `SymbolInfoData.Accessibility`; `SolutionStructureData.SolutionPath`; both strings on `CallableSignature`; `BasicBlockInfo.Kind`; `BasicBlockOperationInfo.Kind`; `AnalyzerInfo.DisplayName`; all strings on `ProjectStructureInfo` and `ProjectReferenceInfo`; `AttributeInfo.Name`; `ApiSymbolInfo.Accessibility`; `ParameterInfo.Name` and `RefKind`; both strings on `OutlineNode`; `OperationNode.Kind`; `FlowRegionInfo.Kind`; `MetadataReferenceInfo.Display`; both strings on `DisposableFinding`; `DependencyInfo.Kind` | Each successful projection explicitly assigns these from a Roslyn enum, symbol, diagnostic, normalized path or a non-empty framework/type-name fallback. No supported producer relies on the default. |
| Core project results | `ProjectInfo.ProjectId`, `ProjectInfo.Name`, `ProjectInfo.Path`, `ProjectInfo.Language`; `ParseOptionsInfo.Language`, `ParseOptionsInfo.DocumentationMode`; `CompilationOptionsInfo.OutputKind`, `CompilationOptionsInfo.OptimizationLevel` when the containing options object exists | Supported Workspaces retain only C# projects with project paths. The Roslyn objects provide these values whenever the corresponding projection exists. |

The requiredness must also propagate through immediate producer models rather than being repaired at publication. Validated `PluginMetadata`, its `PreparedCatalogPlugin.Metadata` and `RegisteredTool.Plugin` containers, and `RegisteredTool.Metadata` must be complete. Mutation summaries must remain required through `MutationCandidate`, `WorkspaceMutationCandidate` and `MutationStagingOutcome`. `WorkspaceCloseOutcome.ClosedPath` must be required before it is projected into `WorkspaceCloseData`.

These properties should lose their empty initializer and use the C# `required` keyword. `TextSelectionSelector.SelectedText` should additionally publish a non-empty constraint because an explicitly empty value is rejected semantically today.

### Empty currently means unavailable, not an empty value

| Contract | Current state | Proposed state |
|---|---|---|
| `ProjectInfo.AssemblyName` | Roslyn `null` is converted to empty. | Make the property nullable and preserve `null`. |
| `ParseOptionsInfo.LanguageVersion` | Non-C# parse options produce empty, although the reusable projection factory explicitly supports language-neutral Roslyn options. | Make the property nullable; C# continues to publish its effective version. |
| `DocumentOptionsData.LanguageVersion` | Missing parse options produce empty. | Make the property nullable when Roslyn does not expose parse options. |
| `DocumentOptionsData.NullableContext` | Missing or non-C# compilation options produce empty. | Make the property nullable when a C# nullable context is unavailable. |
| `CompilationOptionsInfo` | Missing compilation options produce an object whose required-looking scalar values are empty or zero. | Make `CreateCompilationOptionsInfo` return `null` when compilation options are unavailable. Within a present object, make `OutputKind` and `OptimizationLevel` required. |
| `PluginStatus.PluginId`, `DisplayName`, `Version`, `SupportedApiVersion` | Invalid or unreadable raw entry-point metadata can flow through the disabled-status path as empty strings; discovery fallbacks deliberately pass an empty version. | Normalize unavailable raw or fallback metadata to `null` and make the affected status properties nullable. Validated `PluginMetadata` is complete and is copied without normalization. |

This avoids replacing one inaccurate schema with another: values which genuinely cannot be produced are optional and nullable, rather than required strings whose only possible fallback is empty.

### Supported empty values

| Contract | Semantics | Existing coverage |
|---|---|---|
| `CodeContextData.Text` | An empty document or selected context can legitimately return no text. | Query tests cover the projection path; add an explicit empty-document assertion so the contract state remains intentional. |
| `RecoveryStatus.SolutionPath` | Malformed or unsafe recovery evidence intentionally reports an empty path while retaining the recovery record and diagnostic state. | Recovery-store tests cover malformed and unreadable evidence; retain the existing description that states the empty-path meaning. |

These defaults remain. Their targeted tests should make the supported empty state explicit rather than relying on default-object equality.

## Existing coverage assessment

Producer coverage is generally strong, but published requiredness is not locked for this contract surface. Existing schema tests prove that the SDK maps C# `required` members into JSON Schema and test a few representative request properties; they do not inventory these response properties. Several projection tests currently encode the misleading fallback behaviour, including default `CompilationOptionsInfo` values and an empty non-C# parse-options language version.

Implementation therefore needs both producer tests for the revised unavailable states and schema tests that assert representative required, nullable and intentionally-empty shapes. A reflection-based contract guard should cover all published contract assemblies so a future non-nullable published string cannot silently acquire an empty initializer without an explicit allow-empty decision.

## Proposed implementation

1. Apply `required` to the contractually required properties and update every producer and test fixture at compile time.
2. Add a non-empty schema constraint to `TextSelectionSelector.SelectedText` while retaining the resolver's defensive empty check.
3. Preserve absence as `null` for optional Roslyn and plugin metadata instead of projecting empty sentinels.
4. Return no `CompilationOptionsInfo` object when Roslyn supplies no compilation options; keep all scalar members truthful when it is present.
5. Retain only the two intentional empty defaults above and add focused tests documenting those states.
6. Add schema coverage for a server result, shared nested contract, Core plugin result and selection request, plus a complete reflection guard across the published contract surface.

No central schema-transformer special case is proposed. Requiredness should come from the runtime C# contract so construction, nullability and generated schema remain aligned.
