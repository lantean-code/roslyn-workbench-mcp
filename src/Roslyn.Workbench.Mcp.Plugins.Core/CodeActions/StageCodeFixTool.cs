using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.Plugins.Core.CodeActions;

internal sealed class StageCodeFixTool : MutationToolHandler<StageCodeFixRequest>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "stage-code-fix",
        Title = "Stage Code Fix",
        Description = "Revalidates and stages one selected code fix into the active transaction.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new StageCodeFixTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(StageCodeFixRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageCodeFixAsync(request, cancellationToken);
    }
}
