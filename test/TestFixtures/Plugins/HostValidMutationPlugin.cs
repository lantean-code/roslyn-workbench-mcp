using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.TestSupport;

[RoslynPlugin("host.valid.mutation", "Host Valid Mutation Plugin", PluginApiVersions.V1)]
public sealed class HostValidMutationPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _ = configuration.AddMutationTool<Handler>();
    }

    public sealed record Request : WorkspaceBoundRequest
    {
        public string Summary { get; init; } = string.Empty;
    }

    [RoslynTool("host-valid-mutation", "Host Valid Mutation", "Returns a stable host test mutation proposal.")]
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
