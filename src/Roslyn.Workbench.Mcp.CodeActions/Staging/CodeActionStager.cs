using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

/// <summary>
/// Rehydrates and evaluates a referenced Code Action to produce a transaction candidate.
/// </summary>
internal sealed class CodeActionStager : ICodeActionStager
{
    private readonly ICodeActionComposition _composition;
    private readonly ICodeActionResolver _resolver;
    private readonly IPreparedFixAllResolver _preparedFixAllResolver;
    private readonly ICodeActionEvaluator _evaluator;
    private readonly ICodeActionReferenceStore _referenceStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionStager"/> class.
    /// </summary>
    /// <param name="composition">The runtime Code Action composition state.</param>
    /// <param name="resolver">The resolver for ordinary Code Action references.</param>
    /// <param name="preparedFixAllResolver">The resolver for previously prepared Fix All references.</param>
    /// <param name="evaluator">The component that evaluates a Code Action into a candidate solution.</param>
    /// <param name="referenceStore">The store containing short-lived Code Action references.</param>
    public CodeActionStager(
        ICodeActionComposition composition,
        ICodeActionResolver resolver,
        IPreparedFixAllResolver preparedFixAllResolver,
        ICodeActionEvaluator evaluator,
        ICodeActionReferenceStore referenceStore)
    {
        _composition = composition;
        _resolver = resolver;
        _preparedFixAllResolver = preparedFixAllResolver;
        _evaluator = evaluator;
        _referenceStore = referenceStore;
    }

    /// <summary>
    /// Resolves and evaluates the requested Code Action to produce a candidate solution for staging.
    /// </summary>
    /// <param name="request">The referenced action and snapshot precondition to stage.</param>
    /// <param name="context">The current transaction-scoped Code Action context.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes with the candidate solution or a rejection explaining why it could not be produced.</returns>
    public async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageAsync(
        StageCodeActionRequest request,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var runtimeRejection = RejectedIfUnavailable();
        if (runtimeRejection is not null)
        {
            return runtimeRejection;
        }

        var isPreparedFixAll = _referenceStore.IsPreparedFixAll(request.ActionId);
        CodeActionResolution<WorkspaceMutationCandidate> resolvedAction;
        if (isPreparedFixAll)
        {
            resolvedAction = await _preparedFixAllResolver.ResolveActionAsync<WorkspaceMutationCandidate>(
                request.ActionId,
                request.ExpectedSnapshot,
                context,
                cancellationToken);
        }
        else
        {
            resolvedAction = await _resolver.ResolveActionAsync<WorkspaceMutationCandidate>(
                request.ActionId,
                request.ExpectedSnapshot,
                context,
                cancellationToken);
        }

        if (resolvedAction.HasRejection)
        {
            if (resolvedAction.FailureKind != CodeActionResolutionFailureKind.None)
            {
                _referenceStore.Remove(request.ActionId);
            }

            return resolvedAction.Rejection;
        }

        var application = await _evaluator.EvaluateAsync(
            resolvedAction.Action.Action,
            context.CurrentSolution,
            cancellationToken);

        if (application.HasFailure)
        {
            if (isPreparedFixAll)
            {
                _referenceStore.Remove(request.ActionId);
                return Rejected<WorkspaceMutationCandidate>(
                    WorkspaceErrorCodes.MutationCandidateChanged,
                    "The mutation candidate no longer matches the previously prepared operation.",
                    RequiredAction.ResolveTargetAgain);
            }

            return Rejected<WorkspaceMutationCandidate>(application.Failure);
        }

        var candidatePrecondition = resolvedAction.Reference.Recipe.PreparedFixAll?.CandidatePrecondition;
        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = application.CandidateSolution,
            Summary = resolvedAction.Action.Title,
            Precondition = candidatePrecondition,
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
