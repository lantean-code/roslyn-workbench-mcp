# MEF Plugin Composition And Fluent Tool Registration Plan

> **Obsolete:** This plan predates the separation of internal Code Actions from the third-party plugin system and the move of all MCP transport adapters into Host. It is retained for historical context only. Use [2026-07-13-mef-plugin-composition.md](2026-07-13-mef-plugin-composition.md) for implementation.

## Summary

Replace the current assembly scanning and manually constructed tool registration with a MEF-composed plugin configuration model suitable for third-party authors.

The change improves plugin discovery, metadata authoring, handler construction, validation, dependency loading, and diagnostics. It deliberately preserves the existing typed execution pipeline after configuration has been materialised.

The intended authoring shape is:

```csharp
public void Configure(IPluginConfiguration plugin)
{
    plugin.AddQueryTool<GetSolutionStructureTool>();

    plugin.AddMutationTool<RenameSymbolTool>(tool => tool
        .WithName("rename-symbol")
        .WithTitle("Rename Symbol")
        .WithDescription("Renames a resolved symbol.")
        .IsDestructive());
}
```

`GetSolutionStructureTool` may provide its tool metadata declaratively:

```csharp
[RoslynTool(
    "get-solution-structure",
    Title = "Get Solution Structure",
    Description = "Returns solution folders, projects and direct project relationships.")]
internal sealed class GetSolutionStructureTool
    : IQueryToolHandler<GetSolutionStructureRequest, SolutionStructureData>
{
    // Execution only.
}
```

Attributes and fluent configuration feed the same internal metadata builder. Attributes are optional; fluent values override attribute values. Registration fails only when the merged metadata is incomplete or invalid.

## Goals

- Make third-party plugin authoring explicit, predictable, and well documented.
- Use MEF to discover and compose plugin definitions before the host service provider is built.
- Let a plugin register a handler by type with `AddQueryTool<THandler>()` or `AddMutationTool<THandler>()`.
- Remove the requirement for each tool handler to own a static `Register` method or construct itself.
- Support both attribute-based and fluent tool metadata.
- Validate handler lifetime and structural requirements before exposing any tool from a plugin.
- Preserve the current generic request binding, execution adapter, context acquisition, mutation staging, and result publication pipeline.
- Keep host DI and plugin composition separate.
- Isolate a third-party plugin's dependencies and report actionable load diagnostics without preventing unrelated plugins from loading.

## Non-Goals

- Changing the plugin tool invocation pipeline.
- Resolving handlers from the host `IServiceProvider`.
- Allowing handlers to import host services through MEF.
- Hot loading, unloading, or changing `tools/list` after server startup.
- Treating an `AssemblyLoadContext` as a security sandbox.
- Supporting stateful, scoped, transient, or disposable tool handlers.
- Allowing plugins to replace server-owned workspace or transaction tools.

## Existing Pipeline To Preserve

The current execution pipeline is already strongly typed and should remain intact:

```text
PluginRegistry.RegisterQueryTool<TRequest, TResponse>(metadata, handler)
  -> RegisteredTool
  -> QueryPluginToolExecutionAdapter<TRequest, TResponse>(handler)
  -> RegisteredPluginTool
  -> PluginMcpServerTool
  -> request binding
  -> query context acquisition
  -> handler.ExecuteAsync(...)
  -> structured MCP result
```

Mutation tools follow the equivalent path through `RegisterMutationTool<TRequest>` and `MutationPluginToolExecutionAdapter<TRequest>`.

The new composition model ends when it calls the existing generic `PluginRegistry` methods. The following types and responsibilities remain unchanged unless a small compatibility adjustment is independently justified:

- `IQueryToolHandler<TRequest, TResponse>`
- `IMutationToolHandler<TRequest>`
- `PluginRegistry`'s current typed registration methods
- `RegisteredTool`
- `RegisteredPluginTool`
- `IPluginToolExecutionAdapter`
- `QueryPluginToolExecutionAdapter<TRequest, TResponse>`
- `MutationPluginToolExecutionAdapter<TRequest>`
- `PluginMcpServerTool`
- query and mutation execution contexts
- mutation staging and published result shaping

## Composition Must Complete Before Host DI Is Built

