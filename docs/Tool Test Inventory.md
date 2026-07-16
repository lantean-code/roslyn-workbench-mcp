# Tool Test Inventory

Date: 2026-07-15

## Purpose

This inventory records current test ownership after the integration-test reorganisation. Unit projects own each tool's request handling, collaborator interaction, reachable branches and Roslyn algorithm behaviour. Integration projects prove shared runtime capabilities and boundaries; they do not provide a duplicate one-class-per-tool matrix.

The current policy is recorded in `TestingStrategy.md`. The current architecture and outstanding cross-project findings are recorded in `TestArchitectureReaudit-2026-07-10.md`.

## Unit Ownership

| Tool family | Unit project | Current position |
| --- | --- | --- |
| Workspace contracts and selectors | `Roslyn.Workbench.Mcp.Workspace.Test` | Workspace selector validation and domain behaviour |
| Inspection contracts and collection limits | `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Inspection DTO validation alongside owning tools |
| MCP envelopes, schemas and lifecycle contracts | `Roslyn.Workbench.Mcp.Test` | Host-owned serialisation, binding, schema and transport behaviour |
| Plugin execution and configuration | `Roslyn.Workbench.Mcp.Plugins.Test` | Fluent configuration, handler inspection, materialisation, typed visitor and context-adaptation behaviour |
| Inspection and normal refactoring tools | `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Per-tool unit coverage and Roslyn algorithm branches |
| Code-action tools and workflows | `Roslyn.Workbench.Mcp.CodeActions.Test` | Isolated catalogue, discovery, token, workflow and tool behaviour |
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
| Code Action list/describe orchestration and action-info projection | `ListCodeActionsToolTests`, `DescribeCodeActionToolTests`, `CodeActionInfoFactoryTests` | Covered; 100% line and branch coverage, including deterministic ordering, classification, token projection and rejection paths |
| Plugin adaptation of neutral Workspace contexts and failures | `PluginExecutionContextFactoryTests`, `PluginExecutionContextTests` | Covered |
| Code Action adaptation of neutral Workspace contexts and failures | `CodeActionExecutionContextFactoryTests`, `CodeActionExecutionContextTests` | Covered; contexts expose Workspace execution state only and handlers receive stable services through constructor injection |
| Code Action candidate identity and replay, fix-all, scoped-fix and location-fix services | `CodeActionCandidateIdentityTests`, `CodeActionReplayServiceTests`, `CodeActionFixAllServiceTests`, `CodeActionScopedFixServiceTests`, `CodeActionLocationFixServiceTests` | Covered; 100% line and branch coverage, including value-based duplicate candidate handling and every scoped application path |
| Stager separate from Workspace handler context | `WorkspaceExecutionLeaseTests` | Covered at lease boundary |
| Reserved Code Action name disables a colliding plugin | `PluginDiscoveryAndMcpToolIntegrationTests` | Covered |
| Host constructs all tool families | `HostCompositionIntegrationTests` | Covered at composition boundary |
| CodeActions excluded from plugin discovery/status | Separate Code Action catalogue and status mapping tests | Covered behaviourally; keep explicit when status tests change |
| Forbidden production dependency directions | Manual project-reference inspection | Automate with a project-reference/build check in a later architecture-test round |

## Integration Capability Ownership

| Capability or boundary | Integration suite | Representative coverage |
| --- | --- | --- |
| Workspace projection | `WorkspaceProjectionIntegrationTests` | Solution structure, project details and document options through a real workspace |
| Default project structure | `DefaultProjectStructureServiceIntegrationTests` | Real MSBuild target-framework and solution-hierarchy success, empty, malformed, missing and cancellation outcomes; consuming tool unit tests cover retryable failure mapping |
| Semantic inspection | `SemanticInspectionIntegrationTests` | Diagnostics, operation trees and control-flow behaviour |
| Cross-project search | `SolutionSearchIntegrationTests` | Implementations, references, callers, derived types and dependency relationships |
| Selector and snapshot semantics | `SelectorAndSnapshotIntegrationTests` | Resolution, search, metadata, bounded results and stale snapshots |
| Mutation staging | `MutationPipelineIntegrationTests` | Rename, formatting, using changes, preview and transaction staging |
| Controlled code actions | `ControlledProviderWorkflowIntegrationTests` | List, describe, stage, fix-all, token and snapshot workflows |
| Built-in code actions | `BuiltInCodeActionStagingIntegrationTests` | Representative built-in provider staging |
| Code-action composition | `MefCodeActionProviderCatalogIntegrationTests` | Provider catalogue composition and discovery |
| Host composition | `HostCompositionIntegrationTests` | Configuration projection, dependency injection and MCP tool registration |
| Plugin discovery and MCP protocol | `PluginDiscoveryAndMcpToolIntegrationTests`, `RepresentativeMcpToolIntegrationTests` | Fixture assembly loading, metadata, schemas, argument binding and structured results |
| Host lifecycle | `WorkspaceLifecycleMcpIntegrationTests`, `ServerStatusRecoveryIntegrationTests` | Workspace/transaction MCP flow and persisted recovery diagnostics |
| Built-in compatibility governance | `Roslyn.Workbench.Mcp.CodeActions.AuditTest` | Provider ledger, matching and replay-wrapper compatibility |

## Known Partial Branch Coverage

The following entries remain partial pending a later coverage-focused round. They are unit-coverage concerns, not reasons to restore deleted per-tool integration tests.

- `FindUnusedSymbolsTool`: reachable accessibility-filter combinations in `ShouldIncludeSymbol(...)`.
- `GetApiSurfaceTool`: additional declared-symbol, accessibility and attribute combinations.
- `GetCodeMetricsTool`: unreachable delegate/nesting paths and a defensive missing-source-location guard need an explicit implementation decision.
- `GetControlFlowGraphTool`: defensive missing syntax-root or semantic-model handling is not reachable through the supported public flow.
- `GetDiagnosticsTool`: the null arm of `DiagnosticComparer.Equals(...)` is defensive and not reached with Roslyn's non-null diagnostics.
- `GetDocumentOptionsTool`: a non-null, non-C# parse-options fixture is not covered.
- `GetOperationTreeTool`: defensive missing syntax-root or semantic-model handling is not reachable through the supported public flow.
- `RenameSymbolTool`: the same-solution no-change branch is not reachable for a valid source symbol.
- `SortUsingsTool`: defensive null handling for `UsingDirectiveSyntax.Name` is not reachable with parsed or factory-created directives.

These exceptions should be reassessed against an assembly-level coverage report. Approved unreachable defensive branches should be documented rather than exercised through reflection or artificial production hooks.

The Host coverage round also identified deliberate integration boundaries in `Program`, `MsBuildRegistrationService`, `RecoveryStatusReader` and `RoslynWorkbenchHostApplicationBuilderExtensions`. `MsBuildRegistrationService` owns its cached state as the registered DI singleton and handles the ordinary already-registered state explicitly; actual locator discovery, registration failures and the external registration race remain integration boundaries. `PluginCatalogLoader` now has focused unit coverage for orchestration, candidate preparation, collision policy and materialisation, with real MEF and load-context behaviour retained as integration concerns. Defensive assembly-version fallbacks in `ServerStatusService` and MCP SDK schema-exporter compatibility paths in `ToolSchemaBuilder` cannot be driven through the supported unit surface and remain documented rather than forcing production hooks solely for coverage.
