using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

namespace Roslyn.Workbench.Mcp.InvalidToolNamePluginFixture;

[RoslynPlugin("test.invalid.tool.name", "Invalid Tool Name Test Plugin", PluginApiVersions.V1)]
public sealed class InvalidToolNameTestPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.AddQueryTool<Handler>();
    }

    public sealed record Request : WorkspaceBoundRequest;

    public sealed record Response : IQueryResponse;

#pragma warning disable RWMCP022 // The fixture proves runtime rejection when the authoring diagnostic is suppressed.

    [RoslynTool("invalid tool name", "Invalid Tool Name", "Should never be published.")]
    private sealed class Handler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            var response = new Response();
            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }
}
