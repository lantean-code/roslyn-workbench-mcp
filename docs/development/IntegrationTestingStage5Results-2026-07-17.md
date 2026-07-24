# Integration Testing Stage 5 Results

Date: 17 July 2026

## Outcome

Stage 5 is complete. The obsolete in-process MCP harness and the parallel Workspace application graph have been removed. Retained component and compatibility tests now exercise typed owner contracts through a single component-scoped service provider built from the production Workspace, plugin and Code Action registration extensions.

The component adapter owns one isolated state directory, one validated service provider and the loaded Workspace lifetime. It activates typed handlers with that provider and disposes the provider asynchronously. It does not construct a Generic Host, publish MCP tools, bind JSON arguments or translate published response DTOs.

## Replacement evidence

The Stage 3 acceptance suite remains the public-boundary replacement for every removed direct-MCP workflow:

| Removed direct-MCP responsibility | Published-process replacement |
| --- | --- |
| Workspace open, list, status, reload, close and representative query invocation | Workspace lifecycle and query acceptance workflow |
| Transaction start, plugin mutation, preview and commit | Transactional plugin mutation acceptance workflow |
| Code Action list, stage, preview and rollback | Code Action mutation acceptance workflow |
| Recovery status projection | Startup diagnostics and recovery acceptance workflow |
| External plugin publication and query invocation | External plugin package acceptance workflow |

Detailed Workspace, transaction, provider, package and load-context behaviour remains in the owning component suites. Protocol schema, binding, result mapping and exception behaviour remain in Host unit/contract coverage.

## Removed support

The following obsolete responsibilities were deleted:

- `McpIntegrationTestHost` and the MCP server-tool visitor used only by direct invocation tests;
- `PluginToolTestHarness` and `CodeActionToolTestHarness`, including JSON argument and published-result adaptation;
- `WorkspaceCoordinatorFactory`, `WorkspaceRuntime`, `IWorkspaceRuntime` and their options, which manually reconstructed the production application graph;
- the bundled execution-service factory and old bundled coordinator facade; and
- the orphaned single-mutation plugin catalogue factory.

Integration support decreased from 25 to 19 C# source files. Ten obsolete support sources were removed, four focused component sources were added, and the separate Host MCP harness was also removed.

## Retained support responsibilities

Shared support now contains:

- checked-in asset materialisation and isolated temporary-directory ownership;
- documented early process-wide MSBuild registration;
- controlled Code Action providers, classification and provider-catalogue creation;
- bundled plugin catalogue materialisation for typed plugin component sessions;
- a small component Workspace lifetime adapter that delegates registration to production extensions; and
- typed plugin and Code Action sessions that acquire real execution contexts and stage real Workspace mutations without protocol concerns.

The component adapter uses one service provider for the complete scenario. Handler activation uses `ActivatorUtilities` with that provider; no invocation builds a nested provider. The existing production registration extensions remain the source of service topology, so the removed test graph cannot drift independently from Host registration.

No new production composition seam was needed. The only production-project metadata change grants the Plugins.Core integration assembly access to internal Workspace owner results, matching the existing integration-test friend assemblies.

## Project-reference audit

- No unit-test project references `Roslyn.Workbench.Mcp.IntegrationTestSupport`.
- Only component integration, Host integration and Code Action audit projects reference integration support.
- `Roslyn.Workbench.Mcp.AcceptanceTest` has no production project reference; its build-only fixture reference still uses `ReferenceOutputAssembly=false`.
- Integration support no longer references Moq.

## Verification

Focused verification after migration:

- Workspace component integration: 52 passed;
- Plugins.Core component integration: 20 passed;
- CodeActions component integration: 5 passed;
- Host component integration: 40 passed; and
- Code Action compatibility audit: 95 passed.

The component case counts are unchanged from the completed Stage 4 disposition. Stage 5 changes only how those retained tests reach their owner boundaries.

Final repository verification used an explicitly published Debug Host and passed all 1,952 tests, including all 10 published-process acceptance tests. The temporary publish output was removed after the run.
