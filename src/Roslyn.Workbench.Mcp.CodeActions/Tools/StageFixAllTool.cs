using Roslyn.Workbench.Mcp.CodeActions.Contracts;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageFixAllTool : CodeActionMutationToolHandler<StageFixAllRequest>
{
    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "stage-fix-all",
        Title = "Stage Fix All",
        Description = "Revalidates one selected code fix and stages its fix-all variant into the active transaction.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new StageFixAllTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationProposal>> ExecuteCoreAsync(StageFixAllRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageFixAllAsync(request, cancellationToken);
    }
}
