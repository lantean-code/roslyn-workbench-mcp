# Tool Test Inventory

Date: 2026-07-18

> **Current note (2026-08-11):** The historical coverage ledgers below predate removal of `GetCodeMetricsTool` and replacement of `AnalyzeAsyncTool`'s custom heuristics with bundled AsyncFixer01–AsyncFixer06 analysis plus compiler diagnostic CS4014. Current async coverage is owned jointly by `AnalyzeAsyncToolTests`, `AnalyzerDiagnosticServiceTests`, `CompilerDiagnosticServiceTests` and `SemanticInspectionIntegrationTests`; retained metrics rows document the code that was tested before its removal.

## Purpose

This inventory records current test ownership after the integration-test reorganisation. Unit projects own each tool's request handling, collaborator interaction, reachable branches and Roslyn algorithm behaviour. Integration projects prove shared runtime capabilities and boundaries; they do not provide a duplicate one-class-per-tool matrix.

The current policy is recorded in `TestingStrategy.md`. The implemented architecture and final cross-project findings are recorded in `TestArchitectureReaudit-2026-07-18.md`.

## Unit Ownership

| Tool family | Unit project | Current position |
| --- | --- | --- |
| Workspace contracts and selectors | `Roslyn.Workbench.Mcp.Workspace.Test` | Workspace selector validation and domain behaviour |
| Inspection contracts and result limits | `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Inspection DTO and bounded-result validation alongside owning tools |
| MCP envelopes, schemas and lifecycle contracts | `Roslyn.Workbench.Mcp.Test` | Host-owned serialisation, binding, schema and transport behaviour |
| Plugin execution and configuration | `Roslyn.Workbench.Mcp.Plugins.Test` | Fluent configuration, handler inspection, materialisation, typed visitor and context-adaptation behaviour |
| Inspection and normal refactoring tools | `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Per-tool unit coverage and Roslyn algorithm branches |
| Code Action tools and workflows | `Roslyn.Workbench.Mcp.CodeActions.Test` | Isolated composition, exception policy, discovery, replay-reference, Fix All, staging and tool behaviour |
| Server-owned tools | `Roslyn.Workbench.Mcp.Test` | Tool mapping and mock-isolated host service behaviour |

Every production tool currently has dedicated unit coverage in its owning unit project. A tool is not required to have a same-named integration class.

## Execution-Path Ownership

Tool-handler coverage and MCP transport coverage are separate responsibilities:

| Execution path | Handler and context owner | Host transport position |
| --- | --- | --- |
| Plugin query | `Plugins.Test` and `Plugins.Core.Test` | `PluginQueryMcpServerToolTests` covers typed binding, acquisition, all handler outcomes, publication, malformed input, cancellation, exceptions and disposal at 100% line and branch coverage |
| Plugin mutation | `Plugins.Test` and `Plugins.Core.Test` | `PluginMutationMcpServerToolTests` covers acquisition, handler outcomes, separate staging, publication, malformed input, handler/stager cancellation and exceptions, and disposal at 100% line and branch coverage |
| Code Action query | `CodeActions.Test` | `CodeActionQueryMcpServerToolTests` covers typed binding, acquisition, all handler outcomes, publication, malformed input, cancellation, exceptions and disposal at 100% line and branch coverage |
| Code Action mutation | `CodeActions.Test` | `CodeActionMutationMcpServerToolTests` covers acquisition, handler outcomes, separate staging, publication, malformed input, handler/stager cancellation and exceptions, and disposal at 100% line and branch coverage |

All four Host adapter families now have focused unit evidence without moving MCP concerns into Plugins or CodeActions.

## Boundary-Regression Inventory

