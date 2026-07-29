# Required-value initializer audit

Date: 2026-07-29

## Outcome

This audit found 284 members whose declaration currently permits an incomplete object to look valid: 259 production members and 25 test-support or plugin-fixture members. No implementation or test code was remediated as part of this audit.

The findings are not limited to explicit `string.Empty` initializers. They also include implicit zero values for scalar properties, constructed placeholder objects such as `new()`, and type sentinels such as `typeof(object)` where every valid instance should instead receive a deliberate value.

The review classified all 758 candidate declarations found in repository-owned C# source: 575 production declarations and 183 test declarations. Of those, 474 were reviewed as legitimate defaults and are recorded below as non-findings.

## Governing rule

A declaration initializer is appropriate when it creates a genuinely valid and fully functional default for the member. Empty collections, request option defaults, caches, locks, comparers and configured policy defaults are examples of valid defaults.

An initializer is not appropriate when its purpose is to satisfy nullable analysis or permit construction before an identity, path, message, discriminator, result count, timestamp or other projected value has been supplied. In those cases:

- use `required` when callers should explicitly project the member, including when zero, `false` or an empty string can be a legitimate supplied value;
- use a constructor when the member is fundamental to every instance and object-initializer construction adds no value;
- use a nullable type when absence is a valid domain state;
- use get-only state plus named factories for mutually exclusive or invariant-bearing outcomes, in accordance with `src/AGENTS.md`;
- retain runtime semantic validation where a supplied non-null value can still be blank, out of range or otherwise invalid.

The C# `required` modifier enforces member initialization, not content validity. Making a string required therefore does not remove the need to reject blank identifiers or paths at an external boundary.

## Scope and method

The audit covered declaration initializers on settable or init-only properties and fields under `src` and `test`, plus implicit defaults on non-nullable value-type properties. It specifically inventoried `string.Empty`, null/default forms, empty members, empty collections, constructed objects, literal defaults, `Guid`, enum, numeric, Boolean, date/time and source-defined value-type defaults.

Generated outputs, `bin`, `obj` and `test/TestAssets` source workspaces were excluded. The latter are test inputs representing external repositories rather than code compiled as part of this repository.

The inventory was syntax- and semantic-model based. Construction sites and factories were then inspected to distinguish deliberate defaults from omitted required projections. Roslyn MCP navigation was requested but was not available in this session, so the equivalent local Microsoft.CodeAnalysis APIs were used. The classification follows the repository standards and Microsoft's nullable-reference guidance that an initializer should not invent a sentinel merely to silence construction warnings.

## Inventory

| Area | Empty string | Implicit scalar default | Literal default | Empty collection | Constructed/other explicit | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Production | 122 | 143 | 22 | 113 | 175 | 575 |
| Tests and fixtures | 41 | 12 | 6 | 32 | 92 | 183 |
| Total | 163 | 155 | 28 | 145 | 267 | 758 |

| Disposition | Production | Tests and fixtures | Total |
| --- | ---: | ---: | ---: |
| Finding | 259 | 25 | 284 |
| Reviewed legitimate default | 316 | 158 | 474 |
| Total | 575 | 183 | 758 |

## Production findings

### Roslyn.Workbench.Mcp.Abstractions — 31 members

| Type | Members | Recommended representation |
| --- | --- | --- |
| `SolutionFolderInfo` | `Name`, `Path` | Required scalar projections. |
| `SnapshotMatchResult` | `Kind` | Make state get-only and restrict construction to the existing named factories. |
| `DiagnosticInfo` | `Id`, `Severity`, `Message` | Required scalar projections; retain semantic validation for a blank diagnostic identifier or message if blank is invalid at the producing boundary. |
| `DiffSummary` | `AddedLines`, `RemovedLines`, `ChangedLines` | Required projections even though zero is a valid result. |
| `DocumentChange` | `ChangeKind` | Required discriminator. |
| `WarningInfo` | `Code`, `Message` | Required scalar projections. |
| `WorkspaceIdentity` | `WorkspaceId`, `WorkspaceEpoch`, `LoadedPath`, `WorkspaceRoot` | Required identity projections. |
| `DocumentReference` | `DocumentId`, `Path`, `ProjectId` | Required projections. This type describes a resolved document, so empty-string fallbacks do not represent valid alternative selectors. |
| `ResolvedLocation` | `Line`, `Column`, `WorkspaceEpoch` | Required projections even when line or column zero is valid. |
| `ScopeSelector` | `Kind` | Required discriminator. |
| `SnapshotPrecondition` | `WorkspaceEpoch` | Required snapshot identity. |
| `SymbolReference` | `DisplayName`, `Kind` | Required projections. |
| `TextSelectionSelector` | `SelectedText` | Required input. An explicitly supplied empty selection may still be valid, but omission should not be indistinguishable from that choice. |
| `TextSpanRange` | `Start`, `Length` | Required range projections; zero remains a valid explicitly supplied value. |
| `TextSpanSelector` | `Start`, `Length` | Required selector inputs; zero remains a valid explicitly supplied value. |

