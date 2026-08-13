# Review Unit 3: Plugin Platform

Date: 2026-08-13

**Status:** Complete

## Evidence boundary

This review used only the current checked-out source, tests, project/configuration files and current normative review programme and plugin-authoring documentation. It did not use Git history, diffs, changed-file discovery, commits, branches, tags, stashes, reflogs, deleted or renamed artefacts, external backups, historical audits or previous review findings as evidence.

## Scope completed

The review covered the packaged public plugin API and bundled Abstractions assembly; plugin attributes, entry points, registration builders, service registration, handler contracts, execution outcomes and Workspace context adapters; query-cache identity and lifecycle; authoring analysers and their shared validation policy; package layout and the clean external package consumer; package discovery, physical path containment and metadata-only assembly inspection; assembly load contexts, shared/private dependency resolution and MEF composition; metadata/API validation, identity and tool collision policy, transport-schema admission, materialisation, immutable catalogue publication and disposal; bundled and fixture plugins as direct consumers; Host DI, configuration and typed query/mutation adapters.

Direct implementation and consumer paths were followed across `Roslyn.Workbench.Mcp.Abstractions`, `Roslyn.Workbench.Mcp.Workspace`, `Roslyn.Workbench.Mcp.Plugins`, `Roslyn.Workbench.Mcp.Plugins.Analyzers`, `Roslyn.Workbench.Mcp.Plugins.Core` and `Roslyn.Workbench.Mcp`. Current unit, integration and relevant acceptance claims were inspected. No production code was modified.

## Public contract, registration and execution model

`IRoslynPlugin.Configure` receives a startup-only `IPluginConfiguration`. A plugin records handler types rather than instances and may register only typed singleton service mappings. Configuration and every returned metadata builder freeze after `Configure` returns. Query handlers implement exactly one closed `IQueryToolHandler<TRequest,TResponse>` contract with a `WorkspaceBoundRequest`; mutation handlers implement exactly one closed `IMutationToolHandler<TRequest>` contract with a `WorkspaceMutationRequest`. External request and response types must be public. Runtime preparation rejects direct marker implementations, cross-family or multiple contracts, disposable handlers, MEF imports, destructive query declarations, duplicate/invalid tool names and incomplete metadata; state and disposable-field shapes that cannot prove safe singleton behaviour are warnings.

One isolated `ServiceProvider` is built per admitted plugin with scope and build validation enabled. Plugin services and distinct handler types are registered as singletons, and every handler is resolved during catalogue startup. Startup-only reflection closes predeclared registration factories and creates typed `PluginQueryRegistration<TRequest,TResponse>` or `PluginMutationRegistration<TRequest>` instances; normal calls use prebuilt generic MCP adapters without reflection. The provider lifetime is retained by the catalogue, so provider-owned synchronous and asynchronous plugin services are disposed at Host shutdown.

The public execution context exposes an immutable invocation `Solution`, Workspace identity/revision, path and resolver services and a curated set of read-only Host services. It does not expose `Workspace`, `TryApplyChanges` or the transaction stager. Query contexts add a cache scoped by complete Workspace snapshot identity plus exact plugin ID and tool name. Mutation contexts return a `MutationCandidate`; the Host adapter checks for unexpected live-Workspace changes and, only after a successful candidate, stages it through Unit 2's transaction boundary.

## Discovery, loading and catalogue model

Configured plugin roots are enumerated once during Host startup. Only immediate child directories are packages and only top-level DLLs are inspected. Physical containment is checked for package directories, entry assemblies and every managed/native dependency path. The metadata reader uses PE metadata rather than loading candidate code, collects `RoslynPluginAttribute` constructor values and informational version, sanitises failures and requires exactly one marked entry point per package.

