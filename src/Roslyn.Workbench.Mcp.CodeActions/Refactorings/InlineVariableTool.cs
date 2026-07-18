using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class InlineVariableTool : CodeActionMutationToolHandler<InlineVariableRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineTemporary.CSharpInlineTemporaryCodeRefactoringProvider";
    private const string Title = "Inline temporary variable";
    private const string EquivalenceKey = "Inline_temporary_variable";

    private readonly ICodeActionReplayService _replayService;

    public InlineVariableTool(ICodeActionReplayService replayService)
    {
        _replayService = replayService;
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(InlineVariableRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        if (!request.RemoveDeclaration)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("UnsupportedOption", "The removeDeclaration option is not supported by the current Roslyn inline-variable backend.");
        }

        var symbolResolution = await CodeActionSelectorHelpers.ResolveSymbolAsync<WorkspaceMutationCandidate>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
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
        var locationSelector = CodeActionSelectorHelpers.CreateLocationSelector(resolvedLocation);
        if (locationSelector is null)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("SymbolNotSupported", "The selected symbol does not resolve to a replayable source span.", RequiredAction.ResolveTargetAgain);
        }

        return await _replayService.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = locationSelector,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = ProviderId,
            Title = Title,
            EquivalenceKey = EquivalenceKey,
        }, context, cancellationToken).ConfigureAwait(false);
    }
}
