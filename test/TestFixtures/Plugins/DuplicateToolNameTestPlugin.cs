using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.PluginFixtures;

[RoslynPlugin("test.duplicate.tool", "Duplicate Tool Test Plugin", PluginApiVersions.V1)]
public sealed class DuplicateToolNameTestPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        _ = configuration.AddQueryTool<Handler>();
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

    [RoslynTool("test-duplicate-tool", "Duplicate Tool", "First registration.")]
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
