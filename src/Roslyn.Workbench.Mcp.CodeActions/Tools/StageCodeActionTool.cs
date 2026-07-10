using Roslyn.Workbench.Mcp.CodeActions.Contracts;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageCodeActionTool : CodeActionMutationToolHandler<StageCodeActionRequest>
{
    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "stage-code-action",
        Title = "Stage Code Action",
        Description = "Revalidates and stages one selected refactoring action into the active transaction.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new StageCodeActionTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteCoreAsync(StageCodeActionRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageCodeActionAsync(request, cancellationToken);
    }
}
