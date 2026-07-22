namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageCodeActionTool : CodeActionMutationToolHandler<StageCodeActionRequest>
{
    private readonly ICodeActionTokenStager _tokenStager;

    public StageCodeActionTool(ICodeActionTokenStager tokenStager)
    {
        _tokenStager = tokenStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(StageCodeActionRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _tokenStager.StageCodeActionAsync(request, context, cancellationToken);
    }
}
