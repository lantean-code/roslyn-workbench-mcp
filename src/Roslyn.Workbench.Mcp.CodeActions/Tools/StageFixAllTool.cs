namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageFixAllTool : CodeActionMutationToolHandler<StageFixAllRequest>
{
    private readonly ICodeActionFixAllService _fixAllService;

    public StageFixAllTool(ICodeActionFixAllService fixAllService)
    {
        _fixAllService = fixAllService;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(StageFixAllRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _fixAllService.StageFixAllAsync(request, context, cancellationToken);
    }
}
