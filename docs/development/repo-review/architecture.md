# Repository architecture map

## Scope and review basis

This map describes the repository as it exists on 31 July 2026. The review covers all production, test, fixture, tooling, configuration and design-document paths. The repository targets the .NET 10 SDK pinned by `global.json`; the installed SDK is 10.0.102. Production and ordinary test projects enable nullable reference types, implicit usings, latest C# language features and warnings as errors. The plugin authoring analyser is the only production project targeting `netstandard2.0`; the remaining production, test and tooling projects target `net10.0` through their directory build properties.

## Project graph and dependency direction

The intended production dependency direction is:

```text
Microsoft.CodeAnalysis.Workspaces.Common
                    |
                    v
Roslyn.Workbench.Mcp.Abstractions
                    |
        +-----------+--------------------+
        |                                |
        v                                v
Roslyn.Workbench.Mcp.Workspace    Plugins.Analyzers (build/analyser only)
        |                                |
        +---------------+----------------+
        |               |
        v               v
Roslyn.Workbench.Mcp.Plugins   Roslyn.Workbench.Mcp.CodeActions
        |
        v
Roslyn.Workbench.Mcp.Plugins.Core
        |
        +----------------------+------------------+
                               v
                    Roslyn.Workbench.Mcp (Host)
```

The exact production references are:

| Project | Direct project references | Role and dependency observations |
| --- | --- | --- |
| `Roslyn.Workbench.Mcp.Abstractions` | None | Minimal public Workspace-facing selectors, result models and resolver/project/query contracts. It references only `Microsoft.CodeAnalysis.Workspaces.Common`. |
| `Roslyn.Workbench.Mcp.Workspace` | Abstractions | Workspace loading, selection, snapshot state, execution leases, query caches, change detection, transactions, commit locking/writing and recovery. It owns filesystem and MSBuild/Roslyn Workspace implementation concerns. |
| `Roslyn.Workbench.Mcp.Plugins.Analyzers` | None | Compile-time Roslyn analysers for plugin authoring contracts. It is included in the Plugins NuGet package as an analyser asset, not a runtime assembly reference. |
| `Roslyn.Workbench.Mcp.Plugins` | Abstractions, Workspace, Plugins.Analyzers (non-runtime analyser reference) | Public trusted in-process plugin API plus internal adaptation to Workspace. The Workspace reference is private in the package. The packaged Abstractions assembly is added explicitly to the package. |
| `Roslyn.Workbench.Mcp.CodeActions` | Abstractions, Workspace | Internal Code Action composition, discovery, replay, execution and staging. It is deliberately separate from the third-party plugin system. |
| `Roslyn.Workbench.Mcp.Plugins.Core` | Abstractions, Workspace, Plugins | Bundled first-party inspection and mutation handlers using the public plugin execution contracts. |
| `Roslyn.Workbench.Mcp` | Abstractions, Workspace, Plugins, Plugins.Core, CodeActions | Executable MCP Host, bootstrap and composition root. It owns MCP schemas/envelopes, transport adapters, plugin discovery/loading, server-owned lifecycle tools and error reporting. |
| `Roslyn.Workbench.Mcp.ScenarioRunner` | No project references | Separate executable that launches a built Host over MCP stdio, manages external scenario repositories, collects traces and produces validation reports. |

There is no production reference from Workspace to Plugins or CodeActions, from Plugins to CodeActions, or from CodeActions to Plugins. `InternalsVisibleTo` grants the Host and relevant implementation/test assemblies access across several boundaries, so source-level accessibility is broader than the public package/API graph and must be considered during the review.

## Executable entry points and composition roots

### MCP Host

`src/Roslyn.Workbench.Mcp/Program.cs` creates a generic host with `Host.CreateApplicationBuilder(args)`, invokes `AddRoslynWorkbench`, builds the host and runs it. `Hosting/HostStartupComposer.cs` performs pre-DI startup composition: startup-option resolution, Code Action catalogue construction and plugin catalogue bootstrap. `Hosting/RoslynWorkbenchHostApplicationBuilderExtensions.cs` installs the composed catalogues, Workspace, plugin, Code Action and Host services, startup prerequisites, server-owned tools and the MCP SDK with stdio transport. `Hosting/RoslynWorkbenchServiceCollectionExtensions.cs` is the central DI registration surface. Almost all stateful services and all tool adapters are singletons.

