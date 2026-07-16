# Host Architecture Validation

Date: 2026-07-16

Status: Complete — H1-H7 complete

## Purpose

This document records the architecture validation of `Roslyn.Workbench.Mcp`, the executable Host project. It provides the ordered working checklist for the remaining Host production work before the Host unit-test inventory and coverage phase begins.

Integration-test redesign remains deferred until the production structure and unit-test boundaries are stable.

## Current Boundary Position

The production dependency graph is correct:

```text
Host -> CodeActions -> Workspace
Host -> Plugins.Core -> Plugins -> Workspace
Host -> Plugins -> Workspace
Host -> Workspace
```

Validated evidence:

- Host is the only production project that references the MCP SDK.
- Host owns MCP request binding, schema publication, result serialization, transport adapters and server-owned tools.
- Code Actions are loaded through their internal catalogue and do not appear as plugins.
- Plugins.Core is bundled but uses the same plugin configuration and materialisation rules as external plugins.
- External plugin discovery, PE inspection, load-context isolation, MEF composition and deterministic collision handling have focused collaborators.
- Plugin and Code Action invocation remains closed-generic after catalogue materialisation.
- No Host production code uses null-forgiving operators or builds a temporary service provider.

The project boundary does not need redesign. The remaining work is concentrated in Host composition, protocol construction, adapter registration, exception filtering and startup sequencing.

## Executive Assessment

The MEF implementation has already split the former plugin-loader hotspot into appropriately focused services. `PluginCatalogLoader`, `PluginCandidatePreparer`, `PluginPackageDiscovery`, `LoadedPluginPreparer`, the collision policy and the status factory should not be divided further merely because the overall feature is complex. They have distinct reasons to change and their dependency counts remain proportionate.

The classes that do cross a useful responsibility boundary are:

- `RoslynWorkbenchHostApplicationBuilderExtensions`, which currently parses configuration, constructs startup catalogues, configures logging, maps options, registers every subsystem, registers MCP tools and establishes hosted-service ordering;
- `ToolSchemaBuilder`, which combines MCP SDK compatibility reflection, schema export, schema rewriting and process-wide caches;
- the plugin MCP registration path, which constructs tools through service-provider lambdas even though the Code Action path already supports direct container-validatable registration; and
- both MCP base classes, which suppress unhandled exceptions into correlation-bearing responses without logging the matching exception and correlation identifier.

Two production factory classes are not part of the real Host execution path. `PluginMcpServerToolFactory` and `CodeActionMcpServerToolFactory` exist only to support tests and integration harnesses; the latter also retains `IServiceProvider` and runtime activation. They should not remain in production.

## Ordered Implementation Plan

The sections below are deliberately ordered. Complete them in this sequence to avoid changing tool constructors and registrations repeatedly.

## Directory Convention

Host code should use feature-based folders and matching namespaces. Unit-test folders and namespaces should mirror production. Do not introduce generic `Services`, `Factories`, `Models`, `Helpers` or phase-named folders such as `H2`; keep interfaces, implementations, result types and focused helpers with the feature that owns them.

The intended top-level structure after H1-H7 is:

```text
Roslyn.Workbench.Mcp/
  Configuration/
  Contracts/
  Hosting/
  PluginLoading/
  Protocol/
  Status/
  ToolExecution/
    CodeActions/
    Plugins/
  Tools/
```

`Program.cs` and `GlobalUsings.cs` remain at the project root. A folder should only be introduced when it groups a real feature boundary; isolated files should stay with their closest existing owner rather than creating a one-file hierarchy.

## H1: Startup Options and Validation

Status: Complete

### Finding

`StartupOptions` follows the normal mutable options shape and is registered with `AddOptions`. The original parser silently replaced malformed values with defaults without retaining any diagnostic, while the first H1 implementation failed startup for those values. Neither behaviour suited an MCP server normally launched by an agent from pre-defined configuration: silent fallback was unobservable, while fail-fast configuration prevented the server and its diagnostics tools from becoming available.

The plugin catalogue consumes these values before the Host is built, so DI-only validation would occur too late for all callers.

### Decision

Resolve external startup configuration into effective validated options plus structured fallback warnings. An invalid setting falls back independently, preserving every other valid setting and allowing the server to start. Log the warnings through Host logging and expose them through full `server-status`.

