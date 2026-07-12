using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed class HostValidMutationPlugin : IRoslynPlugin
{
    public PluginMetadata Metadata => new()
    {
        PluginId = "host.valid.mutation",
        DisplayName = "Host Valid Mutation Plugin",
        Version = "1.0.0",
        SupportedApiVersion = PluginApiVersions.V1,
    };

    public void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(
            new ToolRegistrationMetadata
            {
                Name = "host-valid-mutation",
                Title = "Host Valid Mutation",
                Description = "Returns a stable host test mutation proposal.",
            },
            new Handler());
    }

    public sealed record Request : WorkspaceBoundRequest
    {
        public string Summary { get; init; } = string.Empty;
    }

    private sealed class Handler : IMutationToolHandler<Request>
    {
        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(Request request, IMutationContext context, CancellationToken cancellationToken)
        {
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<MutationCandidate>.Success(new MutationCandidate
            {
                CandidateSolution = context.CurrentSolution,
                Summary = request.Summary,
            }));
        }
    }
}
