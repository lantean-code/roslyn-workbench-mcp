using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

namespace Roslyn.Workbench.Mcp.PluginFixtures;

[RoslynPlugin("test.valid.mutation", "Valid Mutation Test Plugin", PluginApiVersions.V1)]
public sealed class ValidMutationTestPlugin : IRoslynPlugin
{
    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.AddMutationTool<Handler>();
    }

    public sealed record Request : WorkspaceMutationRequest
    {
        public string Summary { get; init; } = string.Empty;
    }

    [RoslynTool("test-valid-mutation", "Test Valid Mutation", "Returns a predictable mutation proposal for startup tests.")]
    private sealed class Handler : IMutationToolHandler<Request>
    {
        public ValueTask<PluginExecutionResult<MutationCandidate>> ExecuteAsync(Request request, IMutationContext context, CancellationToken cancellationToken)
        {
            var candidate = new MutationCandidate
            {
                CandidateSolution = context.CurrentSolution,
                Summary = request.Summary,
            };

            var result = PluginExecutionResult.Success(candidate);
            return ValueTask.FromResult(result);
        }
    }
}
