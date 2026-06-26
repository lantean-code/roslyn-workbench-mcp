using Roslyn.Workbench.Mcp.Contracts.Results;

using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed class ValidQueryTestPlugin : IRoslynPlugin
{
    public PluginMetadata Metadata => new()
    {
        PluginId = "test.valid.query",
        DisplayName = "Valid Query Test Plugin",
        Version = "1.0.0",
        SupportedApiVersion = PluginApiVersions.V1,
    };

    public void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(
            new ToolRegistrationMetadata
            {
                Name = "test-valid-query",
                Title = "Test Valid Query",
                Description = "Returns a predictable payload for startup tests.",
            },
            new Handler());
    }

    public sealed record Request
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
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<Response>.Success(new Response
            {
                Value = request.Name,
            }));
        }
    }
}
