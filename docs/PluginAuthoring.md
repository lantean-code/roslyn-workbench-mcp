# Third-Party Plugin Authoring

Roslyn Workbench plugins are trusted in-process .NET assemblies discovered once when the stdio server starts. They may add ordinary query and mutation tools. They cannot add Code Actions, replace workspace or transaction lifecycle tools, import Host services, or change the tool list after startup.

## Install the authoring package

Add the public authoring package to the plugin project:

```bash
dotnet add package Roslyn.Workbench.Mcp.Plugins
```

The package supplies the plugin API and includes the matching `Roslyn.Workbench.Mcp.Abstractions` assembly. Plugin projects need only this package and should not add repository project references or a direct Workspace reference.

The package also installs the C# plugin-authoring analyser automatically. Its `RWMCP001`–`RWMCP019` diagnostics appear during command-line builds and in IDEs that support NuGet-delivered Roslyn analysers. See [Plugin authoring diagnostics](https://github.com/lantean-code/roslyn-workbench-mcp/blob/main/docs/PluginAuthoringDiagnostics.md) for each rule and its remediation. Runtime validation remains authoritative when diagnostics are suppressed or a plugin was built without the analyser.

## Entry point

A package has one entry assembly containing exactly one marked plugin entry type:

```csharp
using Roslyn.Workbench.Mcp.Plugins;

[RoslynPlugin("example.tools", "Example Tools", PluginApiVersions.V1)]
public sealed class ExamplePlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        configuration.AddQueryTool<ExampleQueryTool>();
        configuration.AddMutationTool<ExampleMutationTool>()
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
        context;
        cancellationToken.ThrowIfCancellationRequested();

        var data = new ExampleQueryData
        {
            Value = request.Value,
        };

        var executionResult = PluginExecutionResult.Success(data);
        var result = ValueTask.FromResult(executionResult);
        return result;
    }
}
```

Fluent values override attribute values. Builders freeze when `Configure` returns; later mutation throws `InvalidOperationException`.

Handler implementation types may be non-public, but they must implement the appropriate non-generic handler marker through a closed generic handler contract and provide a public parameterless constructor. The generic configuration constraints therefore reject abstract handlers, handlers from the wrong family and handlers without public parameterless construction at compile time. Request and response contracts must be public. Runtime validation reports direct marker implementations, multiple or cross-family handler contracts, disposable handlers, MEF imports and invalid transport contracts as categorised errors. It accumulates all expected authoring diagnostics and atomically disables the plugin when any error exists; exceptions are reserved for unexpected loading, composition, construction or reflection failures. Declared instance or static state and disposable-valued fields publish categorised warnings because structural inspection cannot prove thread safety or resource ownership.

Query request contracts derive from `WorkspaceBoundRequest`. Mutation request contracts derive from `WorkspaceMutationRequest`, which inherits the Workspace selector and adds the required `ExpectedSnapshot` precondition. The Host publishes that inherited precondition as a required, non-null input, so mutation plugins should not redeclare or weaken it. Before interpreting snapshot-sensitive selectors, handlers must use the request resolver's snapshot-aware methods or call `ValidateSnapshot` explicitly; Host then stages successful candidates through the transaction boundary.

Preparation diagnostics use stable IDs for handler contracts, lifetime, composition, instance state, mutable members, static state, legacy registration, tool metadata, behaviour and duplicate tool names. Host adds distinct discovery, plugin-metadata, collision and materialisation IDs. `PluginLoad` is reserved for an unexpected exception crossing the load boundary.

Runtime capabilities come only from `IQueryContext` or `IMutationContext`. Mutation handlers return a candidate solution and summary; Host stages the proposal through Workspace after the handler returns. Plugins do not receive Host dependency injection, MCP objects, file writers, workspace coordinators or Code Action services.

## Supported API and trust boundary

The supported third-party API consists of plugin and tool attributes, configuration builders, handler contracts, execution contexts, execution results, mutation candidates, selector and result contracts, and the read-only analysis services exposed through `IToolExecutionServices`. Host composition, registration, execution leases, staging, result mapping and plugin-catalogue metadata are implementation details and are not public API.

Plugin authors compile against these service contracts and do not construct their implementations. Host supplies the composed `IToolExecutionServices` instance for each execution context. General plugin analysis services are defined by the Plugins assembly. Workspace selectors, result models, resolver contracts and project-system metadata service contracts are supplied by the bundled Abstractions assembly; their Host implementations remain outside the supported plugin API.

`IQueryContext` and `IMutationContext` expose the same immutable Roslyn solution snapshot and read-only resolution and analysis capabilities. `IMutationContext` does not expose a stager. Returning a `MutationCandidate` is the only supported way for a plugin to propose source changes; Host validates and stages that candidate through the active transaction.

`IWorkspaceResolver` remains invocation-scoped on the execution context because it resolves against that context's effective Workspace snapshot. `IToolExecutionServices.WorkspaceSelectorFactory` exposes the stateless `IWorkspaceSelectorFactory` service for converting a `ResolvedLocation` into a replayable `LocationSelector` or location-backed `SymbolSelector`. Use the resolver to obtain snapshot-bound identities and the factory to project those identities into selectors; do not duplicate selector construction in individual tools.

