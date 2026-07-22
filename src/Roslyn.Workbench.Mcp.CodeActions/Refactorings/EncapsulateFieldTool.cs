using Roslyn.Workbench.Mcp.CodeActions.Contracts.Refactorings;

namespace Roslyn.Workbench.Mcp.CodeActions.Refactorings;

internal sealed class EncapsulateFieldTool : CodeActionMutationToolHandler<EncapsulateFieldRequest>
{
    private const string ProviderId = "Microsoft.CodeAnalysis.EncapsulateField.EncapsulateFieldRefactoringProvider";

    private readonly ICodeActionSelectionStager _selectionStager;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    public EncapsulateFieldTool(
        ICodeActionSelectionStager selectionStager,
        ICodeActionToolRequestResolver requestResolver)
    {
        _selectionStager = selectionStager;
        _requestResolver = requestResolver;
    }

    protected override async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ExecuteCoreAsync(EncapsulateFieldRequest request, ICodeActionMutationContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await _requestResolver.ResolveSymbolAsync<WorkspaceMutationCandidate>(
            request.Field,
            request.ExpectedSnapshot,
            context,
            cancellationToken);
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

        var resolvedLocation = context.WorkspaceResolver.CreateResolvedLocation(sourceLocation);
        var locationSelector = _requestResolver.CreateLocationSelector(resolvedLocation);
        if (locationSelector is null)
        {
            return CodeActionExecutionResultFactory.Rejected<WorkspaceMutationCandidate>("SymbolNotSupported", "The selected symbol does not resolve to a replayable source span.", RequiredAction.ResolveTargetAgain);
        }

        string title;
        string equivalenceKey;
        if (request.UpdateReferences)
        {
            title = $"Encapsulate field: '{fieldSymbol.Name}' (and use property)";
            equivalenceKey = $"Encapsulate_field_colon_0_and_use_property_{fieldSymbol.Name}";
        }
        else
        {
            title = $"Encapsulate field: '{fieldSymbol.Name}' (but still use field)";
            equivalenceKey = $"Encapsulate_field_colon_0_but_still_use_field_{fieldSymbol.Name}";
        }

        return await _selectionStager.StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = locationSelector,
            ExpectedSnapshot = request.ExpectedSnapshot,
            ProviderId = ProviderId,
            Title = title,
            EquivalenceKey = equivalenceKey,
        }, context, cancellationToken);
    }
}
