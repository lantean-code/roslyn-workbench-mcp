# Your first plugin

Build a query tool first, then add mutations or advanced services when you need them. A plugin adds ordinary MCP tools; it cannot register Code Actions or replace the Host's workspace and transaction tools.

!!! warning "Load only trusted plugins"
    Plugins execute in-process with the Host user's permissions. They are not sandboxed. External plugins are disabled by default and require deliberate operator opt-in.

## 1. Create the project

Use a .NET 10 class library outside the Roslyn Workbench repository:

```bash
dotnet new classlib --name Example.Tools --framework net10.0
```

Replace its project file with the following. Replace `VERSION` with the exact supported Plugins package release you want to use.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Lantean.Roslyn.Workbench.Mcp.Plugins"
                      Version="VERSION"
                      ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

The package supplies the Plugins API, matching Abstractions contracts and the authoring analyser. No separate Workspace package or repository project references are needed. `ExcludeAssets="runtime"` keeps the Host-supplied contract assemblies out of the plugin output.

## 2. Add a complete query tool

Replace `Class1.cs` with `ExamplePlugin.cs` containing this example. It echoes a value so you can verify registration and execution before adding Roslyn analysis.

```csharp
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

[RoslynPlugin("example.tools", "Example Tools", PluginApiVersions.V1)]
public sealed class ExamplePlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        configuration.AddQueryTool<ExampleQueryTool>();
    }
}

public sealed record ExampleQueryRequest : WorkspaceBoundRequest
{
    public required string Value { get; init; }
}

public sealed record ExampleQueryData : IQueryResponse
{
    public required string Value { get; init; }
}

[RoslynTool(
    "example-query",
    "Example Query",
    "Returns the supplied example value.",
    ResultSummary = "the supplied example value")]
internal sealed class ExampleQueryTool : IQueryToolHandler<ExampleQueryRequest, ExampleQueryData>
{
    public ValueTask<PluginExecutionResult<ExampleQueryData>> ExecuteAsync(
        ExampleQueryRequest request,
        IQueryContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = new ExampleQueryData
        {
            Value = request.Value,
        };

        var executionResult = PluginExecutionResult.Success(data);
        return ValueTask.FromResult(executionResult);
    }
}
```

The entry point registers a handler type, not an instance. Requests and responses are public; handlers may be internal. A query response implements `IQueryResponse` and uses an object JSON contract. Query requests derive from `WorkspaceBoundRequest`, so even this introductory tool runs against a loaded Workspace.

## 3. Build and deploy

```bash
dotnet build Example.Tools/Example.Tools.csproj --configuration Release
dotnet publish Example.Tools/Example.Tools.csproj --configuration Release --output plugins/example-tools
```

Run these commands from the directory containing `Example.Tools`. Review the authoring analyser diagnostics before deployment. Keep the entry DLL, its `.deps.json` and private dependencies together; do not deploy private copies of Host-supplied contract assemblies. See [Packaging and deployment](plugins/deployment.md) for the full layout and shared-assembly rules.

Configure your MCP client to start the Host with an absolute plugin search root:

```text
roslyn-workbench-mcp --operational-mode inspection-only --enable-plugins --plugin-directory /absolute/path/to/plugins
```

On Windows, use a Windows absolute path instead. The Host inspects immediate child directories of the search root, not DLLs placed directly inside the root.

## 4. Verify the tool

1. Restart the Host and reconnect the client.
2. Call `server-status` with `detail: Full`; confirm that `example.tools` is enabled and inspect any load diagnostics.
3. Discover `example-query` through MCP `tools/list`.
4. Open a fully trusted Workspace with `workspace-open`.
5. Call `example-query` with `value: "hello"`. With exactly one Workspace loaded, its optional Workspace selector may be omitted.
6. Confirm that the successful response's `data.value` is `"hello"`.

Do not write source files or call `Workspace.TryApplyChanges` from a query. Use the invocation's `CurrentSolution` for analysis rather than the underlying Workspace's mutable current solution.

## Next steps

- [Mutation plugins](plugins/mutations.md): propose changes without writing files.
- [Contracts and services](plugins/contracts.md): validation, bounded results, singleton services and optional caching.
- [Packaging and deployment](plugins/deployment.md): dependencies, collisions and troubleshooting.
- [Authoring diagnostics](plugins/diagnostics.md): `RWMCP001`–`RWMCP023` and their remediation.

Plugin API V1 is supported throughout Host 1.x. A plugin's own version does not need to match the Host package version; see [Compatibility](compatibility.md#plugin-api).
