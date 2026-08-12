using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

namespace Roslyn.Workbench.Mcp.InvalidPluginFixture;

[RoslynPlugin("test.valid.query", "Valid Query Test Plugin", PluginApiVersions.V1)]
public sealed class ValidQueryTestPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.AddQueryTool<Handler>();
    }

    public sealed record Request : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed record Response
    {
        public string Value { get; init; } = string.Empty;
    }

    [RoslynTool("test-valid-query", "Test Valid Query", "Returns a predictable payload for startup tests.")]
    private sealed class Handler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            var response = new Response
            {
                Value = request.Name,
            };

            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }
}
