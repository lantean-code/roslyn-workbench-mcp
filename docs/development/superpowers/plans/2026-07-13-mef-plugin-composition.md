# MEF Plugin Composition And Packaging Plan

## Status

This plan replaces `2026-07-06-plugin-composition-follow-up.md`.

Implemented on 2026-07-13. External packages now use assembly metadata only; no JSON or other sidecar manifest is part of the plugin contract.

It reflects the current architecture in which:

- Code Actions are internal and are not plugins;
- Workspace owns neutral execution contexts and transaction staging;
- Plugins owns the public third-party extension API and typed registrations;
- Plugins.Core supplies the bundled first-party plugin;
- Host alone owns package discovery, assembly loading, MCP transport and server status.

## Summary

Replace Host's direct assembly scanning and plugin-controlled `IPluginRegistry` mutation with package-aware MEF composition and a fluent plugin configuration model.

MEF composes plugin definitions during startup. It does not construct application services, replace Host dependency injection or participate in tool invocation. After configuration and validation, plugin handlers are materialised into the existing closed generic registrations and dispatched through the existing typed visitor into Host-owned MCP adapters.

The intended authoring shape is:

```csharp
public sealed class ExamplePlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        _ = configuration.AddQueryTool<GetSolutionStructureTool>();

        _ = configuration.AddMutationTool<RenameSymbolTool>()
            .WithName("rename-symbol")
            .WithTitle("Rename Symbol")
            .WithDescription("Stages a symbol rename across the effective solution.")
            .IsDestructive();
    }
}
```

A handler may instead provide some or all tool metadata declaratively:

```csharp
[RoslynTool(
    "get-solution-structure",
    "Get Solution Structure",
    "Returns projects, target frameworks and direct project relationships.")]
internal sealed class GetSolutionStructureTool
    : IQueryToolHandler<GetSolutionStructureRequest, SolutionStructureData>
{
    // Execution only.
}
```

Attributes and fluent configuration feed one internal metadata builder. Fluent values override attribute values. Registration succeeds only when the merged metadata is complete and valid.

## Architectural Decisions

### Code Actions are outside plugin composition

Code Actions continue to use their internal catalogue, registrations and typed visitor. They are never discovered through MEF, never represented by `IRoslynPlugin`, never loaded from plugin packages and never reported as plugins.

Host constructs the Code Action catalogue first. Code Action tool names and server-owned tool names are reserved before plugin collision validation begins.

Nothing in this plan changes Code Action handler registration, runtime composition, execution contexts or MCP adapters.

### Plugins.Core remains a referenced bundled assembly

`Roslyn.Workbench.Mcp.Plugins.Core` remains a normal project reference of Host and is published with the application in the normal output directory. It is not copied into an external `plugins` directory and is not rediscovered through external package enumeration.

Host supplies the Plugins.Core assembly as its bundled plugin input. Host reads the same plugin metadata from that assembly, composes it in the default `AssemblyLoadContext`, and sends `BundledCorePlugin` through the same MEF configuration, metadata resolution, validation and materialisation path as an external plugin.

This provides one authoring model without imposing external package semantics on a trusted built-in component. It also prevents duplicate discovery, makes the bundled plugin available even when no plugin directory exists and preserves the existing compile-time Host-to-Plugins.Core dependency.

### External plugins are packages

An external plugin is one immediate child package directory beneath a configured search root. A package contains exactly one assembly with exactly one `RoslynPluginAttribute`; other DLLs are package dependencies. Arbitrary DLLs directly in a search root are not interpreted as plugins and discovery is not recursive.

The marked entry assembly is the authoritative source for:

- stable plugin ID;
- display name;
- semantic version;
- supported plugin API version;
- entry type and assembly.

The attribute supplies ID, display name and supported API version. Semantic version comes from `AssemblyInformationalVersionAttribute` and is validated with `NuGet.Versioning`. Host reads PE metadata without loading plugin code and validates identity, version and exact API compatibility before composition.

### Package-local MEF composition

Use the modern `System.Composition` implementation. Host creates a separate MEF container for each bundled descriptor or external package and requires exactly one `IRoslynPlugin` export from the entry assembly.