| Boundary | Current evidence | Position |
| --- | --- | --- |
| Plugin fluent configuration, categorised and accumulated handler diagnostics, preparation and closed-generic visitor dispatch | `PluginConfigurationTests`, `PluginHandlerTypeInspectorTests`, `PluginHandlerContractResolverTests`, `PluginHandlerWarningInspectorTests`, `PluginConfigurationPreparerTests`, `PluginToolRegistrationMaterializerTests` | Covered |
| Plugin catalogue preparation, atomic validation failure, diagnostic publication, collision policy and materialisation | `PluginCandidatePreparerTests`, `PluginEntryPointValidatorTests`, `LoadedPluginPreparerTests`, `PluginCollisionPolicyTests`, `PluginCatalogEntryMaterializerTests`, `PluginCatalogLoaderTests` | Covered |
| Plugin query MCP adapter | `PluginQueryMcpServerToolTests` | Covered; 100% line and branch coverage |
| Plugin mutation MCP adapter and separate staging | `PluginMutationMcpServerToolTests` | Covered; 100% line and branch coverage |
| Code Action closed-generic visitor dispatch and duplicate internal names | `CodeActionToolRegistryTests` | Covered |
| Code Action provider matching, nested-action flattening and exact-span diagnostic grouping | `CodeActionDiscoveryServiceTests` | Covered; 100% line and branch coverage without exception-driven registration retries |
| Code Action query MCP adapter | `CodeActionQueryMcpServerToolTests` | Covered; 100% line and branch coverage |
| Code Action mutation MCP adapter and separate staging | `CodeActionMutationMcpServerToolTests` | Covered; 100% line and branch coverage |
| Code Action discovery, Fix All preparation and concise action projection | `ListCodeActionsToolTests`, `PrepareFixAllToolTests`, `CodeActionInfoFactoryTests` | Covered, including deterministic ordering, document/selection/caret discovery, diagnostic context, reference projection, impact bounds and rejection paths |
| Plugin adaptation of neutral Workspace contexts and failures | `PluginExecutionContextFactoryTests`, `PluginExecutionContextTests` | Covered |
| Code Action adaptation of neutral Workspace contexts and failures | `CodeActionExecutionContextFactoryTests`, `CodeActionExecutionContextTests` | Covered; contexts expose Workspace execution state only and handlers receive stable services through constructor injection |
| Code Action replay identity, exact rediscovery, prepared Fix All resolution and source-only candidate validation | `CodeActionResolverTests`, `PreparedFixAllResolverTests`, `CodeActionEvaluatorTests` | Covered, including expiry, stale and ambiguous references, exact candidate identity, supported Fix All scopes and rejected operation shapes |
| Stager separate from Workspace handler context | `WorkspaceExecutionLeaseTests` | Covered at lease boundary |
| Reserved Code Action name disables a colliding plugin | `PluginPackageDiscoveryIntegrationTests` | Covered with a real fixture package |
| Host constructs all tool families | `HostCompositionIntegrationTests` | Covered at composition boundary |
| Code Actions excluded from plugin discovery/status | Separate Code Action composition and status mapping tests | Covered behaviourally; keep explicit when status tests change |
| Forbidden production dependency directions | Manual project-reference inspection | Automate with a project-reference/build check in a later architecture-test round |

## Integration Capability Ownership

