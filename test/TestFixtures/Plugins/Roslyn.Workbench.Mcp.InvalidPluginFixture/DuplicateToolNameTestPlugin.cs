using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

namespace Roslyn.Workbench.Mcp.InvalidPluginFixture;

[RoslynPlugin("test.duplicate.tool", "Duplicate Tool Test Plugin", PluginApiVersions.V1)]
public sealed class DuplicateToolNameTestPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.AddQueryTool<Handler>();
        configuration.AddQueryTool<Handler>();
    }

    public sealed record Request : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed record Response : IQueryResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    [RoslynTool("test-duplicate-tool", "Duplicate Tool", "First registration.")]
    private sealed class Handler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            var response = new Response
            {
                Value = string.Empty,
            };

            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }
}