Metadata and API compatibility are checked before an external load context is created. Duplicate external plugin IDs are detected across discovery results and all packages with that ID are disabled before code loading. Each remaining package receives a non-collectible `AssemblyLoadContext` backed by `AssemblyDependencyResolver`. `Roslyn.Workbench.Mcp.Abstractions`, `Roslyn.Workbench.Mcp.Plugins`, `Microsoft.CodeAnalysis*` and `System.Composition*` resolve through the default context so contract identity is shared; other managed and native dependencies resolve within the package and are rejected if physical resolution escapes it. The current integration fixture proves a private `NuGet.Versioning` dependency loads from the package while the shared contract types retain Host identity.

MEF composes exactly one `IRoslynPlugin` export, invokes `Configure`, disposes the composition container and freezes configuration. Expected preparation diagnostics disable the whole plugin atomically. Unexpected inspection, load, composition, configuration, construction or materialisation failures are converted into per-plugin status where isolation remains possible. Bundled plugin tools are admitted first and become protected names together with server-owned and Code Action tools. An external collision with any protected name, or a tool-name collision between external plugins, disables every affected external plugin before materialisation. The startup lifecycle then creates all typed MCP tools, builds one ordinal frozen dictionary and atomically publishes the runtime catalogue.

## Representative traces

### Packed authoring surface and analyser activation

`Roslyn.Workbench.Mcp.Plugins.csproj` packs its runtime assembly, the matching `Roslyn.Workbench.Mcp.Abstractions.dll` under `lib/net10.0`, the netstandard2.0 analyser under `analyzers/dotnet/cs` and `PluginAuthoring.md` as the package README. Workspace and analyser implementation project references are private and do not become NuGet dependencies. `PluginAnalyzerPackageIntegrationTests` packs the project, inspects the archive/nuspec, restores a clean project containing only a `Roslyn.Workbench.Mcp.Plugins` package reference, proves invalid entry-point and cache code raises `RWMCP015`, `RWMCP020` and `RWMCP021`, and then proves a valid plugin builds without `RWMCP` diagnostics.

The five analysers cover direct Workspace mutation/live-solution access, asynchronous or escaped startup configuration, entry-point cardinality/identity/API, handler shape/accessibility/lifetime/state/behaviour/name, cancellation forwarding, unbounded query collections and unsafe cache keys/values. Shared tool-name validation source is compiled into both runtime and analyser projects. Runtime remains authoritative for dynamic metadata, final fluent overrides, assembly/package structure, dependency graphs and transport representation. Except for candidate `RWMCP2-006`, the inspected analyser and runtime rules agree at the contract boundaries each claims to enforce.

### Valid dependency-bearing external package

Startup option resolution supplies normalised plugin directories to `PluginCatalogStartupLifecycleService`. Discovery finds the Host query fixture's entry DLL by metadata, candidate preparation validates V1 and semantic informational version, creates a package load context, loads its private `NuGet.Versioning` dependency and composes its single plugin export. Configuration preparation resolves its public request/response contract and metadata; schema preflight validates the request; materialisation builds the isolated provider and resolves the singleton handler. Collision-free tools become closed typed MCP adapters and are atomically published. A call binds the request, acquires a Workspace query lease and plugin/tool cache scope, invokes the handler, verifies the live Workspace was not changed, serialises the result and disposes the invocation lease. Package/load-context integration and Host adapter tests exercise these component boundaries; published-process acceptance source covers the combined external query path.

### Invalid, conflicting and incompatible packages

- A malformed top-level DLL disables only its package during metadata inspection; zero or multiple marked entry points fail package cardinality without executing candidate code.
- A physically escaping package, entry assembly or resolved dependency is rejected before that path is loaded. Overlapping roots are canonicalised and each contained immediate package is inspected once.
- Blank metadata, invalid informational versions and unsupported API versions are rejected before load-context creation. Duplicate external plugin IDs are all rejected before code loading.
- MEF zero/multiple exports, throwing `Configure`, invalid handler contracts, invalid DI graphs or throwing handler construction disable the owning plugin and retain categorised/sanitised status. Cleanup is attempted when a provider was already created.
- Duplicate tool names within one plugin disable that plugin during preparation. Reserved/bundled/Code Action collisions and cross-external collisions disable all affected external plugins before provider construction. A bundled collision is a Host startup invariant failure rather than an externally recoverable status.
- Request schema generation always runs before handler construction and disables an invalid plugin. Query response schema generation runs only when output schemas are configured as `Full`, and even `Full` schema generation does not enforce the serializer's object-only successful-data rule; the resulting admission/runtime mismatch is candidate `RWMCP2-006`.

