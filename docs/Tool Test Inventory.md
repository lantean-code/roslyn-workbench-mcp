# Tool Test Inventory

Date: 2026-07-10

## Purpose

This inventory records current test ownership after the integration-test reorganisation. Unit projects own each tool's request handling, collaborator interaction, reachable branches and Roslyn algorithm behaviour. Integration projects prove shared runtime capabilities and boundaries; they do not provide a duplicate one-class-per-tool matrix.

The current architecture and outstanding cross-project findings are recorded in `TestArchitectureReaudit-2026-07-10.md`.

## Unit Ownership

| Tool family | Unit project | Current position |
| --- | --- | --- |
| Workspace contracts and selectors | `Roslyn.Workbench.Mcp.Workspace.Test` | Workspace selector validation and domain behaviour |
| Inspection contracts and collection limits | `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Inspection DTO validation alongside owning tools |
| MCP envelopes, schemas and lifecycle contracts | `Roslyn.Workbench.Mcp.Test` | Host-owned serialisation, binding, schema and transport behaviour |
| Plugin execution and registration | `Roslyn.Workbench.Mcp.Plugins.Test` | Typed registry, visitor and context-adaptation behaviour |
| Inspection and normal refactoring tools | `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Per-tool unit coverage and Roslyn algorithm branches |
| Code-action tools and workflows | `Roslyn.Workbench.Mcp.CodeActions.Test` | Isolated catalogue, discovery, token, workflow and tool behaviour |
| Server-owned tools | `Roslyn.Workbench.Mcp.Test` | Tool mapping and mock-isolated host service behaviour |

Every production tool currently has dedicated unit coverage in its owning unit project. A tool is not required to have a same-named integration class.

## Integration Capability Ownership

| Capability or boundary | Integration suite | Representative coverage |
| --- | --- | --- |
| Workspace projection | `WorkspaceProjectionIntegrationTests` | Solution structure, project details and document options through a real workspace |
| Default project structure | `DefaultProjectStructureServiceIntegrationTests` | Real default project-selection behaviour |
| Semantic inspection | `SemanticInspectionIntegrationTests` | Diagnostics, operation trees and control-flow behaviour |
| Cross-project search | `SolutionSearchIntegrationTests` | Implementations, references, callers, derived types and dependency relationships |
| Selector and snapshot semantics | `SelectorAndSnapshotIntegrationTests` | Resolution, search, metadata, bounded results and stale snapshots |
| Mutation staging | `MutationPipelineIntegrationTests` | Rename, formatting, using changes, preview and transaction staging |
| Controlled code actions | `ControlledProviderWorkflowIntegrationTests` | List, describe, stage, fix-all, token and snapshot workflows |
| Built-in code actions | `BuiltInCodeActionStagingIntegrationTests` | Representative built-in provider staging |
| Code-action composition | `CodeActionRuntimeComposerIntegrationTests` | Runtime composition and provider discovery |
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
