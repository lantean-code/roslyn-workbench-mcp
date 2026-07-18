using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertToInterpolatedStringTool : CodeActionMutationToolHandler<ConvertToInterpolatedStringRequest>
{
    private const string Title = "Convert to interpolated string";
    private const string EquivalenceKey = "Convert_to_interpolated_string";

    private readonly ICodeActionReplayService _replayService;

    public ConvertToInterpolatedStringTool(ICodeActionReplayService replayService)
    {
        _replayService = replayService;
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(ConvertToInterpolatedStringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {

        var snapshotRejection = CodeActionExecutionResultFactory.ValidateSnapshot<WorkspaceMutationCandidate>(context.WorkspaceResolver, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Selection is null)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("InvalidRequest", "A location selector is required.");
        }

        var locationResolution = await context.WorkspaceResolver.ResolveLocationAsync(request.Selection, cancellationToken).ConfigureAwait(false);
        if (locationResolution.Status != SelectorResolveStatus.Resolved)
        {
            return CodeActionExecutionResultFactory.RejectFromStatus<WorkspaceMutationCandidate>(locationResolution.Status, "Location", "location");
        }

        return await _replayService.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = request.Selection,
            ExpectedSnapshot = request.ExpectedSnapshot,
            Title = Title,
            EquivalenceKey = EquivalenceKey,
        }, context, cancellationToken).ConfigureAwait(false);
    }
}