Retain `ValidateOnStart` as an invariant check over the resolved options. A failure after resolution indicates a programming or composition defect and remains exceptional. A syntactically valid state-directory selection that later fails filesystem access must remain an operational recovery failure; it must never silently redirect recovery to the default directory.

Do not replace this with a temporary DI container. Do not create a parameter object solely to shorten the parser's private method signatures.

### Resolution

`StartupOptionsResolver` now produces a `StartupConfigurationSnapshot` containing effective options and structured warnings. Known command-line options without a value, malformed or non-positive numeric and time-span values, unsupported schema modes, blank plugin roots and invalid state-directory syntax fall back only the affected setting. Integer and time-span parsing uses invariant culture, command-line scalar values retain precedence over environment values, repeated plugin roots are preserved and deduplicated, and unknown arguments remain available to the surrounding Host and MCP infrastructure.

`StartupOptionsRules` contains the shared deterministic validity rules. `StartupOptionsValidator` protects the resolved options before either catalogue is built and is registered with `ValidateOnStart` for the normal options pipeline. `StartupConfigurationReporter` logs every fallback after Host logging becomes available, and full `server-status` exposes the same warnings through `startupWarnings`. No temporary service provider is created.

The complete startup configuration feature now lives under the `Configuration` folder and `Roslyn.Workbench.Mcp.Configuration` namespace, with its unit tests in the matching test folder and namespace. Later Host phases should follow the same feature-based convention and avoid generic `Services`, `Factories` or `Models` folders.

### Working checklist

- [x] Resolve malformed, missing or semantically invalid settings independently to their defaults and retain structured warnings.
- [x] Validate positive result, query, revision and token-lifetime values and a non-empty state directory.
- [x] Keep command-line values ahead of environment values and preserve the current repeated plugin-directory behaviour.
- [x] Register the same rules through `AddOptions<StartupOptions>()` and `ValidateOnStart`.
- [x] Log startup fallback warnings and expose them through full `server-status`.
- [x] Add focused resolver, validation, reporting and status unit tests before changing composition structure.

Complexity: medium.

### Recommended location

- Production: `src/Roslyn.Workbench.Mcp/Configuration` with namespace `Roslyn.Workbench.Mcp.Configuration`.
- Unit tests: `test/Roslyn.Workbench.Mcp.Test/Configuration` with namespace `Roslyn.Workbench.Mcp.Test.Configuration`.
- `StartupConfigurationReporter` remains with the configuration feature even though it implements `IHostedService`; its reason to change is configuration reporting.

## H2: MCP Protocol and Schema Boundary

Status: Complete

### Finding

The original `ToolSchemaBuilder` was not a pure static helper. It owned process-wide caches, reflected over MCP SDK compatibility shapes, invoked generic SDK methods, exported schemas and rewrote them for the published contract. `McpToolProtocolFactory` and `ServerOwnedToolBase` called this global implementation directly.

`ToolResultEnvelopeSerializer`, `McpPublishedResultSerializer`, `QueryResponseContractInspector` and `WorkspaceToolResultMapper` remain deterministic transformations with no external resource ownership. Their static form is appropriate. The older null-forgiving concern in `WorkspaceToolResultMapper` is no longer present; its remaining exception represents an invalid internal result invariant.

### Decision

Isolate SDK reflection and caching behind one Host-owned schema provider. Keep envelope schema composition and result serialization as pure helpers. Make protocol construction depend on the schema provider so SDK compatibility is one named boundary and tool construction can be container validated.

This should be a small number of cohesive services, not an interface per schema operation. Startup-only reflection remains acceptable and must not enter invocation.

### Resolution

`McpSdkSchemaProvider` now owns the MCP SDK probes, compatibility reflection and input/value schema caches behind `IMcpSdkSchemaProvider`. `ToolSchemaBuilder` contains only deterministic JSON schema composition and normalization. The instance `ToolSchemaFactory` coordinates those two responsibilities and caches composed direct-response schemas without exposing SDK reflection to callers.

`McpToolProtocolFactory` is now an injected `IMcpToolProtocolFactory` implementation. Server-owned tools, plugin registration and both Code Action adapters construct protocol metadata through that same boundary. Protocol construction remains startup work, while invocation continues through the existing closed-generic adapters without reflection.

