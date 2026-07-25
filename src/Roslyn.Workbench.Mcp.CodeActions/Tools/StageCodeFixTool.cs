namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageCodeFixTool : CodeActionMutationToolHandler<StageCodeFixRequest>
{
    private readonly ICodeActionReferenceStager _referenceStager;

    public StageCodeFixTool(ICodeActionReferenceStager referenceStager)
    {
        _referenceStager = referenceStager;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(StageCodeFixRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _referenceStager.StageCodeFixAsync(request, context, cancellationToken);
    }
}
