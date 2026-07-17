using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.PluginFixtures;

[RoslynPlugin("test.valid.query", "Valid Query Test Plugin", PluginApiVersions.V1)]
public sealed class ValidQueryTestPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        _ = configuration.AddQueryTool<Handler>();
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
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response
            {
                Value = request.Name,
            }));
        }
    }
}
