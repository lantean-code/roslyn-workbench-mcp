using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class ConvertToInterpolatedStringTool : CodeActionMutationToolHandler<ConvertToInterpolatedStringRequest>
{
    private const string Title = "Convert to interpolated string";
    private const string EquivalenceKey = "Convert_to_interpolated_string";

    private readonly ICodeActionSelectionStager _selectionStager;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    public ConvertToInterpolatedStringTool(
        ICodeActionSelectionStager selectionStager,
        ICodeActionToolRequestResolver requestResolver)
    {
        _selectionStager = selectionStager;
        _requestResolver = requestResolver;
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(ConvertToInterpolatedStringRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var snapshotRejection = _requestResolver.ValidateSnapshot<WorkspaceMutationCandidate>(
            context,
            request.ExpectedSnapshot);

        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        var locationResolution = await _requestResolver.ResolveLocationAsync<WorkspaceMutationCandidate>(
            request.Selection,
            context,
            cancellationToken);

        if (locationResolution.HasRejection)
        {
            return locationResolution.Rejection;
        }

        var replayRequest = new ReplayCodeActionRequest
        {
            Location = request.Selection,
            ExpectedSnapshot = request.ExpectedSnapshot,
            Title = Title,
            EquivalenceKey = EquivalenceKey,
        };

        return await _selectionStager.StageReplayCodeActionAsync(
            replayRequest,
            context,
            cancellationToken);
    }
}
