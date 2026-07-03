using Roslyn.Workbench.Mcp.Contracts.CodeActions;

namespace Roslyn.Workbench.Mcp.Plugins.Core.CodeActions;

internal sealed class StageCodeActionTool : MutationToolHandler<StageCodeActionRequest, MutationProposal>
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

    protected override ValueTask<PluginExecutionResult<MutationProposal>> ExecuteCoreAsync(StageCodeActionRequest request, IMutationContext context, CancellationToken cancellationToken)
    {
        return context.CodeActionService.StageCodeActionAsync(request, context, cancellationToken);
    }
}