### Query cache isolation and invalidation

`PluginQueryMcpServerTool` asks the context factory for a scope containing the complete `WorkspaceSnapshotIdentity`, plugin ID and final tool name. `QueryResultCacheScope` also includes cache-key and value type identity. Matching calls coalesce one computation while caller cancellation cancels only that caller's wait. Different plugin, tool, snapshot, key/value type or key value cannot reuse an entry. Null and synchronous/asynchronous disposable values are returned but not retained; recursive same-key factories fail rather than deadlock; a retained invocation scope rejects use after completion. Workspace lifecycle invalidation removes affected epoch/transaction/snapshot generations, and configured entry pressure and sliding expiration are consumed by the singleton cache state. Unit 1's cache-state review and Unit 3's public scope tests jointly exercise pressure, coalescing, isolation, invalidation and unsafe retained-value behaviour.

### Mutation invocation

The typed mutation adapter binds the public `WorkspaceMutationRequest`, acquires a mutation context under the selected session gate and supplies the immutable effective solution. Rejected/no-change results return without staging. A successful candidate is checked for unexpected direct Workspace changes, mapped to a Workspace mutation proposal and staged through `IWorkspaceMutationStager`; transaction validation, linked-document reconciliation and revision append remain those established in Unit 2. Handler, stager and cancellation failures propagate to the top-level protocol boundary while the composite execution lease is always asynchronously disposed. Host mutation adapter tests cover acquisition failure, rejection, no change, unexpected direct mutation, successful/rejected staging, exception and cancellation.

### Catalogue and service disposal

The catalogue retains every successfully materialised provider and attempts disposal in reverse order. Both synchronous and asynchronous catalogue paths continue after a plugin-owned disposal failure and aggregate failures. Startup loading/publication failure disposes all provisional providers while preserving both the original and cleanup exceptions. `PluginCatalogState` atomically clears its published snapshot before disposal, preventing repeat cleanup. The Host owns this singleton and generic Host disposal reaches it; current tests cover provider isolation, async-only and dual disposable services, reverse order, repeated disposal and aggregated failures. Load contexts are intentionally non-collectible and live for the process/catalogue lifetime.

## DI and configuration

Discovery, inspection, path policy, candidate preparation, composition, configuration preparation, registration materialisation, collision policy, schema preflight, catalogue loader/state, MCP tool factory/request handler and Workspace/plugin cache adapters are Host singletons. Plugin providers are separate containers rather than Host scopes; they receive only plugin-authored registrations and automatically registered handlers, while invocation services arrive through the Host-owned context.

`PluginDirectories`, `PluginQueryCacheEntryLimit`, `PluginQueryCacheSlidingExpiration`, `DefaultMaxResults` and `ToolOutputSchemaMode` are declared, parsed from command line/environment, validated and consumed. Missing directories are ignored; invalid limits/modes fail startup validation. `ToolOutputSchemaMode` defaults to `Omit`, which keeps `tools/list` compact but currently also changes whether response contracts receive startup validation. No otherwise unused Unit 3 option or missing DI registration was identified.

## Candidate finding: catalogue admission does not enforce the runtime response contract

`RWMCP2-006` records a startup-admission defect. The default `ToolOutputSchemaMode.Omit` causes `PluginTransportSchemaPreflight` to skip every response contract while `PluginCatalogEntryMaterializer` still enables and publishes the tool. Separately, `QueryResponseContractInspector` expressly excludes `string` from collection warnings and performs no object-shape validation, while `Full` schema generation can represent scalar data. A successful query invocation later serialises the plugin's generic response with `JsonSerializer.SerializeToElement` and then requires `JsonValueKind.Object`. A scalar result therefore fails in either mode, and a response converter or metadata failure skipped by `Omit` fails in the default mode, only after the handler has executed. The top-level filter maps either case to a generic correlated `UnhandledException`, leaving an advertised tool that cannot successfully return data instead of a categorised startup diagnostic.

