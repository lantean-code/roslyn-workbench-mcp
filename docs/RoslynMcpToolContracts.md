# Roslyn MCP Server - Tool Contract Catalogue

## Purpose

This document defines the MCP metadata, request contracts and structured output
shapes for all 81 planned tools. It is the contract source for plugin authors,
the server tool adapter and generated JSON Schemas. It supersedes the request
shapes in `JoshuaRamirez/RoslynMcpServer`; retained tool names preserve their
intent, but use the safer workspace, selector and transaction model defined by
this project.

## Current Execution Surface Note (2026-07-02)

The current build now registers and serves the `get-code-context`,
`find-callees`, `find-overrides`, `get-symbol-dependencies`,
`get-symbol-dependents`, `get-change-impact`, `get-api-surface`,
`convert-expression-body`, and `add-null-checks` contracts described in this
catalogue, and `get-control-flow-graph` now returns populated `regions` data
instead of an empty placeholder array. The narrowed future request contracts
for `move-type-to-file` and `convert-property` now exist in the contracts
project, and the shipped `convert-auto-property-to-full-property` split uses
its own dedicated CLR request contract.

The following point 2 catalogue entries remain planned contracts only and are
not registered by the current build:

- `get-code-metrics`
- `find-unused-symbols`
- `find-duplicate-code`
- `get-dependency-graph`
- `find-dependency-cycles`
- `get-test-impact`
- `analyze-nullability`
- `analyze-async`
- `analyze-disposables`
- `move-type-to-file`
- `move-type-to-namespace`
- `convert-to-async`
- `convert-property`
- `convert-to-pattern-matching`
- `generate-constructor`
- `generate-tostring`

The following mutation families are not planned for implementation in this
server while they depend on non-public Roslyn services or internal IDE-only
generation paths. Their request and response shapes remain in this catalogue
only as aspirational end-state contracts:

- `move-type-to-namespace`
- `convert-to-async`
- `convert-to-pattern-matching`
- `generate-constructor`
- `generate-tostring`
- `extract-interface`
- `extract-base-class`
- `change-signature`
- `generate-equals-hashcode`
- `generate-overrides`
- `implement-interface`

Where those deferred or not-planned tools still appear below, their request and
response shapes should not be read as evidence that the current build publishes
them through `tools/list`.

## MCP Metadata

Every tool listed here is exposed through `tools/list` with the following MCP
metadata:

| Field | Rule |
|---|---|
| `name` | The kebab-case identifier in this catalogue. It is globally unique for the server process. |
| `title` | The human-readable title in this catalogue. |
| `description` | The description in this catalogue, including any operational requirements an agent must understand. |
| `inputSchema` | A JSON Schema generated from the named request record and shared component records below. Schema validation improves tool discovery but the server validates all inputs at runtime. |
| `outputSchema` | Optional. In the default agent-optimised mode the server omits it to reduce `tools/list` size. When the startup `ToolOutputSchemaMode` is `Full`, the published schema is the real `ToolResult<TData>` structured-content schema for the named data shape. |
| `annotations.readOnlyHint` | `true` for query tools; `false` for lifecycle and staged-mutation tools. |
| `annotations.destructiveHint` | Set from the tool's maximum effect. Any tool that can replace or remove staged source, discard staged work, or write source to disk is `true`. |
| `annotations.idempotentHint` | `true` for queries; `false` for state-changing operations. |
| `annotations.openWorldHint` | `false` for every tool. They operate only on the loaded local workspace. |
| Task support | `Forbidden` in v1. The client waits for the normal tool result and may cancel it. |