Focused unit tests cover the pure schema composer, schema-factory orchestration, caching and all server-owned/plugin/Code Action protocol metadata branches. Real MCP SDK export is covered in `McpSdkSchemaProviderIntegrationTests`, including request, object, bounded-collection and nullable-value contracts. Existing Host schema and MCP integration coverage continues to lock the published wire shapes.

### Working checklist

- [x] Extract MCP SDK schema export, compatibility reflection and caches from `ToolSchemaBuilder` into one focused provider.
- [x] Retain pure JSON schema composition separately from SDK export.
- [x] Convert `McpToolProtocolFactory` into a constructor-injected Host service, or an equivalent instance boundary, that consumes the schema provider.
- [x] Make server-owned, plugin and Code Action tool protocol construction use the same boundary.
- [x] Preserve every existing input schema, optional full output schema, annotation, title and description.
- [x] Keep reflection and generic materialisation at startup only.
- [x] Add focused unit coverage for cache-independent composition and integration/audit evidence for the real MCP SDK exporter.

Complexity: high.

### Recommended location

- Production schema export, schema composition, protocol construction, binding and publication: `src/Roslyn.Workbench.Mcp/Protocol` with namespace `Roslyn.Workbench.Mcp.Protocol`.
- Public MCP envelope and server contract types remain under their existing owner in `src/Roslyn.Workbench.Mcp/Contracts`.
- Unit tests: `test/Roslyn.Workbench.Mcp.Test/Protocol` with namespace `Roslyn.Workbench.Mcp.Test.Protocol`.
- Keep the SDK compatibility provider beside the schema implementation in `Protocol`; do not create a generic `Compatibility` folder for one external boundary.

## H3: Typed Tool Registration and Test-Only Factories

### Finding

`CodeActionMcpToolRegistrationVisitor` registers closed registrations, handlers and adapters directly. `PluginMcpToolRegistrationVisitor` instead captures registrations and resolves dependencies inside `AddSingleton` factory delegates. Those dependencies are not visible to normal container validation.

`PluginMcpServerToolFactory` and `CodeActionMcpServerToolFactory` duplicate the real registration path solely for tests and integration support. `CodeActionMcpServerToolFactory` uses `IServiceProvider` with `ActivatorUtilities`, contrary to the established constructor-injection boundary.

### Decision

Align plugin registration with the Code Action pattern. Register the closed plugin registration and closed adapter types directly, and let adapters receive their registration, protocol factory and execution-context factory through constructors. Remove both production factory classes.

Test and integration-support code should either construct adapters from explicit mocks or build the real Host registrations. If a typed visitor remains useful for harnesses, it belongs in test support and must not use a service locator.

### Working checklist

- [x] Register each closed `PluginQueryRegistration<TRequest, TResponse>` or `PluginMutationRegistration<TRequest>` as an instance.
- [x] Register plugin MCP adapters by implementation type without `IServiceProvider` factory lambdas.
- [x] Retain the existing direct Code Action registration pattern.
- [x] Remove `PluginMcpServerToolFactory` and `CodeActionMcpServerToolFactory` from production.
- [x] Replace their unit tests with tests of the real registration visitors and resolvable service descriptors.
- [x] Move any necessary harness-only construction into integration test support with explicit dependencies.
- [x] Add a Host composition test that enables dependency and scope validation and resolves all registered MCP tools.

Complexity: medium after H2.

### Recommended location

- Shared MCP adapter base and registration abstractions: `src/Roslyn.Workbench.Mcp/ToolExecution` with namespace `Roslyn.Workbench.Mcp.ToolExecution`.
- Plugin registrations and adapters: `src/Roslyn.Workbench.Mcp/ToolExecution/Plugins` with namespace `Roslyn.Workbench.Mcp.ToolExecution.Plugins`.
- Code Action registrations and adapters: `src/Roslyn.Workbench.Mcp/ToolExecution/CodeActions` with namespace `Roslyn.Workbench.Mcp.ToolExecution.CodeActions`.
- Unit tests mirror the same `ToolExecution`, `ToolExecution/Plugins` and `ToolExecution/CodeActions` structure beneath `test/Roslyn.Workbench.Mcp.Test`.
- Harness-only construction belongs in the owning integration-test-support feature, not in a production `Factories` folder.

## H4: Unhandled MCP Exception Handling

### Finding