### Roslyn.Workbench.Mcp.CodeActions — 23 members

| Type | Members | Recommended representation |
| --- | --- | --- |
| `BuiltInCodeActionFamily` | `ProviderId`, `Kind` | Required catalogue metadata. The `Refactoring` initializer can conceal a missing family classification. |
| `CodeActionInfo` | `ActionId`, `Title`, `ProviderId`, `WorkspaceEpoch`, `ExpiresAt` | Required action identity and projection data. `Guid.Empty`, zero epoch and empty expiry are not useful fallback states. |
| `CodeActionNameOptionInfo` | `Name`, `Label` | Required option projections. |
| `CodeActionDescriptorContext` | `Kind` | Required discriminator, or factory-owned state if the nullable payload members depend on the kind. |
| `CodeActionLocation` | `Line`, `Column` | Required projections even when zero is valid. |
| `DescribeCodeActionData` | `Context` | Required projected context rather than a blank constructed placeholder. |
| `CodeActionExecutionError` | `Code`, `Message` | Required error projections. |
| `CodeActionExecutionFailure` | `Outcome`, `Error` | Constructor/factory-owned invariant state; a default outcome paired with a blank error is not a valid failure. |
| `CodeActionToolMetadata` | `Name`, `Title`, `Description` | Required catalogue metadata. The default `Behavior` object is valid and is not a finding. |
| `CodeActionDescriptorEntry` | `ExecutionMode`, `ContextKind` | Required descriptor discriminators, or factory-owned state where the other nullable members depend on them. |
| `CodeActionRediscovery` | `ProviderAvailable` | Required result projection; `false` is valid only when deliberately produced. |

### Roslyn.Workbench.Mcp.Plugins.Core — 81 members