The complete plugin tool list must be known while service descriptors can still be added to the host service collection. Plugin discovery therefore cannot be performed by a service resolved from the final host `IServiceProvider`.

The startup sequence is:

```text
Parse startup options
  -> discover plugin packages
  -> create plugin AssemblyLoadContexts
  -> compose plugin definitions with MEF
  -> invoke plugin configuration
  -> validate configured tool metadata and handler types
  -> construct handlers and materialise PluginRegistry entries
  -> produce immutable PluginCatalogSnapshot
  -> add one McpServerTool service descriptor per registered plugin tool
  -> register the completed catalogue for runtime status and disposal
  -> build the host IServiceProvider
```

The builder-time abstraction should be named for composition rather than runtime provision, for example:

```csharp
internal interface IPluginCatalogComposer
{
    PluginCatalog Compose(
        StartupOptions startupOptions,
        IReadOnlyList<PluginPackage> bundledPlugins);
}
```

`PluginCatalog` owns the immutable snapshot plus any MEF containers and load contexts that must remain alive for the process lifetime. The completed catalogue can be registered as a singleton so the host disposes it during shutdown. MEF container types must not escape this boundary.

Do not build a temporary host `IServiceProvider` during composition.

## MEF Responsibility

MEF discovers and composes plugin definitions or configuration modules. It does not replace host DI and does not participate in each tool invocation.

A plugin definition provides:

- stable plugin identity;
- display name;
- semantic version;
- supported plugin API version;
- a `Configure(IPluginConfiguration)` method.

The exact MEF export shape should be proven in a focused spike, but the conceptual contract is:

```csharp
public interface IRoslynPlugin
{
    void Configure(IPluginConfiguration plugin);
}
```

Plugin-level metadata should be available as MEF export metadata or package manifest metadata so compatibility can be checked before running plugin configuration code.

MEF should not compose individual application services into handlers. Handler construction remains controlled by the Roslyn Workbench plugin materialiser.

## Fluent Tool Configuration

`IPluginConfiguration` records tool definitions without constructing handlers:

```csharp
public interface IPluginConfiguration
{
    void AddQueryTool<THandler>();

    void AddQueryTool<THandler>(Action<IToolMetadataBuilder> configure);

    void AddMutationTool<THandler>();

    void AddMutationTool<THandler>(Action<IMutationToolMetadataBuilder> configure);
}
```

The final API may return fluent builders instead of accepting callbacks, but builders must not escape the configuration phase or remain mutable after configuration completes.

Each call records a non-generic internal definition containing at least:

```csharp
internal sealed record ConfiguredPluginTool
{
    public ToolKind Kind { get; init; }

    public Type HandlerType { get; init; } = typeof(object);

    public ConfiguredToolMetadata Metadata { get; init; } = new();
}
```

The stored `Type` is used for startup inspection, validation, construction, and diagnostics. It is not used during tool invocation.

Query and mutation builders expose only author-controlled metadata. Tool kind, request and response types, plugin identity, schema publication mode, and most MCP annotations are derived by the system.

## Attribute And Fluent Metadata

Tool metadata may be supplied with a `RoslynToolAttribute`, fluent configuration, or a combination of both.

