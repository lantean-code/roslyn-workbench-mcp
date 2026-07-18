# Third-Party Plugin Authoring

Roslyn Workbench plugins are trusted in-process .NET assemblies discovered once when the stdio server starts. They may add ordinary query and mutation tools. They cannot add Code Actions, replace workspace or transaction lifecycle tools, import Host services, or change the tool list after startup.

## Entry point

A package has one entry assembly containing exactly one marked plugin entry type:

```csharp
using Roslyn.Workbench.Mcp.Plugins;

[RoslynPlugin("example.tools", "Example Tools", PluginApiVersions.V1)]
public sealed class ExamplePlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        _ = configuration.AddQueryTool<ExampleQueryTool>();
        _ = configuration.AddMutationTool<ExampleMutationTool>()
            .WithName("example-mutation")
            .WithTitle("Example Mutation")
            .WithDescription("Stages an example source mutation.")
            .IsDestructive();
    }
}
```

`RoslynPluginAttribute` is both the discovery metadata and the MEF export. Its ID must be stable and its API version must exactly match the Host API. Set the entry assembly’s `AssemblyInformationalVersionAttribute` to a valid semantic version; Host validates it with NuGet’s SemVer parser. No JSON manifest is used.

## Tool handlers

Configuration records handler types. It does not accept constructed handler instances. A handler may supply all transport metadata through `RoslynToolAttribute`:

```csharp
[RoslynTool(
    "example-query",
    "Example Query",
    "Returns an example response.",
    ResultSummary = "the selected example value")]
internal sealed class ExampleQueryTool
    : IQueryToolHandler<ExampleQueryRequest, ExampleQueryData>
{
    public ValueTask<PluginExecutionResult<ExampleQueryData>> ExecuteAsync(
        ExampleQueryRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(PluginExecutionResult<ExampleQueryData>.Success(new ExampleQueryData
        {
            Value = request.Value,
        }));
    }
}
```

Fluent values override attribute values. Builders freeze when `Configure` returns; later mutation throws `InvalidOperationException`.

Handler implementation types may be non-public, but they must implement the appropriate non-generic handler marker through a closed generic handler contract and provide a public parameterless constructor. The generic configuration constraints therefore reject abstract handlers, handlers from the wrong family and handlers without public parameterless construction at compile time. Request and response contracts must be public. Runtime validation reports direct marker implementations, multiple or cross-family handler contracts, disposable handlers, MEF imports and invalid transport contracts as categorised errors. It accumulates all expected authoring diagnostics and atomically disables the plugin when any error exists; exceptions are reserved for unexpected loading, composition, construction or reflection failures. Declared state and legacy static registration patterns publish categorised warnings because structural inspection cannot prove thread safety.

Preparation diagnostics use stable IDs for handler contracts, lifetime, composition, instance state, mutable members, static state, legacy registration, tool metadata, behaviour and duplicate tool names. Host adds distinct discovery, plugin-metadata, collision and materialisation IDs. `PluginLoad` is reserved for an unexpected exception crossing the load boundary.

Runtime capabilities come only from `IQueryContext` or `IMutationContext`. Mutation handlers return a candidate solution and summary; Host stages the proposal through Workspace after the handler returns. Plugins do not receive Host dependency injection, MCP objects, file writers, workspace coordinators or Code Action services.

Query plugins own the logical size and shape of their results. Collection-returning tools should expose explicit request limits, choose sensible defaults with `IQueryContext.DefaultMaxResults` available as the Host baseline, return deterministic bounded collections with `HasMore`, and let agents request more results or narrow the query when needed. Host does not impose a global serialised response-size ceiling, so plugin authors must also cap or summarise unusually verbose per-item fields rather than relying on byte-led truncation.

## Package layout

Each `--plugin-directory` value is a search root. Every immediate child directory is one package:

```text
plugins/
  example-tools/
    Example.Tools.dll
    Example.Tools.deps.json
    Example.PrivateDependency.dll
```

The marked entry assembly may have any file name. Other DLLs in the same package are dependencies and must not contain `RoslynPluginAttribute`. Discovery is not recursive, and DLLs directly beneath the search root are ignored.

Host creates one non-collectible `AssemblyLoadContext` and `AssemblyDependencyResolver` per valid external package. Plugins, Workspace, `System.Composition` and `Microsoft.CodeAnalysis*` identities are shared from the default context. Other managed and native dependencies resolve from the package, allowing separate plugins to carry different private dependency versions.

Plugins.Core is bundled differently: it remains a normal Host project reference and normal publish output, but its entry point uses the same marker, MEF configuration, validation and materialisation pipeline in the default load context.

## Diagnostics and collisions

Host validates package metadata and compatibility before executing plugin code. It builds the internal Code Action catalogue first and reserves Code Action and server-owned tool names. Collision rules are deterministic:

- reserved and Plugins.Core names defeat external plugins;
- every external package sharing a plugin ID is disabled;
- every external plugin sharing a tool name is disabled;
- no filesystem order chooses a winner.

`server-status` reports enabled plugins, warnings and disabled diagnostics without changing its existing JSON shape. Code Actions are not reported as plugins. Adding, removing or upgrading a package requires restarting the server.

## Deployment checklist

- Target the same supported .NET runtime as the Host.
- Reference the public Plugins and Workspace contracts for compilation but avoid packaging private copies that could obscure dependency errors; Host always supplies their runtime identity.
- Give the entry assembly a valid informational SemVer.
- Place exactly one marked entry assembly in one immediate package directory.
- Keep request and response contracts public and JSON serialisable.
- Keep handlers thread-safe and non-disposable, with a public parameterless constructor.
- Validate names against server-owned, Code Action and Plugins.Core tools.
- Restart the stdio server and inspect `server-status` after deployment.
