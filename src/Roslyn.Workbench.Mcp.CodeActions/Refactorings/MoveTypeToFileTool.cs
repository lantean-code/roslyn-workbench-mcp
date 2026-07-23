using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class MoveTypeToFileTool : CodeActionMutationToolHandler<MoveTypeToFileRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.CodeRefactorings.MoveType.MoveTypeCodeRefactoringProvider";
    private const string _titlePrefix = "Move type to ";

    private readonly ICodeActionSelectionStager _selectionStager;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    public MoveTypeToFileTool(
        ICodeActionSelectionStager selectionStager,
        ICodeActionToolRequestResolver requestResolver)
    {
        _selectionStager = selectionStager;
        _requestResolver = requestResolver;
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(MoveTypeToFileRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (!request.PreserveNamespace)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("UnsupportedOption", "The preserveNamespace option must remain true for the current move-type-to-file backend.");
        }

        var symbolResolution = await _requestResolver.ResolveSymbolAsync<WorkspaceMutationCandidate>(
            request.Type,
            request.ExpectedSnapshot,
            context,
            cancellationToken);

        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol typeSymbol)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("SymbolNotSupported", "The selected symbol is not a type declaration.", RequiredAction.ResolveTargetAgain);
        }

        var sourceLocation = typeSymbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is null)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("SymbolNotSupported", "The selected symbol does not resolve to a source location.", RequiredAction.ResolveTargetAgain);
        }

        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(sourceLocation);
        var locationSelector = _requestResolver.CreateLocationSelector(resolvedLocation);
        if (locationSelector is null)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("SymbolNotSupported", "The selected symbol does not resolve to a replayable source span.", RequiredAction.ResolveTargetAgain);
        }

        return await _selectionStager.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = locationSelector,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = _providerId,
            TitleStartsWith = _titlePrefix,
        }, context, cancellationToken);
    }
}