`RoslynPluginAttribute` derives from `System.Composition.ExportAttribute`, is a MEF metadata attribute and exports `IRoslynPlugin`. The single public marker therefore provides both discovery metadata and composition. Only marked plugin entry points are composed. Handler types are recorded by configuration and constructed later by the plugin materialiser.

A composition failure disables only that plugin.

### Host DI and MEF remain separate

Composition completes before the Host service provider is built. Do not build a temporary `IServiceProvider` during plugin composition.

MEF must not import Host services into plugin definitions or handlers. Runtime capabilities remain available only through `IQueryContext` and `IMutationContext`.

### Load-context lifetime

Each external package uses its own `AssemblyLoadContext` with `AssemblyDependencyResolver`. The public Plugins and Workspace contract assemblies resolve from the default context so type identity is shared. Private managed and native dependencies resolve from the package.

`AssemblyLoadContext` is not disposable. Hot unloading is out of scope, so package load contexts live for the process lifetime. The catalogue retains the loaded registrations and any ownership objects required by the composition implementation. A short-lived MEF container may be disposed after configuration when no composed export must remain alive.

Load contexts provide dependency isolation, not security or reliability isolation. Plugins execute as fully trusted in-process code.

### Startup reflection is isolated from invocation

`AddQueryTool<THandler>()` and `AddMutationTool<THandler>()` constrain `THandler` to the corresponding non-generic handler marker and public parameterless construction. They record the handler type and a typed `new THandler()` factory. Recovering the closed request and response contracts still requires startup inspection unless the public API exposes all generic arguments or uses generated registration code.

This plan permits reflection only during configuration validation and materialisation. The resulting registrations remain:

```text
PluginQueryRegistration<TRequest, TResponse>
PluginMutationRegistration<TRequest>
```

Invocation continues through `IPluginToolRegistrationVisitor<TResult>` without reflection, `dynamic`, `object` invocation or service location.

## Final Dependency And Execution Model

Production dependencies remain:

```text
Host ───────→ CodeActions ──→ Workspace
  ├────────→ Plugins.Core ──→ Plugins ──→ Workspace
  ├────────→ Plugins
  └────────→ Workspace
```

Plugin startup becomes:

```text
Parse startup options
  → build the internal Code Action catalogue
  → reserve Code Action and server-owned tool names
  → create the bundled Plugins.Core descriptor
  → enumerate immediate package directories and inspect assembly PE metadata
  → validate package identity and compatibility
  → create one external load context per valid package
  → compose one IRoslynPlugin per descriptor/package with MEF
  → run Configure(IPluginConfiguration)
  → merge and validate tool metadata
  → inspect handler contracts and lifetime rules
  → validate all local and global collisions
  → construct handlers
  → materialise closed generic plugin registrations
  → produce the immutable plugin catalogue
  → add Host MCP tool descriptors through the typed visitor
  → build the Host service provider
```

Plugin invocation remains:

```text
PluginQueryRegistration<TRequest, TResponse>
  → PluginMcpToolRegistrationVisitor
  → PluginQueryMcpServerTool<TRequest, TResponse>
  → request binding
  → PluginExecutionContextFactory
  → handler.ExecuteAsync(...)
  → Host MCP result publication
```

Mutation follows the equivalent path through `PluginMutationRegistration<TRequest>`, `PluginMutationMcpServerTool<TRequest>` and the separate Workspace mutation stager.

## Public Plugin Configuration API

Replace direct third-party access to `IPluginRegistry` with:

```csharp
public interface IRoslynPlugin
{
    void Configure(IPluginConfiguration configuration);
}

public interface IPluginConfiguration
{
    QueryToolConfigurationBuilder AddQueryTool<THandler>()
        where THandler : class, IQueryToolHandler, new();

    MutationToolConfigurationBuilder AddMutationTool<THandler>()
        where THandler : class, IMutationToolHandler, new();
}
```

The concrete returned builders support chaining and become immutable as soon as `Configure` returns.

`IPluginRegistry` and `PluginRegistry` have been removed. Third-party authors cannot bypass configuration validation by supplying constructed handler instances. `ToolRegistrationMetadata` and the closed generic registrations are internal materialisation details.

Each configuration call records an internal definition containing:

- tool family;
- handler type;
- typed handler factory;
- attribute metadata;
- explicit fluent overrides;
- the declaring plugin identity;
- configuration order for deterministic diagnostics only.