| Type group | Members | Recommended representation |
| --- | --- | --- |
| Display, identity and classification projections | `AnalyzerInfo.DisplayName`; `ApiSymbolInfo.Accessibility`; `AsyncFinding.Kind`; `AttributeInfo.Name`; `BasicBlockInfo.Kind`; `CallableSignature.DisplayName`, `Kind`; `CompilationOptionsInfo.OutputKind`, `OptimizationLevel`; `ControlFlowExit.Kind`; `DependencyInfo.Kind`; `DisposableFinding.Kind`; `DocumentOptionsData.LanguageVersion`, `NullableContext`; `FlowRegionInfo.Kind`; `MetadataReferenceInfo.Display`; `OperationNode.Kind`; `OutlineNode.Name`, `Kind`; `ParameterInfo.Name`, `RefKind`; `ParseOptionsInfo.Language`, `LanguageVersion`, `DocumentationMode`; `SymbolInfoData.Accessibility`; `TypeInfo.DisplayName`, `Kind`; `UnusedSymbolCandidate.Confidence` | Required scalar projections. |
| Project and solution projections | `ProjectInfo.ProjectId`, `Name`, `Path`, `AssemblyName`, `Language`; `ProjectReferenceInfo.ProjectId`, `Name`, `Path`; `ProjectStructureInfo.ProjectId`, `Name`, `Path`; `SolutionStructureData.SolutionPath` | Required scalar projections. If a Roslyn project can genuinely lack a file path or assembly name, represent that absence with nullable properties rather than empty strings. |
| Text projections | `AsyncFinding.Message`; `CodeContextData.Text`; `DisposableFinding.Message`; `DuplicateCodeOccurrence.Context`; `OperationNode.Syntax` | Required projections. The projected content may legitimately be empty, but the producer should state that value explicitly. |
| Boolean and numeric result projections | `ApiSymbolInfo.IsObsolete`; `AttributeInfo.Inherited`; `BasicBlockInfo.Ordinal`, `IsReachable`; `CompilationOptionsInfo.AllowUnsafe`, `WarningLevel`; `ControlFlowAnalysisData.EntryReachable`, `ExitReachable`; `ControlFlowGraphData.BlocksTruncated`, `RegionsTruncated`; `DefinitionLocation.IsMetadata`; `DuplicateCodeGroup.StatementCount`; `FlowRegionInfo.Id`, `FirstBlockOrdinal`, `LastBlockOrdinal`; all five `ImpactSummary` counts; all five `MetricInfo` metrics; `OperationNode.Truncated`; `OperationTreeData.Truncated`; `ParameterInfo.IsOptional`, `HasExplicitDefaultValue`; `ReferenceLocation.IsDefinition`, `IsWrite`; `TypeHierarchyNode.Depth` | Required projections. Zero and `false` can be valid results, but an omitted projection must not silently produce them. |
| Internal projection records | `DuplicateCandidate.StatementCount`; `DuplicateGroupCandidate.StatementCount`, `DiscoveryOrder`; `PendingReference.IsDefinition` | Required internal projections. These records are assembled from analysed data rather than configured with defaults. |

### Roslyn.Workbench.Mcp.Plugins — 25 members

| Type | Members | Recommended representation |
| --- | --- | --- |
| `MutationCandidate` | `Summary` | Required mutation description. |
| `PluginExecutionError` | `Code`, `Message` | Required error projections. |
| `PluginMetadata` | `PluginId`, `DisplayName`, `Version`, `SupportedApiVersion` | Required public plugin metadata, with semantic validation for blank values. This is a public API change and requires normal compatibility/versioning review before remediation. |
| `ToolRegistrationMetadata` | `Name`, `Title`, `Description` | Required public tool metadata, with semantic validation for blank values. The default `Behavior` object remains valid. This is a public API change and requires normal compatibility/versioning review before remediation. |
| `GraphEdge` | `FromId`, `FromDisplayName`, `ToId`, `ToDisplayName`, `Kind` | Required graph projections. |
| `GraphNode` | `Id`, `Kind`, `DisplayName` | Required graph projections. |
| `RegisteredTool` | `Plugin`, `Metadata`, `Kind`, `RequestType`, `ResponseType` | Required registration state. `new()` metadata and `typeof(object)` request/response types are placeholders, not functional defaults. |
| `ToolExecutionFailureResult` | `Outcome`, `Error` | Constructor/factory-owned invariant state; the existing factory pattern should be applied to every valid failure alternative. |

### Roslyn.Workbench.Mcp.Workspace — 61 members

