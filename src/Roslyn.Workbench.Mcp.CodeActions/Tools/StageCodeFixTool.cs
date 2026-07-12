using Roslyn.Workbench.Mcp.CodeActions.Contracts;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageCodeFixTool : CodeActionMutationToolHandler<StageCodeFixRequest>
{
    private static readonly CodeActionToolMetadata _metadata = new()
    {
        Name = "stage-code-fix",
        Title = "Stage Code Fix",
        Description = "Revalidates and stages one selected code fix into the active transaction.",
        Behavior = new CodeActionToolBehavior
        {
            Destructive = true,
        },
    };

    public static void Register(ICodeActionToolRegistry registry)
    {
        registry.RegisterMutationTool(_metadata, new StageCodeFixTool());
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(StageCodeFixRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return context.StageCodeFixAsync(request, cancellationToken);
    }
}
