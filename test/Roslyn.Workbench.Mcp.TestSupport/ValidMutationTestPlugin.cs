using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed class ValidMutationTestPlugin : IRoslynPlugin
{
    public PluginMetadata Metadata => new()
    {
        PluginId = "test.valid.mutation",
        DisplayName = "Valid Mutation Test Plugin",
        Version = "1.0.0",
        SupportedApiVersion = PluginApiVersions.V1,
    };

    public void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(
            new ToolRegistrationMetadata
            {
                Name = "test-valid-mutation",
                Title = "Test Valid Mutation",
                Description = "Returns a predictable mutation proposal for startup tests.",
                Behavior = new ToolBehaviorHints
                {
                    Destructive = false,
                },
            },
            new Handler());
    }

    public sealed record Request
    {
        public string Summary { get; init; } = string.Empty;
    }

    private sealed class Handler : IMutationToolHandler<Request, MutationProposal>
    {
        public ValueTask<PluginExecutionResult<MutationProposal>> ExecuteAsync(Request request, IMutationContext context, CancellationToken cancellationToken)
        {
            _ = context;
            _ = cancellationToken;

            return ValueTask.FromResult(PluginExecutionResult<MutationProposal>.Success(new MutationProposal
            {
                Summary = request.Summary,
            }));
        }
    }
}
