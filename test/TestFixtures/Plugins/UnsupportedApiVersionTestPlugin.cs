using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.PluginFixtures;

[RoslynPlugin("test.unsupported.api", "Unsupported API Test Plugin", "9.9")]
public sealed class UnsupportedApiVersionTestPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

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

    [RoslynTool("test-unsupported-api", "Unsupported API", "Should never register.")]
    private sealed class Handler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            var response = new Response
            {
                Value = string.Empty,
            };

            var result = PluginExecutionResult<Response>.Success(response);
            return ValueTask.FromResult(result);
        }
    }
}
