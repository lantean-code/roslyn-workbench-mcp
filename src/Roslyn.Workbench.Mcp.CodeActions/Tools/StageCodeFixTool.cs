namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageCodeFixTool : CodeActionMutationToolHandler<StageCodeFixRequest>
{
    private readonly ICodeActionTokenStager _tokenStager;

    public StageCodeFixTool(ICodeActionTokenStager tokenStager)
    {
        _tokenStager = tokenStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(StageCodeFixRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _tokenStager.StageCodeFixAsync(request, context, cancellationToken);
    }
}
