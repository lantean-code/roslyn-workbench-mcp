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

        var resolvedAction = _referenceStore.IsPreparedFixAll(request.ActionId)
            ? await _preparedFixAllResolver.ResolveActionAsync<WorkspaceMutationCandidate>(
                request.ActionId,
                request.ExpectedSnapshot,
                context,
                cancellationToken)
            : await _resolver.ResolveActionAsync<WorkspaceMutationCandidate>(
                request.ActionId,
                request.ExpectedSnapshot,
                context,
                cancellationToken);

        if (resolvedAction.HasRejection)
        {
            if (resolvedAction.FailureKind != CodeActionResolutionFailureKind.None)
            {
                _referenceStore.Remove(request.ActionId);
            }

            return resolvedAction.Rejection;
        }

        if (resolvedAction.Descriptor.ExecutionMode == CodeActionExecutionMode.Parameterised)
        {
            var error = new CodeActionExecutionError
            {
                Code = "ActionRequiresParameters",
                Message = "The selected action requires dedicated tool parameters and cannot be replayed generically.",
            };

            return CodeActionExecutionResult.Rejected<WorkspaceMutationCandidate>(error);
        }

        var application = await _evaluator.EvaluateAsync(
            resolvedAction.Action.Action,
            context.CurrentSolution,
            cancellationToken);

        if (application.HasFailure)
        {
            return Rejected<WorkspaceMutationCandidate>(application.Failure);
        }

        var candidate = new WorkspaceMutationCandidate
        {
            CandidateSolution = application.CandidateSolution,
            Summary = resolvedAction.Action.Title,
        };

        return CodeActionExecutionResult.Success(candidate);
    }

    private CodeActionExecutionResult<WorkspaceMutationCandidate>? RejectedIfUnavailable()
    {
        return _composition.Status.IsAvailable
            ? null
            : Rejected<WorkspaceMutationCandidate>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }
}
