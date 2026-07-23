using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class InlineVariableTool : CodeActionMutationToolHandler<InlineVariableRequest>
{
    private const string _providerId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineTemporary.CSharpInlineTemporaryCodeRefactoringProvider";
    private const string _title = "Inline temporary variable";
    private const string _equivalenceKey = "Inline_temporary_variable";

    private readonly ICodeActionSelectionStager _selectionStager;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    public InlineVariableTool(
        ICodeActionSelectionStager selectionStager,
        ICodeActionToolRequestResolver requestResolver)
    {
        _selectionStager = selectionStager;
        _requestResolver = requestResolver;
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(InlineVariableRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (!request.RemoveDeclaration)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("UnsupportedOption", "The removeDeclaration option is not supported by the current Roslyn inline-variable backend.");
        }

        var symbolResolution = await _requestResolver.ResolveSymbolAsync<WorkspaceMutationCandidate>(
            request.Symbol,
            request.ExpectedSnapshot,
            context,
            cancellationToken);

        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not ILocalSymbol localSymbol)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("SymbolNotSupported", "The selected symbol is not a local variable.", RequiredAction.ResolveTargetAgain);
        }

        var sourceLocation = localSymbol.Locations.FirstOrDefault(static location => location.IsInSource);
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
            Title = _title,
            EquivalenceKey = _equivalenceKey,
        }, context, cancellationToken);
    }
}
