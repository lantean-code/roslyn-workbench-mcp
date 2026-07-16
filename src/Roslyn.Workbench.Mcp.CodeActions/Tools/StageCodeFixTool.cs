using Roslyn.Workbench.Mcp.CodeActions.Contracts;

namespace Roslyn.Workbench.Mcp.CodeActions.Tools;

internal sealed class StageCodeFixTool : CodeActionMutationToolHandler<StageCodeFixRequest>
{
    private readonly ICodeActionReplayService _replayService;

    public StageCodeFixTool(ICodeActionReplayService replayService)
    {
        _replayService = replayService;
    }

    protected override ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(StageCodeFixRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        return _replayService.StageCodeFixAsync(request, context, cancellationToken);
    }
}