The stored runtime type is used only during startup contract inspection and closed-generic materialisation. The captured typed factory constructs the handler after validation.

## Tool Metadata

Tool metadata may be attribute-only, fluent-only or a deterministic merge.

Resolution order is:

1. Read `RoslynToolAttribute` from the handler type.
2. Apply explicit fluent values as overrides.
3. Derive tool family, request and response contracts.
4. Derive Host-controlled MCP annotations.
5. Attach the enclosing plugin identity.
6. Validate the completed metadata.

Rules:

- name, title and description are required;
- result summary remains optional;
- tool family is inferred and cannot be configured;
- request and response contract types are inferred and cannot be configured;
- query tools derive read-only and idempotent MCP hints;
- mutation tools may configure destructive behaviour;
- destructive query metadata is invalid;
- open-world and transport annotations remain Host-owned;
- schema publication mode remains a Host startup option;
- plugin identity cannot be overridden by an individual tool.

## Handler Inspection And Materialisation

For each configured query handler:

1. Require a concrete, closed type.
2. Require exactly one closed `IQueryToolHandler<TRequest, TResponse>` contract.
3. Validate `TRequest` and `TResponse` as public transport contract types.
4. Rely on the generic `new()` constraint for concrete public parameterless construction.
5. Validate the objective lifetime and state rules.
6. Construct one handler instance.
7. Close the private generic materialisation bridge.
8. Register a `PluginQueryRegistration<TRequest, TResponse>`.

Mutation performs the equivalent checks for exactly one closed `IMutationToolHandler<TRequest>` and creates `PluginMutationRegistration<TRequest>`.

Handler implementation types may be non-public, preserving encapsulation for bundled and third-party implementations. Their parameterless constructors must be public so the configuration API can capture a typed factory under its `new()` constraint. Transport request and response contracts remain public because Host serialises them.

Handlers are retained for the catalogue lifetime and must be stateless, thread-safe and non-disposable. Runtime validation must enforce only rules that can be determined objectively without brittle heuristics. At minimum reject:

- missing or multiple matching handler contracts;
- query/mutation family mismatch;
- invalid request or response contracts;
- `IDisposable` or `IAsyncDisposable` handlers;
- MEF imports or importing constructors on handler types;
- handler construction or closed-generic materialisation failure.

Declared instance state, writable properties or events, mutable static fields and legacy static registration metadata publish warnings. They do not disable a plugin because structure alone cannot prove unsafe runtime behaviour.

An analyser should report the remaining authoring rules during development. Generic constraints are authoritative for family membership and construction eligibility; runtime validation remains authoritative for rules that generic constraints cannot express, but it must not claim to prove semantic thread safety.

Expected authoring failures accumulate in `PluginPreparationResult` as structured diagnostics with stable IDs. Preparation does not throw for validation flow, and a result containing any error exposes no prepared tools. Host preserves every preparation diagnostic while atomically disabling the plugin. Exceptions remain reserved for unexpected loading, composition, construction, reflection and generic materialisation failures.

Captured handler factories run only after plugin-local validation and global collision validation have succeeded. A constructor failure disables the entire plugin without affecting unrelated plugins.

## Package Loading

Configured plugin directories are search roots containing immediate package subdirectories rather than loose entry assemblies. Each package must contain exactly one marked entry assembly; no sidecar manifest is used.

Host must:

- reject missing, malformed or multiple marked entry points;
- canonicalise and deduplicate package directories;
- validate attribute identity, informational SemVer and exact API compatibility before loading code;
- reject paths escaping the package directory;
- create one load context per package;
- use `AssemblyDependencyResolver` for managed and native dependencies;
- return shared contract assemblies from the default context;
- avoid interpreting dependency assemblies as plugin entry points;
- preserve deterministic package ordering for diagnostics;
- avoid leaking absolute or sensitive host paths in public diagnostics.

Additional MEF composition assemblies are out of scope until a concrete plugin-module use case exists. Dependencies remain ordinary package dependencies resolved by the load context.

## Collision And Failure Semantics

Plugin enablement is atomic: if any configured tool is invalid or cannot be materialised, none of that plugin's tools are exposed.

