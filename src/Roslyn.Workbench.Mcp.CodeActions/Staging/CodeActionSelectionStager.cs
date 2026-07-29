using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal sealed class CodeActionSelectionStager : ICodeActionSelectionStager
{
    private readonly ICodeActionComposition _composition;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionEvaluator _evaluator;
    private readonly ICodeActionToolRequestResolver _requestResolver;

    public CodeActionSelectionStager(
        ICodeActionComposition composition,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionEvaluator evaluator,
        ICodeActionToolRequestResolver requestResolver)
    {
        _composition = composition;
        _discoveryService = discoveryService;
        _evaluator = evaluator;
        _requestResolver = requestResolver;
    }

    public async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageReplayCodeActionAsync(
        ReplayCodeActionRequest request,
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
        var matchingProviders = _discoveryService.GetMatchingRefactoringProviders(request.ProviderId);
        if (matchingProviders.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeActionUnavailable", "No matching refactoring provider is available.");
        }

        var candidates = new List<DiscoveredCodeAction>();
        foreach (var provider in matchingProviders)
        {
            var actions = await _discoveryService.DiscoverRefactoringsAsync(provider, document, span, cancellationToken);
            foreach (var action in actions)
            {
                if (!string.IsNullOrWhiteSpace(request.Title)
                    && !string.Equals(action.Title, request.Title, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(request.TitleStartsWith)
                    && !action.Title.StartsWith(request.TitleStartsWith, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(request.TitleDoesNotContain)
                    && action.Title.Contains(request.TitleDoesNotContain, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(request.EquivalenceKey)
                    && !string.Equals(action.EquivalenceKey, request.EquivalenceKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (request.ActionPath is { Count: > 0 }
                    && !action.ActionPath.SequenceEqual(request.ActionPath))
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
                action.ActionPath);

            if (candidateIdentities.Add(identity))
            {
                distinctCandidates.Add(action);
            }
        }

        if (distinctCandidates.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeActionUnavailable", "No matching replayable refactoring was available at the selected location.");
        }

        if (distinctCandidates.Count > 1)
        {
            var error = new CodeActionExecutionError
            {
                Code = "ActionAmbiguous",
                Message = "The requested refactoring could not be selected uniquely.",
            };

            return CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(error, RequiredAction.ResolveTargetAgain);
        }

        var candidate = distinctCandidates[0];
        if (candidate.Descriptor.ExecutionMode == CodeActionExecutionMode.Replay)
        {
            return await ApplyActionAsync(candidate.Action, candidate.Title, context, cancellationToken);
        }

        if (candidate.Descriptor.ExecutionMode == CodeActionExecutionMode.Parameterised)
        {
            var error = new CodeActionExecutionError
            {
                Code = "ActionRequiresParameters",
                Message = "The selected action requires dedicated tool parameters and cannot be replayed generically.",
            };

            return CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(error);
        }

        return Rejected<WorkspaceMutationCandidate>("CodeActionUnavailable", "The selected action is not replayable in this server build.", RequiredAction.ResolveTargetAgain);
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageSelectionAsync(
        LocationSelector selection,
        SnapshotPrecondition expectedSnapshot,
        CancellationToken cancellationToken,
        ICodeActionExecutionContext context,
        string providerId,
        string? title = null,
        string? titleStartsWith = null,
        string? titleDoesNotContain = null,
        string? equivalenceKey = null,
        IReadOnlyList<int>? actionPath = null)
    {
        var request = new ReplayCodeActionRequest
        {
            Location = selection,
            ExpectedSnapshot = expectedSnapshot,
            ProviderId = providerId,
            Title = title,
            TitleStartsWith = titleStartsWith,
            TitleDoesNotContain = titleDoesNotContain,
            EquivalenceKey = equivalenceKey,
            ActionPath = actionPath,
        };

        return StageReplayCodeActionAsync(request, context, cancellationToken);
    }

    private async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> ApplyActionAsync(
        CodeAction action,
        string summary,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var application = await _evaluator.EvaluateAsync(
            action,
            context.CurrentSolution,
            cancellationToken);

        if (application.HasFailure)
        {
            return Rejected<WorkspaceMutationCandidate>(application.Failure);
        }

        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = application.CandidateSolution,
            Summary = summary,
        };

        return CodeActionExecutionResult.Success(candidate);
    }

    private CodeActionExecutionResult<WorkspaceMutationCandidate>? RejectedIfUnavailable()
    {
        if (_composition.Status.IsAvailable)
        {
            return null;
        }

        return Rejected<WorkspaceMutationCandidate>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }
}