| Capability or boundary | Integration suite | Representative coverage |
| --- | --- | --- |
| Workspace projection | `WorkspaceProjectionIntegrationTests` | Solution structure, project details and document options through a real workspace |
| Solution hierarchy | `SolutionHierarchyServiceIntegrationTests` | Real solution-persistence success, empty, malformed, missing and cancellation outcomes; consuming tool unit tests cover retryable failure mapping |
| Project target frameworks | `ProjectTargetFrameworkServiceIntegrationTests` | Real MSBuild evaluation of imported, single, absent, malformed and missing target-framework inputs |
| Semantic inspection | `SemanticInspectionIntegrationTests` | General and async compiler diagnostics, operation trees and control-flow behaviour |
| Cross-project search | `SolutionSearchIntegrationTests` | Implementations, references, callers, derived types and dependency relationships |
| Selector and snapshot semantics | `SelectorAndSnapshotIntegrationTests` | Resolution, search, metadata, bounded results and stale snapshots |
| Mutation staging | `MutationPipelineIntegrationTests` | Rename, formatting, using changes, preview and transaction staging |
| Controlled Code Actions | `ControlledProviderWorkflowIntegrationTests` | Discovery, concise projection, single-action staging, Fix All preparation/staging, reference and snapshot workflows |
| Built-in code actions | `BuiltInCodeActionStagingIntegrationTests` | Representative built-in provider staging |
| Code-action composition | `MefCodeActionCompositionIntegrationTests` | Provider composition and discovery |
| Host composition | `HostCompositionIntegrationTests` | Configuration projection, dependency injection and MCP tool registration |
| Plugin package discovery | `PluginPackageDiscoveryIntegrationTests`, `PluginAssemblyMetadataReaderIntegrationTests`, `PluginAssemblyLoadContextIntegrationTests`, `MefPluginComposerIntegrationTests` | Fixture assembly loading, PE metadata, private dependency routing, MEF composition, collisions and failure isolation |
| MCP protocol and published workflows | `PublishedHostProtocolIntegrationTests`, `WorkspaceWorkflowIntegrationTests`, `ExternalPluginWorkflowIntegrationTests`, `CodeActionWorkflowIntegrationTests` | Real stdio initialisation, tool/schema publication, query, plugin mutation, three-tool Code Action workflow and external plugin invocation |
| Host lifecycle and recovery | `PublishedHostLifetimeIntegrationTests`, `StartupAndRecoveryWorkflowIntegrationTests`, `ServerStatusRecoveryIntegrationTests` | End-of-stdin lifetime, restart/configuration fallback and persisted recovery diagnostics |
| Built-in compatibility governance | `Roslyn.Workbench.Mcp.CodeActions.AuditTest` | Exact provider composition, exception policy, diagnostic mapping, mixed-provider leaves and replay compatibility |

## Acceptance Ownership

Acceptance remains representative rather than per-tool. The published-host suite covers prerequisite diagnostics, startup stderr, tool/schema publication, Workspace lifecycle and semantic queries, plugin transactions, the complete three-tool Code Action workflow, durable create-and-replace commit, external plugins, restart/recovery diagnostics and end-of-stdin process lifetime. Ordinary provider compatibility and branch outcomes remain with the owning Unit/Contract, Integration and Audit projects.

## Partial Branch Reassessment

The 2026-07-18 coverage-focused round is complete. A fresh `Roslyn.Workbench.Mcp.Plugins.Core.Test` Coverlet run passed 272 tests. The raw Plugins.Core assembly result moved from 78.12% line/67.44% branch coverage to 78.17% line/68.27% branch coverage. These assembly-wide percentages include contracts, registrations, compiler-lowered conditions and defensive Roslyn guards; they are evidence for investigation rather than a release threshold.

