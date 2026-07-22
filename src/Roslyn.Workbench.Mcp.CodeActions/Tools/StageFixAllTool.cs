namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageFixAllTool : CodeActionMutationToolHandler<StageFixAllRequest>
{
    private readonly ICodeActionFixAllStager _fixAllStager;

    public StageFixAllTool(ICodeActionFixAllStager fixAllStager)
    {
        _fixAllStager = fixAllStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(StageFixAllRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _fixAllStager.StageFixAllAsync(request, context, cancellationToken);
    }
}