| Type group | Members | Recommended representation |
| --- | --- | --- |
| Change detection | `WorkspaceInputDirectoryFingerprint.Path`; `WorkspaceInputFileFingerprint.Path`, `LastWriteTimeUtc`, `Length`; `WorkspaceInputChange.DetectionSource`, `Kind` | Required fingerprint and discriminator projections. A zero timestamp or length is valid only when explicitly observed. |
| Lifecycle and execution | `WorkspaceCloseOutcome.ClosedPath`; `WorkspaceExecutionFailure.Status`, `Error`; `WorkspaceOpenOutcome.Workspace`, `ProjectCount`, `DocumentCount`; `WorkspaceReloadOutcome.Workspace`, `ProjectCount`, `DocumentCount`; `WorkspaceStatusOutcome.State`, `Workspace`, `ProjectCount`, `DocumentCount`, `ReloadRequired` | Required projections. Failure state should be constructor/factory-owned where status and error form an invariant. |
| Errors and mutation descriptions | `WorkspaceOperationError.Code`, `Message`; `MutationData.Operation`, `Summary`; `MutationPreview.Summary`; `MutationStagingOutcome.Operation`, `Summary`; `WorkspaceMutationCandidate.Summary` | Required scalar projections. |
| Diff results | `DiffHunk.OriginalStartLine`, `OriginalLineCount`, `UpdatedStartLine`, `UpdatedLineCount`; `DocumentDiff.Truncated` | Required projections, including valid zero and `false` values. |
| Recovery | `RecoveryStatus.WorkspaceRoot`, `CommitId`, `SolutionPath`, `State` | `CommitId` and `State` should be mandatory. `WorkspaceRoot` and `SolutionPath` should be nullable when malformed or legacy evidence genuinely lacks them; the current empty-string substitution hides that state. Because validity varies with `State`, a constructor/factory-owned outcome is preferable to independent init-only properties. |
| Workspace instance/session | `WorkspaceInstanceInfo.InstanceId`, `LoadedPath`, `WorkspaceRoot`, `WorkspaceState`; `WorkspaceSessionSnapshot.ProjectCount`, `DocumentCount` | Required identity and snapshot projections. |
| Transactions | all nine scalar members of `TransactionInfo`; `MutationStagingOutcome.Transaction`, `Preview`, `Changes`; `TransactionHistoryOutcome.Transaction`; `TransactionPreviewOutcome.Transaction`; `TransactionStartOutcome.Transaction`; `TransactionCommitOutcome.Committed`; `TransactionRollbackOutcome.State`; `WorkspaceTransaction.MaxRevisions` | Required projections. Constructor/factory-owned state is preferable for commit and rollback outcomes. `WorkspaceTransaction.CurrentRevision = 0` is a valid initial transaction state and is not a finding. |

### Roslyn.Workbench.Mcp host — 38 members

| Type group | Members | Recommended representation |
| --- | --- | --- |
| Tool results and errors | `ToolError.Code`, `Message`; `ToolResult<TData>.Outcome` | Required error data. `ToolResult<TData>` is invariant-bearing and already has companion factories, so `Outcome` should be get-only factory-owned state rather than independently defaultable. |
| Status contracts | `ComponentStatus.IsAvailable`; `PluginStatus.PluginId`, `DisplayName`, `Version`, `SupportedApiVersion`, `Enabled`; all five scalar `ServerConfiguration` members; `ServerStatusData.ToolCount`; `WorkspaceCloseData.ClosedPath`; `WorkspaceOpenData.ProjectCount`, `DocumentCount`; `WorkspaceReloadData.ProjectCount`, `DocumentCount`; `WorkspaceStatusData.State`, `ReloadRequired`; `TransactionCommitData.Committed`; `TransactionRollbackData.State` | Required projections. Zero and `false` are valid only when explicitly projected. |
| Requests | `WorkspaceOpenRequest.Path` | Required input plus existing blank-path validation. |
| Plugin loading | all five `PluginEntryPointMetadata` string members; `PluginPackageCandidate.PackageDirectory`, `EntryAssemblyPath`, `EntryPoint`; `PluginPackageDiscoveryResult.FallbackIdentity`; `PreparedCatalogPlugin.Metadata`, `Preparation`; `PluginAssemblyInspection.IsManagedAssembly` | Required discovery and preparation state. |
| Constructed status placeholder | `ServerStatusData.CodeActions` | Remove the contradictory nullable-plus-`new()` declaration default. Either require the projected component status or leave it nullable without a placeholder according to the intended detail-level contract. |

## Test-support and fixture findings — 25 members