Validation order is:

1. Package marker or bundled assembly shape.
2. Entry assembly loading.
3. MEF export cardinality and composition.
4. Plugin API compatibility.
5. Plugin configuration execution.
6. Metadata completeness and naming rules.
7. Handler contract and lifetime rules.
8. Duplicate names within the plugin.
9. Global plugin-ID and tool-name collisions.
10. Handler construction.
11. Closed generic registration materialisation.
12. Host schema and response-contract inspection.

Global collision policy is deterministic:

- Code Action and server-owned names are always reserved;
- the bundled Plugins.Core identity and tool names take precedence over external packages;
- an external plugin colliding with a reserved or bundled name is disabled;
- all external plugins sharing a plugin ID are disabled;
- all external plugins sharing a tool name are disabled;
- filesystem enumeration order never selects a winner.

Diagnostics identify the plugin, validation stage, handler type and tool name when available. Invalid plugins remain visible as disabled entries in `server-status`; Code Actions remain absent from plugin status.

## Catalogue And Host Integration

Replace the current snapshot-only loader with a composition result that owns:

- an immutable catalogue snapshot;
- enabled closed generic registrations;
- disabled plugin diagnostics;
- external package load-context ownership for process lifetime;
- any MEF resources that genuinely must outlive configuration.

MEF and load-context implementation types must not escape the Host composition boundary or appear in public plugin APIs.

Host continues to add one `McpServerTool` service descriptor per materialised plugin registration before building the service provider. The typed registration visitor constructs the correct Host query or mutation adapter. Handler instances are supplied directly by the registration; they are not resolved from Host DI.

## Implementation Phases

### Phase 1: Composition and loading spike

- Add the centrally versioned `System.Composition` package set to Host.
- Create one minimal external plugin package fixture.
- Load it through a package-local `AssemblyLoadContext` and `AssemblyDependencyResolver`.
- Share Plugins and Workspace contract identity with the default context.
- Compose exactly one `IRoslynPlugin` through the combined export/metadata marker.
- Prove composition completes before Host DI is built.
- Record the exact MEF container lifetime.

Do not migrate production plugins during the spike.

### Phase 2: Package and bundled descriptors

- Define the assembly-metadata package identity contract.
- Add PE-metadata inspection, SemVer validation and package-root enumeration.
- Add the Host-owned external package loader.
- Add the Host-owned bundled descriptor for Plugins.Core.
- Use the default load context for bundled composition.
- Remove loose-DLL discovery.

### Phase 3: Configuration and metadata model

- Change `IRoslynPlugin` from `Register` to `Configure`.
- Add `IPluginConfiguration` and metadata builders.
- Add immutable configured-tool definitions.
- Add `RoslynToolAttribute` and metadata merging.
- Add plugin-local validation and deterministic diagnostics.
- Migrate fixture plugins to configuration without switching Host production loading.

### Phase 4: Handler materialisation

- Add handler contract inspection.
- Add objective lifetime and state validation.
- Add query and mutation generic materialisation bridges.
- Materialise the existing closed generic registrations.
- Prove the typed visitor and Host adapters receive the constructed handlers.
- Prove there is no reflection or service lookup during invocation.

### Phase 5: Global catalogue composition

- Build all configured plugin definitions before constructing handlers.
- Reserve Code Action and server-owned names.
- Implement deterministic plugin-ID and tool-name collision handling.
- Construct handlers only for fully enabled plugins.
- Produce the immutable catalogue and status diagnostics.
- Replace `PluginCatalogLoader` in Host startup.

### Phase 6: Migrate Plugins.Core

- Export `BundledCorePlugin` through `RoslynPluginAttribute`.
- Convert it to `Configure(IPluginConfiguration)`.
- Migrate bundled query and mutation handlers to type-based configuration.
- Move metadata to attributes where locality improves maintenance.
- Use fluent configuration for central overrides or exceptional metadata.
- Remove `BundledCoreToolRegistrar` and individual static `Register` methods.
- Preserve all existing handler execution behaviour and wire contracts.

CodeActions is explicitly excluded from this phase.

### Phase 7: Remove the legacy authoring surface