This is not merely omission of optional wire metadata: the object-valued, serialisable response contract is required by the execution path in both output modes. The focused preflight test explicitly expects no response validation in `Omit`; a serializer test explicitly proves a scalar query payload throws; and no admission test joins those real constraints. The published-process unsupported-contract test forces `Full`, and its fixture combines one bad request with one bad response, so it cannot prove response-only rejection under the default. Full evidence and remediation direction are retained in `../findings.md`.

## Earlier-unit revisit

Query and mutation consumers were retraced through the Unit 1 Workspace acquisition, snapshot, cache and lifecycle contracts and Unit 2 staging boundary. Plugin scopes include the complete snapshot and plugin/tool identity; context leases are transferred/disposed correctly; query adapters check unexpected direct Workspace mutation; mutation adapters cannot stage without a snapshot-bearing request and Host validation. No additional substantiated Workspace, transaction or recovery risk was exposed, so Units 1 and 2 and the architecture map did not require correction. `RWMCP2-006` crosses into Host schema/serialisation and unexpected-exception behaviour and must be revisited in Unit 6.

## Test evidence and gaps

Executed under the pinned .NET 10 SDK with WSL artefacts routed to `/tmp/artifacts/roslyn-workbench-mcp`:

- `Roslyn.Workbench.Mcp.Plugins.Test`: 127/127 passed;
- `Roslyn.Workbench.Mcp.Plugins.Analyzers.Test`: 63/63 passed;
- `Roslyn.Workbench.Mcp.Test`: 480/480 passed; and
- `Roslyn.Workbench.Mcp.IntegrationTest`: 68/68 passed, including load-context, metadata, MEF, catalogue and packed external-consumer coverage.

These suites substantiate public API shape, configuration freezing, preparation/materialisation, singleton provider isolation/disposal, typed execution adapters, cache scope semantics, analyser diagnostics, package contents and component-level discovery/loading. Acceptance source additionally claims published-Host valid query/mutation, incompatible, throwing, collision, schema, restart-only discovery and stdout-containment behaviour. Acceptance tests were inspected but not executed because no acceptance artefact changed and repository policy does not authorise an automatic acceptance run for this review.

The principal coverage gap is the candidate scenario: no package containing a valid request and scalar or otherwise response-only runtime transport failure is admitted and invoked in default `Omit` mode, and no admission test proves the serializer's object-only rule under `Full`. The unsupported-schema acceptance fixture uses `Full` and also contains an independently invalid request tool. Native Windows path/reparse behaviour and genuinely distinct private dependency versions across two simultaneously loaded third-party packages were not independently executed in this WSL unit; current path-policy and load-context tests cover their component rules.

## Candidate findings

| ID | Severity | Confidence | Summary |
| --- | --- | --- | --- |
| RWMCP2-006 | P2 | High | Catalogue admission does not enforce the query serializer's object-valued, serialisable response contract, so an unusable tool can be advertised and fail only after its handler executes. |

## Conclusions and limitations

No additional substantiated defect was found in package composition, public API ownership, analyser packaging, discovery cardinality, metadata-only inspection, API/identity collision ordering, physical containment, shared/private dependency selection, MEF cardinality, configuration freezing, plugin provider isolation, query-cache scoping, typed adapter creation or catalogue disposal. Handler singleton state warnings were treated as an intentional trusted-plugin constraint because runtime cannot prove thread safety and the authoring documentation states the responsibility explicitly.

The architecture map remains accurate. Review stops here; bundled tool behaviour, Code Actions, complete Host/protocol review, error reporting, operational infrastructure, repository-wide passes and final independent candidate validation have not begun.
