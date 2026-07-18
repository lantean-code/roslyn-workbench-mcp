namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageCodeActionTool : CodeActionMutationToolHandler<StageCodeActionRequest>
{
    private readonly ICodeActionReplayService _replayService;

    public StageCodeActionTool(ICodeActionReplayService replayService)
    {
        _replayService = replayService;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(StageCodeActionRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _replayService.StageCodeActionAsync(request, context, cancellationToken);
    }
}
