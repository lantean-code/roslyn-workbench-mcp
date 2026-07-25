namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageCodeActionTool : CodeActionMutationToolHandler<StageCodeActionRequest>
{
    private readonly ICodeActionReferenceStager _referenceStager;

    public StageCodeActionTool(ICodeActionReferenceStager referenceStager)
    {
        _referenceStager = referenceStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(StageCodeActionRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _referenceStager.StageCodeActionAsync(request, context, cancellationToken);
    }
}