- Remove `IPluginRegistry` and `PluginRegistry` after migration.
- Remove public handler-instance registration.
- Remove the legacy loose-assembly loader.
- Remove obsolete registration tests and replace them with behavioural configuration tests.
- Update architecture, testing, tool-contract and authoring documentation.

### Phase 8: Third-party authoring support

- Publish complete XML documentation for public plugin contracts.
- Add a minimal package template.
- Add representative query and mutation examples.
- Add an analyser for handler contract and lifetime rules.
- Add an offline package-validation command or reusable test helper.
- Document trust, dependency isolation, compatibility, diagnostics and restart requirements.

## Test Strategy

### Plugins unit tests

- attribute-only, fluent-only and merged metadata;
- fluent precedence and incomplete metadata;
- query/mutation family derivation;
- destructive query rejection;
- handler contract cardinality and mismatch;
- public request and response contract validation;
- internal handler construction;
- state, disposal and import restrictions;
- closed generic query and mutation materialisation;
- typed visitor dispatch;
- unchanged Workspace context adaptation and staging mapping;
- no invocation-time reflection or service lookup.

Remove reflection-only public-surface shape tests. Public API compatibility is compile-time and behavioural evidence, not a unit-test reflection exercise.

### Host unit tests

- PE-metadata discovery and informational-version validation;
- package boundary and canonical-path validation;
- load-context dependency routing;
- shared contract assembly routing;
- MEF export absence, multiplicity and composition failure;
- bundled descriptor composition in the default context;
- deterministic plugin ID and tool-name collisions;
- reservation of Code Action and server-owned names;
- plugin status diagnostics;
- immutable catalogue ownership;
- query and mutation MCP registration through typed visitors.

### Plugins.Core unit tests

- bundled configuration exposes every expected tool exactly once;
- attributes and fluent overrides produce the existing names, titles, descriptions, summaries and destructive hints;
- handler execution tests remain unchanged except for removing static-registration assertions.

### Integration tests

- a packaged external query plugin loads and executes through MCP;
- a packaged external mutation plugin stages through Workspace;
- malformed packages do not prevent unrelated plugins from loading;
- dependency DLLs are not treated as entry points;
- two packages can carry conflicting versions of one private dependency;
- Plugins and Workspace contract identity is shared with Host;
- Plugins.Core loads through bundled MEF composition without an external plugin directory;
- Code Actions remain available but absent from plugin discovery and status;
- wire-level tool metadata, schemas, requests and responses remain unchanged.

Integration execution remains at the end of the structural migration, after unit coverage and architecture are stable.

## Acceptance Criteria

- Host uses MEF to compose plugin entry points before building DI.
- External plugins are discovered from marked entry-assembly metadata in immediate package directories, not loose DLL scanning.
- Each external plugin has isolated dependency resolution through its own load context.
- Plugins.Core remains a referenced bundled assembly and uses the same MEF configuration/materialisation path.
- Code Actions do not reference or participate in the plugin system.
- Third-party authors register handler types through `IPluginConfiguration` rather than constructing handlers or mutating `IPluginRegistry`.
- Attribute and fluent metadata merge deterministically.
- Invalid plugins are atomically disabled with actionable status diagnostics.
- Collision outcomes are independent of filesystem enumeration order.
- Internal handler implementations may remain non-public, their parameterless constructors are public, and transport contracts remain public.
- One handler instance is constructed only after all validation succeeds.
- Materialised tools use the existing closed generic registrations and typed visitor.
- Host remains the sole owner of MCP adapters, schemas, binding and publication.
- No reflection, `dynamic`, `object` invocation or service lookup occurs during tool invocation.
- Existing plugin tool names, annotations, schemas, request JSON and response JSON remain unchanged.
- Code Actions remain absent from plugin status.

## Assumptions

- The repository remains greenfield and public plugin API compatibility may be broken.
- Third-party Code Actions remain unsupported.
- Plugins execute as trusted in-process code.
- Hot loading and unloading remain unsupported.
- Handler instances remain stateless, thread-safe and non-disposable.
- Plugin composition and tool discovery occur once at startup.
- The marked entry assembly is authoritative for external plugin identity and compatibility; no sidecar manifest is used.
- The bundled Plugins.Core marker and informational version are authoritative for bundled identity and compatibility.
- Additional MEF module assemblies are deferred until a concrete use case exists.