The Host publishes four typed transport-adapter families: plugin query, plugin mutation, Code Action query and Code Action mutation. Server-owned tools directly adapt lifecycle and transaction services. `Protocol/*` owns binding, ComponentModel validation, schema production, structured result serialisation and protocol metadata. `UnhandledToolExceptionFilter` is the common unexpected-exception boundary.

### Scenario runner

`tools/Roslyn.Workbench.Mcp.ScenarioRunner/Program.cs` installs Ctrl+C cancellation and calls `ScenarioApplication.RunAsync`. The application parses and validates the suite, prepares repositories, launches Host processes, drives MCP operations, captures EventPipe/process diagnostics and writes reports. It is an operational test/performance tool rather than a runtime dependency of the Host.

### Lock fixture

`test/Roslyn.Workbench.Mcp.Workspace.LockFixture/Program.cs` is a small test executable used to hold cross-process file locks during Workspace integration coverage.

## Major subsystems and responsibilities

### Public Workspace abstractions

`Roslyn.Workbench.Mcp.Abstractions` defines workspace, project, document, span, location, symbol and scope selectors; snapshot preconditions; bounded collection and mutation summary contracts; and the resolver, project-structure, target-framework and reference-discovery service contracts exposed to plugin handlers. These types are the lowest-level cross-project and package boundary.

### Workspace loading, state and query execution

`Roslyn.Workbench.Mcp.Workspace` loading services resolve `.sln`, `.slnx` and `.csproj` inputs, create `MSBuildWorkspace` instances, evaluate project compatibility and retain loaded C# projects. The session store holds immutable Host/session snapshots and operation gates. Selection and resolver services turn public selectors into Roslyn projects, documents, symbols and spans against a specific epoch/revision. Execution-context factories acquire query or mutation leases. Workspace and plugin query caches are snapshot-generation-aware and are invalidated through lifecycle observers. Project structure, target-framework resolution and reference discovery are shared query services.

### Change detection, coordination and transaction consistency

The Workspace project monitors source, solution and evaluated project inputs; fingerprints inputs; marks workspaces out of date; and publishes advisory cross-instance status. A global transaction slot allows one loaded workspace to own an active transaction. Mutations are validated, linked-document changes are reconciled and candidate solutions are appended as bounded revisions. Commit planning derives file operations, commit locking coordinates across processes, the writer uses recovery manifests and atomic file operations, and recovery services inspect/repair unfinished commits. The public write boundary is `transaction-commit`; query and mutation handlers should never write source files directly.

### Third-party plugin API and runtime adaptation

`Roslyn.Workbench.Mcp.Plugins` exposes plugin entry points, handler/context interfaces, query/mutation registration builders, tool metadata, execution results and named result-cache keys. Plugins are trusted, in-process and loaded only at startup. Internal adapters acquire Workspace contexts, expose query-only solutions/services, stage mutation candidates through Workspace and map Workspace failures into plugin outcomes. Preparation/materialisation validates registered handler shapes and metadata. `Plugins.Analyzers` provides compile-time diagnostics for entry points, handlers, invocation/cancellation patterns and cache keys.

### Bundled core tools

`Roslyn.Workbench.Mcp.Plugins.Core` is a first-party plugin containing public request/response contracts and handlers for semantic inspection, navigation, analysis, project/document structure and two mutation operations (`format-document` and `rename-symbol`). Shared base handlers own common cancellation/failure mapping. The tools use Roslyn syntax/semantic APIs and shared plugin services, then return deterministic bounded result contracts.

### Internal Code Action system

`Roslyn.Workbench.Mcp.CodeActions` composes Roslyn code-fix and refactoring providers through MEF, activates applicable analyzers, discovers actions, filters them through policy, records snapshot-bound replay recipes, resolves list/stage/fix-all requests, evaluates `CodeActionOperation` results and hands changed solutions to the Workspace staging pipeline. Its three Host-published tools are `list-code-actions`, `prepare-fix-all` and `stage-code-action`. Code Actions are explicitly not plugins and are not part of plugin status or plugin collision ownership.

### Host protocol, plugin loading and lifecycle tools

The executable project owns startup option precedence/validation, MSBuild locator registration, MCP schema and request binding, result envelopes, the four typed tool adapters, server/workspace/transaction tool contracts, plugin package discovery and path policy, assembly metadata inspection, one load context per package, MEF composition, compatibility validation, deterministic collision handling and status reporting.

### Error reporting

