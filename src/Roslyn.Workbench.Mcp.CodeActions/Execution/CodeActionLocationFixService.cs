using static Roslyn.Workbench.Mcp.CodeActions.Execution.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class CodeActionLocationFixService : ICodeActionLocationFixService
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionOperationService _operationService;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionDescriptorRegistry _descriptorRegistry;

    public CodeActionLocationFixService(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionOperationService operationService,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionDescriptorRegistry descriptorRegistry)
    {
        _providerCatalog = providerCatalog;
        _discoveryService = discoveryService;
        _operationService = operationService;
        _diagnosticService = diagnosticService;
        _descriptorRegistry = descriptorRegistry;
    }

    public async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageLocationCodeFixAsync(
        LocationCodeFixRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runtimeRejection = RejectedIfUnavailable();
        if (runtimeRejection is not null)
        {
            return runtimeRejection;
        }

        var snapshotRejection = ValidateSnapshot<WorkspaceMutationCandidate>(context.WorkspaceResolver, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Location is null)
        {
            return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "A location selector is required.");
        }

        if (request.DiagnosticIds.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "At least one diagnostic ID is required.");
        }

        var location = await context.WorkspaceResolver.ResolveLocationAsync(request.Location, cancellationToken);
        if (location.Status != SelectorResolveStatus.Resolved || location.Value is null)
        {
            return RejectFromStatus<WorkspaceMutationCandidate>(location.Status, "Location", "location");
        }

        var document = context.CurrentSolution.GetDocument(location.Value.SourceTree);
        if (document is null)
        {
            return Rejected<WorkspaceMutationCandidate>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
        }

        var span = location.Value.SourceSpan;
        var matchingProviders = _discoveryService.GetMatchingCodeFixProviders(request.ProviderId);
        if (matchingProviders.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code-fix provider is available.");
        }

        var diagnostics = await _diagnosticService.GetLocationScopedCodeFixDiagnosticsAsync(
            document,
            span,
            request.DiagnosticIds,
            request.AnalyzerTypeName,
            request.SyntheticDiagnosticId,
            cancellationToken);
        if (diagnostics.IsDefaultOrEmpty)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code fix was available at the selected location.");
        }

        var candidates = new List<ClassifiedCodeAction>();
        foreach (var provider in matchingProviders)
        {
            var actions = await _discoveryService.DiscoverCodeFixesAsync(provider, document, diagnostics, cancellationToken);
            foreach (var action in actions)
            {
                if (!string.IsNullOrWhiteSpace(request.Title)
                    && !string.Equals(action.Title, request.Title, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(request.EquivalenceKey)
                    && !string.Equals(action.EquivalenceKey, request.EquivalenceKey, StringComparison.Ordinal))
                {
                    continue;
                }

                var descriptor = _descriptorRegistry.Classify(action.Action, action.ProviderId, action.Title);
                if (!descriptor.IsVisible)
                {
                    continue;
                }

                candidates.Add(new ClassifiedCodeAction
                {
                    Action = action,
                    Descriptor = descriptor,
                });
            }
        }

        var distinctCandidates = candidates
            .GroupBy(static candidate => new CodeActionCandidateIdentity(
                candidate.Action.ProviderId,
                candidate.Action.Title,
                candidate.Action.EquivalenceKey,
                candidate.Action.ActionPath,
                candidate.Action.DiagnosticIds))
            .Select(static group => group.First())
            .ToArray();
        if (distinctCandidates.Length == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code fix was available at the selected location.");
        }

        if (distinctCandidates.Length > 1)
        {
            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "ActionAmbiguous",
                Message = "The requested code fix could not be selected uniquely.",
            }, RequiredAction.ResolveTargetAgain);
        }

        var candidate = distinctCandidates[0];
        return candidate.Descriptor.ExecutionMode switch
        {
            CodeActionExecutionMode.Replay => await _operationService.CreateMutationCandidateAsync(candidate.Action.Action, candidate.Action.Title, context, cancellationToken),
            CodeActionExecutionMode.Parameterised => await _operationService.CreateMutationCandidateAsync(candidate.Action.Action, candidate.Action.Title, context, cancellationToken),
            _ => Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "The selected action is not replayable in this server build.", RequiredAction.ResolveTargetAgain),
        };
    }
    private CodeActionExecutionResult<WorkspaceMutationCandidate>? RejectedIfUnavailable()
    {
        return _providerCatalog.Status.IsAvailable
            ? null
            : Rejected<WorkspaceMutationCandidate>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }
    private sealed record ClassifiedCodeAction
    {
        public required DiscoveredCodeAction Action { get; init; }

        public required CodeActionDescriptorEntry Descriptor { get; init; }
    }
}
