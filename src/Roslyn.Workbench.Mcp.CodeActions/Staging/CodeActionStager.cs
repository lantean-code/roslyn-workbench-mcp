using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal sealed class CodeActionStager : ICodeActionStager
{
    private readonly ICodeActionComposition _composition;
    private readonly ICodeActionResolver _resolver;
    private readonly IPreparedFixAllResolver _preparedFixAllResolver;
    private readonly ICodeActionEvaluator _evaluator;
    private readonly ICodeActionReferenceStore _referenceStore;

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