The Host captures bounded process-local unexpected-error records and exposes correlated local detail. A separate prepare/review/submit workflow projects an allow-listed external report, stores an immutable prepared submission, obtains consent when required and dispatches either to an embedded-build Sentry destination or a stderr logging adapter. It owns network egress, privacy redaction, consent state and lifecycle invalidation.

### Scenario and performance infrastructure

The scenario runner owns suite configuration, repository restoration, Host launch, state-sequence execution, MCP requests, EventPipe collection, trace analysis, thresholds and report output. Checked-in scenario definitions cover representative repository and transaction workflows but are not part of the ordinary unit/integration test solution path.

## Public and cross-project contracts

The principal public package boundary comprises `Roslyn.Workbench.Mcp.Plugins` plus the explicitly packaged `Roslyn.Workbench.Mcp.Abstractions` assembly. Important contract families are:

- `WorkspaceBoundRequest`, `WorkspaceMutationRequest`, selectors and `SnapshotPrecondition`, which bind every operation to a selected Workspace snapshot.
- `IWorkspaceResolver`, `IProjectStructureService`, `IProjectTargetFrameworkResolver` and `IReferenceDiscoveryService`, which expose read-only Roslyn-backed services to plugins.
- `IQueryToolHandler<TRequest,TResponse>`, `IMutationToolHandler<TRequest>`, `IQueryContext`, `IMutationContext`, `IQueryResultCache` and registration builders, which define the plugin authoring and execution model.
- `PluginExecutionOutcome<T>`, `PluginExecutionResult<T>` and mutation candidate/result types, which bridge plugin handlers to Host adapters.
- Code Action registrations, execution contexts and results, which are internal cross-project contracts consumed by the Host through friend assembly access.
- Workspace lifecycle/transaction outcome types, execution leases and operation errors, which are internal cross-project contracts consumed by Host, Plugins and CodeActions.
- Host MCP request/result contracts and `ToolResult<T>`, which define the wire-visible JSON surface.

Compatibility is influenced by generated MCP schemas, System.Text.Json serialisation, ComponentModel validation, public XML contracts, tool metadata, plugin API version checks and assembly-load identity sharing.

## External boundaries and resource ownership

| Boundary | Owners | Notes |
| --- | --- | --- |
| stdin/stdout MCP transport | Host and MCP SDK | stdout is protocol data; diagnostics/logging must remain on stderr. Tool publication is fixed for process lifetime. |
| MSBuild evaluation and Roslyn Workspace | Host registration plus Workspace loading | Opening a workspace evaluates trusted MSBuild and can load analyzers. `LoadedWorkspace` owns `MSBuildWorkspace`, watchers and associated lifetime. |
| Source/project filesystem | Workspace change detection, loading, transactions and recovery | Reads solution/project/source inputs; transaction commit is the only intended source write. Atomic writes, locks, containment checks and recovery records protect consistency. |
| State directory | Workspace recovery/coordination and Host configuration | Stores recovery and cross-instance status. Unix owner-only directory/file permissions and symlink/reparse rejection form a security boundary. |
| Plugin filesystem and assembly loading | Host `PluginLoading` | Searches immediate package children, reads assembly metadata, creates non-collectible package load contexts and resolves private managed/native dependencies. Plugins are fully trusted in-process code, not sandboxed. |
| Network | Sentry error-report dispatcher only | Network egress must occur only after explicit submission and applicable consent. The logging dispatcher uses stderr instead. |
| Process execution and git | Scenario runner | Starts Host/git/external commands in scenario repositories and collects exit/output/diagnostics. |
| EventPipe/process diagnostics | Scenario runner | Uses `Microsoft.Diagnostics.NETCore.Client` and TraceEvent to collect performance artefacts. |
| Memory caches and shared state | Workspace, Plugins, CodeActions, Host error reporting | Singleton cache/state stores are bounded by options and keyed/invalidation-scoped by snapshot, plugin/tool and expiry. |

## Plugin and extension mechanisms

- Third-party plugins: immediate subdirectories beneath configured roots; one marked entry assembly per package; one non-collectible `AssemblyLoadContext` per valid package; Host-shared Plugins, Abstractions, System.Composition and Roslyn identities; deterministic plugin ID/tool-name collision policy; no hot reload.
- Bundled plugins: `BundledCorePlugin` is prepared and materialised through the same plugin registration contracts but loaded with the Host and wins collisions against external packages.
- Code Actions: internal provider catalogue discovered through Roslyn/MEF and filtered to supported public paths. Replay references are temporary, process-local and Workspace snapshot-bound.
- Plugin authoring analysers: compiler diagnostics shipped as analyser assets in the Plugins NuGet package.
- Workspace lifecycle observers: multiple singleton implementations receive snapshot invalidation/close/reload events for plugin query cache, Code Action references and error-reporting consent.

