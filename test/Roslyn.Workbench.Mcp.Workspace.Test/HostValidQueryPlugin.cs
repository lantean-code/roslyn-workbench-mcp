using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace.Test;

public sealed class HostValidQueryPlugin : IRoslynPlugin
{
    public PluginMetadata Metadata => new()
    {
        PluginId = "host.valid.query",
        DisplayName = "Host Valid Query Plugin",
        Version = "1.0.0",
        SupportedApiVersion = PluginApiVersions.V1,
    };

    public void Register(IPluginRegistry registry)
    {
        registry.RegisterQueryTool(
            new ToolRegistrationMetadata
            {
                Name = "host-valid-query",
                Title = "Host Valid Query",
                Description = "Returns a stable host test payload.",
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
