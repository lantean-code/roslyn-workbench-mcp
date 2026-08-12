using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

namespace Roslyn.Workbench.Mcp.InvalidPluginFixture;

[RoslynPlugin("test.unsupported.api", "Unsupported API Test Plugin", "9.9")]
public sealed class UnsupportedApiVersionTestPlugin : IRoslynPlugin
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

    [RoslynTool("test-unsupported-api", "Unsupported API", "Should never register.")]
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