| Type or fixture | Members | Recommended representation |
| --- | --- | --- |
| `BuiltInCodeActionAuditCase` | `ProviderId`, `SourceNote` | Require `ProviderId`. Make `SourceNote` nullable because many valid refactoring cases omit it; empty text currently represents absence. The default `ExpectedRuntimeOutcome.NotOffered`, `Kind.Refactoring` and default factories are deliberate audit-case defaults and are not findings. |
| `BuiltInCodeActionAuditProbe` | `LocationStatus`, `VisibilityOutcome`, `RuntimeOutcome`, `MatchingActionCount`, `IsVisibleInList` | Required result projections, including valid zero and `false` values. |
| `BuiltInCodeActionProviderAssessmentEntry` | `ProviderId`, `Kind`, `Status` | Required assessment projections. |
| Two nested `ParameterisedOptions` records in `TestCodeActionProviders` | `Replacement` | Required operation option; the fallback branches already supply deliberate replacement values. |
| `InMemoryRoslynDocumentDefinition` | `Name`, `Source` | Required fixture inputs. An empty source document remains possible when explicitly supplied. |
| `InMemoryRoslynProjectDefinition` | `Name` | Required fixture input. |
| Host plugin fixtures | `HostValidMutationPluginFixture.Request.Summary`; `HostValidQueryPluginFixture.Request.Name`, `Response.Value`, `Response.PrivateDependencyVersion` | Required fixture contracts. |
| Invalid and compatibility plugin fixtures | `DuplicateToolNameTestPlugin.Request.Name`, `Response.Value`; `UnsupportedApiVersionTestPlugin.Request.Name`, `Response.Value`; `ValidMutationTestPlugin.Request.Summary`; `ValidQueryTestPlugin.Request.Name`, `Response.Value` | Required fixture contracts. Keeping a fixture invalid in one intended dimension does not justify unrelated blank-value defaults. |

## Reviewed non-findings

The following 474 declarations were reviewed and should retain their defaults:

- all 145 empty-collection declarations, including `BoundedCollection.Empty<T>()`, because they create valid empty results or internal collection state;
- all 28 literal policy and option defaults, including request Boolean defaults, configured limits, schema/manifest version `2`, cache size and timeouts;
- 153 production constructed or other explicit defaults, including caches, locks, comparers, serializer settings, static diagnostic descriptors, descriptor registries, request limits, workspace option defaults and default `Behavior` objects;
- 92 test constructed or other explicit defaults, including mocks, deterministic identifiers/timestamps, test infrastructure, timeouts, collections and deliberate audit factories;
- 28 production implicit semantic defaults: 23 optional request Boolean flags, three non-destructive behaviour flags, `CodeActionProviderCapability.RequiresActionResolution`, and the initial `WorkspaceTransaction.CurrentRevision`;
- five test implicit semantic defaults: `BuiltInCodeActionAuditCase.ExpectedRuntimeOutcome`, `BoundedResponse.Count`, the two enum-binder request values, and `HostValidQueryPluginFixture.Request.Throw`;
- 23 intentionally permissive test-only string DTO members used by protocol schema, binding, envelope mapping, query-boundary and analyzer-warning tests. These are local negative or shape fixtures rather than domain objects, and changing them to `required` would change the condition those tests exercise.

The 23 deliberately permissive string fixtures are located in `McpSdkSchemaProviderIntegrationTests`, `ToolExecutionFailureResultTests`, `PluginHandlerWarningInspectorTests`, `QueryResponseContractInspectorTests`, `McpToolProtocolFactoryTests`, `ToolResultEnvelopeSerializerTests`, `ToolRequestBinderTests`, `ToolSchemaFactoryTests`, `CodeActionMcpToolRegistrationVisitorTests`, `CodeActionMutationMcpServerToolTests`, `CodeActionQueryMcpServerToolTests`, `McpServerToolBaseTests`, `PluginMcpToolRegistrationVisitorTests`, `PluginMutationMcpServerToolTests`, `PluginQueryMcpServerToolTests` and `WorkspaceToolResultMapperTests`.

## Suggested remediation order

No remediation was performed, but the findings should be addressed in this order:

1. Convert invariant-bearing result and failure types to constructor/factory-owned state so invalid combinations cannot be constructed.
2. Correct optional absence represented by empty strings, especially `RecoveryStatus.WorkspaceRoot`, `RecoveryStatus.SolutionPath` and `BuiltInCodeActionAuditCase.SourceNote`.
3. Make internal production identities, metadata, discriminators and scalar result projections required.
4. Review public Abstractions and Plugins contract changes for source compatibility and schema effects before making required members public.
5. Update test-support and plugin-fixture contracts, then fix construction sites exposed by the compiler.

## Validation

This was a record-only audit. No production or test implementation was changed, so build, analyzer and test runs are not required for the audit document itself. The temporary semantic inventory test used to produce the candidate list was removed after classification.
