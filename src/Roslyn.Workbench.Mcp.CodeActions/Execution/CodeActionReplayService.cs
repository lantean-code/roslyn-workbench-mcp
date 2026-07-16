using static Roslyn.Workbench.Mcp.CodeActions.Execution.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal sealed class CodeActionReplayService : ICodeActionReplayService
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionDiscoveryService _discoveryService;
    private readonly ICodeActionResolutionService _resolutionService;
    private readonly ICodeActionOperationService _operationService;
    private readonly ICodeActionDescriptorRegistry _descriptorRegistry;

    public CodeActionReplayService(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionDiscoveryService discoveryService,
        ICodeActionResolutionService resolutionService,
        ICodeActionOperationService operationService,
        ICodeActionDescriptorRegistry descriptorRegistry)
    {
        _providerCatalog = providerCatalog;
        _discoveryService = discoveryService;
        _resolutionService = resolutionService;
        _operationService = operationService;
        _descriptorRegistry = descriptorRegistry;
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageCodeActionAsync(
        StageCodeActionRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var runtimeRejection = RejectedIfUnavailable();
        if (runtimeRejection is not null)
        {
            return ValueTask.FromResult(runtimeRejection);
        }

        return StageAsync(request.ActionId, request.ExpectedSnapshot, DiscoveredActionKind.Refactoring, context, cancellationToken);
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

        var snapshotRejection = ValidateSnapshot<WorkspaceMutationCandidate>(context.WorkspaceResolver, request.ExpectedSnapshot);
        if (snapshotRejection is not null)
        {
            return snapshotRejection;
        }

        if (request.Location is null)
        {
            return Rejected<WorkspaceMutationCandidate>("InvalidRequest", "A location selector is required.");
        }

        var location = await context.WorkspaceResolver.ResolveLocationAsync(request.Location, cancellationToken).ConfigureAwait(false);
        if (location.Status != SelectorResolveStatus.Resolved || location.Value is null)
        {
            return RejectFromStatus<WorkspaceMutationCandidate>(location.Status, "Location");
        }

        var document = context.CurrentSolution.GetDocument(location.Value.SourceTree);
        if (document is null)
        {
            return Rejected<WorkspaceMutationCandidate>("LocationNotFound", "The location selector did not resolve to a source document.", RequiredAction.ResolveTargetAgain);
        }

        var span = location.Value.SourceSpan;
        var matchingProviders = _discoveryService.GetMatchingRefactoringProviders(request.ProviderId);
        if (matchingProviders.Count == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeActionUnavailable", "No matching refactoring provider is available.");
        }

        var candidates = new List<ClassifiedCodeAction>();
        foreach (var provider in matchingProviders)
        {
            var actions = await _discoveryService.DiscoverRefactoringsAsync(provider, document, span, cancellationToken).ConfigureAwait(false);
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
                candidate.Action.ActionPath))
            .Select(static group => group.First())
            .ToArray();
        if (distinctCandidates.Length == 0)
        {
            return Rejected<WorkspaceMutationCandidate>("CodeActionUnavailable", "No matching replayable refactoring was available at the selected location.");
        }

        if (distinctCandidates.Length > 1)
        {
            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "ActionAmbiguous",
                Message = "The requested refactoring could not be selected uniquely.",
            }, RequiredAction.ResolveTargetAgain);
        }

        var candidate = distinctCandidates[0];
        return candidate.Descriptor.ExecutionMode switch
        {
            CodeActionExecutionMode.Replay => await _operationService.CreateMutationCandidateAsync(candidate.Action.Action, candidate.Action.Title, context, cancellationToken).ConfigureAwait(false),
            CodeActionExecutionMode.Parameterised => CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "ActionRequiresParameters",
                Message = "The selected action requires dedicated tool parameters and cannot be replayed generically.",
            }),
            _ => Rejected<WorkspaceMutationCandidate>("CodeActionUnavailable", "The selected action is not replayable in this server build.", RequiredAction.ResolveTargetAgain),
        };
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageCodeFixAsync(
        StageCodeFixRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var runtimeRejection = RejectedIfUnavailable();
        if (runtimeRejection is not null)
        {
            return ValueTask.FromResult(runtimeRejection);
        }

        return StageAsync(request.ActionId, request.ExpectedSnapshot, DiscoveredActionKind.CodeFix, context, cancellationToken);
    }

    public ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageSelectionAsync(
        LocationSelector? selection,
        SnapshotPrecondition? expectedSnapshot,
        CancellationToken cancellationToken,
        ICodeActionExecutionContext context,
        string providerId,
        string? title = null,
        string? titleStartsWith = null,
        string? titleDoesNotContain = null,
        string? equivalenceKey = null,
        IReadOnlyList<int>? actionPath = null)
    {
        if (selection is null)
        {
            return ValueTask.FromResult(Rejected<WorkspaceMutationCandidate>(
                "InvalidRequest",
                "A location selector is required."));
        }

        return StageReplayCodeActionAsync(new ReplayCodeActionRequest
        {
            Location = selection,
            ExpectedSnapshot = expectedSnapshot,
            ProviderId = providerId,
            Title = title,
            TitleStartsWith = titleStartsWith,
            TitleDoesNotContain = titleDoesNotContain,
            EquivalenceKey = equivalenceKey,
            ActionPath = actionPath,
        }, context, cancellationToken);
    }

    private async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageAsync(
        string actionId,
        SnapshotPrecondition? expectedSnapshot,
        DiscoveredActionKind expectedKind,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var resolvedAction = await _resolutionService.ResolveActionAsync<WorkspaceMutationCandidate>(
            actionId,
            expectedSnapshot,
            expectedKind,
            context,
            cancellationToken).ConfigureAwait(false);
        if (resolvedAction.HasRejection)
        {
            return resolvedAction.Rejection;
        }

        if (resolvedAction.Descriptor.ExecutionMode == CodeActionExecutionMode.Parameterised)
        {
            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(new CodeActionExecutionError
            {
                Code = "ActionRequiresParameters",
                Message = "The selected action requires dedicated tool parameters and cannot be replayed generically.",
            });
        }

        return await _operationService.CreateMutationCandidateAsync(
            resolvedAction.Action.Action,
            resolvedAction.Action.Title,
            context,
            cancellationToken).ConfigureAwait(false);
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
