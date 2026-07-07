using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed class UnsupportedApiVersionTestPlugin : IRoslynPlugin
{
    public PluginMetadata Metadata => new()
    {
        PluginId = "test.unsupported.api",
        DisplayName = "Unsupported API Test Plugin",
        Version = "1.0.0",
        SupportedApiVersion = "9.9",
    };

    public void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(
            new ToolRegistrationMetadata
            {
                Name = "test-unsupported-api",
                Title = "Unsupported API",
                Description = "Should never register.",
            },
            new Handler());
    }

    public sealed record Request : WorkspaceBoundRequest
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed record Response
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class Handler : IQueryToolHandler<Request, Response>
    {
        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(Request request, IQueryContext context, CancellationToken cancellationToken)
        {
            _ = request;
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response
            {
                Value = string.Empty,
            }));
        }
    }
}
