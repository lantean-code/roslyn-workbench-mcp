using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.Plugins.Core.CodeActions;

internal sealed class StageFixAllTool : MutationToolHandler<StageFixAllRequest>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "stage-fix-all",
        Title = "Stage Fix All",
        Description = "Revalidates one selected code fix and stages its fix-all variant into the active transaction.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new StageFixAllTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(StageFixAllRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageFixAllAsync(request, cancellationToken);
    }
}
