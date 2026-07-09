using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageCodeActionTool : CodeActionMutationToolHandler<StageCodeActionRequest>
{
    private static readonly ToolRegistrationMetadata _metadata = new()
    {
        Name = "stage-code-action",
        Title = "Stage Code Action",
        Description = "Revalidates and stages one selected refactoring action into the active transaction.",
        Behavior = new ToolBehaviorHints
        {
            Destructive = true,
        },
    };

    public static void Register(IPluginRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new StageCodeActionTool());
    }

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(StageCodeActionRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageCodeActionAsync(request, cancellationToken);
    }
}
