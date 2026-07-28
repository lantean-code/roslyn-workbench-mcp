namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageCodeActionTool : CodeActionMutationToolHandler<StageCodeActionRequest>
{
    private readonly ICodeActionStager _stager;

    public StageCodeActionTool(ICodeActionStager stager)
    {
        _stager = stager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(StageCodeActionRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _stager.StageAsync(request, context, cancellationToken);
    }
}
