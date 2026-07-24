using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal sealed class LocationCodeFixStager : ILocationCodeFixStager
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionEvaluator _evaluator;
    private readonly ICodeActionDiagnosticService _diagnosticService;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    public LocationCodeFixStager(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionEvaluator evaluator,
        ICodeActionDiagnosticService diagnosticService,
        ICodeActionToolRequestResolver requestResolver)
    {
        _providerCatalog = providerCatalog;
        _discoveryService = discoveryService;
        _evaluator = evaluator;
        _diagnosticService = diagnosticService;
        _requestResolver = requestResolver;
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

        var snapshotRejection = _requestResolver.ValidateSnapshot<WorkspaceMutationCandidate>(
            context,
            request.ExpectedSnapshot);

        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.DiagnosticIds.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "At least one diagnostic ID is required.");
        }

        var locationResolution = await _requestResolver.ResolveLocationAsync<WorkspaceMutationCandidate>(
            request.Location,
            context,
            cancellationToken);

        if (locationResolution.HasRejection)
        {
            return locationResolution.Rejection;
        }

        var document = locationResolution.Value.Document;
        var span = locationResolution.Value.Span;
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

        if (diagnostics.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code fix was available at the selected location.");
        }

        var candidates = new List<DiscoveredCodeAction>();
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

                if (!action.Descriptor.IsVisible)
                {
                    continue;
                }

                candidates.Add(action);
            }
        }

        var distinctCandidates = new List<DiscoveredCodeAction>();
        var candidateIdentities = new HashSet<CodeActionCandidateIdentity>();
        foreach (var action in candidates)
        {
            var identity = new CodeActionCandidateIdentity(
                action.ProviderId,
                action.Title,
                action.EquivalenceKey,
                action.ActionPath,
                action.DiagnosticIds);

            if (candidateIdentities.Add(identity))
            {
                distinctCandidates.Add(action);
            }
        }

        if (distinctCandidates.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "No matching code fix was available at the selected location.");
        }

        if (distinctCandidates.Count > 1)
        {
            var error = new CodeActionExecutionError
            {
                Code = "ActionAmbiguous",
                Message = "The requested code fix could not be selected uniquely.",
            };

            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(error, RequiredAction.ResolveTargetAgain);
        }

        var candidate = distinctCandidates[0];
        if (candidate.Descriptor.ExecutionMode is CodeActionExecutionMode.Replay or CodeActionExecutionMode.Parameterised)
        {
            var application = await _evaluator.EvaluateAsync(
                candidate.Action,
                context.CurrentSolution,
                cancellationToken);

            if (application.HasFailure)
            {
                return Rejected<WorkspaceMutationCandidate>(application.Failure);
            }

            var mutationCandidate = new WorkspaceMutationCandidate
            {
                CandidateSolution = application.CandidateSolution,
                Summary = candidate.Title,
            };

            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(mutationCandidate);
        }

        return Rejected<WorkspaceMutationCandidate>("CodeFixUnavailable", "The selected action is not replayable in this server build.", RequiredAction.ResolveTargetAgain);
    }

    private CodeActionExecutionResult<WorkspaceMutationCandidate>? RejectedIfUnavailable()
    {
        return _providerCatalog.Status.IsAvailable
            ? null
            : Rejected<WorkspaceMutationCandidate>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }
}