`IToolExecutionServices.ReferenceDiscoveryService` performs cached Roslyn reference discovery within an explicit document set. It excludes occurrences from unselected project contexts and removes duplicate occurrences within each selected Roslyn document. Supply `IQueryContext.WorkspaceIdentity.WorkspaceId`, `CurrentSolution`, the resolved symbol and the documents selected for the request. The returned `ReferenceOccurrence` values retain Roslyn documents, locations and related definition symbols; each tool remains responsible for ordering, limiting, optional enrichment and projection into its own response contract.

Plugins are trusted in-process code, not a security sandbox. Treat `CurrentSolution` as query-only and do not call `CurrentSolution.Workspace.TryApplyChanges`, mutate its associated Roslyn workspace, write source files directly, use reflection to reach Host internals, or otherwise bypass the transaction pipeline. A Roslyn `Solution` exposes its associated `Workspace` transitively, and in-process code can also use ordinary file and reflection APIs, so the Host cannot enforce adversarial isolation while loading plugins into its process. Plugins that are not fully trusted must not be loaded.

Query plugins own the logical size and shape of their results. Collection-returning tools should expose explicit nullable-integer request limits, choose a positive default for each tool or distinct collection, publish that default in the tool's input schema, and apply `[Range(0, int.MaxValue)]` so the Host rejects negative values during request binding. An explicitly requested zero means that no items should be returned; zero must not be used as the declared default. `ResultLimit.GetEffectiveValue` from `Roslyn.Workbench.Mcp.Workspace.Results` resolves an omitted limit against its positive default and throws when called directly with a negative requested limit or a non-positive default. Return deterministic results through `BoundedCollection.CreatePrebounded<TItem>` from the same namespace. The `CreatePrebounded` name makes the division of responsibility explicit: the plugin must stop or limit the underlying work before constructing the response; the result contract only publishes the already-bounded items together with `HasMore` and an optional cheaply available `TotalCount`. Agents can then request more results or narrow the query when needed. `IQueryContext.DefaultMaxResults` is available as a compatibility baseline when a plugin has not yet established a more appropriate default; it is not a substitute for a curated, published tool contract. Host does not impose a global serialised response-size ceiling, so plugin authors must also cap or summarise unusually verbose per-item fields rather than relying on byte-led truncation.

Request presence, nullability and value validity are separate contracts. Use C# `required` for a member that callers must supply, a non-nullable property type when explicit `null` is invalid, and ComponentModel validation attributes such as `[Required]`, `[Range]`, `[StringLength]` or `[AllowedValues]` for constraints on the supplied value. `[Required]` does not replace the C# `required` modifier, and `[NotNull]` is a nullable-analysis annotation rather than a member-presence rule. The Host retains the JSON Schema `required` array, corrects SDK-generated property nullability, publishes declared property defaults, and evaluates property validation attributes during binding before acquiring an execution context. Validation that cannot be represented by a standard attribute, such as selector consistency or domain-specific path rules, remains the responsibility of the owning resolver or service.

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

Host creates one non-collectible `AssemblyLoadContext` and `AssemblyDependencyResolver` per valid external package. Plugins, Abstractions, `System.Composition` and `Microsoft.CodeAnalysis*` identities are shared from the default context. Other managed and native dependencies resolve from the package, allowing separate plugins to carry different private dependency versions.

The bundled first-party tools are loaded with the Host rather than discovered from an external package directory. They use the same entry-point marker, validation and materialisation rules, and their tool names take precedence over external plugin names.

## Diagnostics and collisions

Host validates package metadata and compatibility before executing plugin code. It composes the three Host-owned Code Action tools first and reserves those names together with other server-owned names. Collision rules are deterministic:

- reserved and Plugins.Core names defeat external plugins;
- every external package sharing a plugin ID is disabled;
- every external plugin sharing a tool name is disabled;
- no filesystem order chooses a winner.

`server-status` reports enabled plugins, warnings and disabled diagnostics without changing its existing JSON shape. Code Actions are not reported as plugins. Adding, removing or upgrading a package requires restarting the server.

## Deployment checklist

- Target the same supported .NET runtime as the Host.
- Reference the public Plugins NuGet package for compilation. It includes the matching Abstractions assembly; do not package private copies of either assembly in the deployed plugin because Host supplies their runtime identity.
- Build with all default `RWMCP` diagnostics enabled and justify any deliberate suppression.
- Give the entry assembly a valid informational SemVer.
- Place exactly one marked entry assembly in one immediate package directory.
- Keep request and response contracts public and JSON serialisable.
- Derive query requests from `WorkspaceBoundRequest` and mutation requests from `WorkspaceMutationRequest`.
- Keep handlers thread-safe and non-disposable, with a public parameterless constructor.
- Validate names against server-owned, Code Action and Plugins.Core tools.
- Restart the stdio server and inspect `server-status` after deployment.
