using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.PluginFixtures;

public sealed class DuplicateToolNameTestPlugin : IRoslynPlugin
{
    public PluginMetadata Metadata => new()
    {
        PluginId = "test.duplicate.tool",
        DisplayName = "Duplicate Tool Test Plugin",
        Version = "1.0.0",
        SupportedApiVersion = PluginApiVersions.V1,
    };

    public void Register(IPluginRegistry registry)
    {
        var metadata = new ToolRegistrationMetadata
        {
            Name = "test-duplicate-tool",
            Title = "Duplicate Tool",
            Description = "First registration.",
        };

        registry.RegisterQueryTool(metadata, new Handler());
        registry.RegisterQueryTool(metadata, new Handler());
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