| Tool | Reassessment and disposition |
| --- | --- |
| `FindUnusedSymbolsTool` | Focused coverage now proves `Internal`, `ProtectedOrInternal` and `ProtectedAndInternal` inclusion plus private/local inclusion and protected/public exclusion. Remaining uncovered paths require a selected C# document to lose its syntax root or semantic model, or a source diagnostic candidate to lose its source projection. Those states cannot be produced through the supported resolver and Roslyn document flow and are approved defensive guards. |
| `GetApiSurfaceTool` | Existing tests cover every supported declaration family, all three thresholds, containing-type accessibility, obsolete inclusion/exclusion and malformed local declarations. Remaining alternatives require an unattached variable declarator, a null attribute class or `NotApplicable` accessibility on a declaration family admitted by the tool. They are approved defensive Roslyn-shape guards. |
| `GetCodeMetricsTool` | The audit found real behaviour gaps: delegate declarations were supported by projection but excluded from selection, and nesting traversal stopped at non-nesting container nodes. Production traversal now descends through all syntax nodes while incrementing only supported nesting statements, delegate selection is enabled, and one focused test covers every supported nesting statement kind. File branch coverage improved from 113/126 to 128/130. The remaining field-parent and missing-source-location alternatives protect Roslyn symbol invariants and are approved defensive guards. |
| `GetControlFlowGraphTool` | Missing syntax-root or semantic-model paths cannot occur after the resolver returns a source location in a supported C# document. The successful resolution also structurally contains both the node and semantic model. `ControlFlowGraphResolver` derives the enclosing symbol from its node and semantic model, preventing callers from supplying a mismatched owner. Independent resolver coverage exercises all six real C# executable-root shapes, unsupported and operation-free targets, method/local-function/lambda nesting, anonymous functions stored in block operations and branch values, and standalone parameter-initializer graphs owned by local or anonymous functions. A derived nested symbol absent from the current graph therefore retains that standalone graph instead of attempting invalid descent. The remaining alternatives protect nullable Roslyn nested-graph results and a future unsupported operation root; neither can arise through the current supported flow with real Roslyn objects. These guards remain because Roslyn services are external boundaries; fake Roslyn objects or test-only production seams are not justified. |
| `GetDiagnosticsTool` | `Distinct` receives non-null Roslyn diagnostics, so the comparer null arm is not reachable. Negative equality alternatives after an equal combined hash depend on incidental hash collisions rather than a supported diagnostic outcome. The null and collision-only paths are approved defensive comparer behaviour. |
| `GetDocumentOptionsTool` | The Host supports C# SDK-style projects. C# options and the missing-language-services fallback are covered; a non-null non-C# `ParseOptions` instance cannot enter the supported tool flow and is approved as an out-of-scope defensive projection. |
| `GetOperationTreeTool` | Missing syntax-root or semantic-model paths cannot occur after resolving a source location in a supported C# document, and a successful internal resolution structurally contains both values. The guards are approved external-boundary defences. |
| `RenameSymbolTool` | For a valid source symbol, Roslyn's renamer returns a candidate `Solution`; the reference-equal no-change result cannot be produced through the supported API flow. The guard remains as defensive handling for future Roslyn behaviour and is approved without a fake symbol or test hook. |

No reachable case from the previous nine-entry partial-branch inventory remains open. Raw condition coverage can report compiler-lowered nullable and ordering fallback alternatives; those are not separate supported behaviours and must not be forced through reflection, fake Roslyn runtime objects or production test hooks.

## Comprehensive Tool Coverage Ledger

The same 2026-07-18 report was audited across every `*Tool.cs` file. The table records raw uncovered executable lines and branch counts after the final test changes. `Covered` means the supported alternative now has direct evidence. `Approved defensive` means the remaining raw alternatives require a state that cannot enter through the supported C# Workspace, resolver or Roslyn API flow.