The official C# MCP SDK supports generated input schemas, optional structured
output schemas, titles and these behavioural annotations. Plugin tools are registered
as server-owned `PluginMcpServerTool` instances, one per validated
`RegisteredTool`, rather than by scanning plugin methods for MCP attributes.
The adapter publishes the schema and metadata held by `RegisteredTool`,
deserializes the full argument object into the named request record, and
returns a structured `CallToolResult` carrying `ToolResult<TData>`. See the
[C# SDK tool documentation](https://csharp.sdk.modelcontextprotocol.io/concepts/tools/tools.html)
and [McpServerTool API](https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Server.McpServerTool.html).

The adapter may attach plugin identity and contract version as internal metadata
for diagnostics, but requirements that an agent must act upon are always stated
in the standard tool description. No client-specific metadata extension is
required for normal operation. Tool descriptions may include a short result hint
when it materially improves discovery, but they do not restate full response
DTOs or JSON Schema fragments.

For implementation, each input type is named by converting the tool name to
PascalCase and appending `Request`: for example, `find-references` uses
`FindReferencesRequest`. A tool's declared structured output type is always
`ToolResult<TData>`, where `TData` is the type named in that tool's output
column. `PluginMcpServerTool` invokes `ToolExecutor`, which validates and
constructs the appropriate context before it invokes the plugin's typed
handler. This gives each plugin a predictable C# contract without adding a
second, non-MCP metadata format or making the plugin a protocol endpoint.

## Common Output Envelope

Every successful or expected domain outcome is returned as structured JSON in
`ToolResult<TData>`:

```text
ToolResult<TData>
- outcome: Succeeded | NoChange | Rejected | Conflict | Faulted
- workspaceId?: string
- workspaceEpoch?: long
- transactionRevision?: int
- data?: TData
- changes?: ChangeSummary
- diagnostics: DiagnosticInfo[]
- warnings: WarningInfo[]
- error?: ToolError
- requiredAction?: RequiredAction
```

`Succeeded` and `NoChange` set MCP `isError` to `false`. `Rejected`,
`Conflict` and `Faulted` set it to `true`. Malformed MCP requests remain
protocol-level failures rather than `ToolResult` values.

The runtime output shape is a discriminated `oneOf` on `outcome`, not merely an
object with optional fields. `Succeeded` query results require `data` and do
not contain `changes`; successful staged mutations require both `data` and the
top-level `changes`; successful lifecycle, preview, history, commit and
rollback operations may return `data` without `changes`; `NoChange` never
contains `changes`; `Rejected` and `Conflict` require `error` and contain
neither `data` nor `changes`; `Faulted` requires `error` and contains no
partially staged data or changes. When `ToolOutputSchemaMode` is `Full`,
`PluginMcpServerTool` publishes this explicit schema rather than relying on an
SDK-generated generic type schema. In the default `Omit` mode, the runtime JSON
shape remains unchanged even though `tools/list` does not carry `outputSchema`.
Response readability is preserved because these contracts serialize enums as
strings via `JsonStringEnumConverter<TEnum>` rather than numeric values.

Common fields used by the data shapes below:

| Type | Shape |
|---|---|
| `WorkspaceSelector` | `{ workspaceId?: string, alias?: string, path?: string }` |
| `WorkspaceIdentity` | `{ workspaceId: string, alias?: string, workspaceEpoch: long, loadedPath: string }` |
| `TransactionInfo` | `{ revision: int, revisionCount: int, maxRevisions: int, remainingRevisions: int, canMutate: boolean, canUndo: boolean, canRedo: boolean, canCommit: boolean, canRollback: boolean }` |
| `ChangeSummary` | `{ added: DocumentChange[], modified: DocumentChange[], deleted: DocumentChange[], affectedSymbols: SymbolReference[] }`. This is the only public location for cross-cutting mutation change data. |
| `DocumentChange` | `{ document: DocumentReference, changeKind: Added|Modified|Deleted, preview?: DiffSummary }` |
| `DiagnosticInfo` | `{ id: string, severity: Hidden|Info|Warning|Error, message: string, location?: ResolvedLocation }` |
| `WarningInfo` | `{ code: string, message: string }` |
| `ToolError` | `{ code: string, message: string, correlationId?: string }` |
| `RequiredAction` | `OpenWorkspace | StartTransaction | RollbackTransaction | ReloadWorkspace | ResolveTargetAgain | CommitOrRollback | ReduceTransactionHistory | Retry | ResolveRecovery | NarrowRequest` |
| `RecoveryStatus` | `{ commitId: string, solutionPath: string, state: Prepared|Applying|Committed|Restored|RecoveryConflict|RecoveryIncomplete, message?: string }` |

## Shared Request Components

The server resolves every selector against the effective solution: the loaded
baseline outside a transaction, or the current staged solution during one.
All positions and spans are zero-based UTF-16 values, directly aligned with
Roslyn `TextSpan`.

| Component | Fields |
|---|---|
| `WorkspaceSelector` | `workspaceId?: string`, `alias?: string`, `path?: string`. At least one is required; multiple values must resolve to one loaded workspace session. |
| `DocumentSelector` | `path?: string`, `documentId?: string`. Exactly one is required. `path` is normalised and workspace-relative. |
| `ProjectSelector` | `projectId?: string`, `name?: string`, `path?: string`. At least one is required; multiple values must resolve to one project. |
| `SnapshotPrecondition` | `{ workspaceId?: string, workspaceEpoch: long, transactionRevision?: int }`. It asserts the effective solution snapshot on which a prior location, selection or symbol result was obtained. |
| `TextSpanSelector` | `document: DocumentSelector`, `start: int`, `length: int`. |
| `TextSelectionSelector` | `document: DocumentSelector`, `selectedText: string`, `contextBefore?: string`, `contextAfter?: string`. A unique match becomes a canonical `TextSpanSelector`. |
| `LocationSelector` | `span?: TextSpanSelector`, `selection?: TextSelectionSelector`. Exactly one is required. |
| `SymbolSelector` | `location?: LocationSelector`, `documentationCommentId?: string`. Query tools accept exactly one resolver; mutations require `location`. A documentation ID is accepted only when it resolves to one source symbol. It cannot identify a local symbol or a particular declaration of a partial symbol. |
| `ScopeSelector` | `kind: Solution|Project|Document|Projects`, `project?: ProjectSelector`, `document?: DocumentSelector`, `projects?: ProjectSelector[]`. The additional selector must match `kind`. |
| `ProjectRelativePath` | `value: string`. A relative path beneath the owning project's canonical directory. Absolute paths, `..` traversal, and a canonical path that escapes through a symlink are rejected. |
| `ResultLimit` | `maxResults?: int`. Omitted means the server startup `DefaultMaxResults`, initially `100`. Must be at least `1`. There is no cursor or fixed result-count ceiling in v1; a larger request recomputes the ordered result set. The server may truncate at its configured response-byte limit. |
| `ResolvedLocation` | `{ workspaceId?: string, document: DocumentReference, span: { start: int, length: int }, line: int, column: int, workspaceEpoch: long, transactionRevision?: int }` |
| `DocumentReference` | `{ documentId: string, path: string, projectId: string }` |
| `SymbolReference` | `{ displayName: string, kind: string, documentationCommentId?: string, location?: ResolvedLocation }`. Metadata names may be returned in descriptive text, but are never accepted as identity selectors. |
| `CodeActionInfo` | `{ workspaceId?: string, actionId: string, title: string, providerId: string, kind?: string, equivalenceKey?: string, actionPath: int[], diagnosticIds: string[], workspaceEpoch: long, transactionRevision?: int, expiresAt: string, executionMode?: Replay|Parameterised|Unsupported, executorTool?: string, describeTool?: string, unsupportedReasonCode?: string, requirements?: string[] }`. `actionId` is opaque and integrity-protected; it binds these fields and is valid only for the stated snapshot and token lifetime. |
| `MutationPreview` | `{ summary: string }` |
| `DocumentDiff` | `{ document: DocumentReference, hunks: DiffHunk[], truncated: boolean }` |
| `DiffSummary` | `{ addedLines: int, removedLines: int, changedLines: int }` |
| `CollectionTruncation` | `ResultLimit | ResponseByteLimit | NodeLimit | EdgeLimit` |

## Behaviour Classes

The `Behaviour` column in the tool tables maps to fixed MCP annotations:

| Behaviour | `readOnly` | `destructive` | `idempotent` | Meaning |
|---|---:|---:|---:|---|
| `Q` | true | false | true | Query. It never changes workspace or transaction state. |
| `S` | false | false | false | Server or workspace lifecycle operation. |
| `M` | false | true | false | Transactional mutation. It can replace or remove staged source, though it never writes files directly. |
| `C` | false | true | false | Commit or rollback. |

All tools use `openWorld = false` and task support `Forbidden`.

`M` is marked destructive because a staged source replacement or deletion is a
destructive update to the transaction environment even though it is reversible.
`C` is destructive. `transaction-history` is an `S` exception and is also
destructive because an undo followed by a new mutation drops the redo branch.

## Snapshot, Path and Result-Limit Preconditions

Every mutation request includes `expectedSnapshot: SnapshotPrecondition`.
Every query that accepts a `LocationSelector`, `TextSpanSelector`, or a
location-based `SymbolSelector` also includes it. Fresh discovery queries that
only select a document, project or scope do not require it, but may accept it
as an assertion. A mismatch returns `Conflict` with `SnapshotMismatch` and
`ResolveTargetAgain`; the server never reinterprets an old source span against
a newer revision.

`targetPath` fields use `ProjectRelativePath`, relative to the project owning
the source type unless the tool explicitly names a target project. The
coordinator verifies project ownership and the canonical path for every source
document creation, deletion and relocation. Linked or escaping document paths
are rejected for those operations in v1.

Collection results have a deterministic order for one snapshot: source
locations sort by normalised path then span, symbols by fully qualified display
name then location, and other values by documented stable key. `hasMore` means
the ordered result set contained more eligible values than returned. A larger
`maxResults` recomputes from the start; there are deliberately no cursors.
`truncationReasons` reports `ResultLimit` and/or `ResponseByteLimit` where a
collection result is shortened. A non-collection result that cannot be reduced
without changing its meaning is rejected with `ResponseLimitExceeded` and
`NarrowRequest` rather than being silently malformed or partially serialized.

Code-action tokens are stateless revalidation recipes, not handles to cached
provider objects. The server-side `CodeAction` cache has capacity zero; expiry
is controlled by the startup `CodeActionTokenLifetime`, initially five minutes.
`list-code-actions` omits action families that the current server build knows
it cannot execute. The published `unsupported` execution mode is reserved for
visible fallback actions that were discovered successfully but have not yet
been implemented or explicitly hidden in that build.

Every request that executes against a loaded workspace may include
`workspace?: WorkspaceSelector`. When exactly one workspace is loaded, the
selector may be omitted and the server routes the request there. When more than
one workspace is loaded, omitting it is rejected with
`WorkspaceSelectorRequired`.

## Server and Workspace Context (8)

| Tool | Source | Behaviour | Title and description | Input parameters | `data` output shape |
|---|---|---|---|---|---|
| `server-status` | New server | Q | **Server Status**. Returns server diagnostics and unfinished commit recovery state without requiring a workspace. | None. | `ServerStatusData { serverVersion, protocolVersion, roslynVersion, msBuild: ComponentStatus, configuration: ServerConfiguration, toolCount, plugins: PluginStatus[], recovery: RecoveryStatus[] }` |
| `workspace-open` | New server | S | **Open Workspace**. Loads an additional `.sln`, `.slnx` or `.csproj` as a writable workspace session. All loaded C# projects must be SDK style. | `path: string` - absolute solution or project path; `alias?: string` - optional caller-friendly label. | `WorkspaceOpenData { workspace: WorkspaceIdentity, projectCount, documentCount, loadDiagnostics: DiagnosticInfo[] }` |
| `workspace-list` | New server | Q | **Workspace List**. Returns every loaded workspace plus the current global transaction owner, if any. | None. | `WorkspaceListData { workspaces: WorkspaceIdentity[], transactionOwnerWorkspaceId?: string }` |
| `workspace-close` | New server | S | **Close Workspace**. Disposes one selected loaded workspace. An active transaction on that workspace must first be committed or rolled back. | `workspace?: WorkspaceSelector`. | `WorkspaceCloseData { closedPath: string }` |
| `workspace-status` | Replaces `diagnose` | Q | **Workspace Status**. Returns one selected loaded workspace's lifecycle state, project-load diagnostics and transaction capabilities. | `workspace?: WorkspaceSelector`. | `WorkspaceStatusData { state: Ready|TransactionActive|TransactionConflicted|WorkspaceOutOfDate, workspace: WorkspaceIdentity, projectCount: int, documentCount: int, loadDiagnostics: DiagnosticInfo[], transaction?: TransactionInfo, reloadRequired: boolean }` |
| `workspace-reload` | New server | S | **Reload Workspace**. Reloads one selected workspace that is out of date. Unavailable while that workspace owns an active transaction. | `workspace?: WorkspaceSelector`. | `WorkspaceReloadData { workspace: WorkspaceIdentity, projectCount, documentCount, loadDiagnostics: DiagnosticInfo[] }` |
| `get-solution-structure` | New plugin | Q | **Get Solution Structure**. Returns solution folders, projects, target frameworks and direct project relationships. | `includeDocuments?: boolean = false`, `limit?: ResultLimit`. | `SolutionStructureData { solutionPath, folders: SolutionFolderInfo[], projects: ProjectStructureInfo[], returnedCount, hasMore }` |
| `get-project-details` | New plugin | Q | **Get Project Details**. Returns project properties, documents, direct references, analyzers and compilation options. | `project: ProjectSelector`, `includeDocuments?: boolean = true`, `limit?: ResultLimit`. | `ProjectDetailsData { project: ProjectInfo, documents?: DocumentReference[], projectReferences: ProjectReferenceInfo[], metadataReferences: MetadataReferenceInfo[], analyzers: AnalyzerInfo[], compilationOptions: CompilationOptionsInfo, returnedCount?, hasMore? }` |
| `get-document-options` | New plugin | Q | **Get Document Options**. Returns parse options, nullable context, language version, analyzers and editor-config-derived options. | `document: DocumentSelector`. | `DocumentOptionsData { document: DocumentReference, languageVersion, nullableContext, parseOptions: ParseOptionsInfo, analyzerConfig: AnalyzerConfigInfo }` |

## Semantic Inspection and Navigation (19)

| Tool | Source | Behaviour | Title and description | Input parameters | `data` output shape |
|---|---|---|---|---|---|
| `get-document-outline` | Existing | Q | **Get Document Outline**. Returns the semantic namespace, type and member hierarchy of one document. | `document: DocumentSelector`, `includeMembers?: boolean = true`. | `DocumentOutlineData { document: DocumentReference, root: OutlineNode }` |
| `get-code-context` | New | Q | **Get Code Context**. Returns a focused code window and enclosing semantic context. | `location: LocationSelector`, `beforeLines?: int = 10`, `afterLines?: int = 10`, `includeDiagnostics?: boolean = true`. | `CodeContextData { location: ResolvedLocation, text: string, enclosingSymbols: SymbolReference[], diagnostics: DiagnosticInfo[] }` |
| `search-symbols` | Existing | Q | **Search Symbols**. Searches declarations by name, metadata name and optional semantic filters. | `query?: string`, `metadataName?: string`, `scope?: ScopeSelector`, `kinds?: string[]`, `accessibilities?: string[]`, `namespace?: string`, `limit?: ResultLimit`. At least one of `query` or `metadataName` is required. | `SymbolSearchData { symbols: SymbolReference[], returnedCount: int, hasMore: boolean }` |
| `resolve-symbol` | New | Q | **Resolve Symbol**. Resolves the symbol at a location or selection and returns its canonical selector. | `location: LocationSelector`. | `ResolveSymbolData { symbol: SymbolReference, selector: SymbolSelector, declarations: ResolvedLocation[] }` |
| `get-symbol-info` | Existing | Q | **Get Symbol Info**. Returns detailed metadata for a resolved symbol. | `symbol: SymbolSelector`, `includeMembers?: boolean = false`, `includeDocumentation?: boolean = true`. | `SymbolInfoData { symbol: SymbolReference, accessibility, modifiers: string[], type?: TypeInfo, parameters?: ParameterInfo[], returnType?: TypeInfo, documentation?: string, declarations: ResolvedLocation[] }` |
| `get-symbol-members` | New | Q | **Get Symbol Members**. Lists declared members and optionally inherited or explicit-interface members. | `symbol: SymbolSelector`, `includeInherited?: boolean = false`, `includeExplicitInterface?: boolean = false`, `limit?: ResultLimit`. | `SymbolMembersData { symbol: SymbolReference, members: SymbolReference[], returnedCount: int, hasMore: boolean }` |
| `get-symbol-attributes` | New | Q | **Get Symbol Attributes**. Returns declared and inherited attributes with constructor and named arguments. | `symbol: SymbolSelector`, `includeInherited?: boolean = false`, `limit?: ResultLimit`. | `SymbolAttributesData { symbol: SymbolReference, attributes: AttributeInfo[], returnedCount: int, hasMore: boolean }` |
| `go-to-definition` | Existing | Q | **Go To Definition**. Returns source or metadata definitions for a symbol. | `symbol: SymbolSelector`. | `DefinitionData { symbol: SymbolReference, definitions: DefinitionLocation[] }` |
| `find-references` | Existing | Q | **Find References**. Finds source references, optionally including declarations and access classification. | `symbol: SymbolSelector`, `scope?: ScopeSelector`, `includeDefinitions?: boolean = true`, `includeContext?: boolean = true`, `limit?: ResultLimit`. | `ReferenceSearchData { symbol: SymbolReference, references: ReferenceLocation[], returnedCount: int, hasMore: boolean }` |
| `find-callers` | Existing | Q | **Find Callers**. Returns direct source call sites and containing symbols. | `symbol: SymbolSelector`, `scope?: ScopeSelector`, `includeContext?: boolean = true`, `limit?: ResultLimit`. | `CallerSearchData { symbol: SymbolReference, callers: CallerInfo[], returnedCount: int, hasMore: boolean }` |
| `find-callees` | New | Q | **Find Callees**. Returns symbols directly invoked by a method or selected executable body. | `symbol?: SymbolSelector`, `location?: LocationSelector`, `includeIndirect?: boolean = false`, `limit?: ResultLimit`. Exactly one of `symbol` or `location`. | `CalleeSearchData { source: SymbolReference, callees: SymbolReference[], returnedCount: int, hasMore: boolean }` |
| `find-implementations` | Existing | Q | **Find Implementations**. Finds implementations of an interface or abstract member. | `symbol: SymbolSelector`, `scope?: ScopeSelector`, `limit?: ResultLimit`. | `ImplementationSearchData { symbol: SymbolReference, implementations: SymbolReference[], returnedCount: int, hasMore: boolean }` |
| `find-overrides` | New | Q | **Find Overrides**. Finds overrides of a virtual or abstract member. | `symbol: SymbolSelector`, `scope?: ScopeSelector`, `limit?: ResultLimit`. | `OverrideSearchData { symbol: SymbolReference, overrides: SymbolReference[], returnedCount: int, hasMore: boolean }` |
| `find-derived-types` | New | Q | **Find Derived Types**. Finds derived types with optional depth and project filters. | `symbol: SymbolSelector`, `scope?: ScopeSelector`, `maxDepth?: int`, `limit?: ResultLimit`. | `DerivedTypesData { baseType: SymbolReference, derivedTypes: TypeHierarchyNode[], returnedCount: int, hasMore: boolean }` |
| `get-type-hierarchy` | Existing | Q | **Get Type Hierarchy**. Returns base types, implemented interfaces and optional derived types. | `symbol: SymbolSelector`, `includeDerived?: boolean = true`, `maxDepth?: int`, `limit?: ResultLimit`. | `TypeHierarchyData { type: SymbolReference, baseTypes: SymbolReference[], interfaces: SymbolReference[], derivedTypes?: TypeHierarchyNode[], returnedCount?: int, hasMore?: boolean }` |
| `find-overloads` | New | Q | **Find Overloads**. Returns overloads and parameter signatures for a method or constructor. | `symbol: SymbolSelector`, `limit?: ResultLimit`. | `OverloadSearchData { symbol: SymbolReference, overloads: CallableSignature[], returnedCount: int, hasMore: boolean }` |
| `get-partial-declarations` | New | Q | **Get Partial Declarations**. Returns every declaration of a partial type or method. | `symbol: SymbolSelector`, `limit?: ResultLimit`. | `PartialDeclarationsData { symbol: SymbolReference, declarations: ResolvedLocation[], returnedCount: int, hasMore: boolean }` |
| `get-symbol-dependencies` | New | Q | **Get Symbol Dependencies**. Returns symbols, types and assemblies directly used by a symbol. | `symbol: SymbolSelector`, `includeAssemblies?: boolean = true`, `limit?: ResultLimit`. | `SymbolDependenciesData { symbol: SymbolReference, dependencies: DependencyInfo[], returnedCount: int, hasMore: boolean }` |
| `get-symbol-dependents` | New | Q | **Get Symbol Dependents**. Returns symbols that directly depend on a symbol. | `symbol: SymbolSelector`, `scope?: ScopeSelector`, `limit?: ResultLimit`. | `SymbolDependentsData { symbol: SymbolReference, dependents: SymbolReference[], returnedCount: int, hasMore: boolean }` |

## Analysis and Architecture (16)

| Tool | Source | Behaviour | Title and description | Input parameters | `data` output shape |
|---|---|---|---|---|---|
| `get-diagnostics` | Existing | Q | **Get Diagnostics**. Returns compiler and configured analyzer diagnostics for a scope. | `scope?: ScopeSelector`, `severities?: string[]`, `ids?: string[]`, `limit?: ResultLimit`. | `DiagnosticsData { diagnostics: DiagnosticInfo[], returnedCount: int, hasMore: boolean }` |
| `get-code-metrics` | Existing | Q | **Get Code Metrics**. Returns projected logical lines, cyclomatic complexity, nesting depth, type coupling and a derived maintainability score for a symbol or scope. | `scope?: ScopeSelector = Solution`, `symbol?: SymbolSelector`, `includeChildren?: boolean = false`, `limit?: ResultLimit`, `expectedSnapshot?: SnapshotPrecondition` required for location-based symbol selectors. | `CodeMetricsData { metrics: MetricInfo { symbol?: SymbolReference, location?: ResolvedLocation, logicalLines: int, cyclomaticComplexity: int, maxNestingDepth: int, coupling: int, maintainabilityIndex: int }[], returnedCount: int, hasMore: boolean }` |
| `analyze-control-flow` | Existing | Q | **Analyze Control Flow**. Returns reachability, exit paths and return behaviour for a selected executable region. | `location: LocationSelector`. | `ControlFlowAnalysisData { region: ResolvedLocation, entryReachable: boolean, exitReachable: boolean, exits: ControlFlowExit[], returns: ResolvedLocation[] }` |
| `analyze-data-flow` | Existing | Q | **Analyze Data Flow**. Returns reads, writes, data in/out and captured variables for a selected region. | `location: LocationSelector`. | `DataFlowAnalysisData { region: ResolvedLocation, variablesDeclared: SymbolReference[], readInside: SymbolReference[], writtenInside: SymbolReference[], dataFlowsIn: SymbolReference[], dataFlowsOut: SymbolReference[], captured: SymbolReference[] }` |
| `get-operation-tree` | New | Q | **Get Operation Tree**. Returns a compact typed `IOperation` tree for a selected expression, statement or member. | `location: LocationSelector`, `maxDepth?: int = 8`. | `OperationTreeData { root: OperationNode, truncated: boolean }` |
| `get-control-flow-graph` | New | Q | **Get Control Flow Graph**. Returns basic blocks, branches and regions for an executable symbol or body. | `symbol?: SymbolSelector`, `location?: LocationSelector`. Exactly one is required. | `ControlFlowGraphData { owner: SymbolReference, blocks: BasicBlockInfo[], regions: FlowRegionInfo[] }` |
| `find-unused-symbols` | New | Q | **Find Unused Symbols**. Identifies candidate unused locals and members from compiler-unused diagnostics, with confidence reasons. | `scope?: ScopeSelector = Solution`, `includeInternal?: boolean = false`, `excludeGenerated?: boolean = true`, `limit?: ResultLimit`. | `UnusedSymbolsData { candidates: UnusedSymbolCandidate { symbol: SymbolReference, location?: ResolvedLocation, confidence: string, reasons: string[] }[], returnedCount: int, hasMore: boolean }` |
| `find-duplicate-code` | New | Q | **Find Duplicate Code**. Groups identical executable blocks by their normalized statement sequence for review. It is advisory and does not refactor code. | `scope?: ScopeSelector = Solution`, `minimumStatements?: int = 3`, `limit?: ResultLimit`. | `DuplicateCodeData { groups: DuplicateCodeGroup { statementCount: int, occurrences: DuplicateCodeOccurrence { symbol?: SymbolReference, location?: ResolvedLocation, context: string }[] }[], returnedCount: int, hasMore: boolean }` |
| `get-dependency-graph` | New | Q | **Get Dependency Graph**. Builds a bounded dependency graph at a selected granularity. | `scope?: ScopeSelector = Solution`, `granularity: Project|Namespace|Type|Symbol`, `maxDepth?: int = 3`, `maxNodes?: int`, `maxEdges?: int`. | `DependencyGraphData { nodes: GraphNode { id: string, kind: string, displayName: string, symbol?: SymbolReference }[], edges: GraphEdge { fromId: string, fromDisplayName: string, toId: string, toDisplayName: string, kind: string }[], returnedNodeCount: int, returnedEdgeCount: int, truncationReasons: CollectionTruncation[] }` |
| `find-dependency-cycles` | New | Q | **Find Dependency Cycles**. Detects dependency cycles at project, namespace or type granularity. | `scope?: ScopeSelector = Solution`, `granularity: Project|Namespace|Type`, `limit?: ResultLimit`. | `DependencyCyclesData { cycles: DependencyCycle { nodes: GraphNode[] }[], returnedCount: int, hasMore: boolean }` |
| `get-change-impact` | New | Q | **Get Change Impact**. Estimates blast radius using references, callers, overrides, implementations and public surface. | `symbol: SymbolSelector`, `scope?: ScopeSelector`, `limit?: ResultLimit`. | `ChangeImpactData { symbol: SymbolReference, impact: ImpactSummary, locations: ReferenceLocation[], returnedCount: int, hasMore: boolean }` |
| `get-api-surface` | New | Q | **Get API Surface**. Describes exported API symbols for a solution, project, namespace or type. | `scope: ScopeSelector`, `minimumAccessibility?: Public|Protected|Internal = Public`, `includeObsolete?: boolean = true`, `limit?: ResultLimit`. | `ApiSurfaceData { symbols: ApiSymbolInfo[], returnedCount: int, hasMore: boolean }` |
| `get-test-impact` | New | Q | **Get Test Impact**. Identifies likely impacted tests using built-in test-like type and method naming conventions. | `symbol: SymbolSelector`, `testScope?: ScopeSelector = Solution`, `includeReasons?: boolean = true`, `limit?: ResultLimit`. | `TestImpactData { symbol: SymbolReference, tests: TestImpactInfo { test: SymbolReference, location?: ResolvedLocation, reasons: string[] }[], returnedCount: int, hasMore: boolean }` |
| `analyze-nullability` | New | Q | **Analyze Nullability**. Returns nullable-flow issues and unsafe dereferences from compiler nullability diagnostics. | `scope?: ScopeSelector = Solution`, `location?: LocationSelector`, `limit?: ResultLimit`, `expectedSnapshot?: SnapshotPrecondition` required for location-based selectors. | `NullabilityAnalysisData { findings: NullabilityFinding { diagnostic: DiagnosticInfo }[], returnedCount: int, hasMore: boolean }` |
| `analyze-async` | New | Q | **Analyze Async**. Identifies supported async antipatterns using syntax and operation analysis. | `scope?: ScopeSelector = Solution`, `limit?: ResultLimit`. | `AsyncAnalysisData { findings: AsyncFinding { kind: string, symbol?: SymbolReference, location?: ResolvedLocation, message: string }[], returnedCount: int, hasMore: boolean }` |
| `analyze-disposables` | New | Q | **Analyze Disposables**. Identifies candidate undisposed local `IDisposable` or `IAsyncDisposable` values. Findings are advisory. | `scope?: ScopeSelector = Solution`, `limit?: ResultLimit`. | `DisposableAnalysisData { findings: DisposableFinding { kind: string, symbol?: SymbolReference, type?: TypeInfo, location?: ResolvedLocation, message: string }[], returnedCount: int, hasMore: boolean }` |

## Specific Refactorings, Generation and Formatting (28)

Every tool in this group requires `TransactionActive`, acquires the exclusive
workspace operation lease and returns `MutationData` on success:

```text
MutationData
- operation: string
- summary: string
- transaction: TransactionInfo
- preview: MutationPreview
```

Structural tools explicitly state that the target SDK-style project must include
the resulting source file through its own conventions. The server never edits a
project file. Every mutation input below additionally requires
`expectedSnapshot: SnapshotPrecondition`; action-staging inputs supply it both
explicitly and through their snapshot-bound action token.

| Tool | Source | Behaviour | Title and description | Input parameters |
|---|---|---|---|---|
| `move-type-to-file` | Existing | M | **Move Type To File**. Moves a selected type to its own Roslyn-chosen file within the same project, while still requiring the SDK-style project to include the resulting file by convention. Arbitrary caller-selected target paths are not supported. | `type: SymbolSelector`, `preserveNamespace?: boolean = true`, `expectedSnapshot: SnapshotPrecondition`. |
| `move-type-to-namespace` | Existing | M | **Move Type To Namespace**. Not planned for this server while the current Roslyn move-to-namespace path still depends on internal service and options seams. This action family is omitted from `list-code-actions` unless a supported public API path becomes available. | `type: SymbolSelector`, `targetNamespace: string`, `relocateFile?: boolean = false`, `targetPath?: ProjectRelativePath`, `updateUsings?: boolean = true`, `expectedSnapshot: SnapshotPrecondition`. |
| `rename-symbol` | Existing | M | **Rename Symbol**. Renames a resolved symbol and updates references across the effective solution. | `symbol: SymbolSelector`, `newName: string`, `renameOverloads?: boolean = false`, `renameImplementations?: boolean = true`, `renameFile?: boolean = false`. |
| `extract-method` | Existing | M | **Extract Method**. Extracts a valid statement or expression selection through the replay-backed Roslyn backend. | `selection: LocationSelector`, `targetKind?: Method|LocalFunction = Method`. |
| `introduce-variable` | Existing | M | **Introduce Variable**. Stages one supported Roslyn introduce-variable leaf action through the replay-backed backend. | `selection: LocationSelector`, `kind: Local|LocalAllOccurrences|LocalConstant|LocalConstantAllOccurrences|Constant|ConstantAllOccurrences|Field|FieldAllOccurrences|QueryVariable|QueryVariableAllOccurrences`. |
| `extract-interface` | Existing | M | **Extract Interface**. Not planned for this server while the Roslyn implementation still depends on options-service interaction. This action family is omitted from `list-code-actions` unless a supported public API path becomes available. | `type: SymbolSelector`, `interfaceName: string`, `members: SymbolSelector[]`, `targetPath?: ProjectRelativePath`. |
| `extract-base-class` | Existing | M | **Extract Base Class**. Not planned for this server while the Roslyn implementation still depends on options-service interaction. This action family is omitted from `list-code-actions` unless a supported public API path becomes available. | `type: SymbolSelector`, `baseClassName: string`, `members: SymbolSelector[]`, `targetPath?: ProjectRelativePath`, `makeAbstract?: boolean = false`. |
| `introduce-parameter` | Existing | M | **Introduce Parameter**. Promotes a supported expression to a parameter through the replay-backed Roslyn backend. | `selection: LocationSelector`, `allOccurrences?: boolean = false`, `strategy?: UpdateCallSitesDirectly|IntoExtractedMethod|IntoNewOverload = UpdateCallSitesDirectly`. |
| `inline-variable` | Existing | M | **Inline Variable**. Replaces local variable references with its initializer. `removeDeclaration` currently requires the default `true`; `false` is rejected by the replay-backed Roslyn backend. | `symbol: SymbolSelector`, `removeDeclaration?: boolean = true`. |
| `change-signature` | Existing | M | **Change Signature**. Not planned for this server while the required Roslyn feature service remains internal-only. This action family is omitted from `list-code-actions` unless a supported public API path becomes available. | `method: SymbolSelector`, `parameters: ParameterChange[]`, `updateCallSites?: boolean = true`. |
| `encapsulate-field` | Existing | M | **Encapsulate Field**. Encapsulates one field through the replay-backed Roslyn backend. | `field: SymbolSelector`, `updateReferences?: boolean = true`. |
| `convert-to-async` | Existing | M | **Convert To Async**. Not planned for this server while the public Roslyn surface currently exposes only narrow code-fix cases instead of the documented end-state synchronous-to-asynchronous conversion workflow. | `method: SymbolSelector`, `renameToAsync?: boolean = true`, `propagate?: None|ContainingType|Solution = None`. |
| `convert-expression-body` | Existing | M | **Convert Expression Body**. Stages the supported Roslyn block-body or expression-body conversion offered at the selected declaration location. | `selection: LocationSelector`, `expectedSnapshot: SnapshotPrecondition`. |
| `convert-property` | Existing | M | **Convert Property**. Converts a selected property between supported auto-property-to-full-property and full-property-to-auto-property forms, without caller-specified backing-field naming. This contract ships in the contracts project as `ConvertPropertyRequest` with `ConvertPropertyDirection`. | `selection: LocationSelector`, `direction: ToFull|ToAutoWhenSafe`, `expectedSnapshot: SnapshotPrecondition`. |
| `convert-foreach-linq` | Existing | M | **Convert Foreach LINQ**. Stages one supported Roslyn foreach or LINQ conversion through the replay-backed backend. | `selection: LocationSelector`, `conversionKind: ForeachToQuery|ForeachToCallForm|LinqToForeach`. |
| `convert-to-interpolated-string` | Existing | M | **Convert To Interpolated String**. Converts supported concatenation or formatting expressions through the replay-backed Roslyn backend. | `selection: LocationSelector`. |
| `convert-to-pattern-matching` | Existing | M | **Convert To Pattern Matching**. Not planned for this server while the relevant Roslyn fixes still depend on diagnostics that are not surfaced through the server's current public compilation and analyzer diagnostics path. | `selection: LocationSelector`, `expectedSnapshot: SnapshotPrecondition`. |
| `generate-constructor` | Existing | M | **Generate Constructor**. Not planned for this server while the current Roslyn path remains a dialog-backed member-pick flow. This action family is omitted from `list-code-actions` unless a supported public API path becomes available. | `type: SymbolSelector`, `members: SymbolSelector[]`, `accessibility?: Private|Internal|Protected|Public = Public`, `addNullChecks?: boolean = false`. |
| `generate-equals-hashcode` | Existing | M | **Generate Equals And GetHashCode**. Not planned for this server while the required Roslyn feature service remains internal-only. This action family is omitted from `list-code-actions` unless a supported public API path becomes available. | `type: SymbolSelector`, `members: SymbolSelector[]`, `implementEquatable?: boolean = false`. |
| `generate-overrides` | Existing | M | **Generate Overrides**. Not planned for this server while the Roslyn implementation still depends on internal generation APIs. This action family is omitted from `list-code-actions` unless a supported public API path becomes available. | `type: SymbolSelector`, `members: SymbolSelector[]`. |
| `generate-tostring` | Existing | M | **Generate ToString**. Not planned for this server while no supported public Roslyn generation seam has been identified for this workflow in the current build. | `type: SymbolSelector`, `members: SymbolSelector[]`, `format?: string`. |
| `implement-interface` | Existing | M | **Implement Interface**. Not planned for this server while the required Roslyn feature service remains internal-only. This action family is omitted from `list-code-actions` unless a supported public API path becomes available. | `type: SymbolSelector`, `interface: SymbolSelector`, `members?: SymbolSelector[]`, `explicitImplementation?: boolean = false`. |
| `add-null-checks` | Existing | M | **Add Null Checks**. Stages the supported Roslyn parameter null-check refactoring at the selected parameter location. | `selection: LocationSelector`, `expectedSnapshot: SnapshotPrecondition`. |
| `add-missing-usings` | Existing | M | **Add Missing Usings**. Adds imports needed to resolve unbound type references. `preferGlobalUsings` is reserved and currently rejected when set to `true`. | `scope: ScopeSelector`, `preferGlobalUsings?: boolean = false`. |
| `remove-unused-usings` | Existing | M | **Remove Unused Usings**. Removes unused import directives. | `scope: ScopeSelector`. |
| `sort-usings` | Existing | M | **Sort Usings**. Orders import directives using the loaded workspace options. | `document: DocumentSelector`, `systemFirst?: boolean`. |
| `format-document` | Existing | M | **Format Document**. Formats one source document using loaded workspace options. | `document: DocumentSelector`, `range?: TextSpanSelector`. |

## Code Actions and Transaction Control (10)

| Tool | Source | Behaviour | Title and description | Input parameters | `data` output shape |
|---|---|---|---|---|---|
| `list-code-actions` | New plugin | Q | **List Code Actions**. Lists applicable installed refactorings and code fixes at a target, but only for built-in Roslyn families that this server build has explicitly audited. Listed actions publish execution metadata that declares whether they are replayable, parameterised or unsupported. | `location: LocationSelector`, `expectedSnapshot: SnapshotPrecondition`, `includeRefactorings?: boolean = true`, `includeCodeFixes?: boolean = true`, `diagnosticIds?: string[]`, `limit?: ResultLimit`. | `CodeActionListData { actions: CodeActionInfo[], returnedCount: int, hasMore: boolean, truncationReasons?: CollectionTruncation[] }` |
| `describe-code-action` | New plugin | Q | **Describe Code Action**. Revalidates one discovered action and returns its descriptor plus any preflight context required before dedicated execution. | `actionId: string`, `expectedSnapshot: SnapshotPrecondition`. | `DescribeCodeActionData { descriptor: CodeActionInfo, context: CodeActionDescriptorContext }` |
| `stage-code-action` | New plugin | M | **Stage Code Action**. Re-runs the recorded provider and stages exactly one matching replayable refactoring action. Parameterised actions are rejected instead of being replayed generically. | `actionId: string`, `expectedSnapshot: SnapshotPrecondition`. | `MutationData` |
| `stage-code-fix` | New plugin | M | **Stage Code Fix**. Re-runs the recorded provider and stages exactly one matching code fix. | `actionId: string`, `expectedSnapshot: SnapshotPrecondition`. | `MutationData` |
| `stage-fix-all` | New plugin | M | **Stage Fix All**. Re-runs the recorded provider and stages one matching code fix across a selected scope, subject to any configured fix-all cap. | `actionId: string`, `scope: ScopeSelector`, `maxChanges?: int`, `expectedSnapshot: SnapshotPrecondition`. | `MutationData` |
| `transaction-start` | New server | S | **Start Transaction**. Captures the immutable base solution for one selected workspace and opens an empty staged revision journal. It rejects if another workspace already owns the global transaction slot. | `workspace?: WorkspaceSelector`. | `TransactionStartData { transaction: TransactionInfo }` |
| `transaction-preview` | Replaces `get-change-set` | Q | **Preview Transaction**. Returns transaction summaries, or a detailed diff for one explicitly selected document. | `document?: DocumentSelector`, `includeDiff?: boolean = false`, `contextLines?: int = 3`. | `TransactionPreviewData { transaction: TransactionInfo, documents: DocumentChange[], diff?: DocumentDiff }` |
| `transaction-history` | New server | S | **Transaction History**. Moves the staged revision backward or forward. | `direction: Undo|Redo`, `expectedSnapshot: SnapshotPrecondition`. | `TransactionHistoryData { transaction: TransactionInfo }` |
| `transaction-commit` | Replaces `apply-change-set` | C | **Commit Transaction**. Rechecks the final source-file manifest and durably applies the staged transaction to disk. It does not compile the solution or modify project files. | `expectedSnapshot: SnapshotPrecondition`. | `TransactionCommitData { committed: boolean, transaction?: TransactionInfo }` |
| `transaction-rollback` | Replaces `discard-change-set` | C | **Rollback Transaction**. Discards staged revisions without writing files. A conflicted transaction leaves the workspace requiring reload. | None. | `TransactionRollbackData { state: Ready|WorkspaceOutOfDate }` |

## Runtime Validation Rules

The JSON Schema records the shape of tool arguments. The tool executor still
performs the following runtime checks:

- The non-waiting shared-query or exclusive-operation lease is available.
- The workspace lifecycle state permits the requested operation.
- A mutation has an active, non-conflicted transaction with remaining revision
  capacity.
- Query tools resolve against the staged solution while a transaction is
  active. In `TransactionConflicted`, normal query and mutation work is
  rejected; only `transaction-preview` and `transaction-rollback` remain
  available.
- A required snapshot precondition matches the effective solution before a
  prior location, symbol or action token is used.
- Selectors resolve uniquely against the current effective solution.
- The relevant workspace-input manifest is fresh before semantic work. File
  watcher notifications are a dirty hint only; manifest verification is the
  authority.
- A candidate solution contains only allow-listed regular source-document text
  changes and validated regular source-document create/delete operations. All
  project, reference, option, analyzer, additional-document, and document
  metadata changes are rejected.
- Every created, deleted or relocated document has a canonical owned path.
- An action token is rejected with `ActionExpired` when its expiry or snapshot
  has passed, or with `ActionAmbiguous` when re-running its provider does not
  yield exactly one matching action. The server retains no provider-created
  `CodeAction` instances between requests.
- Tool-specific Roslyn semantic preconditions are satisfied.

## Cancellation and Commit Recovery

Queries and candidate-solution construction honour cancellation before they
alter server state. Revision staging, rollback, and transaction-history have a
short state-transition boundary: a cancellation observed before that boundary
leaves state unchanged; one observed after it does not undo the completed
transition. The client can confirm the resulting revision through
`workspace-status`.

Stage 5 commit durability is currently represented by server-owned recovery
status records under `StateDirectory/recovery`. Commit writes `Prepared`, then
`Applying`, and removes the record on success. If file application fails with
an I/O or access error, the server records `RecoveryIncomplete`, returns
`Faulted`, and exposes `ResolveRecovery`. `server-status` reports all remaining
recovery records, and `workspace-open` refuses to open a solution that still
has an unresolved recovery record. Automatic restore from durable per-file
backups is not part of the current Stage 5 implementation.

## Original Server Mapping

The `Existing` tools retain the conceptual capability from
`JoshuaRamirez/RoslynMcpServer`. Its request DTOs used absolute solution and
source-file paths plus optional line/column and `preview` arguments. This server
replaces those with one loaded workspace, shared selectors, staged mutations
and common structured results. Its retained `maxResults` behaviour informs the
optional `ResultLimit` design, but the unbounded defaults are not retained.

## Sources

- [RoslynMcpServer tool registry](https://github.com/JoshuaRamirez/RoslynMcpServer/blob/master/src/RoslynMcp.Cli/ToolRegistry.cs)
- [RoslynMcpServer README tool inventory](https://github.com/JoshuaRamirez/RoslynMcpServer/blob/master/README.md)
- [MCP C# SDK tools](https://csharp.sdk.modelcontextprotocol.io/concepts/tools/tools.html)
- [MCP C# SDK tool metadata](https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.Server.McpServerToolAttribute.html)