`McpServerToolBase` and `ServerOwnedToolBase` previously caught unhandled exceptions independently and returned a sanitized `UnhandledException` response containing a new correlation identifier. That duplicated a transport-level concern across both tool families and required every concrete tool to receive an exception-handling dependency.

Cancellation is correctly rethrown and must remain outside exception translation.

### Decision

Add one Host-owned `CallTool` request filter at the MCP server boundary. The filter creates the correlation identifier, logs the exception and requested tool identity with that identifier, and returns the unchanged sanitized MCP envelope. Register it once through the MCP SDK request-filter builder. Tool classes and their base classes do not participate in unhandled exception translation.

Do not merge the two base classes solely to remove a small amount of orchestration: one binds server-owned request contracts and one delegates pre-bound plugin/Code Action arguments. Their execution responsibilities are meaningfully different.

### Working checklist

- [x] Introduce one focused `CallTool` exception filter with `ILogger`.
- [x] Register the filter through the MCP SDK `WithRequestFilters` API.
- [x] Log the exception, tool name and generated correlation identifier without exposing exception details to MCP clients.
- [x] Register the filter once around the MCP server's tool-call pipeline.
- [x] Remove unhandled exception handling dependencies from all tools, adapters and tool base classes.
- [x] Continue to rethrow `OperationCanceledException`.
- [x] Preserve the current `UnhandledException` code, public message, envelope and `IsError` behaviour.
- [x] Cover correlation matching, logging, cancellation and sanitized publication.

Complexity: medium.

### Recommended location

- Shared call-tool exception filtering: `src/Roslyn.Workbench.Mcp/ToolExecution`; registration remains in Host composition.
- Plugin and Code Action adapter-specific result mapping remains in their respective `ToolExecution/Plugins` and `ToolExecution/CodeActions` folders.
- Server-owned tool execution remains in `src/Roslyn.Workbench.Mcp/Tools`; those tools contain no exception-filtering dependency.
- Unit tests mirror the production owner under `test/Roslyn.Workbench.Mcp.Test/ToolExecution` or `Tools` as appropriate.

## H5: Host Composition Root

### Finding

`RoslynWorkbenchHostApplicationBuilderExtensions.AddRoslynWorkbench` is the correct composition root, but its helper methods now conceal too many phases. `AddCoreServices` registers Workspace, Plugins, CodeActions and Host services in one long list. `PluginCatalogComposition.CreateLoader` hides a second manually constructed object graph before DI.

Pre-DI catalogue materialisation is legitimate because the complete MCP tool set must be known before Host DI is built. The issue is not manual construction at the composition root; it is that the boundary and resulting startup state are implicit.

### Decision

Represent pre-DI work as an explicit startup composition containing validated options, the Code Action catalogue and the plugin catalogue. Give plugin bootstrap construction one focused owner. Split registrations by architectural ownership while keeping `AddRoslynWorkbench` as the readable ordered coordinator.

Do not build a temporary service provider. Do not introduce interfaces for deterministic registration extensions. Do not split each registration line into a separate class.

### Resolution

`HostStartupComposer` now owns the ordered pre-DI phase and returns an explicit `HostStartupComposition` containing the validated configuration, Code Action catalogue and plugin catalogue. It creates the Code Action catalogue first and passes the combined Code Action and server-owned names into plugin loading as protected names. `PluginCatalogBootstrap` is the named owner of the manual plugin-loading object graph; the former hidden static loader factory has been removed.

The Host builder now reads as the startup sequence described below. Options, Workspace, Plugins, CodeActions, Host services, MCP tools and startup prerequisites are registered through cohesive ownership-based extensions while preserving singleton lifetimes and constructor-visible service dependencies. Host composition remains the only pre-DI coordinator and does not build a temporary service provider.

Host-owned composition types now live under `Hosting`, and external plugin discovery and loading types live under `PluginLoading`, with matching unit-test folders and namespaces. Focused composition coverage validates the startup snapshots, protected-name ordering, complete container with scope and build validation, singleton subsystem services, all registered MCP tools and the framework-level call-tool filter.

### Working checklist

