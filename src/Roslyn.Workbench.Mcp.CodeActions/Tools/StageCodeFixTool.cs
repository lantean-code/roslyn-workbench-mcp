using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageCodeFixTool : CodeActionMutationToolHandler<StageCodeFixRequest>
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

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(StageCodeFixRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageCodeFixAsync(request, cancellationToken);
    }
}
