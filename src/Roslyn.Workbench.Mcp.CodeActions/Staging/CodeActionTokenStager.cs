using static Roslyn.Workbench.Mcp.CodeActions.Execution.Results.CodeActionExecutionResultFactory;

namespace Roslyn.Workbench.Mcp.CodeActions.Staging;

internal sealed class CodeActionTokenStager : ICodeActionTokenStager
{
    private readonly ICodeActionProviderCatalog _providerCatalog;
    private readonly ICodeActionResolver _resolver;
    private readonly ICodeActionEvaluator _evaluator;

    public CodeActionTokenStager(
        ICodeActionProviderCatalog providerCatalog,
        ICodeActionResolver resolver,
        ICodeActionEvaluator evaluator)
    {
        _providerCatalog = providerCatalog;
        _resolver = resolver;
        _evaluator = evaluator;
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

    private async ValueTask<CodeActionExecutionResult<WorkspaceMutationCandidate>> StageAsync(
        string actionId,
        SnapshotPrecondition expectedSnapshot,
        DiscoveredActionKind expectedKind,
        ICodeActionExecutionContext context,
        CancellationToken cancellationToken)
    {
        var resolvedAction = await _resolver.ResolveActionAsync<WorkspaceMutationCandidate>(
            actionId,
            expectedSnapshot,
            expectedKind,
            context,
            cancellationToken);

        if (resolvedAction.HasRejection)
        {
            return resolvedAction.Rejection;
        }

        if (resolvedAction.Descriptor.ExecutionMode == CodeActionExecutionMode.Parameterised)
        {
            var error = new CodeActionExecutionError
            {
                Code = "ActionRequiresParameters",
                Message = "The selected action requires dedicated tool parameters and cannot be replayed generically.",
            };

            return CodeActionExecutionResult<WorkspaceMutationCandidate>.Rejected(error);
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

        return CodeActionExecutionResult<WorkspaceMutationCandidate>.Success(candidate);
    }

    private CodeActionExecutionResult<WorkspaceMutationCandidate>? RejectedIfUnavailable()
    {
        return _providerCatalog.Status.IsAvailable
            ? null
            : Rejected<WorkspaceMutationCandidate>("CodeActionsUnavailable", "Code-action composition is unavailable.");
    }
}