- [x] Add an immutable Host startup-composition result for validated options and catalogue snapshots.
- [x] Give plugin bootstrap object-graph construction one named owner instead of a hidden static loader factory.
- [x] Keep Code Action catalogue creation before plugin collision validation.
- [x] Split service registration into cohesive Workspace, Plugins, CodeActions and Host registration groups.
- [x] Keep all dependencies constructor visible and preserve singleton lifetimes.
- [x] Leave the top-level extension as a short sequence: compose, configure logging/options, register subsystems, register tools, register startup prerequisites and transport.
- [x] Add composition tests for the startup result and complete service graph.

Complexity: high, but mostly structural after H1-H4.

### Recommended location

- Host builder extensions, startup composition results and composition-root orchestration: `src/Roslyn.Workbench.Mcp/Hosting` with namespace `Roslyn.Workbench.Mcp.Hosting`.
- External plugin discovery, PE inspection, load contexts, MEF composition, collision handling and catalogue loading: `src/Roslyn.Workbench.Mcp/PluginLoading` with namespace `Roslyn.Workbench.Mcp.PluginLoading`.
- Plugin public API and execution types remain in the separate Plugins project; `PluginLoading` contains Host-owned loading only.
- Unit tests mirror `Hosting` and `PluginLoading` beneath `test/Roslyn.Workbench.Mcp.Test`.
- Keep `Program.cs` at the Host project root as the executable entry point.

## H6: Startup Lifecycle Ordering

### Finding

MSBuild registration and durable commit recovery are separate `IHostedService` registrations placed before the MCP server registration. The Generic Host starts registered hosted services during startup, but the safety requirement that recovery completes before MCP publication is currently expressed only by registration order.

The Generic Host lifecycle provides a distinct `StartingAsync` phase before hosted-service `StartAsync`, which is a clearer boundary for startup prerequisites.

### Decision

Make recovery-before-transport explicit through a Host startup-prerequisite lifecycle boundary. MSBuild registration may participate in the same prerequisite phase or remain a separate service if no ordering exists between it and recovery. Ordinary MSBuild unavailability should continue to produce component status rather than terminate the server; an unrecoverable recovery conflict must continue to block Workspace opening according to Workspace policy.

### Resolution

`StartupPrerequisiteLifecycleService` now owns the Host's prerequisite `IHostedLifecycleService.StartingAsync` phase. It records MSBuild availability through the existing registration service and then awaits durable Workspace recovery with the Host cancellation token. The Generic Host completes every lifecycle service's `StartingAsync` phase before invoking any hosted service's `StartAsync`, so the stdio MCP transport cannot accept tool calls before recovery finishes.

The former independent MSBuild-registration and recovery hosted services have been removed. Their underlying services remain separate: MSBuild registration continues to translate ordinary discovery failures into component status, while Workspace recovery retains its existing recovery-conflict and Workspace-opening policy. The Host-specific MSBuild bridge and registration types now live with the lifecycle coordinator under `Hosting`.

Focused tests cover prerequisite execution, pre-start cancellation, no-op lifecycle phases and the real Generic Host ordering against a transport hosted service.

### Working checklist

- [x] Replace implicit hosted-service ordering with an explicit startup-prerequisite lifecycle boundary.
- [x] Ensure recovery completes before any MCP transport can accept tool calls.
- [x] Preserve graceful cancellation during startup.
- [x] Keep MSBuild availability reporting separate from recovery outcomes.
- [x] Add a focused Host lifecycle test proving prerequisite completion precedes transport startup.

Complexity: low to medium.

### Recommended location

- MSBuild registration, Workspace recovery startup coordination and other Host lifecycle prerequisites: `src/Roslyn.Workbench.Mcp/Hosting`.
- `HostConfiguredMsBuildWorkspaceFactory` remains in `Hosting` because it bridges Host composition into Workspace creation.
- Unit tests: `test/Roslyn.Workbench.Mcp.Test/Hosting` with the matching namespace.
- Do not create separate folders for individual hosted services; group them by the Host lifecycle feature.

## H7: Focused Cleanup and Re-Audit

### Finding

The remaining Host services are generally cohesive. In particular:

- the plugin discovery and composition collaborators should remain separate;
- `ServerStatusService` is a reasonable status projection service;
- `MsBuildRegistrationService` is a focused external-runtime boundary;
- pure serializers, inspectors, mappers and registration ledgers should remain static; and
- the separate plugin and Code Action MCP mutation adapters should not be forced behind a generic abstraction because their result and staging semantics differ.

