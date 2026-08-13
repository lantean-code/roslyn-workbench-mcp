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
    public string Value { get; init; } = string.Empty;
}

public sealed record ExampleQueryData : IQueryResponse
{
    public string Value { get; init; } = string.Empty;
}

[RoslynTool(
    "example-query",
    "Example Query",
    "Returns an example response.")]
internal sealed class ExampleQueryTool :
    IQueryToolHandler<ExampleQueryRequest, ExampleQueryData>
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

        var executionResult = PluginExecutionResult.Success<ExampleQueryData>(data);
        var result = ValueTask.FromResult(executionResult);
        return result;
    }
}
