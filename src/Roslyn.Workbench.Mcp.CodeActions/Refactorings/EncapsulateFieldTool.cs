using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class EncapsulateFieldTool : CodeActionMutationToolHandler<EncapsulateFieldRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.EncapsulateField.EncapsulateFieldRefactoringProvider";

    private readonly ICodeActionReplayService _replayService;

    public EncapsulateFieldTool(ICodeActionReplayService replayService)
    {
        _replayService = replayService;
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(EncapsulateFieldRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await CodeActionSelectorHelpers.ResolveSymbolAsync<WorkspaceMutationCandidate>(request.Field, request.ExpectedSnapshot, context, cancellationToken).ConfigureAwait(false);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not IFieldSymbol fieldSymbol)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("SymbolNotSupported", "The selected symbol is not a field.", RequiredAction.ResolveTargetAgain);
        }

        var sourceLocation = fieldSymbol.Locations.FirstOrDefault(static location => location.IsInSource);
        if (sourceLocation is null)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("SymbolNotSupported", "The selected symbol does not resolve to a source location.", RequiredAction.ResolveTargetAgain);
        }

        var locationSelector = CodeActionSelectorHelpers.CreateLocationSelector(context.WorkspaceResolver.CreateResolvedLocation(sourceLocation));
        if (locationSelector is null)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("SymbolNotSupported", "The selected symbol does not resolve to a replayable source span.", RequiredAction.ResolveTargetAgain);
        }

        var (title, equivalenceKey) = request.UpdateReferences
            ? ($"Encapsulate field: '{fieldSymbol.Name}' (and use property)", $"Encapsulate_field_colon_0_and_use_property_{fieldSymbol.Name}")
            : ($"Encapsulate field: '{fieldSymbol.Name}' (but still use field)", $"Encapsulate_field_colon_0_but_still_use_field_{fieldSymbol.Name}");

        return await _replayService.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = locationSelector,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = ProviderId,
            Title = title,
            EquivalenceKey = equivalenceKey,
        }, context, cancellationToken).ConfigureAwait(false);
    }
}