There is small duplicated `CallToolResult` construction across the four plugin and Code Action adapters, and the Host friend-assembly list should be checked after removal of the production test factories.

### Resolution

The four plugin and Code Action adapters now use one protected `McpServerToolBase.CreateStructuredResult` helper for the identical MCP result projection. Their query, mutation, failure and staging orchestration remains in the owning closed-generic adapters.

The final invariant audit also found that the Code Action query lease still permitted construction of a successful lease without a context. Query leases now use the same acquired/rejected factory and `MemberNotNullWhen` pattern as the mutation path. The neutral Workspace query lease exposes the corresponding state relationship, allowing both Code Action and plugin context factories to remove redundant impossible-state throws.

The exception review retained broad catches only at intentional isolation boundaries: unhandled MCP transport failures are logged and correlated; plugin code loading, configuration and materialisation failures disable only the affected plugin with diagnostics; and MSBuild discovery failures remain component-status data. PE inspection, package discovery and path validation retain filtered catches for their documented external failure sets. No expected Host workflow uses exceptions as flow control.

`Roslyn.Workbench.Mcp.TestSupport` no longer has Host friend access because it does not reference or consume Host internals. New architecture coverage locks the remaining direct consumers and asserts that only Host owns the MCP SDK package. The existing production dependency-graph checks remain green.

Status projection now lives under `Status`, and the Workspace-to-tool result mapper lives with server-owned `Tools`, with matching unit-test folders and namespaces. The final null-forgiving, constructor-guard and getter audits found no Host violations.

### Working checklist

- [x] Move genuinely identical adapter result construction into `McpServerToolBase` without hiding plugin or Code Action orchestration.
- [x] Re-audit exception filters at external plugin, MEF and MSBuild boundaries and retain broad catches only where startup isolation is intentional and diagnosed.
- [x] Review Host `InternalsVisibleTo` entries after test-support construction changes and remove stale access.
- [x] Re-run the forbidden dependency and MCP ownership architecture tests.
- [x] Re-run the null-forgiving, constructor-guard, flow-control-exception and expression-bodied-property audits for changed Host files.
- [x] Mark this document complete only after the production re-audit finds no unresolved Host architecture issues.

Complexity: low.

### Recommended location

- Adapter cleanup remains under the appropriate `ToolExecution` owner.
- Plugin and MEF exception-boundary cleanup remains under `PluginLoading`.
- MSBuild and startup-lifecycle cleanup remains under `Hosting`.
- Server status projection, including `IServerStatusService` and `ServerStatusService`, belongs in `src/Roslyn.Workbench.Mcp/Status`, with tests in `test/Roslyn.Workbench.Mcp.Test/Status`.
- `WorkspaceToolResultMapper` belongs with server-owned tools in `Tools` because it adapts Workspace results for those Host tools.
- Architecture regression tests may use `test/Roslyn.Workbench.Mcp.Test/Architecture`; do not create a production `Architecture` or `Cleanup` folder.

## Unit Testing Next Step

After H1-H7 are complete, create or refresh the Host unit-test inventory against the final classes. The inventory should record each logic-bearing Host class, existing line and branch coverage, pattern compliance and missing scenarios.

Priority groups should be:

1. startup options, startup composition and lifecycle prerequisites;
2. schema provider and protocol construction;
3. typed registration visitors and all four MCP adapters;
4. unhandled tool exception filtering and server-owned tool base behaviour;
5. plugin package discovery, metadata, preparation, collision and catalogue orchestration; and
6. status projection, result mapping, binding and serializers.

The Host integration redesign remains the following phase. It should cover real stdio transport, real MCP SDK schema export, real MEF/load contexts, MSBuild registration, durable restart recovery and end-to-end tool publication only after unit-testable Host logic is complete.

## Validation Sources

The design decisions above are consistent with current [.NET Generic Host](https://learn.microsoft.com/dotnet/core/extensions/generic-host) and [.NET options](https://learn.microsoft.com/dotnet/core/extensions/options#options-validation) guidance:

- the Generic Host owns DI, logging, startup and `IHostedService` lifecycle;
- options can be validated during startup with `ValidateOnStart`; and
- `IHostedLifecycleService` provides a startup phase before ordinary hosted-service `StartAsync`.

Repository source, current architecture documents and existing Host tests remain the authority for project-specific wire compatibility and ownership constraints.