Suggested attribute shape:

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RoslynToolAttribute : Attribute
{
    public RoslynToolAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string? ResultSummary { get; set; }

    public bool Destructive { get; set; }
}
```

Metadata resolution is deterministic:

1. Read attribute values from the handler type.
2. Apply explicitly configured fluent values as overrides.
3. Add values derived from plugin identity, tool kind, and handler contracts.
4. Validate the merged metadata.
5. Materialise the existing `ToolRegistrationMetadata` used by `PluginRegistry`.

Expected outcomes:

| Attribute metadata | Fluent metadata | Outcome |
|---|---|---|
| Complete | None | Valid |
| None | Complete | Valid |
| Complete | Partial overrides | Valid; fluent values win |
| Partial | Completes missing values | Valid |
| None | None | Registration error |
| Partial | Still incomplete | Registration error |

Rules:

- Tool name, title, and description are required after merging.
- Only one tool metadata attribute is allowed.
- Tool kind is inferred from the implemented handler interface and is never declared in metadata.
- Query tools derive `ReadOnlyHint = true` and `IdempotentHint = true`.
- Mutation tools derive their mutation annotations and may configure destructive behaviour.
- Destructive metadata on a query is an authoring error.
- `OpenWorldHint` remains host-controlled and false.
- Request and response types are inferred from the closed handler interface.
- Output schema publication remains a host startup setting.
- Plugin identity is inherited from the enclosing plugin definition and cannot be overridden per tool.

`ToolRegistrationMetadata` should remain the validated internal representation passed to the existing registry. Third-party authors should normally use the attribute or fluent builder rather than construct that record directly.

## Handler Materialisation And Generic Bridge

`AddQueryTool<THandler>()` intentionally records `typeof(THandler)`. At materialisation time, the system must bridge that runtime type back into the existing generic registry.

The materialiser performs startup-only reflection:

1. Inspect the handler type's implemented interfaces.
2. For a configured query, require exactly one closed `IQueryToolHandler<TRequest, TResponse>`.
3. For a configured mutation, require exactly one closed `IMutationToolHandler<TRequest>`.
4. Extract and validate the request and response contract types.
5. Construct the handler with its permitted parameterless constructor.
6. Close a private generic materialisation method with the discovered contract types.
7. Call the existing generic `PluginRegistry` registration method with the typed handler.

Conceptual query bridge:

```csharp
private static void MaterialiseQueryTool<TRequest, TResponse>(
    PluginRegistry registry,
    ToolRegistrationMetadata metadata,
    object handler)
    where TRequest : WorkspaceBoundRequest
{
    registry.RegisterQueryTool(
        metadata,
        (IQueryToolHandler<TRequest, TResponse>)handler);
}
```

The mutation bridge follows the same pattern.

Reflection is confined to catalogue construction. The result is the same typed adapter holding the same concrete handler reference as the current system. There is no reflection, service resolution, or handler lookup on the invocation path.

The materialiser may cache closed materialisation delegates per handler contract if measurements show value, but caching is not required for correctness because composition happens once at startup.

## Handler Lifetime And Safety Rules

Handlers remain single instances retained for the lifetime of the plugin catalogue. They must be stateless, thread-safe, and non-disposable. Application capabilities continue to arrive through `IQueryContext` or `IMutationContext` on each invocation.

Before constructing a handler, reject types that:

- are abstract, interfaces, open generics, or otherwise non-constructible;
- do not implement exactly one handler contract matching the configured query or mutation kind;
- do not provide the permitted parameterless constructor;
- declare instance fields or mutable static fields;
- declare writable instance properties or events that imply retained state;
- implement `IDisposable` or `IAsyncDisposable`;
- declare MEF imports or importing constructors;
- use invalid, open, or incompatible request or response contract types.

The exact structural rules require a compatibility pass against all bundled handlers before implementation. Compiler-generated members must be handled deliberately rather than producing accidental false positives.

Runtime inspection cannot prove semantic thread safety. Add a companion analyser or build-time validation package for third-party authors so violations are reported during development. Runtime validation remains authoritative because analysers can be disabled.

Constructor execution occurs only after the whole plugin's configuration, metadata, compatibility, and collision checks have succeeded. Construction failures disable that plugin and appear in `server-status`; unrelated plugins continue loading.

## Plugin Package And Assembly Loading

Treat a plugin as a package directory rather than treating every DLL in a configured directory as a plugin entry assembly.

A package manifest should identify:

- plugin ID;
- display name;
- version;
- supported plugin API version;
- entry assembly;
- optional additional MEF composition assemblies.

Each external plugin package should use its own `AssemblyLoadContext` with `AssemblyDependencyResolver`:

- Roslyn Workbench plugin contract assemblies resolve from the host context so type identity is shared.
- Private plugin dependencies resolve from the plugin package.
- Dependency DLLs are not independently interpreted as plugin entry points.
- A failed package can be discarded without corrupting the catalogue for unrelated packages.

Bundled plugins must follow the same configuration and materialisation path, although their package locations may be supplied directly by the host.

Plugins run as fully trusted in-process code. `AssemblyLoadContext` provides dependency isolation, not security isolation. This must be explicit in third-party author documentation.

## Validation And Failure Semantics

Plugin enablement remains atomic: if any configured tool is invalid or cannot be materialised, none of that plugin's tools are exposed.

Validation order should be deterministic:

1. Package manifest and entry assembly.
2. Plugin export cardinality and plugin metadata.
3. Plugin API compatibility.
4. Plugin configuration execution.
5. Tool metadata completeness and naming rules.
6. Handler structural and contract validation.
7. Duplicate names within the plugin.
8. Duplicate plugin IDs and global tool names across all candidates.
9. Handler construction.
10. Existing `PluginRegistry` contract and schema validation.

Global collision behaviour must not depend silently on directory enumeration order. Server-owned tool names are reserved and cannot be overridden. The implementation plan must define whether two conflicting external plugins are both disabled or whether an explicit deterministic precedence policy exists.

Diagnostics should identify the plugin, handler type, tool name when available, validation stage, and an actionable message without leaking sensitive host details.

## Host Integration

The completed `PluginCatalogSnapshot` remains the host-facing representation used by:

- plugin MCP tool registration;
- `server-status` plugin diagnostics;
- tool counts;
- integration tests.

The existing pre-build loop that adds one `McpServerTool` service descriptor per `RegisteredPluginTool` can remain initially. Moving final `McpServerOptions.ToolCollection` assembly into `IConfigureOptions<McpServerOptions>` is optional and must not move plugin discovery past the service-provider build boundary.

If an options configurator is introduced, it consumes the already completed catalogue. It must not discover plugins, materialise handlers, add service descriptors, or resolve a composition service that has not yet run.

## Proposed Implementation Phases

### Phase 1: Prove The Composition Boundary

- Select and add the MEF implementation.
- Create a minimal exported plugin fixture.
- Compose it before host DI is built.
- Prove plugin metadata is available early enough for compatibility checks.
- Prove the resulting tools can be registered into the existing MCP server without changing either execution adapter.
- Record container and load-context ownership and disposal behaviour.

### Phase 2: Introduce The Configuration Model

- Add `IPluginConfiguration`.
- Add query and mutation metadata builders.
- Add `ConfiguredPluginTool` and the immutable completed plugin configuration.
- Change plugin entry points from direct `IPluginRegistry` mutation to fluent configuration.
- Keep `ToolRegistrationMetadata` as the validated bridge into `PluginRegistry`.
- Migrate the small fixture plugins first.

### Phase 3: Add Metadata Attributes And Resolution

- Add `RoslynToolAttribute`.
- Implement attribute discovery and fluent override precedence.
- Validate required fields and family-specific metadata.
- Add focused tests for attribute-only, fluent-only, combined, and missing metadata.

### Phase 4: Materialise Into The Existing Registry

- Add the startup-only handler contract inspector.
- Add handler structural validation.
- Construct handlers after plugin-wide validation.
- Add the generic reflection bridge into the existing registry methods.
- Verify that the resulting `RegisteredPluginTool` and adapter types are identical to current registrations.

### Phase 5: Add Package-Aware Loading

- Define and document the package manifest.
- Add per-package `AssemblyLoadContext` and `AssemblyDependencyResolver` handling.
- Share the public plugin contract assembly with the host context.
- Isolate load and composition failures per package.
- Make collision handling deterministic.

### Phase 6: Migrate Bundled Plugins

- Convert `BundledCorePlugin` and `BundledCodeActionsPlugin` to fluent configuration.
- Move tool metadata to attributes where that improves locality, retaining fluent configuration where central ownership is clearer.
- Remove individual handler `Register` methods.
- Remove or repurpose the bundled static registrars after all tools use the new configuration path.
- Preserve all existing handler execution implementations.

### Phase 7: Third-Party Authoring Support

- Publish the plugin contracts and metadata APIs with complete XML documentation.
- Add a minimal plugin template and representative query and mutation examples.
- Add an analyser or validation package for handler lifetime rules.
- Add an offline plugin-package validation command or test helper.
- Document API compatibility, packaging, dependency isolation, trust, diagnostics, and restart requirements.

## Test Plan

### Plugin API And Configuration Tests

- Attribute-only query registration.
- Fluent-only query registration.
- Attribute-only mutation registration.
- Fluent-only mutation registration.
- Fluent overrides attribute metadata.
- Partial attribute plus fluent completion.
- Missing required metadata fails.
- Destructive query metadata fails.
- Duplicate handler and tool-name registration fails deterministically.

### Handler Validation Tests

- Valid parameterless stateless query and mutation handlers pass.
- Missing or inaccessible permitted constructor fails.
- Stateful instance fields fail.
- Mutable static state fails.
- Disposable handlers fail.
- MEF imports on handlers fail.
- Multiple or mismatched handler contracts fail.
- Open or invalid request and response contracts fail.
- Constructor is not run before plugin-wide metadata and collision validation completes.

### Materialisation And Execution Tests

- The materialiser calls the existing generic query registry method.
- The materialiser calls the existing generic mutation registry method.
- Registered query tools contain `QueryPluginToolExecutionAdapter<TRequest, TResponse>`.
- Registered mutation tools contain `MutationPluginToolExecutionAdapter<TRequest>`.
- The adapters retain the constructed handler for catalogue lifetime.
- Existing query invocation behaviour is unchanged.
- Existing mutation staging behaviour is unchanged.
- No handler lookup or reflection occurs during invocation.

### Composition And Package Tests

- Plugin tools are fully determined before host DI is built.
- Bundled and external plugins use the same configuration and materialisation path.
- A malformed plugin does not prevent unrelated plugins from loading.
- Duplicate plugin IDs and tool names have deterministic outcomes.
- Dependency DLLs are not treated as plugin entry assemblies.
- Two plugins can carry different versions of the same private dependency.
- Plugin contract type identity is shared with the host.
- Plugin catalogue, MEF containers, and load contexts are disposed at shutdown.

## Likely File Areas

Primary plugin contracts and configuration:

- `src/Roslyn.Workbench.Mcp.Plugins/IRoslynPlugin.cs`
- `src/Roslyn.Workbench.Mcp.Plugins/IPluginRegistry.cs`
- new configuration and metadata builder types under `src/Roslyn.Workbench.Mcp.Plugins`
- new handler inspection and materialisation types under `src/Roslyn.Workbench.Mcp.Plugins`

Host composition and package loading:

- `src/Roslyn.Workbench.Mcp/PluginCatalogLoader.cs`
- `src/Roslyn.Workbench.Mcp/PluginCatalogSnapshot.cs`
- `src/Roslyn.Workbench.Mcp/RoslynWorkbenchHostApplicationBuilderExtensions.cs`
- new MEF catalogue and plugin package loading types under `src/Roslyn.Workbench.Mcp`

Bundled plugin registration:

- `src/Roslyn.Workbench.Mcp.Plugins.Core/BundledCorePlugin.cs`
- `src/Roslyn.Workbench.Mcp.Plugins.Core/BundledCoreToolRegistrar.cs`
- `src/Roslyn.Workbench.Mcp.CodeActions/BundledCodeActionsPlugin.cs`
- `src/Roslyn.Workbench.Mcp.CodeActions/BundledCodeActionToolRegistrar.cs`
- individual bundled handler metadata and `Register` methods

Tests:

- `test/Roslyn.Workbench.Mcp.Plugins.Test`
- `test/Roslyn.Workbench.Mcp.IntegrationTest/PluginDiscoveryAndMcpToolTests.cs`
- `test/TestFixtures/Plugins`
- host composition tests under `test/Roslyn.Workbench.Mcp.Test`

## Acceptance Criteria

- A third-party plugin can declare query and mutation tools with `AddQueryTool<THandler>()` and `AddMutationTool<THandler>()`.
- Tool metadata can be attribute-only, fluent-only, or a deterministic merge of both.
- Missing or invalid merged metadata disables the plugin with an actionable diagnostic.
- Plugin authors do not construct handlers or write per-handler registration methods.
- The plugin system constructs one handler instance per registered tool after validation.
- The current generic execution adapters retain those handler instances and execute without runtime reflection or service lookup.
- The full tool list is fixed before the host service provider is built.
- MEF and plugin load-context types do not leak into host services or handler APIs.
- Host services remain available only through invocation contexts.
- Invalid plugins do not prevent unrelated plugins or the server from starting.
- `server-status` reports plugin composition, compatibility, validation, and construction failures.
- Bundled plugins use the same public authoring and materialisation model intended for third parties.