| Tool | Raw lines | Raw branches | Final disposition |
| --- | --: | --: | --- |
| `AnalyzeAsyncTool` | 2 | 42/50 | All four supported Task/ValueTask return families, awaited and unawaited calls, non-task calls and missing executable bodies are covered. Remaining alternatives require a method invocation without a Roslyn named return type or absent projected ordering fields. Approved defensive. |
| `AnalyzeControlFlowTool` | 11 | 26/34 | Snapshot, selector, location, source-document, no-statement, successful analysis and null return projection are covered. Remaining lines are successful-result invariants, Roslyn's nullable analysis-result guard and missing C# syntax/semantic services after source resolution. Approved defensive. |
| `AnalyzeDataFlowTool` | 11 | 26/34 | Snapshot, selector, location, source-document, no-statement and successful analysis outputs are covered. Remaining lines are successful-result invariants, Roslyn's nullable analysis-result guard and missing C# syntax/semantic services after source resolution. Approved defensive. |
| `AnalyzeDisposablesTool` | 0 | 48/56 | Method, local-function and top-level disposable locals; using declarations/statements; synchronous and asynchronous disposal; non-disposable values; ordering and bounds are covered. The top-level case was added in this round. Remaining alternatives are nullable projection fallbacks and impossible ancestor/body combinations for a local declaration. Approved defensive. |
| `FindCalleesTool` | 24 | 86/98 | Symbol/location and depth validation, every supported executable-body syntax, direct and depth-bounded indirect traversal, foreign declarations and non-executable targets are covered. Remaining lines guard absent Roslyn semantic/operation results, impossible successful-result shapes, missing enclosing symbols and an internally unreachable null selector. Approved defensive. |
| `FindCallersTool` | 0 | 18/22 | Caller discovery, document filtering, optional context and ordered projection are covered. Remaining alternatives require a source caller location without a source tree/current document or a resolved location without ordering fields. Approved defensive. |
| `FindDuplicateCodeTool` | 4 | 34/38 | Supported/unsupported documents, minimum statement bounds, all supported executable block types, skipped projections, ordering and bounds are covered. A qualifying statement sequence cannot normalise to blank, and a source executable block has an enclosing symbol. Approved defensive. |
| `FindReferencesTool` | 4 | 54/62 | Definitions, context, pre-enrichment result bounds, filtered projections and read/write classification for assignment, increment/decrement, `ref` and `out` are covered. Remaining lines require a source `ReferenceLocation` without a document or syntax root. Approved defensive. |
| `FindUnusedSymbolsTool` | 2 | 36/48 | Local, private, internal, protected, protected-internal, private-protected and public accessibility outcomes are covered. Missing C# syntax/semantic services and nullable candidate ordering projections cannot arise after a supported compiler diagnostic resolves to its source document. Approved defensive. |
| `GetApiSurfaceTool` | 2 | 59/62 | All supported declaration families, thresholds, containing accessibility, explicit ordered projection and obsolete alternatives are covered. Remaining lines require `NotApplicable` accessibility on an admitted declaration; remaining alternatives require an unattached variable declarator or null attribute class. Approved defensive. |
| `GetChangeImpactTool` | 0 | 40/46 | Reference, caller, override, implementation, private-surface, filtered-location and pre-enrichment result-bound behaviour is covered. Remaining alternatives require a source reference without a document or semantic model. Approved defensive. |
| `GetCodeContextTool` | 23 | 34/72 | Snapshot/location failures, code windows, enclosing symbols and real diagnostics are behaviourally covered. Some projection lambda sequence points remain reported as uncovered despite the diagnostic assertions executing them. Other alternatives are successful-result invariants, comparer null/hash-collision paths and nullable ordering fallbacks. Approved defensive/tooling artefact. |
| `GetCodeMetricsTool` | 2 | 118/122 | Delegate selection, explicit candidate deduplication and ordering, bounded metric projection and traversal through every supported nesting statement are covered. Remaining lines require a declared source symbol without a source location; remaining alternatives require a field declarator without its Roslyn parent chain. Approved defensive. |
| `GetControlFlowGraphTool` | 0 | 20/20 | Symbol and location modes, unsupported targets, exceptional regions, bounded blocks/regions, explicit operation projection and location failures are covered. Covered. |
| `GetDependencyGraphTool` | 0 | 20/20 | Validation, graph construction, node/edge bounds and all included/excluded edge short-circuit alternatives are covered. Covered. |
| `GetDiagnosticsTool` | 2 | 48/60 | Project/document scopes, compiler/analyser diagnostics, filters, ordering, bounds and duplicate removal are covered. Remaining alternatives are comparer null/hash-collision paths and nullable ordering/projection fallbacks. Approved defensive. |
| `GetDocumentOptionsTool` | 0 | 9/10 | C# options and the missing-language-services fallback are covered. A non-null non-C# `ParseOptions` instance cannot enter the supported C# Host flow. Approved out-of-scope defensive. |
| `GetOperationTreeTool` | 7 | 33/40 | Snapshot/location failures, missing operations, child-operation fallback, constants and depth truncation are covered. Remaining lines protect successful-result invariants and missing C# syntax/semantic services after source resolution. Approved defensive. |
| `GetPartialDeclarationsTool` | 0 | 4/6 | Resolution, skipped projections, ordering and bounds are covered. Remaining alternatives require a retained resolved source location without document/span ordering fields. Approved defensive. |
| `GetProjectDetailsTool` | 2 | 44/50 | Documents, project/metadata references, analysers, explicit ordering stages, nullable document projection, bounds and target-framework failures are covered through the public tool flow. The remaining lines require a Roslyn project reference to remain after its target project has disappeared; remaining alternatives are that defensive branch plus path/display fallbacks owned by real MSBuild/analyser integration shapes. Approved defensive and boundary coverage. |
| `GetSymbolAttributesTool` | 0 | 16/20 | Declared/inherited attributes, bounds and null constructor/named argument values are covered. Remaining alternatives require Roslyn `AttributeData` with no attribute class or a typed constant without a type. Approved defensive. |
| `GetSymbolDependenciesTool` | 2 | 64/66 | Signature and operation dependencies across methods, properties, fields, named types, local functions, accessors and anonymous functions are covered. Remaining lines require a resolved C# source declaration without a semantic model or a nullable ordering projection. Approved defensive. |
| `GetSymbolDependentsTool` | 2 | 11/14 | Resolution, document filtering, recursion/self-exclusion, ordering and bounds are covered. Remaining lines require SymbolFinder to return a source reference without its document or a supported document without a semantic model. Approved defensive. |
| `GetSymbolInfoTool` | 0 | 10/12 | Method/type metadata, documentation options, skipped locations and ordered source declarations are covered. Remaining alternatives require a retained resolved source location without document/span ordering fields. Approved defensive. |
| `GetSymbolMembersTool` | 0 | 15/16 | Invalid targets, declared/inherited/interface members, implicit filtering, deduplication and ordering are covered. The remaining alternative is the empty-path fallback for a projected member location. Approved defensive ordering fallback. |
| `GoToDefinitionTool` | 0 | 8/12 | Source and metadata definitions, skipped null projections and ordering are covered. Remaining alternatives require a retained definition with missing document/span ordering fields. Approved defensive. |
| `RenameSymbolTool` | 2 | 5/6 | Resolution, name validation and successful rename are covered. Roslyn does not return the original `Solution` reference for a valid source rename; the no-change arm is retained for future Roslyn behaviour. Approved defensive. |
| `ResolveSymbolTool` | 0 | 12/14 | Snapshot/location/symbol failures, source selectors/declarations and metadata fallback are covered. Remaining alternatives require a retained resolved source location without document/span ordering fields. Approved defensive. |
| `SearchSymbolsTool` | 0 | 41/44 | Query and metadata-name modes, kind/accessibility/namespace filters, global namespace, missing projections, ordering and bounds are covered. Remaining alternatives are the post-validation missing-pattern invariant and a null containing namespace that Roslyn symbols do not expose. Approved defensive. |

The comprehensive ledger contains no untested supported tool behaviour known from the fresh report. Future production changes must add behaviour-focused coverage, and Roslyn/MSBuild upgrades should rerun this ledger before comparing performance results.

The Host coverage round also identified deliberate integration boundaries in `Program`, `MsBuildRegistrationService`, `RecoveryStatusReader` and `RoslynWorkbenchHostApplicationBuilderExtensions`. `MsBuildRegistrationService` owns its cached state as the registered DI singleton and handles the ordinary already-registered state explicitly; actual locator discovery, registration failures and the external registration race remain integration boundaries. `PluginCatalogLoader` now has focused unit coverage for orchestration, candidate preparation, collision policy and materialisation, with real MEF and load-context behaviour retained as integration concerns. Defensive assembly-version fallbacks in `ServerStatusService` and MCP SDK schema-exporter compatibility paths in `ToolSchemaBuilder` cannot be driven through the supported unit surface and remain documented rather than forcing production hooks solely for coverage.