## Test projects and coverage ownership

| Test project | Claimed coverage |
| --- | --- |
| `Roslyn.Workbench.Mcp.Workspace.Test` | Workspace unit/contract coverage: selectors, state transitions, gates, caches, loading components, change detection, transactions, commit/recovery collaborators and result contracts. |
| `Roslyn.Workbench.Mcp.Workspace.IntegrationTest` | Real filesystem/MSBuild/Roslyn Workspace, loading compatibility, change monitoring, transaction staging/commit/recovery, locks and cross-component Workspace flows. |
| `Roslyn.Workbench.Mcp.Plugins.Test` | Public plugin contracts, preparation/materialisation, handler validation, execution-context adaptation, cache behaviour and Workspace result mapping. |
| `Roslyn.Workbench.Mcp.Plugins.Analyzers.Test` | Roslyn analyser diagnostics and supported plugin authoring patterns. |
| `Roslyn.Workbench.Mcp.Plugins.Core.Test` | Unit/contract coverage for bundled inspection/refactoring handlers and result projections. |
| `Roslyn.Workbench.Mcp.Plugins.Core.IntegrationTest` | Bundled tool execution against real Workspace/Roslyn graphs and shared services. |
| `Roslyn.Workbench.Mcp.CodeActions.Test` | Code Action composition, policy, discovery, replay, evaluation, context acquisition, staging, registrations and tool handlers. |
| `Roslyn.Workbench.Mcp.CodeActions.IntegrationTest` | Real provider discovery/replay/staging flows against Workspace fixtures. |
| `Roslyn.Workbench.Mcp.CodeActions.AuditTest` | Provider catalogue and replay-family audit coverage. |
| `Roslyn.Workbench.Mcp.Test` | Host unit/contract coverage: startup configuration, protocol schema/binding, plugin loading policy, MCP adapters, lifecycle tools, status and error-reporting components. |
| `Roslyn.Workbench.Mcp.IntegrationTest` | Full Host composition, MCP publication, plugin discovery/collisions, runtime adapters and selected end-to-end server flows. |
| `Roslyn.Workbench.Mcp.AcceptanceTest` | Published Host process and MCP client acceptance coverage. It is intentionally not run automatically during ordinary review validation. |
| `Roslyn.Workbench.Mcp.TestSupport` | Shared unit-test Roslyn objects and visible mock wiring. |
| `Roslyn.Workbench.Mcp.IntegrationTestSupport` | Real Workspace/Host fixtures and multi-component integration helpers. |
| Plugin fixtures and Workspace test assets | External package discovery/composition failures, query/mutation plugins, project/solution compatibility, linked/multi-target projects and code-action profiles. |
| Scenario runner suites | Operational, repository-scale, state-sequence and performance scenarios outside the xUnit test graph. |

The test dependency direction mirrors production ownership: unit projects reference their implementation plus unit support; integration/audit projects may reference integration support; Host integration support intentionally references all runtime implementation projects. Test assembly-wide categories are enforced by `test/Directory.Build.targets`.

## Representative end-to-end operation paths to trace

1. Workspace open: MCP request binding → `WorkspaceOpenTool` → lifecycle/load workflow → root/project compatibility resolution → `MSBuildWorkspace` → session store, monitor and lifecycle observers → Host result mapping.
2. Plugin query: MCP adapter → request binding/validation → plugin execution context factory → Workspace selector and query lease → exact-snapshot resolver/services/cache → bundled or external handler → bounded result → plugin/Host envelope serialisation.
3. Plugin mutation: MCP adapter → mutation lease → snapshot/request resolution → handler creates candidate solution → Workspace mutation candidate processing/linked-document merge → transaction revision append → preview/result mapping.
4. Code Action stage: list/discover action → reference store recipe → stage request snapshot validation → action replay/provider resolution → operation evaluation → Workspace candidate staging → transaction revision append.
5. Transaction commit: lifecycle tool → exclusive lease/snapshot guard → commit plan → cross-process locks → recovery manifest → atomic file operations → cleanup/reload/state transition and observer invalidation.
6. Unexpected tool failure and external reporting: common exception filter → bounded local capture/correlation → details → allow-listed projection and prepared immutable submission → consent → logging or Sentry dispatcher.

